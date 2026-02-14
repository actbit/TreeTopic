using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Routing;
using MaskedUUID.AspNetCore.Types;
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
    private readonly IRealtimeAccessService _realtimeAccessService;
    private readonly ILogger<RoomTopicHub> _logger;

    public RoomTopicHub(
        IRealtimeAccessService realtimeAccessService,
        ILogger<RoomTopicHub> logger)
    {
        _realtimeAccessService = realtimeAccessService;
        _logger = logger;
    }

    public async Task JoinTenant(string tenantId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                return;

            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var currentTenant = RoomTopicHubGroups.ResolveTenantKey(tenantInfo);

            if (!string.IsNullOrWhiteSpace(currentTenant) &&
                !string.Equals(currentTenant, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[RoomTopicHub] JoinTenant denied by tenant mismatch connection={ConnectionId} currentTenant={CurrentTenant} requestedTenant={RequestedTenant}",
                    Context.ConnectionId, currentTenant, tenantId);
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

    public async Task JoinRoom(MaskedGuid roomId)
    {
        try
        {
            if (!await _realtimeAccessService.CanJoinRoomAsync(roomId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[RoomTopicHub] JoinRoom denied: no permission connection={ConnectionId} room={RoomId} user={UserId}",
                    Context.ConnectionId, roomId, Context.UserIdentifier);
                return;
            }

            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = RoomTopicHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = RoomTopicHubGroups.Room(tenant, roomId.ToString());
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation("[RoomTopicHub] JoinRoom allowed connection={ConnectionId} room={Room} user={UserId}",
                Context.ConnectionId, roomId, Context.UserIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in JoinRoom: {Message}", ex.Message);
        }
    }

    public async Task LeaveRoom(MaskedGuid roomId)
    {
        try
        {
            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = RoomTopicHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = RoomTopicHubGroups.Room(tenant, roomId.ToString());
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[RoomTopicHub] Client disconnected: {ConnectionId}, Exception: {Exception}",
                Context.ConnectionId, exception?.Message ?? "None");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomTopicHub] Error in OnDisconnectedAsync: {Message}", ex.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
