namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Container runtime status information
/// </summary>
public class ContainerStatusDto
{
    /// <summary>
    /// Container name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Container ID (12-character short form)
    /// </summary>
    public string ContainerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current status (running, stopped, exited, etc.)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Health check status (healthy, unhealthy, starting, none)
    /// </summary>
    public string Health { get; set; } = string.Empty;
    
    /// <summary>
    /// Timestamp when container was started
    /// </summary>
    public DateTime StartedAt { get; set; }
}
