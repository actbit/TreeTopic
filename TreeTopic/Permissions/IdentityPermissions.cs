namespace TreeTopic.Permissions;

/// <summary>
/// ASP.NET Core Identityレベルの権限
/// ApplicationRoleに関連する権限名
/// </summary>
public static class IdentityPermissions
{
    // ユーザー管理
    public const string UserRead = "identity.user.read";
    public static readonly PermissionRequirement UserReadReq = new(PermissionScope.Role, UserRead);

    public const string UserManage = "identity.user.manage";
    public static readonly PermissionRequirement UserManageReq = new(PermissionScope.Role, UserManage);

    // ロール管理
    public const string RoleRead = "identity.role.read";
    public static readonly PermissionRequirement RoleReadReq = new(PermissionScope.Role, RoleRead);

    public const string RoleManage = "identity.role.manage";
    public static readonly PermissionRequirement RoleManageReq = new(PermissionScope.Role, RoleManage);

    // 権限管理
    public const string PermissionRead = "identity.permission.read";
    public static readonly PermissionRequirement PermissionReadReq = new(PermissionScope.Role, PermissionRead);

    public const string PermissionManage = "identity.permission.manage";
    public static readonly PermissionRequirement PermissionManageReq = new(PermissionScope.Role, PermissionManage);

    // テナント管理
    public const string TenantRead = "identity.tenant.read";
    public static readonly PermissionRequirement TenantReadReq = new(PermissionScope.Role, TenantRead);

    public const string TenantManage = "identity.tenant.manage";
    public static readonly PermissionRequirement TenantManageReq = new(PermissionScope.Role, TenantManage);
}

