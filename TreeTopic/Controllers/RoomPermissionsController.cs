using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// Room権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/roomroles/{roleName}/permissions")]
[Authorize]
public class RoomPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RoomPermissionsController> _logger;

    public RoomPermissionsController(
        ApplicationDbContext db,
        ILogger<RoomPermissionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Room権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionScanService permissionScanService)
    {
        var permissions = permissionScanService.GetRoomPermissions();

        return Ok(permissions.Select(p => new
        {
            name = p.Name,
            scope = p.Scope.ToString()
        }).ToList());
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.RoomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        var permissions = await _db.RoomRolePermissions
            .AsNoTracking()
            .Where(p => p.RoomRoleId == role.Id)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);

        return Ok(new { roleName, roleId = role.Id, permissions });
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] string roleName,
        [FromBody] AddRoomPermissionRequest request,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.RoomRoles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.RoomRolePermissions
            .AnyAsync(p => p.RoomRoleId == role.Id && p.PermissionName == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new RoomRolePermission
        {
            RoomRoleId = role.Id,
            PermissionName = request.PermissionName
        };

        _db.RoomRolePermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to RoomRole {RoleName}", request.PermissionName, roleName);

        return Ok(new { permissionId = permission.Id, name = permission.PermissionName });
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] string roleName,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        // ロールの存在確認
        var role = await _db.RoomRoles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        var permission = await _db.RoomRolePermissions
            .FirstOrDefaultAsync(p => p.RoomRoleId == role.Id && p.PermissionName == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.RoomRolePermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from RoomRole {RoleName}", permissionName, roleName);

        return NoContent();
    }
}

/// <summary>
/// Room権限割り当てリクエスト
/// </summary>
public record AddRoomPermissionRequest(string PermissionName);
