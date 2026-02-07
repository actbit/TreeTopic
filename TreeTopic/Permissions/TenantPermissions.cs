namespace TreeTopic.Permissions;

/// <summary>
/// ASP.NET Core Identityレベルの権限
/// ApplicationRoleに関連する権限名
/// </summary>
public static class TenantPermissions
{
    // テナント自体の管理
    public const string TenantRead = "tenant.read";
    public static readonly PermissionRequirement TenantReadReq = new(PermissionScope.Role, TenantRead);

    public const string TenantManage = "tenant.manage";
    public static readonly PermissionRequirement TenantManageReq = new(PermissionScope.Role, TenantManage);

    // ユーザー管理
    public const string UserRead = "tenant.user.read";
    public static readonly PermissionRequirement UserReadReq = new(PermissionScope.Role, UserRead);

    public const string UserManage = "tenant.user.manage";
    public static readonly PermissionRequirement UserManageReq = new(PermissionScope.Role, UserManage);

    public const string UserManagement = "tenant.usermanagement";
    public static readonly PermissionRequirement UserManagementReq = new(PermissionScope.Role, UserManagement);

    // ロール管理
    public const string RoleRead = "tenant.role.read";
    public static readonly PermissionRequirement RoleReadReq = new(PermissionScope.Role, RoleRead);

    public const string RoleManage = "tenant.role.manage";
    public static readonly PermissionRequirement RoleManageReq = new(PermissionScope.Role, RoleManage);

    // 権限管理
    public const string PermissionRead = "tenant.permission.read";
    public static readonly PermissionRequirement PermissionReadReq = new(PermissionScope.Role, PermissionRead);

    public const string PermissionManage = "tenant.permission.manage";
    public static readonly PermissionRequirement PermissionManageReq = new(PermissionScope.Role, PermissionManage);

    // ルーム管理（テナントレベル - 全ルームへのアクセス）
    public const string RoomRead = "tenant.room.read";
    public static readonly PermissionRequirement RoomReadReq = new(PermissionScope.Role, RoomRead);

    public const string RoomManage = "tenant.room.manage";
    public static readonly PermissionRequirement RoomManageReq = new(PermissionScope.Role, RoomManage);

    // トピック管理（テナントレベル - 全トピックへのアクセス）
    public const string TopicRead = "tenant.topic.read";
    public static readonly PermissionRequirement TopicReadReq = new(PermissionScope.Role, TopicRead);

    public const string TopicManage = "tenant.topic.manage";
    public static readonly PermissionRequirement TopicManageReq = new(PermissionScope.Role, TopicManage);

    public const string TopicReadMessages = "tenant.topic.readMessages";
    public static readonly PermissionRequirement TopicReadMessagesReq = new(PermissionScope.Role, TopicReadMessages);

    public const string TopicWriteMessages = "tenant.topic.writeMessages";
    public static readonly PermissionRequirement TopicWriteMessagesReq = new(PermissionScope.Role, TopicWriteMessages);
}
