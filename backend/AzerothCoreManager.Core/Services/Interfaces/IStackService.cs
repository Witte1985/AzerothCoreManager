using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Persists and retrieves managed AzerothCore stacks.
/// </summary>
public interface IStackService
{
    Task<IReadOnlyList<StackDetailsDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<StackDetailsDto?> GetAsync(string stackId, CancellationToken cancellationToken = default);

    Task<StackDetailsDto> CreateAsync(StackConfigurationDto configuration, CancellationToken cancellationToken = default);

    Task<StackDetailsDto?> UpdateAsync(string stackId, StackConfigurationDto configuration, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string stackId, CancellationToken cancellationToken = default);

    Task<bool> StartAsync(string stackId, CancellationToken cancellationToken = default);

    Task<bool> StopAsync(string stackId, CancellationToken cancellationToken = default);

    Task<bool> RestartAsync(string stackId, CancellationToken cancellationToken = default);
}
