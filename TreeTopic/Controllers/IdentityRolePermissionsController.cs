using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

/// <summary>
/// ApplicationRole（Identityロール）権限管理
/// TenantレベルのロールにIdentity権限を割り当てる
/// </summary>
[ApiController]
[Route("{tenant}/api/identityroles/{roleName}/permissions")]
[Authorize]
public class IdentityRolePermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<IdentityRolePermissionsController> _logger;

    public IdentityRolePermissionsController(
        ApplicationDbContext db,
        ILogger<IdentityRolePermissionsController> _logger)
    {
        _db = db;
        this._logger = _logger;
    }

    /// <summary>
    /// Identity権限一覧を取得
    /// </summary>
    [HttpGet("available")]
    [RequireAny(IdentityPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions()
    {
        var permissions = new[]
        {
            new { name = IdentityPermissions.UserRead, label = "ユーザー閲覧", description = "ユーザー情報を閲覧できます" },
            new { name = IdentityPermissions.UserManage, label = "ユーザー管理", description = "ユーザー情報を変更できます" },
            new { name = IdentityPermissions.RoleRead, label = "ロール閲覧", description = "ロール情報を閲覧できます" },
            new { name = IdentityPermissions.RoleManage, label = "ロール管理", description = "ロールを管理できます" },
            new { name = IdentityPermissions.PermissionRead, label = "権限閲覧", description = "権限設定を閲覧できます" },
            new { name = IdentityPermissions.PermissionManage, label = "権限管理", description = "権限を管理できます" },
            new { name = IdentityPermissions.TenantRead, label = "テナント閲覧", description = "テナント情報を閲覧できます" },
            new { name = IdentityPermissions.TenantManage, label = "テナント管理", description = "テナントを管理できます" }
        };

        return Ok(permissions.Select(p => new
        {
            name = p.name,
            label = p.label,
            description = p.description,
            scope = "identity"
        }).ToList());
    }

    /// <summary>
    /// ApplicationRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(IdentityPermissions.RoleRead)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var permissions = await _db.Permissions
            .AsNoTracking()
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return Ok(new { roleName, roleId = role.Id, permissions });
    }

    /// <summary>
    /// ApplicationRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(IdentityPermissions.RoleManage)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] string roleName,
        [FromBody] AddIdentityPermissionRequest request,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.Permissions
            .AnyAsync(p => p.RoleId == role.Id && p.Name == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new Permission
        {
            RoleId = role.Id,
            Name = request.PermissionName
        };

        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to Role {RoleName}", request.PermissionName, roleName);

        return Ok(new { permissionId = permission.Id, name = permission.Name });
    }

    /// <summary>
    /// ApplicationRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(IdentityPermissions.RoleManage)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] string roleName,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "Role not found" });
        }

        var permission = await _db.Permissions
            .FirstOrDefaultAsync(p => p.RoleId == role.Id && p.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.Permissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from Role {RoleName}", permissionName, roleName);

        return NoContent();
    }
}

/// <summary>
/// Identity権限割り当てリクエスト
/// </summary>
public record AddIdentityPermissionRequest(string PermissionName);
