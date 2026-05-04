namespace AzerothCoreManager.Infrastructure.Configuration;

/// <summary>
/// Configuration options for GitHub API integration
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>
    /// List of critical workflow names to check for build status
    /// </summary>
    public List<string> CriticalWorkflows { get; set; } = new()
    {
        "build-containers",
        "Build and Integration Test"
    };

    /// <summary>
    /// Optional GitHub personal access token for higher rate limits
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Minimum remaining rate limit before showing warning
    /// </summary>
    public int RateLimitBuffer { get; set; } = 100;
}
