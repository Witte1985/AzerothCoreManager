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
}
