namespace TreeTopic.Permissions;

/// <summary>
/// ASP.NET Core Identityレベルの権限
/// ApplicationRoleに関連する権限名
/// </summary>
public static class IdentityPermissions
{
    // ユーザー管理
    public const string UserRead = "tenant.identity.user.read";
    public static readonly PermissionRequirement UserReadReq = new(PermissionScope.Role, UserRead);

    public const string UserManage = "tenant.identity.user.manage";
    public static readonly PermissionRequirement UserManageReq = new(PermissionScope.Role, UserManage);

    public const string UserManagement = "tenant.identity.usermanagement";
    public static readonly PermissionRequirement UserManagementReq = new(PermissionScope.Role, UserManagement);

    // ロール管理
    public const string RoleRead = "tenant.identity.role.read";
    public static readonly PermissionRequirement RoleReadReq = new(PermissionScope.Role, RoleRead);

    public const string RoleManage = "tenant.identity.role.manage";
    public static readonly PermissionRequirement RoleManageReq = new(PermissionScope.Role, RoleManage);

    // 権限管理
    public const string PermissionRead = "tenant.identity.permission.read";
    public static readonly PermissionRequirement PermissionReadReq = new(PermissionScope.Role, PermissionRead);

    public const string PermissionManage = "tenant.identity.permission.manage";
    public static readonly PermissionRequirement PermissionManageReq = new(PermissionScope.Role, PermissionManage);

    // テナント管理
    public const string TenantRead = "tenant.identity.tenant.read";
    public static readonly PermissionRequirement TenantReadReq = new(PermissionScope.Role, TenantRead);

    public const string TenantManage = "tenant.identity.tenant.manage";
    public static readonly PermissionRequirement TenantManageReq = new(PermissionScope.Role, TenantManage);
}

