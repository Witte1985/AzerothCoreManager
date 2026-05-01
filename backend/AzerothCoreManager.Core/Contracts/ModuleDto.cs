namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Module information for AzerothCore
/// </summary>
public class ModuleDto
{
    /// <summary>
    /// Unique module identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Module description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Git repository URL
    /// </summary>
    public string Repository { get; set; } = string.Empty;
    
    /// <summary>
    /// Git branch to clone
    /// </summary>
    public string Branch { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this module requires the Playerbots server variant
    /// </summary>
    public bool RequiresPlayerbots { get; set; }
}
