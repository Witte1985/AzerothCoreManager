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
    private readonly DockerOptions _dockerOptions;
    private readonly ILogger<StackVersionService> _logger;
    private readonly string _buildsPath;

    public StackVersionService(
        AzerothCoreDbContext dbContext,
        IModuleCatalogService moduleCatalog,
        IOptions<DockerOptions> dockerOptions,
        ILogger<StackVersionService> logger)
    {
        _dbContext = dbContext;
        _moduleCatalog = moduleCatalog;
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

        return new StackUpdateStatusDto
        {
            StackId = stackId,
            HasUpdates = stack.IsOutdated,
            IsCoreOutdated = stack.IsCoreOutdated,
            OutdatedModuleCount = stack.OutdatedModuleCount,
            CurrentCoreSha = stack.CoreCommitSha,
            LatestCoreSha = stack.LatestAvailableCoreSha,
            OutdatedModules = outdatedModules,
            LastCheckedAt = stack.LastUpdateCheckAt
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
}
