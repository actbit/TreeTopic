using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TreeTopic.Dtos;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Hubs;

public interface IRoomUserSyncHubClient
{
    Task TopicUnreadUpdated(TopicUnreadUpdateEvent payload);
}

public record TopicUnreadUpdateEvent(
    MaskedGuid RoomId,
    MaskedGuid TopicId,
    int UnreadCount,
    DateTime? LastReadAt);

[Authorize]
public class RoomUserSyncHub : Hub<IRoomUserSyncHubClient>
{
    private readonly ILogger<RoomUserSyncHub> _logger;
    private readonly IRealtimeAccessService _realtimeAccessService;

    public RoomUserSyncHub(
        ILogger<RoomUserSyncHub> logger,
        IRealtimeAccessService realtimeAccessService)
    {
        _logger = logger;
        _realtimeAccessService = realtimeAccessService;
    }

    public async Task JoinRoomUserGroup(MaskedGuid roomId, MaskedGuid userId)
    {
        try
        {
            if (!await _realtimeAccessService.CanJoinRoomUserGroupAsync(roomId, userId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[RoomUserSyncHub] JoinRoomUserGroup denied connection={ConnectionId} room={Room} user={User}",
                    Context.ConnectionId, roomId, userId);
                return;
            }

            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = RoomUserSyncHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = RoomUserSyncHubGroups.RoomUser(tenant, roomId.ToString(), userId.ToString());
            _logger.LogInformation("[RoomUserSyncHub] JoinRoomUserGroup connection={ConnectionId} room={Room} user={User} group={Group}", Context.ConnectionId, roomId, userId, groupName);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomUserSyncHub] Error in JoinRoomUserGroup: {Message}", ex.Message);
        }
    }

    public async Task LeaveRoomUserGroup(MaskedGuid roomId, MaskedGuid userId)
    {
        try
        {
            var tenantInfo = Context.GetHttpContext()?.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
            var tenant = RoomUserSyncHubGroups.ResolveTenantKey(tenantInfo);
            var groupName = RoomUserSyncHubGroups.RoomUser(tenant, roomId.ToString(), userId.ToString());
            _logger.LogInformation("[RoomUserSyncHub] LeaveRoomUserGroup connection={ConnectionId} room={Room} user={User} group={Group}", Context.ConnectionId, roomId, userId, groupName);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomUserSyncHub] Error in LeaveRoomUserGroup: {Message}", ex.Message);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            _logger.LogInformation("[RoomUserSyncHub] Client disconnected: {ConnectionId}, Exception: {Exception}",
                Context.ConnectionId, exception?.Message ?? "None");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomUserSyncHub] Error in OnDisconnectedAsync: {Message}", ex.Message);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

public static class RoomUserSyncHubGroups
{
    public static string ResolveTenantKey(ApplicationTenantInfo? tenantInfo)
    {
        return tenantInfo?.Identifier ?? "default";
    }

    public static string RoomUser(string tenantKey, string roomId, string userId) => $"tenant:{tenantKey}:room_{roomId}_user_{userId}";
}
