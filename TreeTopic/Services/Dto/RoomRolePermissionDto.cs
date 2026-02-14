using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Services;

/// <summary>
/// RoomRole権限情報DTO
/// </summary>
public record RoomRolePermissionDto(
    MaskedGuid Id,
    MaskedGuid RoomRoleId,
    string RoleName,
    string PermissionName);