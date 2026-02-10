using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// RoomUserとRoomRoleの多対多関係を管理
/// </summary>
[ApiController]
[Route("{tenant}/api/roomusers/{roomUserId}/roles")]
[Authorize]
public class RoomUserRolesController : ControllerBase
{
    private readonly RoomUserManager _roomUserManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RoomUserRolesController> _logger;

    public RoomUserRolesController(
        RoomUserManager roomUserManager,
        ApplicationDbContext db,
        ILogger<RoomUserRolesController> logger)
    {
        _roomUserManager = roomUserManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// RoomUserに割り当てられているRoomRole一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetUserRoles(
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        var roles = await _db.RoomUserRoomRoles
            .AsNoTracking()
            .Include(rur => rur.RoomRole)
            .Where(rur => rur.RoomUserId == roomUserGuid)
            .Select(rur => new
            {
                rur.Id,
                rur.RoomRoleId,
                RoleName = rur.RoomRole!.Name,
                rur.RoomRole.Description
            })
            .ToListAsync(cancellationToken);

        return Ok(new { roomUserId = roomUserGuid, roles });
    }

    /// <summary>
    /// RoomUserにRoomRoleを追加
    /// </summary>
    [HttpPost("{roleName}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddRoleToUser(
        [FromRoute] MaskedGuid roomUserId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        // RoomUserの存在確認
        var roomUser = await _db.RoomUsers.FindAsync(roomUserGuid, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // RoomRoleの存在確認
        var role = await _db.RoomRoles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        // ロールを追加
        await _roomUserManager.AddRoleAsync(roomUser, role.Id, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new { message = "Role added to user", roomUserId = roomUserGuid, roleName });
    }

    /// <summary>
    /// RoomUserからRoomRoleを削除
    /// </summary>
    [HttpDelete("{roleName}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemoveRoleFromUser(
        [FromRoute] MaskedGuid roomUserId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        // RoomUserの存在確認
        var roomUser = await _db.RoomUsers.FindAsync(roomUserGuid, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // RoomRoleの存在確認
        var role = await _db.RoomRoles
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        // ロールを削除
        await _roomUserManager.RemoveRoleAsync(roomUser, role.Id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// RoomUserのRoomRoleを一括設定（置き換え）
    /// </summary>
    [HttpPut]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> SetUserRoles(
        [FromRoute] MaskedGuid roomUserId,
        [FromBody] SetUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        // RoomUserの存在確認
        var roomUser = await _db.RoomUsers.FindAsync(roomUserGuid, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // 全てのロール名が有効か確認
        var existingRoles = await _db.RoomRoles
            .Where(r => request.RoleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (existingRoles.Count != request.RoleNames.Count)
        {
            return BadRequest(new { message = "One or more roles not found" });
        }

        // ロールを設定
        await _roomUserManager.SetRolesAsync(roomUser, existingRoles, cancellationToken);

        return Ok(new { message = "Roles updated", roomUserId = roomUserGuid, roleCount = request.RoleNames.Count });
    }
}
