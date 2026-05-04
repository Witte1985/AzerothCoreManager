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
    
    /// <summary>
    /// Import a discovered stack into the manager database
    /// </summary>
    /// <param name="stackId">Stack identifier from discovery</param>
    /// <param name="request">Import configuration (name, passwords)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Imported stack details</returns>
    /// <exception cref="StackNotFoundException">Stack not found or orphaned</exception>
    /// <exception cref="StackConflictException">Stack ID or ports conflict with existing stacks</exception>
    Task<StackDetailsDto> ImportDiscoveredStackAsync(
        string stackId, 
        ImportStackRequestDto request, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initialize SOAP admin account for a stack by creating the account in the worldserver database
    /// </summary>
    /// <param name="stackId">Stack identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if account was created successfully, false if already initialized</returns>
    /// <exception cref="StackNotFoundException">Stack not found</exception>
    /// <exception cref="InvalidOperationException">Stack is not running or worldserver not accessible</exception>
    Task<bool> InitializeAdminAccountAsync(string stackId, CancellationToken cancellationToken = default);
}
