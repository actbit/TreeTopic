using MaskedUUID.AspNetCore.Types;
using System.Security.Claims;

namespace TreeTopic.Services;

public interface IRealtimeAccessService
{
    Task<bool> CanJoinTopicAsync(MaskedGuid topicId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
    Task<bool> CanJoinRoomAsync(MaskedGuid roomId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
    Task<bool> CanJoinRoomUserGroupAsync(MaskedGuid roomId, MaskedGuid userId, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
