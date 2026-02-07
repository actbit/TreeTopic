using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;
using TreeTopic.Common;

namespace TreeTopic.Controllers;

/// <summary>
/// ルームユーザー権限管理
/// RoomUserに直接Room権限を割り当てる
/// </summary>
[ApiController]
[Route("{tenant}/api/roomusers/{roomUserId}/permissions")]
[Authorize]
public class RoomUserPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RoomUserPermissionsController> _logger;

    public RoomUserPermissionsController(
        ApplicationDbContext db,
        ILogger<RoomUserPermissionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Room権限一覧を取得
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
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
    /// RoomUserに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetUserPermissions(
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;
        var permissions = await _db.RoomPermissions
            .AsNoTracking()
            .Where(p => p.RoomUserId == roomUserGuid)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return Ok(new { roomUserId = roomUserGuid, permissions });
    }

    /// <summary>
    /// RoomUserに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddPermissionToUser(
        [FromRoute] MaskedGuid roomUserId,
        [FromBody] AddRoomUserPermissionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate permission name
        var validRoomPermissions = Permissions.PermissionHelper.GetRoomPermissions();
        if (!validRoomPermissions.Contains(request.PermissionName))
        {
            return BadRequest(new { message = $"Invalid permission name: {request.PermissionName}" });
        }

        var roomUserGuid = (Guid)roomUserId;

        // RoomUserの存在確認
        var roomUser = await _db.RoomUsers.FindAsync(new[] { roomUserGuid }, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.RoomPermissions
            .AnyAsync(p => p.RoomUserId == roomUserGuid && p.Name == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new RoomPermission
        {
            RoomUserId = roomUserGuid,
            Name = request.PermissionName
        };

        _db.RoomPermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to RoomUser {RoomUserId}", request.PermissionName, roomUserGuid);

        return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.Name });
    }

    /// <summary>
    /// RoomUserから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemovePermissionFromUser(
        [FromRoute] MaskedGuid roomUserId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        var permission = await _db.RoomPermissions
            .FirstOrDefaultAsync(p => p.RoomUserId == roomUserGuid && p.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.RoomPermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from RoomUser {RoomUserId}", permissionName, roomUserGuid);

        return NoContent();
    }

    /// <summary>
    /// RoomUserのすべての権限を削除
    /// </summary>
    [HttpDelete]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemoveAllPermissionsFromUser(
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        var permissions = await _db.RoomPermissions
            .Where(p => p.RoomUserId == roomUserGuid)
            .ToListAsync(cancellationToken);

        if (permissions.Any())
        {
            _db.RoomPermissions.RemoveRange(permissions);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("All permissions removed from RoomUser {RoomUserId}", roomUserGuid);
        }

        return NoContent();
    }
}
