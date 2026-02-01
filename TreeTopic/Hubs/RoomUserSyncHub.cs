using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TreeTopic.Dtos;

namespace TreeTopic.Hubs;

public interface IRoomUserSyncHubClient
{
    Task TopicUnreadUpdated(TopicUnreadUpdateEvent payload);
}

public record TopicUnreadUpdateEvent(
    string RoomId,
    string TopicId,
    int UnreadCount,
    DateTime? LastReadAt);

[Authorize]
public class RoomUserSyncHub : Hub<IRoomUserSyncHubClient>
{
    private readonly ILogger<RoomUserSyncHub> _logger;

    public RoomUserSyncHub(ILogger<RoomUserSyncHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinRoomUserGroup(string roomId, string userId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
            return;

        var groupName = RoomUserSyncHubGroups.RoomUser(roomId, userId);
        _logger.LogInformation("[RoomUserSyncHub] JoinRoomUserGroup connection={ConnectionId} room={Room} user={User} group={Group}", Context.ConnectionId, roomId, userId, groupName);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public Task LeaveRoomUserGroup(string roomId, string userId)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
            return Task.CompletedTask;

        var groupName = RoomUserSyncHubGroups.RoomUser(roomId, userId);
        _logger.LogInformation("[RoomUserSyncHub] LeaveRoomUserGroup connection={ConnectionId} room={Room} user={User} group={Group}", Context.ConnectionId, roomId, userId, groupName);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}

public static class RoomUserSyncHubGroups
{
    public static string RoomUser(string roomId, string userId) => $"room_{roomId}_user_{userId}";
}
