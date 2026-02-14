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
    /// 表示名（UseMainName設定に基づいて解決済み）
    /// </summary>
    public string? DisplayName { get; set; }
    /// <summary>
    /// アイコンURL（UseMainIcon設定に基づいて解決済み）
    /// </summary>
    public string? IconUrl { get; set; }
    /// <summary>
    /// メインアカウントの名前を使用するかどうか
    /// </summary>
    public bool UseMainName { get; set; }
    /// <summary>
    /// メインアカウントのアイコンを使用するかどうか
    /// </summary>
    public bool UseMainIcon { get; set; }
    /// <summary>
    /// ロールID（オプション）
    /// </summary>
    public MaskedGuid? RoomRoleId { get; set; }
    /// <summary>
    /// ロール名（表示用）
    /// </summary>
    public string? RoomRoleName { get; set; }
    /// <summary>
    /// ユーザーのメールアドレス（権限エディタ用）
    /// </summary>
    public string? Email { get; set; }
    /// <summary>
    /// ユーザー名（権限エディタ用）
    /// </summary>
    public string? UserName { get; set; }
    /// <summary>
    /// 権限フラグ（権限エディタ用）
    /// </summary>
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManage { get; set; }
    /// <summary>
    /// トピック権限フラグ（権限エディタ用）
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
