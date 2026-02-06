using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// ApplicationRole（テナントロール）権限管理
/// TenantレベルのロールにTenant権限を割り当てる
/// </summary>
[ApiController]
[Route("{tenant}/api/tenantroles/{roleName}/permissions")]
[Authorize]
public class TenantRolePermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TenantRolePermissionsController> _logger;

    public TenantRolePermissionsController(
        ApplicationDbContext db,
        ILogger<TenantRolePermissionsController> _logger)
    {
        _db = db;
        this._logger = _logger;
    }

    /// <summary>
    /// Tenant権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionScanService permissionScanService)
    {
        var permissions = permissionScanService.GetTenantPermissions();

        return Ok(permissions.Select(p => new
        {
            name = p.Name,
            scope = p.Scope.ToString()
        }).ToList());
    }

    /// <summary>
    /// ApplicationRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(TenantPermissions.RoleRead)]
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

        return Ok(new { roleName, roleId = new MaskedGuid(role.Id), permissions });
    }

    /// <summary>
    /// ApplicationRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(TenantPermissions.RoleManage)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] string roleName,
        [FromBody] AddTenantPermissionRequest request,
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

        return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.Name });
    }

    /// <summary>
    /// ApplicationRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(TenantPermissions.RoleManage)]
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
/// Tenant権限割り当てリクエスト
/// </summary>
public record AddTenantPermissionRequest(string PermissionName);
