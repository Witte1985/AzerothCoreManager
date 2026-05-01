using Microsoft.AspNetCore.SignalR;

namespace AzerothCoreManager.Api.Hubs;

/// <summary>
/// SignalR hub for build progress subscriptions.
/// </summary>
public class BuildProgressHub : Hub
{
    public Task SubscribeToBuild(string stackId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, stackId);
    }

    public Task UnsubscribeFromBuild(string stackId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, stackId);
    }
}
