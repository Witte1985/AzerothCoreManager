using System.Diagnostics;
using System.Text.Json;
using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Configuration;
using AzerothCoreManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// Service for checking if stacks have updates available
/// </summary>
public sealed class StackVersionService : IStackVersionService
{
    private readonly AzerothCoreDbContext _dbContext;
    private readonly IModuleCatalogService _moduleCatalog;
    private readonly IGitHubApiService _githubApi;
    private readonly DockerOptions _dockerOptions;
    private readonly ILogger<StackVersionService> _logger;
    private readonly string _buildsPath;

    public StackVersionService(
        AzerothCoreDbContext dbContext,
        IModuleCatalogService moduleCatalog,
        IGitHubApiService githubApi,
        IOptions<DockerOptions> dockerOptions,
        ILogger<StackVersionService> logger)
    {
        _dbContext = dbContext;
        _moduleCatalog = moduleCatalog;
        _githubApi = githubApi;
        _dockerOptions = dockerOptions.Value;
        _logger = logger;
        
        var configuredPath = _dockerOptions.BuildsPath;
        _buildsPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath);
    }

    public async Task<StackUpdateStatusDto> CheckAndCacheStatusAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(s => s.Id == stackId, cancellationToken);

        if (stack == null)
        {
            throw new InvalidOperationException($"Stack {stackId} not found");
        }

        var result = new StackUpdateStatusDto
        {
            StackId = stackId,
            CurrentCoreSha = stack.CoreCommitSha,
            LastCheckedAt = DateTime.UtcNow
        };

        try
        {
            // Check core repository if built
            if (!string.IsNullOrEmpty(stack.CoreCommitSha) && !string.IsNullOrEmpty(stack.CoreRepositoryUrl))
            {
                var latestCoreSha = await GetRemoteCommitShaAsync(stack.CoreRepositoryUrl, stack.CoreBranch, cancellationToken);
                result.LatestCoreSha = latestCoreSha;
                result.IsCoreOutdated = !string.Equals(stack.CoreCommitSha, latestCoreSha, StringComparison.OrdinalIgnoreCase);
                
                _logger.LogInformation("Stack {StackId} SHA comparison: Current=[{Current}] Latest=[{Latest}] IsOutdated={IsOutdated}", 
                    stackId, stack.CoreCommitSha, latestCoreSha, result.IsCoreOutdated);
                
                if (result.IsCoreOutdated)
                {
                    _logger.LogInformation("Stack {StackId} core is outdated: {Current} -> {Latest}", 
                        stackId, stack.CoreCommitSha.Substring(0, Math.Min(7, stack.CoreCommitSha.Length)), 
                        latestCoreSha.Substring(0, Math.Min(7, latestCoreSha.Length)));
                }
                
                // Fetch CI build status for latest core version
                await FetchAndCacheCiBuildStatusAsync(stack, latestCoreSha, cancellationToken);
                
                // Populate result with cached CI status
                if (!string.IsNullOrEmpty(stack.LatestCoreBuildStatus))
                {
                    var cachedChecks = string.IsNullOrEmpty(stack.LatestCoreBuildChecksJson)
                        ? new List<CiCheckDto>()
                        : JsonSerializer.Deserialize<List<CiCheckDto>>(stack.LatestCoreBuildChecksJson) ?? new List<CiCheckDto>();
                    
                    result.LatestCoreBuildStatus = new CiBuildStatusDto
                    {
                        Status = stack.LatestCoreBuildStatus,
                        CriticalChecks = cachedChecks,
                        CheckedAt = stack.LatestCoreBuildStatusCheckedAt ?? DateTime.UtcNow
                    };
                }
            }

            // Check modules
            var moduleVersions = JsonSerializer.Deserialize<List<ModuleVersionInfo>>(stack.ModuleVersionsJson) ?? [];
            var allModules = await _moduleCatalog.ListAsync(stack.ServerType, cancellationToken);
            
            foreach (var moduleVersion in moduleVersions)
            {
                var module = allModules.FirstOrDefault(m => m.Id == moduleVersion.ModuleId);
                if (module == null) continue;

                try
                {
                    var latestModuleSha = await GetRemoteCommitShaAsync(moduleVersion.Repository, moduleVersion.Branch, cancellationToken);
                    var isOutdated = !string.Equals(moduleVersion.CommitSha, latestModuleSha, StringComparison.OrdinalIgnoreCase);
                    
                    if (isOutdated)
                    {
                        result.OutdatedModules.Add(new ModuleVersionStatusDto
                        {
                            ModuleId = moduleVersion.ModuleId,
                            ModuleName = module.Name,
                            IsOutdated = true,
                            CurrentCommitSha = moduleVersion.CommitSha,
                            LatestCommitSha = latestModuleSha
                        });
                        
                        _logger.LogInformation("Stack {StackId} module {ModuleId} is outdated: {Current} -> {Latest}",
                            stackId, moduleVersion.ModuleId,
                            moduleVersion.CommitSha.Substring(0, Math.Min(7, moduleVersion.CommitSha.Length)),
                            latestModuleSha.Substring(0, Math.Min(7, latestModuleSha.Length)));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check updates for module {ModuleId} on stack {StackId}", moduleVersion.ModuleId, stackId);
                }
            }

            result.OutdatedModuleCount = result.OutdatedModules.Count;
            result.HasUpdates = result.IsCoreOutdated || result.OutdatedModuleCount > 0;

            // Update cached status in database
            stack.IsOutdated = result.HasUpdates;
            stack.IsCoreOutdated = result.IsCoreOutdated;
            stack.OutdatedModuleCount = result.OutdatedModuleCount;
            stack.LatestAvailableCoreSha = result.LatestCoreSha;
            stack.OutdatedModulesJson = JsonSerializer.Serialize(result.OutdatedModules);
            stack.LastUpdateCheckAt = result.LastCheckedAt;
            
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Stack {StackId} update check completed: HasUpdates={HasUpdates}, Core={CoreOutdated}, Modules={ModuleCount}",
                stackId, result.HasUpdates, result.IsCoreOutdated, result.OutdatedModuleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check updates for stack {StackId}", stackId);
            // Return partial result with error indication
            result.LastCheckedAt = DateTime.UtcNow;
        }

        return result;
    }

    public async Task<StackUpdateStatusDto?> GetCachedStatusAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(s => s.Id == stackId, cancellationToken);

        if (stack == null)
        {
            return null;
        }

        var outdatedModules = string.IsNullOrEmpty(stack.OutdatedModulesJson)
            ? new List<ModuleVersionStatusDto>()
            : JsonSerializer.Deserialize<List<ModuleVersionStatusDto>>(stack.OutdatedModulesJson) ?? new List<ModuleVersionStatusDto>();

        // Populate CI build status if available
        CiBuildStatusDto? ciBuildStatus = null;
        if (!string.IsNullOrEmpty(stack.LatestCoreBuildStatus))
        {
            var cachedChecks = string.IsNullOrEmpty(stack.LatestCoreBuildChecksJson)
                ? new List<CiCheckDto>()
                : JsonSerializer.Deserialize<List<CiCheckDto>>(stack.LatestCoreBuildChecksJson) ?? new List<CiCheckDto>();
            
            ciBuildStatus = new CiBuildStatusDto
            {
                Status = stack.LatestCoreBuildStatus,
                CriticalChecks = cachedChecks,
                CheckedAt = stack.LatestCoreBuildStatusCheckedAt ?? DateTime.UtcNow
            };
        }

        return new StackUpdateStatusDto
        {
            StackId = stackId,
            HasUpdates = stack.IsOutdated,
            IsCoreOutdated = stack.IsCoreOutdated,
            OutdatedModuleCount = stack.OutdatedModuleCount,
            CurrentCoreSha = stack.CoreCommitSha,
            LatestCoreSha = stack.LatestAvailableCoreSha,
            OutdatedModules = outdatedModules,
            LastCheckedAt = stack.LastUpdateCheckAt,
            LatestCoreBuildStatus = ciBuildStatus
        };
    }

    /// <summary>
    /// Get the latest commit SHA from a remote Git repository
    /// </summary>
    private async Task<string> GetRemoteCommitShaAsync(string repositoryUrl, string branch, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"ls-remote {repositoryUrl} refs/heads/{branch}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to get remote commit SHA from {repositoryUrl} ({branch}): {error}");
        }

        // Output format: "SHA\trefs/heads/branch"
        var parts = output.Split('\t', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidOperationException($"Invalid git ls-remote output for {repositoryUrl} ({branch})");
        }

        return parts[0].Trim();
    }
    
    /// <summary>
    /// Fetch CI build status from GitHub and cache it in the database
    /// </summary>
    private async Task FetchAndCacheCiBuildStatusAsync(
        Data.Entities.ManagedStackEntity stack,
        string commitSha,
        CancellationToken cancellationToken)
    {
        // Cache for 5 minutes to avoid hitting GitHub API rate limits
        var cacheExpiration = TimeSpan.FromMinutes(5);
        var now = DateTime.UtcNow;
        
        if (stack.LatestCoreBuildStatusCheckedAt.HasValue &&
            now - stack.LatestCoreBuildStatusCheckedAt.Value < cacheExpiration)
        {
            _logger.LogDebug("Using cached CI build status for stack {StackId} (age: {Age:N1}s)",
                stack.Id, (now - stack.LatestCoreBuildStatusCheckedAt.Value).TotalSeconds);
            return;
        }
        
        try
        {
            // Parse repository URL to extract owner/repo (supports both GitHub URLs)
            var (owner, repo) = ParseGitHubRepository(stack.CoreRepositoryUrl);
            var repository = $"{owner}/{repo}";
            
            _logger.LogInformation("Fetching CI build status for {Repository} @ {Sha}", repository, commitSha.Substring(0, 7));
            
            var buildStatus = await _githubApi.GetCommitBuildStatusAsync(repository, commitSha, cancellationToken);
            
            // Store in database
            stack.LatestCoreBuildStatus = buildStatus.Status;
            stack.LatestCoreBuildChecksJson = JsonSerializer.Serialize(buildStatus.CriticalChecks);
            stack.LatestCoreBuildStatusCheckedAt = now;
            
            _logger.LogInformation("Stack {StackId} CI build status: {Status} ({PassedChecks} passed, {FailedChecks} failed)",
                stack.Id, buildStatus.Status, buildStatus.PassedChecks, buildStatus.FailedChecks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CI build status for stack {StackId}", stack.Id);
            // Don't fail the entire update check if CI status fetch fails
            stack.LatestCoreBuildStatus = "unknown";
            stack.LatestCoreBuildChecksJson = "[]";
            stack.LatestCoreBuildStatusCheckedAt = now;
        }
    }
    
    /// <summary>
    /// Parse GitHub repository URL to extract owner and repo name
    /// </summary>
    private (string owner, string repo) ParseGitHubRepository(string repositoryUrl)
    {
        // Support both HTTPS and SSH URLs:
        // https://github.com/azerothcore/azerothcore-wotlk.git
        // git@github.com:azerothcore/azerothcore-wotlk.git
        
        var url = repositoryUrl.TrimEnd('/').Replace(".git", "");
        
        if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = url.Substring("https://github.com/".Length).Split('/');
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
        }
        else if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = url.Substring("git@github.com:".Length).Split('/');
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
        }
        
        throw new InvalidOperationException($"Unable to parse GitHub repository URL: {repositoryUrl}");
    }
}
