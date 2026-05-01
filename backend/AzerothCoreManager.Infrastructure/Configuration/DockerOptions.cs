namespace AzerothCoreManager.Infrastructure.Configuration;

/// <summary>
/// Configuration for Docker access and build storage.
/// </summary>
public sealed class DockerOptions
{
    public const string SectionName = "Docker";

    public string SocketPath { get; set; } = "unix:///var/run/docker.sock";

    public string BuildsPath { get; set; } = "/builds";
    
    /// <summary>
    /// Host filesystem path that corresponds to BuildsPath when running in Docker.
    /// Used for volume mounting in generated docker-compose files.
    /// Example: If BuildsPath=/app/data/stacks and this container mounts ./data to /app/data,
    /// then HostDataPath should be the absolute host path to ./data (e.g., /home/user/project/data).
    /// Leave empty to use BuildsPath as-is (for non-containerized deployments).
    /// </summary>
    public string? HostDataPath { get; set; }
    
    /// <summary>
    /// Docker Compose command format. Options: "plugin" (docker compose), "standalone" (docker-compose), or "auto" (detect).
    /// </summary>
    public string ComposeCommand { get; set; } = "auto";
}
