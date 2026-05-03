using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Service for Docker operations
/// </summary>
public interface IDockerService
{
    /// <summary>
    /// Checks whether the Docker daemon is reachable.
    /// </summary>
    Task<bool> IsDockerAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists containers managed by Docker, optionally filtered to a specific compose stack.
    /// </summary>
    Task<IReadOnlyList<ContainerStatusDto>> ListContainersAsync(
        string? composeProjectName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams logs from a Docker container.
    /// </summary>
    /// <param name="containerId">Container ID or name</param>
    /// <param name="tail">Number of lines to fetch initially (default: 500)</param>
    /// <param name="onLogReceived">Callback invoked for each log line with (message, isError)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StreamContainerLogsAsync(
        string containerId,
        int tail,
        Func<string, bool, Task> onLogReceived,
        CancellationToken cancellationToken = default);
}
