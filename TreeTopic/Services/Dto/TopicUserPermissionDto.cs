using MaskedUUID.AspNetCore.Types;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// Topicユーザー権限情報DTO
/// </summary>
public record TopicUserPermissionDto(
    MaskedGuid Id,
    MaskedGuid TopicId,
    MaskedGuid RoomUserId,
    string? UserName,
    string? DisplayName,
    string Name);