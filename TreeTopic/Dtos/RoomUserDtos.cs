using System;
using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class RoomUserDto
{
    public MaskedGuid Id { get; set; }
    public MaskedGuid ApplicationUserId { get; set; }
    public MaskedGuid RoomId { get; set; }
    /// <summary>
    /// Display name (already resolved based on UseMainName setting)
    /// </summary>
    public string? DisplayName { get; set; }
    /// <summary>
    /// Icon URL (already resolved based on UseMainIcon setting)
    /// </summary>
    public string? IconUrl { get; set; }
    /// <summary>
    /// Whether to use the main account's name
    /// </summary>
    public bool UseMainName { get; set; }
    /// <summary>
    /// Whether to use the main account's icon
    /// </summary>
    public bool UseMainIcon { get; set; }
    /// <summary>
    /// Role ID (optional)
    /// </summary>
    public MaskedGuid? RoomRoleId { get; set; }
    /// <summary>
    /// Role Name (for display)
    /// </summary>
    public string? RoomRoleName { get; set; }
    /// <summary>
    /// User's email (for permission editor)
    /// </summary>
    public string? Email { get; set; }
    /// <summary>
    /// User's username (for permission editor)
    /// </summary>
    public string? UserName { get; set; }
    /// <summary>
    /// Permission flags (for permission editor)
    /// </summary>
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManage { get; set; }
    /// <summary>
    /// Topic permission flags (for permission editor)
    /// </summary>
    public bool CanReadTopic { get; set; }
    public bool CanWriteTopic { get; set; }
    public bool CanDeleteTopic { get; set; }
    public bool CanManageTopic { get; set; }
}

/// <summary>
/// RoomUserのロール設定リクエスト
/// </summary>
public class SetRoomUserRoleRequest
{
    public MaskedGuid? RoleId { get; set; }
}

/// <summary>
/// RoomUserの権限更新リクエスト
/// </summary>
public class UpdateRoomUserPermissionsRequest
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManage { get; set; }
    public bool CanReadTopic { get; set; }
    public bool CanWriteTopic { get; set; }
    public bool CanDeleteTopic { get; set; }
    public bool CanManageTopic { get; set; }
}

public class CreateRoomUserRequest
{
    [Required]
    public MaskedGuid ApplicationUserId { get; set; }

    [StringLength(255)]
    public string? Name { get; set; }

    public bool? UseMainName { get; set; }
}

public class JoinRoomUserRequest
{
    [StringLength(255)]
    public string? Name { get; set; }

    public bool? UseMainName { get; set; }

    public bool? UseMainIcon { get; set; }
}

public class UpdateRoomUserRequest
{
    [StringLength(255, MinimumLength = 1)]
    public string? DisplayName { get; set; }

    public bool? UseMainName { get; set; }

    public bool? UseMainIcon { get; set; }
}
