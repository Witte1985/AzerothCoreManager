namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Service for Git operations
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks whether the git executable is available in the current environment.
    /// </summary>
    Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken = default);
}
