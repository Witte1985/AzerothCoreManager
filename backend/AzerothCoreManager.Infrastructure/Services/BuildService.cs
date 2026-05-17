using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Configuration;
using AzerothCoreManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// Orchestrates AzerothCore builds: clone, configure, build Docker images, stream progress.
/// </summary>
public sealed class BuildService : IBuildService
{
    private static readonly ConcurrentDictionary<string, BuildStatusDto> BuildStates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> BuildCancellations = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _buildsPath;
    private readonly DockerOptions _dockerOptions;
    private readonly IBuildEventPublisher _eventPublisher;
    private readonly ILogger<BuildService> _logger;

    public BuildService(
        IServiceScopeFactory scopeFactory,
        IOptions<DockerOptions> dockerOptions,
        IBuildEventPublisher eventPublisher,
        ILogger<BuildService> logger)
    {
        _scopeFactory = scopeFactory;
        _dockerOptions = dockerOptions.Value;
        _eventPublisher = eventPublisher;
        _logger = logger;
        
        // Resolve relative paths from the current directory (project root when using dotnet run/watch)
        var configuredPath = _dockerOptions.BuildsPath;
        _buildsPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath);
        
        // Ensure the builds directory exists
        Directory.CreateDirectory(_buildsPath);
        _logger.LogInformation("Builds path resolved to: {BuildsPath}", _buildsPath);
    }

    public async Task<BuildStatusDto> StartAsync(
        string stackId,
        StackConfigurationDto? configuration = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AzerothCoreDbContext>();
        
        var stack = await dbContext.ManagedStacks.SingleAsync(item => item.Id == stackId, cancellationToken);

        // Cancel any existing build before starting a new one (allows rebuilding stuck/failed builds)
        if (BuildStates.TryGetValue(stackId, out var existingBuild))
        {
            if (existingBuild.CurrentPhase is not (BuildPhase.Completed or BuildPhase.Failed))
            {
                _logger.LogWarning("Cancelling existing build for stack {StackId} to start rebuild", stackId);
                if (BuildCancellations.TryGetValue(stackId, out var existingCts))
                {
                    existingCts.Cancel();
                }
            }
        }

        // If no configuration provided, use existing stack configuration (for rebuilds)
        var buildConfig = configuration ?? new StackConfigurationDto
        {
            StackName = stack.StackName,
            ServerType = stack.ServerType,
            ModuleIds = JsonSerializer.Deserialize<List<string>>(stack.ModuleIdsJson) ?? [],
            Database = new DatabaseConfigDto
            {
                RootPassword = stack.DatabaseRootPassword,
                Port = stack.DatabasePort
            },
            Ports = new PortConfigDto
            {
                AuthServer = stack.AuthServerPort,
                WorldServer = stack.WorldServerPort,
                SoapPort = stack.SoapPort
            },
            Advanced = new AdvancedConfigDto
            {
                MaxPlayers = stack.MaxPlayers,
                RealmName = stack.RealmName,
                CustomEnvVars = JsonSerializer.Deserialize<Dictionary<string, string>>(stack.CustomEnvVarsJson) ?? new Dictionary<string, string>()
            }
        };
        
        if (buildConfig is null)
        {
            throw new InvalidOperationException("Configuration is required to start a build (no existing configuration found)");
        }
        
        // Debug logging for rebuilds
        if (configuration is null)
        {
            _logger.LogInformation("Rebuild: Using existing config - DB Password length: {PasswordLength}, Stack: {StackName}",
                buildConfig.Database.RootPassword?.Length ?? 0, buildConfig.StackName);
        }

        var buildStatus = new BuildStatusDto
        {
            BuildId = Guid.NewGuid().ToString("N"),
            CurrentPhase = BuildPhase.Cloning,
            ProgressPercent = 0,
            CurrentStep = "Initializing build...",
            RecentLogs = [$"Starting build for stack '{stack.StackName}'"],
            StartedAt = DateTime.UtcNow
        };

        BuildStates[stackId] = buildStatus;
        stack.Status = StackStatus.Building;
        await dbContext.SaveChangesAsync(cancellationToken);

        var buildCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        BuildCancellations[stackId] = buildCts;

        _ = Task.Run(async () => await ExecuteBuildAsync(stackId, stack.StackName, buildConfig, buildCts.Token), CancellationToken.None);

        return buildStatus;
    }

    private async Task ExecuteBuildAsync(
        string stackId,
        string stackName,
        StackConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting build for stack {StackId}", stackId);
            
            var buildPath = Path.Combine(_buildsPath, stackId);
            Directory.CreateDirectory(buildPath);
            _logger.LogInformation("Build path created: {BuildPath}", buildPath);

            // Mark all directories as safe for git (avoids "dubious ownership" errors in Docker
            // where files may be owned by a different UID than the running process)
            await RunProcessAsync(stackId, "git", "config --global --add safe.directory *", buildPath, cancellationToken);

            // Determine repository URL and branch
            // For updates (configuration is null), use stored values from database if available
            // For new builds, use the configuration-based defaults
            string repoUrl;
            string branch;
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AzerothCoreDbContext>();
                var stack = await dbContext.ManagedStacks.SingleOrDefaultAsync(s => s.Id == stackId, cancellationToken);
                
                if (stack is not null && !string.IsNullOrEmpty(stack.CoreRepositoryUrl))
                {
                    // Use stored repository info (handles imported stacks and updates)
                    repoUrl = stack.CoreRepositoryUrl;
                    branch = !string.IsNullOrEmpty(stack.CoreBranch) ? stack.CoreBranch : "master";
                    
                    _logger.LogInformation(
                        "Using stored repository info for stack {StackId}: {RepoUrl} @ {Branch}",
                        stackId, repoUrl, branch);
                }
                else
                {
                    // Fall back to ServerType-based defaults (new builds only)
                    (repoUrl, branch) = configuration.ServerType switch
                    {
                        ServerType.Playerbots => ("https://github.com/mod-playerbots/azerothcore-wotlk.git", "Playerbot"),
                        _ => ("https://github.com/azerothcore/azerothcore-wotlk.git", "master")
                    };
                    
                    _logger.LogInformation(
                        "Using default repository for ServerType {ServerType}: {RepoUrl} @ {Branch}",
                        configuration.ServerType, repoUrl, branch);
                    
                    // Save repository info to database for future updates
                    if (stack is not null)
                    {
                        stack.CoreRepositoryUrl = repoUrl;
                        stack.CoreBranch = branch;
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            await CloneRepositoryAsync(stackId, buildPath, repoUrl, branch, configuration, cancellationToken);
            _logger.LogInformation("Repository cloned successfully for stack {StackId}", stackId);
            
            await PrepareModulesAsync(stackId, buildPath, configuration, cancellationToken);
            _logger.LogInformation("Modules prepared for stack {StackId}", stackId);
            
            await GenerateDockerComposeAsync(stackId, buildPath, configuration, cancellationToken);
            _logger.LogInformation("Docker Compose generated for stack {StackId}", stackId);
            
            await BuildDockerImagesAsync(stackId, buildPath, cancellationToken);
            _logger.LogInformation("Docker images ready for stack {StackId}", stackId);
            
            await CompleteBuildAsync(stackId);
            _logger.LogInformation("Build completed successfully for stack {StackId}", stackId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Build cancelled for stack {StackId}", stackId);
            await FailBuildAsync(stackId, "Build was cancelled by user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Build failed for stack {StackId}", stackId);
            await FailBuildAsync(stackId, $"Build failed: {ex.Message}");
            throw; // Re-throw to ensure we see it in logs
        }
        finally
        {
            BuildCancellations.TryRemove(stackId, out _);
        }
    }

    private async Task CloneRepositoryAsync(
        string stackId, 
        string buildPath, 
        string repoUrl, 
        string branch, 
        StackConfigurationDto configuration, 
        CancellationToken cancellationToken)
    {
        await UpdateBuildStatusAsync(stackId, BuildPhase.Cloning, 10, "Cloning AzerothCore repository...", null);

        var repoPath = Path.Combine(buildPath, "azerothcore-wotlk");
        
        if (Directory.Exists(repoPath))
        {
            await AddLogAsync(stackId, "Repository already exists, pulling latest changes...");
            await RunProcessAsync(stackId, "git", "pull", repoPath, cancellationToken);
        }
        else
        {
            await AddLogAsync(stackId, $"Cloning {configuration.ServerType} AzerothCore repository from GitHub...");
            await AddLogAsync(stackId, $"Repository: {repoUrl} @ {branch}");
            await RunProcessAsync(
                stackId,
                "git",
                $"clone --depth 1 --branch {branch} {repoUrl} azerothcore-wotlk",
                buildPath,
                cancellationToken);
        }

        await UpdateBuildStatusAsync(stackId, BuildPhase.Cloning, 25, "Repository cloned successfully", null);
    }

    private async Task PrepareModulesAsync(
        string stackId,
        string buildPath,
        StackConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        await UpdateBuildStatusAsync(stackId, BuildPhase.PreparingModules, 30, "Preparing modules...", null);

        if (configuration.ModuleIds.Count == 0)
        {
            await AddLogAsync(stackId, "No modules selected, skipping module preparation");
            return;
        }

        await AddLogAsync(stackId, $"Integrating {configuration.ModuleIds.Count} module(s)...");
        
        var modulesPath = Path.Combine(buildPath, "azerothcore-wotlk", "modules");
        Directory.CreateDirectory(modulesPath);

        using var scope = _scopeFactory.CreateScope();
        var moduleCatalog = scope.ServiceProvider.GetRequiredService<IModuleCatalogService>();
        var allModules = await moduleCatalog.ListAsync(configuration.ServerType, cancellationToken);

        foreach (var moduleId in configuration.ModuleIds)
        {
            var module = allModules.FirstOrDefault(m => m.Id == moduleId);
            if (module == null)
            {
                await AddLogAsync(stackId, $"Warning: Module {moduleId} not found in catalog, skipping");
                continue;
            }

            var moduleDir = Path.Combine(modulesPath, moduleId);
            
            if (Directory.Exists(moduleDir))
            {
                await AddLogAsync(stackId, $"Module {module.Name} already exists, pulling latest...");
                await RunProcessAsync(stackId, "git", "pull", moduleDir, cancellationToken);
            }
            else
            {
                await AddLogAsync(stackId, $"Cloning module: {module.Name}");
                await RunProcessAsync(
                    stackId,
                    "git",
                    $"clone --depth 1 --branch {module.Branch} {module.Repository} {moduleId}",
                    modulesPath,
                    cancellationToken);
            }
        }

        await UpdateBuildStatusAsync(stackId, BuildPhase.PreparingModules, 40, "Modules prepared", null);
    }

    private async Task GenerateDockerComposeAsync(
        string stackId,
        string buildPath,
        StackConfigurationDto configuration,
        CancellationToken cancellationToken)
    {
        await UpdateBuildStatusAsync(stackId, BuildPhase.Building, 45, "Generating Docker Compose configuration...", null);

        // AzerothCore already has docker-compose.yml, we create override and .env
        var repoPath = Path.Combine(buildPath, "azerothcore-wotlk");
        var overridePath = Path.Combine(repoPath, "docker-compose.override.yml");
        var envPath = Path.Combine(repoPath, ".env");
        
        // Create .env file with configuration (use stackId as unique tag)
        var envContent = GenerateEnvContent(stackId, configuration);
        await File.WriteAllTextAsync(envPath, envContent, cancellationToken);
        await AddLogAsync(stackId, "Environment configuration created");

        // Create docker-compose.override.yml for custom settings
        var modulesPath = TranslateToHostPath(Path.Combine(repoPath, "modules"));
        var overrideContent = GenerateDockerComposeOverride(stackId, configuration, modulesPath);
        await File.WriteAllTextAsync(overridePath, overrideContent, cancellationToken);
        await AddLogAsync(stackId, "Docker Compose override created");

        await UpdateBuildStatusAsync(stackId, BuildPhase.Building, 50, "Configuration generated", null);
    }

    private string GenerateEnvContent(string stackId, StackConfigurationDto config)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AzerothCore Environment Configuration");
        
        // Quote password to handle special characters
        sb.AppendLine($"DOCKER_DB_ROOT_PASSWORD=\"{config.Database.RootPassword}\"");
        sb.AppendLine($"DOCKER_DB_EXTERNAL_PORT={config.Database.Port}");
        sb.AppendLine($"DOCKER_WORLD_EXTERNAL_PORT={config.Ports.WorldServer}");
        sb.AppendLine($"DOCKER_SOAP_EXTERNAL_PORT={config.Ports.SoapPort}");
        
        // Auth server port - need to override in docker-compose.override.yml
        sb.AppendLine($"DOCKER_AUTH_EXTERNAL_PORT={config.Ports.AuthServer}");
        
        // Use stackId as unique image tag to avoid collision between stacks
        sb.AppendLine($"DOCKER_IMAGE_TAG={stackId}");
        sb.AppendLine($"COMPOSE_PROJECT_NAME={DockerComposeOverrideGenerator.GetComposeProjectName(stackId)}");
        
        // User/Group IDs for Podman/Docker
        sb.AppendLine("DOCKER_USER_ID=1000");
        sb.AppendLine("DOCKER_GROUP_ID=1000");
        sb.AppendLine("DOCKER_USER=acore");
        
        // Volume paths - must be host paths when using Docker socket
        // If HostDataPath is configured, translate container paths to host paths
        var stackPath = Path.Combine(_buildsPath, stackId, "azerothcore-wotlk");
        var logsPath = TranslateToHostPath(Path.Combine(stackPath, "env/dist/logs"));
        var etcPath = TranslateToHostPath(Path.Combine(stackPath, "env/dist/etc"));
        
        sb.AppendLine($"DOCKER_VOL_LOGS={logsPath}");
        sb.AppendLine($"DOCKER_VOL_ETC={etcPath}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Translates a container-internal path to the corresponding host path.
    /// Required when using Docker socket to create containers from within a container.
    /// </summary>
    private string TranslateToHostPath(string containerPath)
    {
        // If no HostDataPath configured, return path as-is (non-containerized deployment)
        if (string.IsNullOrWhiteSpace(_dockerOptions.HostDataPath))
        {
            return containerPath;
        }
        
        // BuildsPath example: /app/data/stacks
        // HostDataPath example: /home/user/project/data
        // containerPath example: /app/data/stacks/abc123/azerothcore-wotlk/env/dist/logs
        // Expected result: /home/user/project/data/stacks/abc123/azerothcore-wotlk/env/dist/logs
        
        // Find the parent of BuildsPath (/app/data)
        var containerDataPath = Path.GetDirectoryName(_buildsPath);
        if (string.IsNullOrEmpty(containerDataPath))
        {
            _logger.LogWarning("Could not determine container data path from BuildsPath {BuildsPath}", _buildsPath);
            return containerPath;
        }
        
        // Check if containerPath starts with the container data path
        if (!containerPath.StartsWith(containerDataPath, StringComparison.Ordinal))
        {
            _logger.LogWarning("Container path {ContainerPath} does not start with {ContainerDataPath}", 
                containerPath, containerDataPath);
            return containerPath;
        }
        
        // Replace container data path with host data path
        var relativePath = Path.GetRelativePath(containerDataPath, containerPath);
        var hostPath = Path.Combine(_dockerOptions.HostDataPath, relativePath);
        
        _logger.LogDebug("Translated path: {ContainerPath} -> {HostPath}", containerPath, hostPath);
        return hostPath;
    }

    private string GenerateDockerComposeOverride(string stackId, StackConfigurationDto config, string modulesHostPath)
    {
        return DockerComposeOverrideGenerator.Generate(stackId, config.Advanced.CustomEnvVars, modulesHostPath);
    }

    private async Task BuildDockerImagesAsync(string stackId, string buildPath, CancellationToken cancellationToken)
    {
        await UpdateBuildStatusAsync(stackId, BuildPhase.CreatingImages, 60, "Building Docker images...", null);
        await AddLogAsync(stackId, "Starting Docker build process (this may take several minutes)...");

        var repoPath = Path.Combine(buildPath, "azerothcore-wotlk");

        // Build using Docker Compose in the azerothcore-wotlk directory
        await AddLogAsync(stackId, "Building AzerothCore from source using Docker Compose...");
        await AddLogAsync(stackId, "This will take 15-30 minutes on first build (compiling C++ code)...");
        
        try
        {
            await UpdateBuildStatusAsync(stackId, BuildPhase.CreatingImages, 65, "Building images from source...", null);

            // Get the appropriate docker compose command
            var (command, argPrefix) = DockerComposeHelper.GetDockerCompose(_dockerOptions.ComposeCommand);
            var composeArgs = string.IsNullOrEmpty(argPrefix) ? "build" : $"{argPrefix} build";
            
            // Build all services defined in docker-compose.yml (run from azerothcore-wotlk dir)
            // Use cache for faster builds (removed --no-cache)
            await RunProcessAsync(
                stackId,
                command,
                composeArgs,
                repoPath, // Run from the repo directory where docker-compose.yml is
                cancellationToken);

            await UpdateBuildStatusAsync(stackId, BuildPhase.CreatingImages, 95, "All images built successfully", null);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("exited with code"))
        {
            // Check if images were actually created despite non-zero exit code
            // podman-compose can return 125 even on successful builds
            await AddLogAsync(stackId, $"Build process exited with non-zero code, verifying images...");
            
            var imagesExist = await VerifyImagesExistAsync(stackId, repoPath, cancellationToken);
            if (imagesExist)
            {
                await AddLogAsync(stackId, "Images verified successfully - build completed despite exit code");
                await UpdateBuildStatusAsync(stackId, BuildPhase.CreatingImages, 95, "All images built successfully", null);
            }
            else
            {
                throw new InvalidOperationException($"Failed to build Docker images: {ex.Message}", ex);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to build Docker images: {ex.Message}", ex);
        }

        await AddLogAsync(stackId, "All Docker images are ready");
    }

    private async Task<bool> VerifyImagesExistAsync(string stackId, string repoPath, CancellationToken cancellationToken)
    {
        try
        {
            // Check if the main AzerothCore images exist
            // Try podman first (Fedora), fallback to docker
            var dockerCommand = File.Exists("/usr/bin/podman") ? "podman" : "docker";
            
            // Images are tagged with stackId for isolation
            var imageNames = new[]
            {
                $"localhost/acore/ac-wotlk-worldserver:{stackId}",
                $"localhost/acore/ac-wotlk-authserver:{stackId}",
                $"localhost/acore/ac-wotlk-db-import:{stackId}"
            };

            var foundCount = 0;
            foreach (var imageName in imageNames)
            {
                var verifyProcess = new ProcessStartInfo
                {
                    FileName = dockerCommand,
                    Arguments = $"images -q {imageName}",
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(verifyProcess);
                if (process == null) continue;

                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    foundCount++;
                }
            }
            
            var allExist = foundCount >= 3;
            await AddLogAsync(stackId, $"Image verification: found {foundCount}/3 images");
            return allExist;
        }
        catch (Exception ex)
        {
            await AddLogAsync(stackId, $"Failed to verify images: {ex.Message}");
            return false;
        }
    }

    private async Task CompleteBuildAsync(string stackId)
    {
        await UpdateBuildStatusAsync(stackId, BuildPhase.Completed, 100, "Build completed successfully!", null);
        
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AzerothCoreDbContext>();
        
        var stack = await dbContext.ManagedStacks.SingleOrDefaultAsync(s => s.Id == stackId);
        if (stack is not null)
        {
            stack.Status = StackStatus.Stopped;
            
            // Capture version information (commit SHAs) for update tracking
            try
            {
                var buildPath = Path.Combine(_buildsPath, stackId);
                var repoPath = Path.Combine(buildPath, "azerothcore-wotlk");
                
                // Capture core repository SHA
                if (Directory.Exists(repoPath))
                {
                    stack.CoreCommitSha = await GetCurrentCommitShaAsync(repoPath, CancellationToken.None);
                    stack.LastBuiltAt = DateTime.UtcNow;
                    _logger.LogInformation("Captured core commit SHA {Sha} for stack {StackId}", 
                        stack.CoreCommitSha, stackId);
                }
                
                // Capture module SHAs
                var moduleVersions = new List<ModuleVersionInfo>();
                var modulesPath = Path.Combine(repoPath, "modules");
                
                if (Directory.Exists(modulesPath))
                {
                    var moduleIds = JsonSerializer.Deserialize<List<string>>(stack.ModuleIdsJson) ?? [];
                    var moduleCatalog = scope.ServiceProvider.GetRequiredService<IModuleCatalogService>();
                    var allModules = await moduleCatalog.ListAsync(stack.ServerType, CancellationToken.None);
                    
                    foreach (var moduleId in moduleIds)
                    {
                        var modulePath = Path.Combine(modulesPath, moduleId);
                        if (Directory.Exists(modulePath))
                        {
                            var module = allModules.FirstOrDefault(m => m.Id == moduleId);
                            if (module != null)
                            {
                                var sha = await GetCurrentCommitShaAsync(modulePath, CancellationToken.None);
                                moduleVersions.Add(new ModuleVersionInfo
                                {
                                    ModuleId = moduleId,
                                    CommitSha = sha,
                                    Repository = module.Repository,
                                    Branch = module.Branch
                                });
                                _logger.LogInformation("Captured module {ModuleId} commit SHA {Sha}", 
                                    moduleId, sha);
                            }
                        }
                    }
                }
                
                stack.ModuleVersionsJson = JsonSerializer.Serialize(moduleVersions);
                
                // Clear update flags since we just built the latest version
                stack.IsOutdated = false;
                stack.IsCoreOutdated = false;
                stack.OutdatedModuleCount = 0;
                stack.LatestAvailableCoreSha = stack.CoreCommitSha;
                stack.OutdatedModulesJson = "[]";
                stack.LastUpdateCheckAt = DateTime.UtcNow;
                
                _logger.LogInformation("Cleared update flags for stack {StackId} after successful build", stackId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture version information for stack {StackId}", stackId);
                // Don't fail the build if version capture fails
            }
            
            await dbContext.SaveChangesAsync();
        }

        await _eventPublisher.PublishBuildCompletedAsync(stackId, true);
    }

    private async Task FailBuildAsync(string stackId, string errorMessage)
    {
        if (BuildStates.TryGetValue(stackId, out var buildStatus))
        {
            buildStatus.CurrentPhase = BuildPhase.Failed;
            buildStatus.CurrentStep = "Build failed";
            buildStatus.ErrorMessage = errorMessage;
            buildStatus.RecentLogs.Add($"ERROR: {errorMessage}");

            await _eventPublisher.PublishBuildFailedAsync(stackId, errorMessage);
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AzerothCoreDbContext>();
        
        var stack = await dbContext.ManagedStacks.SingleOrDefaultAsync(s => s.Id == stackId);
        if (stack is not null)
        {
            stack.Status = StackStatus.Stopped;
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateBuildStatusAsync(
        string stackId,
        BuildPhase phase,
        int progressPercent,
        string currentStep,
        string? logLine)
    {
        if (!BuildStates.TryGetValue(stackId, out var buildStatus))
        {
            return;
        }

        buildStatus.CurrentPhase = phase;
        buildStatus.ProgressPercent = progressPercent;
        buildStatus.CurrentStep = currentStep;

        if (logLine is not null)
        {
            buildStatus.RecentLogs.Add(logLine);
            if (buildStatus.RecentLogs.Count > 50)
            {
                buildStatus.RecentLogs.RemoveAt(0);
            }
        }

        await _eventPublisher.PublishPhaseChangedAsync(stackId, phase.ToString());
        await _eventPublisher.PublishProgressUpdatedAsync(stackId, progressPercent, currentStep);
    }

    private async Task AddLogAsync(string stackId, string logLine)
    {
        if (!BuildStates.TryGetValue(stackId, out var buildStatus))
        {
            _logger.LogWarning("Attempted to add log for non-existent build: {StackId}", stackId);
            return;
        }

        var timestampedLog = $"[{DateTime.UtcNow:HH:mm:ss}] {logLine}";
        buildStatus.RecentLogs.Add(timestampedLog);
        
        if (buildStatus.RecentLogs.Count > 50)
        {
            buildStatus.RecentLogs.RemoveAt(0);
        }

        _logger.LogInformation("Build {StackId}: {LogLine}", stackId, logLine);
        
        try
        {
            await _eventPublisher.PublishLogReceivedAsync(stackId, timestampedLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish log to SignalR for stack {StackId}", stackId);
        }
    }

    private async Task RunProcessAsync(
        string stackId,
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        // Use synchronous event handlers to avoid async void issues
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                // Fire and forget - don't await to avoid blocking the event handler
                _ = AddLogAsync(stackId, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _ = AddLogAsync(stackId, $"STDERR: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process '{fileName} {arguments}' exited with code {process.ExitCode}");
        }
    }

    public Task<BuildStatusDto?> GetStatusAsync(string stackId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildStates.TryGetValue(stackId, out var buildStatus);
        return Task.FromResult(buildStatus);
    }

    public async Task<bool> CancelAsync(string stackId, CancellationToken cancellationToken = default)
    {
        if (!BuildCancellations.TryGetValue(stackId, out var cts))
        {
            return false;
        }

        await AddLogAsync(stackId, "Cancellation requested...");
        cts.Cancel();
        
        return true;
    }

    public async Task<long> CleanupAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var buildPath = Path.Combine(_buildsPath, stackId);
        long freedSpace = 0;

        if (Directory.Exists(buildPath))
        {
            var dirInfo = new DirectoryInfo(buildPath);
            freedSpace = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            
            Directory.Delete(buildPath, recursive: true);
            _logger.LogInformation("Cleaned up build directory for stack {StackId}, freed {FreedSpace} bytes", stackId, freedSpace);
        }

        BuildStates.TryRemove(stackId, out _);
        BuildCancellations.TryRemove(stackId, out _);
        
        return freedSpace;
    }
    
    /// <summary>
    /// Get the current commit SHA from a git repository
    /// </summary>
    private async Task<string> GetCurrentCommitShaAsync(string gitRepoPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse HEAD",
            WorkingDirectory = gitRepoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to get commit SHA: git exited with code {process.ExitCode}");
        }

        return output.Trim();
    }
}

/// <summary>
/// Module version information for tracking
/// </summary>
internal record ModuleVersionInfo
{
    public string ModuleId { get; init; } = string.Empty;
    public string CommitSha { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
}
