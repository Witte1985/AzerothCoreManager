using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Core.Services.Interfaces;
using AzerothCoreManager.Infrastructure.Configuration;
using AzerothCoreManager.Infrastructure.Data;
using AzerothCoreManager.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AzerothCoreManager.Infrastructure.Services;

/// <summary>
/// Persistence-backed stack service.
/// </summary>
public sealed class StackService : IStackService
{
    private static readonly TimeSpan LifecycleVerificationTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LifecyclePollInterval = TimeSpan.FromSeconds(2);
    private static readonly string[] RequiredRunningServiceNames = ["database", "authserver", "worldserver"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AzerothCoreDbContext _dbContext;
    private readonly IDockerService _dockerService;
    private readonly DockerOptions _dockerOptions;

    public StackService(AzerothCoreDbContext dbContext, IDockerService dockerService, IOptions<DockerOptions> dockerOptions)
    {
        _dbContext = dbContext;
        _dockerService = dockerService;
        _dockerOptions = dockerOptions.Value;
    }

    public async Task<IReadOnlyList<StackDetailsDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var stacks = await _dbContext.ManagedStacks
            .OrderByDescending(stack => stack.CreatedAt)
            .ToListAsync(cancellationToken);

        var stackDtos = new List<StackDetailsDto>(stacks.Count);
        foreach (var stack in stacks)
        {
            stackDtos.Add(await MapAsync(stack, cancellationToken));
        }

        return stackDtos;
    }

    public async Task<StackDetailsDto?> GetAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        return stack is null
            ? null
            : await MapAsync(stack, cancellationToken);
    }

    public async Task<StackDetailsDto> CreateAsync(StackConfigurationDto configuration, CancellationToken cancellationToken = default)
    {
        var stack = new ManagedStackEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            StackName = configuration.StackName.Trim(),
            NormalizedStackName = NormalizeStackName(configuration.StackName),
            ServerType = configuration.ServerType,
            Status = StackStatus.Stopped,
            ModuleIdsJson = JsonSerializer.Serialize(configuration.ModuleIds, JsonOptions),
            DatabaseRootPassword = configuration.Database.RootPassword,
            DatabasePort = configuration.Database.Port,
            AuthServerPort = configuration.Ports.AuthServer,
            WorldServerPort = configuration.Ports.WorldServer,
            SoapPort = configuration.Ports.SoapPort,
            MaxPlayers = configuration.Advanced.MaxPlayers,
            RealmName = configuration.Advanced.RealmName.Trim(),
            CustomEnvVarsJson = JsonSerializer.Serialize(configuration.Advanced.CustomEnvVars, JsonOptions),
            SoapUsername = configuration.Advanced.SoapUsername,
            SoapPassword = configuration.Advanced.SoapPassword,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ManagedStacks.Add(stack);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await MapAsync(stack, cancellationToken);
    }

    public async Task<StackDetailsDto?> UpdateAsync(string stackId, StackConfigurationDto configuration, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        if (stack is null)
        {
            return null;
        }

        EnsureStackLifecycleAllowed(stack, "update");

        var wasRunning = stack.Status == StackStatus.Running;
        var oldModuleIds = Deserialize<List<string>>(stack.ModuleIdsJson) ?? [];
        var newModuleIds = configuration.ModuleIds ?? [];
        var modulesChanged = !oldModuleIds.SequenceEqual(newModuleIds);

        // Stop stack if it's running
        if (wasRunning)
        {
            await StopAsync(stackId, cancellationToken);
        }

        // Update database record
        stack.ModuleIdsJson = JsonSerializer.Serialize(configuration.ModuleIds, JsonOptions);
        stack.DatabaseRootPassword = configuration.Database.RootPassword;
        stack.DatabasePort = configuration.Database.Port;
        stack.AuthServerPort = configuration.Ports.AuthServer;
        stack.WorldServerPort = configuration.Ports.WorldServer;
        stack.SoapPort = configuration.Ports.SoapPort;
        stack.MaxPlayers = configuration.Advanced.MaxPlayers;
        stack.RealmName = configuration.Advanced.RealmName.Trim();
        stack.CustomEnvVarsJson = JsonSerializer.Serialize(configuration.Advanced.CustomEnvVars, JsonOptions);
        stack.SoapUsername = configuration.Advanced.SoapUsername;
        stack.SoapPassword = configuration.Advanced.SoapPassword;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Regenerate runtime configuration files if stack has been built
        var stackPath = GetStackPath(stackId);
        var repoPath = Path.Combine(stackPath, "azerothcore-wotlk");
        if (Directory.Exists(repoPath))
        {
            await EnsureRuntimeConfigurationAsync(stack, repoPath, cancellationToken);
        }

        // Restart stack if it was running and modules haven't changed
        if (wasRunning && !modulesChanged)
        {
            await StartAsync(stackId, cancellationToken);
        }

        return await MapAsync(stack, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        if (stack is null)
        {
            return false;
        }

        var stackPath = GetStackPath(stackId);
        var repoPath = Path.Combine(stackPath, "azerothcore-wotlk");

        // Stop containers if running
        try
        {
            if (Directory.Exists(repoPath))
            {
                await RunDockerComposeAsync(stackId, "down -v", repoPath, cancellationToken);
            }
        }
        catch
        {
            // Container might not exist, continue with cleanup
        }

        // Remove Docker images (gracefully handle if already removed)
        await RemoveDockerImagesAsync(stackId, cancellationToken);

        // Remove stack directory (gracefully handle if already removed)
        if (Directory.Exists(stackPath))
        {
            try
            {
                Directory.Delete(stackPath, recursive: true);
            }
            catch (IOException)
            {
                // Directory might be in use or already removed, continue
            }
            catch (UnauthorizedAccessException)
            {
                // Permission issue, continue anyway
            }
        }

        // Remove from database
        _dbContext.ManagedStacks.Remove(stack);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StartAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        if (stack is null)
        {
            return false;
        }

        EnsureStackLifecycleAllowed(stack, "start");

        var stackPath = GetStackPath(stackId);
        if (!Directory.Exists(stackPath))
        {
            throw new InvalidOperationException($"Stack directory not found: {stackPath}");
        }

        var repoPath = Path.Combine(stackPath, "azerothcore-wotlk");
        stack.Status = StackStatus.Starting;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await EnsureRuntimeConfigurationAsync(stack, repoPath, cancellationToken);
            await RunDockerComposeAsync(stackId, "up -d", repoPath, cancellationToken);
            await WaitForRunningServicesAsync(stackId, cancellationToken);

            stack.Status = StackStatus.Running;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            stack.Status = StackStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> StopAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        if (stack is null)
        {
            return false;
        }

        EnsureStackLifecycleAllowed(stack, "stop");

        var stackPath = GetStackPath(stackId);
        if (!Directory.Exists(stackPath))
        {
            throw new InvalidOperationException($"Stack directory not found: {stackPath}");
        }

        var repoPath = Path.Combine(stackPath, "azerothcore-wotlk");
        try
        {
            await RunDockerComposeAsync(stackId, "down", repoPath, cancellationToken);
            await WaitForStackToStopAsync(stackId, cancellationToken);

            stack.Status = StackStatus.Stopped;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            stack.Status = StackStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RestartAsync(string stackId, CancellationToken cancellationToken = default)
    {
        var stack = await _dbContext.ManagedStacks
            .SingleOrDefaultAsync(item => item.Id == stackId, cancellationToken);

        if (stack is null)
        {
            return false;
        }

        EnsureStackLifecycleAllowed(stack, "restart");

        var stackPath = GetStackPath(stackId);
        if (!Directory.Exists(stackPath))
        {
            throw new InvalidOperationException($"Stack directory not found: {stackPath}");
        }

        var repoPath = Path.Combine(stackPath, "azerothcore-wotlk");
        stack.Status = StackStatus.Starting;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await EnsureRuntimeConfigurationAsync(stack, repoPath, cancellationToken);
            await RunDockerComposeAsync(stackId, "up -d", repoPath, cancellationToken);
            await WaitForRunningServicesAsync(stackId, cancellationToken);

            stack.Status = StackStatus.Running;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch
        {
            stack.Status = StackStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private string GetStackPath(string stackId)
    {
        var configuredPath = _dockerOptions.BuildsPath;
        var baseDir = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(configuredPath);

        return Path.Combine(baseDir, stackId);
    }

    private async Task RunDockerComposeAsync(string stackId, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var (command, argPrefix) = DockerComposeHelper.GetDockerCompose(_dockerOptions.ComposeCommand);
        var composeProjectName = GetComposeProjectName(stackId);
        var fullArgs = string.IsNullOrEmpty(argPrefix)
            ? $"--project-name {composeProjectName} {arguments}"
            : $"{argPrefix} --project-name {composeProjectName} {arguments}";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = fullArgs,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["COMPOSE_PROJECT_NAME"] = composeProjectName;

        using var process = new Process { StartInfo = startInfo };
        var stdout = new List<string>();
        var stderr = new List<string>();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                stdout.Add(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                stderr.Add(eventArgs.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {command} process");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorOutput = stderr.Count > 0
                ? string.Join(Environment.NewLine, stderr)
                : string.Join(Environment.NewLine, stdout);

            throw new InvalidOperationException($"{command} {fullArgs} failed: {errorOutput}");
        }
    }

    private async Task<StackDetailsDto> MapAsync(ManagedStackEntity stack, CancellationToken cancellationToken)
    {
        // Get cached update status
        var outdatedModules = string.IsNullOrEmpty(stack.OutdatedModulesJson)
            ? new List<ModuleVersionStatusDto>()
            : Deserialize<List<ModuleVersionStatusDto>>(stack.OutdatedModulesJson) ?? new List<ModuleVersionStatusDto>();

        var updateStatus = new StackUpdateStatusDto
        {
            StackId = stack.Id,
            HasUpdates = stack.IsOutdated,
            IsCoreOutdated = stack.IsCoreOutdated,
            OutdatedModuleCount = stack.OutdatedModuleCount,
            CurrentCoreSha = stack.CoreCommitSha,
            LatestCoreSha = stack.LatestAvailableCoreSha,
            OutdatedModules = outdatedModules,
            LastCheckedAt = stack.LastUpdateCheckAt
        };

        return new StackDetailsDto
        {
            StackId = stack.Id,
            StackName = stack.StackName,
            ServerType = stack.ServerType,
            Status = stack.Status,
            CreatedAt = stack.CreatedAt,
            Containers = await GetContainersAsync(stack.Id, cancellationToken),
            Configuration = new StackConfigurationDto
            {
                StackName = stack.StackName,
                ServerType = stack.ServerType,
                ModuleIds = Deserialize<List<string>>(stack.ModuleIdsJson) ?? [],
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
                    CustomEnvVars = Deserialize<Dictionary<string, string>>(stack.CustomEnvVarsJson) ?? new Dictionary<string, string>()
                }
            },
            UpdateStatus = updateStatus
        };
    }

    private async Task<List<ContainerStatusDto>> GetContainersAsync(string stackId, CancellationToken cancellationToken)
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(GetComposeProjectName(stackId), cancellationToken);
            return containers.ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private async Task EnsureRuntimeConfigurationAsync(
        ManagedStackEntity stack,
        string repoPath,
        CancellationToken cancellationToken)
    {
        var environmentPath = Path.Combine(repoPath, ".env");
        var overridePath = Path.Combine(repoPath, "docker-compose.override.yml");
        var composeProjectName = GetComposeProjectName(stack.Id);

        // Generate volume paths for Docker-in-Docker mounting
        var logsPath = TranslateToHostPath(Path.Combine(repoPath, "env/dist/logs"));
        var etcPath = TranslateToHostPath(Path.Combine(repoPath, "env/dist/etc"));

        var environment = new StringBuilder()
            .AppendLine("# AzerothCore Environment Configuration")
            .AppendLine($"DOCKER_DB_ROOT_PASSWORD=\"{stack.DatabaseRootPassword}\"")
            .AppendLine($"DOCKER_DB_EXTERNAL_PORT={stack.DatabasePort}")
            .AppendLine($"DOCKER_WORLD_EXTERNAL_PORT={stack.WorldServerPort}")
            .AppendLine($"DOCKER_SOAP_EXTERNAL_PORT={stack.SoapPort}")
            .AppendLine($"DOCKER_AUTH_EXTERNAL_PORT={stack.AuthServerPort}")
            .AppendLine($"DOCKER_IMAGE_TAG={stack.Id}")
            .AppendLine($"COMPOSE_PROJECT_NAME={composeProjectName}")
            .AppendLine("DOCKER_USER_ID=1000")
            .AppendLine("DOCKER_GROUP_ID=1000")
            .AppendLine("DOCKER_USER=acore")
            .AppendLine($"DOCKER_VOL_LOGS={logsPath}")
            .AppendLine($"DOCKER_VOL_ETC={etcPath}");

        await File.WriteAllTextAsync(environmentPath, environment.ToString(), cancellationToken);
        await File.WriteAllTextAsync(
            overridePath,
            GenerateRuntimeDockerComposeOverride(stack, composeProjectName),
            cancellationToken);
    }

    private static string GenerateRuntimeDockerComposeOverride(ManagedStackEntity stack, string composeProjectName)
    {
        var customEnvironment = Deserialize<Dictionary<string, string>>(stack.CustomEnvVarsJson)
            ?? new Dictionary<string, string>();

        var sb = new StringBuilder();
        sb.AppendLine("# Docker Compose Override - Runtime Configuration");
        sb.AppendLine("# Generated by AzerothCore Manager");
        sb.AppendLine();
        sb.AppendLine("services:");
        AppendServiceOverride(sb, "ac-database", $"{composeProjectName}-database");
        AppendServiceOverride(sb, "ac-db-import", $"{composeProjectName}-db-import");
        AppendWorldserverOverride(sb, composeProjectName, customEnvironment);
        AppendAuthserverOverride(sb, composeProjectName);
        AppendServiceOverride(sb, "ac-client-data-init", $"{composeProjectName}-client-data-init");
        AppendServiceOverride(sb, "ac-tools", $"{composeProjectName}-tools");
        AppendServiceOverride(sb, "ac-dev-server", $"{composeProjectName}-dev-server");
        return sb.ToString();
    }

    private static void AppendServiceOverride(StringBuilder sb, string serviceName, string containerName)
    {
        sb.AppendLine($"  {serviceName}:");
        sb.AppendLine($"    container_name: {containerName}");
    }

    private static void AppendWorldserverOverride(
        StringBuilder sb,
        string composeProjectName,
        IReadOnlyDictionary<string, string> customEnvironment)
    {
        sb.AppendLine("  ac-worldserver:");
        sb.AppendLine($"    container_name: {composeProjectName}-worldserver");

        if (customEnvironment.Count == 0)
        {
            return;
        }

        sb.AppendLine("    environment:");
        foreach (var (key, value) in customEnvironment)
        {
            sb.AppendLine($"      {key}: \"{value}\"");
        }
    }

    private static void AppendAuthserverOverride(StringBuilder sb, string composeProjectName)
    {
        sb.AppendLine("  ac-authserver:");
        sb.AppendLine($"    container_name: {composeProjectName}-authserver");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"${DOCKER_AUTH_EXTERNAL_PORT}:3724\"");
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
        
        // Get the BuildsPath from options
        var buildsPath = _dockerOptions.BuildsPath;
        
        // Find the parent of BuildsPath (/app/data)
        var containerDataPath = Path.GetDirectoryName(buildsPath);
        if (string.IsNullOrEmpty(containerDataPath))
        {
            return containerPath;
        }
        
        // Check if containerPath starts with the container data path
        if (!containerPath.StartsWith(containerDataPath, StringComparison.Ordinal))
        {
            return containerPath;
        }
        
        // Replace container data path with host data path
        var relativePath = Path.GetRelativePath(containerDataPath, containerPath);
        var hostPath = Path.Combine(_dockerOptions.HostDataPath, relativePath);
        
        return hostPath;
    }

    private async Task WaitForRunningServicesAsync(string stackId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LifecycleVerificationTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var containers = await GetContainersAsync(stackId, cancellationToken);
            if (HasRequiredRunningServices(containers))
            {
                return;
            }

            await Task.Delay(LifecyclePollInterval, cancellationToken);
        }

        throw new InvalidOperationException("Stack containers did not reach a running state before the startup timeout elapsed.");
    }

    private async Task WaitForStackToStopAsync(string stackId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + LifecycleVerificationTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var containers = await GetContainersAsync(stackId, cancellationToken);
            if (containers.Count == 0 || containers.All(container => !IsRunning(container)))
            {
                return;
            }

            await Task.Delay(LifecyclePollInterval, cancellationToken);
        }

        throw new InvalidOperationException("Stack containers did not stop before the shutdown timeout elapsed.");
    }

    private static bool HasRequiredRunningServices(IEnumerable<ContainerStatusDto> containers)
    {
        var runningContainers = containers
            .Where(IsRunning)
            .Select(container => container.Name)
            .ToList();

        return RequiredRunningServiceNames.All(serviceName =>
            runningContainers.Any(containerName =>
                containerName.Contains(serviceName, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsRunning(ContainerStatusDto container)
    {
        return container.Status.Contains("running", StringComparison.OrdinalIgnoreCase)
            || container.Status.Contains("up", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureStackLifecycleAllowed(ManagedStackEntity stack, string operation)
    {
        if (stack.Status == StackStatus.Building)
        {
            throw new InvalidOperationException($"Cannot {operation} stack '{stack.StackName}' while it is building.");
        }
    }

    private static string GetComposeProjectName(string stackId)
    {
        return $"acore-{stackId}";
    }

    private static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string NormalizeStackName(string stackName)
    {
        return stackName.Trim().ToUpperInvariant();
    }

    private static async Task RemoveDockerImagesAsync(string stackId, CancellationToken cancellationToken)
    {
        // Detect whether to use podman or docker
        var dockerCommand = File.Exists("/usr/bin/podman") ? "podman" : "docker";
        
        // Images are tagged with stackId for isolation between stacks
        // Podman tags images with localhost/ prefix
        var imageNames = new[]
        {
            $"localhost/acore/ac-wotlk-worldserver:{stackId}",
            $"localhost/acore/ac-wotlk-authserver:{stackId}",
            $"localhost/acore/ac-wotlk-db-import:{stackId}",
            // Also try without localhost prefix for Docker compatibility
            $"acore/ac-wotlk-worldserver:{stackId}",
            $"acore/ac-wotlk-authserver:{stackId}",
            $"acore/ac-wotlk-db-import:{stackId}"
        };

        foreach (var imageName in imageNames)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = dockerCommand,
                    Arguments = $"rmi {imageName} -f",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    // Ignore exit code - image might already be removed
                }
            }
            catch
            {
                // Image might not exist or already removed, continue
            }
        }

        // Clean up dangling images (intermediate build stages)
        try
        {
            var pruneInfo = new ProcessStartInfo
            {
                FileName = dockerCommand,
                Arguments = "image prune -f",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var pruneProcess = Process.Start(pruneInfo);
            if (pruneProcess != null)
            {
                await pruneProcess.WaitForExitAsync(cancellationToken);
            }
        }
        catch
        {
            // Prune might fail, continue anyway
        }
    }
}
