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
    
    Task<bool> ApplyModuleConfigAsync(string stackId, Dictionary<string, string> envVars, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize the SOAP admin account for a stack by inserting it directly into the auth database.
    /// </summary>
    /// <param name="stackId">Stack identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Credentials if account was freshly created, null if already initialized</returns>
    /// <exception cref="StackNotFoundException">Stack not found</exception>
    /// <exception cref="InvalidOperationException">Stack is not running or database not accessible</exception>
    Task<SoapCredentialsDto?> InitializeAdminAccountAsync(string stackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve stored SOAP admin credentials for a stack (for recovery purposes).
    /// </summary>
    /// <returns>Credentials or null if the stack does not exist</returns>
    Task<SoapCredentialsDto?> GetSoapCredentialsAsync(string stackId, CancellationToken cancellationToken = default);
}
