namespace TreeTopic.Permissions;

/// <summary>
/// ASP.NET Core Identityレベルの権限
/// ApplicationRoleに関連する権限名
/// </summary>
public static class IdentityPermissions
{
    // ユーザー管理
    public const string UserRead = "identity.user.read";
    public const string UserManage = "identity.user.manage";

    // ロール管理
    public const string RoleRead = "identity.role.read";
    public const string RoleManage = "identity.role.manage";

    // 権限管理
    public const string PermissionRead = "identity.permission.read";
    public const string PermissionManage = "identity.permission.manage";

    // テナント管理
    public const string TenantRead = "identity.tenant.read";
    public const string TenantManage = "identity.tenant.manage";
}
