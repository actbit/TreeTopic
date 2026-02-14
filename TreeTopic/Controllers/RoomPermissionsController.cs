using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// Room権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/rooms/{roomId}/roomroles/{roleName}/permissions")]
[Authorize]
public class RoomPermissionsController : BaseController
{
    private readonly IRoomPermissionsService _service;
    private readonly ApplicationDbContext _db;

    public RoomPermissionsController(IRoomPermissionsService service, ApplicationDbContext db)
    {
        _service = service;
        _db = db;
    }

    /// <summary>
    /// Room権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(PermissionScope.Role, TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionCatalogService permissionCatalogService)
    {
        return Ok(permissionCatalogService.GetRoomPermissions());
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        var role = await _db.RoomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = $"RoomRole '{roleName}' not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, role.Id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _service.GetRolePermissionsAsync(roleName, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new { roleName, permissions = result.Data });
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        [FromBody] AddRoomPermissionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        var role = await _db.RoomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = $"RoomRole '{roleName}' not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, role.Id, cancellationToken))
        {
            return Forbid();
        }

        // 権限名を検証
        var validRoomPermissions = Permissions.PermissionHelper.GetRoomPermissions();
        if (!validRoomPermissions.Contains(request.PermissionName))
        {
            return BadRequest(new { message = $"Invalid permission name: {request.PermissionName}. Valid permissions: {string.Join(", ", validRoomPermissions)}" });
        }

        var result = await _service.AddPermissionToRoleAsync(roleName, request.PermissionName, cancellationToken);
        if (result.IsSuccess)
        {
            var permission = result.Data;
            return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.PermissionName });
        }

        return result.Error?.Type == ErrorType.Conflict
            ? Conflict(new { message = "Permission already assigned" })
            : result.ToApiResult();
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        var role = await _db.RoomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = $"RoomRole '{roleName}' not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, role.Id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _service.RemovePermissionFromRoleAsync(roleName, permissionName, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.Error?.Type switch
        {
            ErrorType.NotFound => NotFound(new { message = result.Error.Message }),
            _ => StatusCode(500, new { message = result.Error?.Message })
        };
    }

    /// <summary>
    /// RoomRoleの全権限をクリア
    /// </summary>
    [HttpDelete("clear")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> ClearRolePermissions(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        var role = await _db.RoomRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role == null)
        {
            return NotFound(new { message = $"RoomRole '{roleName}' not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, role.Id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _service.ClearRolePermissionsAsync(roleName, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return NotFound(new { message = result.Error?.Message });
    }

    private Task<bool> RoomExistsAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == roomId, cancellationToken);
    }

    private async Task<bool> CanManageRoleScopeAsync(Guid roomId, Guid roleId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return false;
        }

        if (await HasTenantPermissionAsync(userId, TenantPermissions.RoomManage, cancellationToken))
        {
            return true;
        }

        var impactedRoomIds = await GetImpactedRoomIdsAsync(roleId, cancellationToken);
        return impactedRoomIds.Count == 0 || impactedRoomIds.All(id => id == roomId);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private async Task<bool> HasTenantPermissionAsync(Guid userId, string permissionName, CancellationToken cancellationToken)
    {
        var roleIds = await _db.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return false;
        }

        return await _db.Permissions
            .AsNoTracking()
            .AnyAsync(p => roleIds.Contains(p.RoleId) && p.Name == permissionName, cancellationToken);
    }

    private async Task<List<Guid>> GetImpactedRoomIdsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var roomIdsFromRoomUsers = await _db.RoomUserRoomRoles
            .AsNoTracking()
            .Where(rur => rur.RoomRoleId == roleId)
            .Join(_db.RoomUsers.AsNoTracking(),
                rur => rur.RoomUserId,
                ru => ru.Id,
                (_, ru) => ru.RoomId)
            .ToListAsync(cancellationToken);

        var roomIdsFromTopics = await _db.TopicRolePermissions
            .AsNoTracking()
            .Where(trp => trp.RoomRoleId == roleId)
            .Join(_db.Topics.AsNoTracking(),
                trp => trp.TopicId,
                t => t.Id,
                (_, t) => t.RoomId)
            .ToListAsync(cancellationToken);

        return roomIdsFromRoomUsers
            .Concat(roomIdsFromTopics)
            .Distinct()
            .ToList();
    }
}
