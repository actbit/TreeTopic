using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TreeTopic.Dtos;
using MaskedUUID.AspNetCore.Types;
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

    public async Task JoinRoomUserGroup(string roomId, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                return;

            if (!await _realtimeAccessService.CanJoinRoomUserGroupAsync(roomId, userId, Context.User, Context.ConnectionAborted))
            {
                _logger.LogWarning("[RoomUserSyncHub] JoinRoomUserGroup denied connection={ConnectionId} room={Room} user={User}",
                    Context.ConnectionId, roomId, userId);
                return;
            }

            var groupName = RoomUserSyncHubGroups.RoomUser(roomId, userId);
            _logger.LogInformation("[RoomUserSyncHub] JoinRoomUserGroup connection={ConnectionId} room={Room} user={User} group={Group}", Context.ConnectionId, roomId, userId, groupName);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RoomUserSyncHub] Error in JoinRoomUserGroup: {Message}", ex.Message);
        }
    }

    public async Task LeaveRoomUserGroup(string roomId, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                return;

            var groupName = RoomUserSyncHubGroups.RoomUser(roomId, userId);
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

            // Note: Groups are automatically cleaned up by SignalR when a connection closes
            // No manual cleanup needed unless using custom group management
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
    public static string RoomUser(string roomId, string userId) => $"room_{roomId}_user_{userId}";
}
