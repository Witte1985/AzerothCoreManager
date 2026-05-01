namespace AzerothCoreManager.Core.Contracts;

/// <summary>
/// Stack operational status
/// </summary>
public enum StackStatus
{
    /// <summary>
    /// Stack is currently building
    /// </summary>
    Building,
    
    /// <summary>
    /// Stack is deployed but containers are stopped
    /// </summary>
    Stopped,
    
    /// <summary>
    /// Stack is starting up
    /// </summary>
    Starting,
    
    /// <summary>
    /// Stack is running normally
    /// </summary>
    Running,
    
    /// <summary>
    /// Stack has failed or is in error state
    /// </summary>
    Failed
}
