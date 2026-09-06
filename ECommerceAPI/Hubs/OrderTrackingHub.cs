using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ECommerceAPI.Hubs;

[Authorize]
public class OrderTrackingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != null)
        {
            var groupName = GetUserGroupName(userId.Value);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            var groupName = GetUserGroupName(userId.Value);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                          ?? Context.User?.FindFirst("sub");

        if (userIdClaim == null)
            return null;

        return Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }

    public static string GetUserGroupName(Guid userId) => $"user:{userId}";
}

