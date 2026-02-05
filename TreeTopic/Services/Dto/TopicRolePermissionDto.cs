using MaskedUUID.AspNetCore.Types;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// Topicロール権限情報DTO
/// </summary>
public record TopicRolePermissionDto(
    MaskedGuid Id,
    MaskedGuid TopicId,
    MaskedGuid RoomRoleId,
    string? RoleName,
    string? RoleDescription,
    string Name);