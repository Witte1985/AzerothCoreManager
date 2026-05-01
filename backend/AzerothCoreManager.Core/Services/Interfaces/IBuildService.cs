using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Service for orchestrating AzerothCore builds
/// </summary>
public interface IBuildService
{
    Task<BuildStatusDto> StartAsync(
        string stackId,
        StackConfigurationDto? configuration = null,
        CancellationToken cancellationToken = default);

    Task<BuildStatusDto?> GetStatusAsync(string stackId, CancellationToken cancellationToken = default);

    Task<bool> CancelAsync(string stackId, CancellationToken cancellationToken = default);

    Task<long> CleanupAsync(string stackId, CancellationToken cancellationToken = default);
}
