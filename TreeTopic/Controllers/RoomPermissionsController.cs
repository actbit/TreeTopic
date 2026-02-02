using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Controllers;

/// <summary>
/// Room権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/roomroles/{roleId}/permissions")]
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
    /// Room権限一覧を取得
    /// </summary>
    [HttpGet("available")]
    [RequireAny(IdentityPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions()
    {
        var permissions = Permissions.PermissionHelper.GetRoomPermissions();

        return Ok(permissions.Select(p => new
        {
            name = p,
            scope = "room"
        }).ToList());
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] MaskedGuid roleId,
        CancellationToken cancellationToken)
    {
        var roleGuid = (Guid)roleId;
        var permissions = await _db.RoomRolePermissions
            .AsNoTracking()
            .Where(p => p.RoomRoleId == roleGuid)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);

        return Ok(new { roleId = roleGuid, permissions });
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] MaskedGuid roleId,
        [FromBody] AddRoomPermissionRequest request,
        CancellationToken cancellationToken)
    {
        var roleGuid = (Guid)roleId;

        // RoomRoleの存在確認
        var role = await _db.RoomRoles.FindAsync(new[] { roleGuid }, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.RoomRolePermissions
            .AnyAsync(p => p.RoomRoleId == roleGuid && p.PermissionName == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new RoomRolePermission
        {
            RoomRoleId = roleGuid,
            PermissionName = request.PermissionName
        };

        _db.RoomRolePermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to RoomRole {RoleId}", request.PermissionName, roleGuid);

        return Ok(new { permissionId = permission.Id, name = permission.PermissionName });
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] MaskedGuid roleId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var roleGuid = (Guid)roleId;

        var permission = await _db.RoomRolePermissions
            .FirstOrDefaultAsync(p => p.RoomRoleId == roleGuid && p.PermissionName == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.RoomRolePermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from RoomRole {RoleId}", permissionName, roleGuid);

        return NoContent();
    }
}

/// <summary>
/// Room権限割り当てリクエスト
/// </summary>
public record AddRoomPermissionRequest(string PermissionName);
