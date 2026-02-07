using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

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
    private readonly IRealtimeAccessService _realtimeAccessService;
    private readonly ILogger<RoomTopicHub> _logger;

    public RoomTopicHub(
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IRealtimeAccessService realtimeAccessService,
        ILogger<RoomTopicHub> logger)
    {
        _tenantAccessor = tenantAccessor;
        _realtimeAccessService = realtimeAccessService;
        _logger = logger;
    }

    public async Task JoinTenant(string tenantId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return;

            var routeTenant = Context.GetHttpContext()?.GetRouteValue("tenant")?.ToString();
            if (!string.IsNullOrWhiteSpace(routeTenant) &&
                !string.Equals(routeTenant, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[RoomTopicHub] JoinTenant denied by tenant mismatch connection={ConnectionId} routeTenant={RouteTenant} requestedTenant={RequestedTenant}",
                    Context.ConnectionId, routeTenant, tenantId);
                return;
            }

            var groupName = RoomTopicHubGroups.Tenant(tenantId);
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                _logger.LogInformation("[RoomTopicHub] JoinTenant connection={ConnectionId} tenant={Tenant} group={Group}", Context.ConnectionId, tenantId, groupName);
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in JoinTenant: {Message}", ex.Message);
        }
    }

    public async Task JoinRoom(string roomId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return;

            if (!await _realtimeAccessService.CanJoinRoomAsync(roomId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[RoomTopicHub] JoinRoom denied connection={ConnectionId} room={RoomId} user={UserId}",
                    Context.ConnectionId, roomId, Context.UserIdentifier);
                return;
            }

            var groupName = RoomTopicHubGroups.Room(roomId);
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                _logger.LogInformation("[RoomTopicHub] JoinRoom connection={ConnectionId} room={Room} group={Group}", Context.ConnectionId, roomId, groupName);
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in JoinRoom: {Message}", ex.Message);
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return;

            var groupName = RoomTopicHubGroups.Room(roomId);
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                _logger.LogInformation("[RoomTopicHub] LeaveRoom connection={ConnectionId} room={Room} group={Group}", Context.ConnectionId, roomId, groupName);
            }
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in LeaveRoom: {Message}", ex.Message);
        }
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[RoomTopicHub] Client disconnected: {ConnectionId}, Exception: {Exception}",
                Context.ConnectionId, exception?.Message ?? "None");

            // Note: Groups are automatically cleaned up by SignalR when a connection closes
            // No manual cleanup needed unless using custom group management
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in OnDisconnectedAsync: {Message}", ex.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
