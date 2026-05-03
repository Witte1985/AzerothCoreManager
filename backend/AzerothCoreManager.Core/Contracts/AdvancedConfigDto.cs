namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Advanced configuration options for AzerothCore server
/// </summary>
public class AdvancedConfigDto
{
    /// <summary>
    /// Maximum number of concurrent players (default: 100)
    /// </summary>
    public int MaxPlayers { get; set; } = 100;
    
    /// <summary>
    /// Display name for the realm
    /// </summary>
    public string RealmName { get; set; } = string.Empty;
    
    /// <summary>
    /// Custom environment variables for AzerothCore containers
    /// </summary>
    public Dictionary<string, string> CustomEnvVars { get; set; } = new();
    
    /// <summary>
    /// SOAP admin username (default: admin)
    /// </summary>
    public string SoapUsername { get; set; } = "admin";
    
    /// <summary>
    /// SOAP admin password (default: admin)
    /// </summary>
    public string SoapPassword { get; set; } = "admin";
}
