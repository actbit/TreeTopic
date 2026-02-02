namespace TreeTopic.Permissions;

/// <summary>
/// Roomレベルの権限
/// RoomUser/RoomRoleに関連する権限名
/// </summary>
public static class RoomPermissions
{
    /// <summary>ルームへの参加（ルームアクセスの基本）</summary>
    public const string Join = "room.join";
    public static readonly PermissionRequirement JoinReq = new(PermissionScope.Room, Join);

    /// <summary>ルームの情報を読み取る</summary>
    public const string Read = "room.read";
    public static readonly PermissionRequirement ReadReq = new(PermissionScope.Room, Read);

    /// <summary>ルーム内で書き込み（トピック作成、ファイルアップロード、シェア作成）</summary>
    public const string Write = "room.write";
    public static readonly PermissionRequirement WriteReq = new(PermissionScope.Room, Write);

    /// <summary>ルーム内リソースの削除（シェア、ファイル等）</summary>
    public const string Delete = "room.delete";
    public static readonly PermissionRequirement DeleteReq = new(PermissionScope.Room, Delete);

    /// <summary>ルーム設定の変更</summary>
    public const string Manage = "room.manage";
    public static readonly PermissionRequirement ManageReq = new(PermissionScope.Room, Manage);

    /// <summary>ルームユーザーの管理</summary>
    public const string ManageUsers = "room.manageUsers";
    public static readonly PermissionRequirement ManageUsersReq = new(PermissionScope.Room, ManageUsers);

    /// <summary>ルームロールの管理</summary>
    public const string ManageRoles = "room.manageRoles";
    public static readonly PermissionRequirement ManageRolesReq = new(PermissionScope.Room, ManageRoles);
}
