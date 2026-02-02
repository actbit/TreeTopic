namespace TreeTopic.Permissions;

/// <summary>
/// ASP.NET Core Identityレベルの権限
/// ApplicationRoleに関連する権限名
/// </summary>
public static class IdentityPermissions
{
    // ユーザー管理
    public static readonly PermissionRequirement UserRead = new(PermissionScope.Role, "identity.user.read");
    public static readonly PermissionRequirement UserManage = new(PermissionScope.Role, "identity.user.manage");

    // ロール管理
    public static readonly PermissionRequirement RoleRead = new(PermissionScope.Role, "identity.role.read");
    public static readonly PermissionRequirement RoleManage = new(PermissionScope.Role, "identity.role.manage");

    // 権限管理
    public static readonly PermissionRequirement PermissionRead = new(PermissionScope.Role, "identity.permission.read");
    public static readonly PermissionRequirement PermissionManage = new(PermissionScope.Role, "identity.permission.manage");

    // テナント管理
    public static readonly PermissionRequirement TenantRead = new(PermissionScope.Role, "identity.tenant.read");
    public static readonly PermissionRequirement TenantManage = new(PermissionScope.Role, "identity.tenant.manage");
}

