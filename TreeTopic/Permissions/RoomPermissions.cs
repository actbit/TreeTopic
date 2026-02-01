namespace TreeTopic.Permissions;

/// <summary>
/// Roomレベルの権限
/// RoomUser/RoomRoleに関連する権限名
/// </summary>
public static class RoomPermissions
{
    /// <summary>ルームへの参加（ルームアクセスの基本）</summary>
    public const string Join = "room.join";

    /// <summary>ルームの情報を読み取る</summary>
    public const string Read = "room.read";

    /// <summary>ルーム内で書き込み（トピック作成、ファイルアップロード、シェア作成）</summary>
    public const string Write = "room.write";

    /// <summary>ルーム内リソースの削除（シェア、ファイル等）</summary>
    public const string Delete = "room.delete";

    /// <summary>ルーム設定の変更</summary>
    public const string Manage = "room.manage";

    /// <summary>ルームユーザーの管理</summary>
    public const string ManageUsers = "room.manageUsers";

    /// <summary>ルームロールの管理</summary>
    public const string ManageRoles = "room.manageRoles";
}
