namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Complete configuration for an AzerothCore stack
/// </summary>
public class StackConfigurationDto
{
    /// <summary>
    /// Unique name for the stack (alphanumeric with dashes)
    /// </summary>
    public string StackName { get; set; } = string.Empty;
    
    /// <summary>
    /// Server type (Standard or Playerbots)
    /// </summary>
    public ServerType ServerType { get; set; }
    
    /// <summary>
    /// List of module IDs to include in build
    /// </summary>
    public List<string> ModuleIds { get; set; } = new();
    
    /// <summary>
    /// Database configuration
    /// </summary>
    public DatabaseConfigDto Database { get; set; } = new();
    
    /// <summary>
    /// Port assignments
    /// </summary>
    public PortConfigDto Ports { get; set; } = new();
    
    /// <summary>
    /// Advanced configuration options
    /// </summary>
    public AdvancedConfigDto Advanced { get; set; } = new();
}
