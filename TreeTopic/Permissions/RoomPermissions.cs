namespace TreeTopic.Permissions;

/// <summary>
/// Roomレベルの権限
/// RoomUser/RoomRoleに関連する権限名
/// </summary>
public static class RoomPermissions
{
    /// <summary>ルームへの参加（ルームアクセスの基本）</summary>
    public static readonly PermissionRequirement Join = new(PermissionScope.Room, "room.join");

    /// <summary>ルームの情報を読み取る</summary>
    public static readonly PermissionRequirement Read = new(PermissionScope.Room, "room.read");

    /// <summary>ルーム内で書き込み（トピック作成、ファイルアップロード、シェア作成）</summary>
    public static readonly PermissionRequirement Write = new(PermissionScope.Room, "room.write");

    /// <summary>ルーム内リソースの削除（シェア、ファイル等）</summary>
    public static readonly PermissionRequirement Delete = new(PermissionScope.Room, "room.delete");

    /// <summary>ルーム設定の変更</summary>
    public static readonly PermissionRequirement Manage = new(PermissionScope.Room, "room.manage");

    /// <summary>ルームユーザーの管理</summary>
    public static readonly PermissionRequirement ManageUsers = new(PermissionScope.Room, "room.manageUsers");

    /// <summary>ルームロールの管理</summary>
    public static readonly PermissionRequirement ManageRoles = new(PermissionScope.Room, "room.manageRoles");
}
