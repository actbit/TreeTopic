using System.ComponentModel.DataAnnotations;

namespace TreeTopic.Dtos;

/// <summary>
/// SetupToken経由でのロール設定リクエスト基本DTO
/// </summary>
public class SetupTokenRequest
{
    [Required(ErrorMessage = "SetupToken is required")]
    public string SetupToken { get; set; } = string.Empty;
}

/// <summary>
/// ロール作成リクエスト（SetupToken経由）
/// </summary>
public class SetupRoleCreationRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>
/// ロール削除リクエスト（SetupToken経由）
/// ASP.NET Core Identity ではロール名は一意なため、名前で指定
/// </summary>
public class SetupRoleDeletionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;
}

/// <summary>
/// パーミッション追加リクエスト（SetupToken経由）
/// ロール名で指定（ASP.NET Core Identity ではロール名は一意）
/// </summary>
public class SetupPermissionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Permission name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 256 characters")]
    public string PermissionName { get; set; } = string.Empty;
}

/// <summary>
/// パーミッション削除リクエスト（SetupToken経由）
/// ロール名で指定（ASP.NET Core Identity ではロール名は一意）
/// </summary>
public class SetupPermissionDeletionRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters")]
    public string RoleName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Permission name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 256 characters")]
    public string PermissionName { get; set; } = string.Empty;
}

/// <summary>
/// デフォルトロール設定リクエスト
/// </summary>
public class SetupDefaultRoleRequest : SetupTokenRequest
{
    [Required(ErrorMessage = "Default role name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Default role name must be between 1 and 256 characters")]
    public string DefaultRoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// デフォルトロールに付与するパーミッション名のリスト
    /// </summary>
    public List<string> DefaultPermissions { get; set; } = new List<string>();
}

/// <summary>
/// ロール設定完了レスポンス
/// </summary>
public class RoleSetupCompletionRequest : SetupTokenRequest
{
}

/// <summary>
/// ロール設定完了レスポンス
/// </summary>
public class RoleSetupCompletionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int RolesCreated { get; set; }
    public int PermissionsAdded { get; set; }
    public string? DefaultRoleName { get; set; }
}
