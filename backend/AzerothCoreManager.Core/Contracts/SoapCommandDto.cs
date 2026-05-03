namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Request to execute a SOAP command on an AzerothCore worldserver
/// </summary>
public class SoapCommandRequest
{
    /// <summary>
    /// The command to execute (e.g., "account create username password")
    /// </summary>
    public string Command { get; set; } = string.Empty;
}

/// <summary>
/// Response from a SOAP command execution
/// </summary>
public class SoapCommandResponse
{
    /// <summary>
    /// Whether the command executed successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Raw response text from the server
    /// </summary>
    public string Result { get; set; } = string.Empty;
    
    /// <summary>
    /// Error message if the command failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
