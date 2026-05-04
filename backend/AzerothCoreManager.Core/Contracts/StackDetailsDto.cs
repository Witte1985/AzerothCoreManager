namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Complete details about a deployed stack
/// </summary>
public class StackDetailsDto
{
    /// <summary>
    /// Unique stack identifier
    /// </summary>
    public string StackId { get; set; } = string.Empty;
    
    /// <summary>
    /// Stack name
    /// </summary>
    public string StackName { get; set; } = string.Empty;
    
    /// <summary>
    /// Server type (Standard or Playerbots)
    /// </summary>
    public ServerType ServerType { get; set; }
    
    /// <summary>
    /// Current operational status
    /// </summary>
    public StackStatus Status { get; set; }
    
    /// <summary>
    /// Status of all containers in the stack
    /// </summary>
    public List<ContainerStatusDto> Containers { get; set; } = new();
    
    /// <summary>
    /// Stack configuration
    /// </summary>
    public StackConfigurationDto Configuration { get; set; } = new();
    
    /// <summary>
    /// Timestamp when stack was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Update status for this stack (if available)
    /// </summary>
    public StackUpdateStatusDto? UpdateStatus { get; set; }
    
    /// <summary>
    /// Whether the SOAP admin account has been initialized
    /// </summary>
    public bool IsAdminAccountInitialized { get; set; }
    
    /// <summary>
    /// When the admin account was initialized
    /// </summary>
    public DateTime? AdminAccountInitializedAt { get; set; }
}
