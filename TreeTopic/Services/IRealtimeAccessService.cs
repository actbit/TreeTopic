using System.Security.Claims;

namespace TreeTopic.Services;

public interface IRealtimeAccessService
{
    Task<bool> CanJoinTopicAsync(string topicId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
    Task<bool> CanJoinRoomAsync(string roomId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
    Task<bool> CanJoinRoomUserGroupAsync(string roomId, string userId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
