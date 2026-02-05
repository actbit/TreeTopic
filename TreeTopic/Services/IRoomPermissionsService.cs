using TreeTopic.Common;

namespace TreeTopic.Services;

/// <summary>
/// Room権限管理サービスインターフェース
/// </summary>
public interface IRoomPermissionsService
{
    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    Task<Result<List<string>>> GetRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    Task<Result<RoomRolePermissionDto>> AddPermissionToRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    Task<Result> RemovePermissionFromRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// RoomRoleの全権限をクリア
    /// </summary>
    Task<Result> ClearRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default);
}