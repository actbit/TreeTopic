using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Hubs;

public interface IRoomTopicHubClient
{
    Task RoomCreated(RoomRealtimeDto room);
    Task RoomUpdated(RoomRealtimeDto room);
    Task RoomDeleted(RoomDeletedEvent payload);
    Task TopicCreated(TopicRealtimeDto topic);
    Task TopicUpdated(TopicRealtimeDto topic);
    Task TopicDeleted(TopicDeletedEvent payload);
}

[Authorize]
public class RoomTopicHub : Hub<IRoomTopicHubClient>
{
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public RoomTopicHub(IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public Task JoinTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Task.CompletedTask;

        var groupName = RoomTopicHubGroups.Tenant(tenantId);
        var logger = Context.GetHttpContext()?.RequestServices.GetService<ILogger<RoomTopicHub>>();
        logger?.LogInformation("[RoomTopicHub] JoinTenant connection={ConnectionId} tenant={Tenant} group={Group}", Context.ConnectionId, tenantId, groupName);
        return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public Task JoinRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Task.CompletedTask;

        var groupName = RoomTopicHubGroups.Room(roomId);
        var logger = Context.GetHttpContext()?.RequestServices.GetService<ILogger<RoomTopicHub>>();
        logger?.LogInformation("[RoomTopicHub] JoinRoom connection={ConnectionId} room={Room} group={Group}", Context.ConnectionId, roomId, groupName);
        return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public Task LeaveRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
            return Task.CompletedTask;

        var groupName = RoomTopicHubGroups.Room(roomId);
        var logger = Context.GetHttpContext()?.RequestServices.GetService<ILogger<RoomTopicHub>>();
        logger?.LogInformation("[RoomTopicHub] LeaveRoom connection={ConnectionId} room={Room} group={Group}", Context.ConnectionId, roomId, groupName);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private string ResolveTenantKey()
    {
        var tenantFromRoute = Context.GetHttpContext()?.GetRouteValue("tenant")?.ToString();
        if (!string.IsNullOrWhiteSpace(tenantFromRoute))
        {
            return tenantFromRoute;
        }

        return RoomTopicHubGroups.ResolveTenantKey(_tenantAccessor.MultiTenantContext?.TenantInfo);
    }
}
