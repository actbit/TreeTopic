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
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/rooms/{roomId}/[controller]")]
public class RoomRolesController : ControllerBase
{
    private readonly RoomRoleManagementService _roleService;
    private readonly ILogger<RoomRolesController> _logger;
    private readonly ApplicationDbContext _db;

    public RoomRolesController(
        RoomRoleManagementService roleService,
        ILogger<RoomRolesController> logger,
        ApplicationDbContext db)
    {
        _roleService = roleService;
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// すべてのロールを取得
    /// </summary>
    [HttpGet]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<ActionResult<List<RoomRoleDto>>> List(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        var result = await _roleService.ListRolesAsync(cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => r.Select(RoomRoleManagementService.ToDto).ToList());
        }

        var roles = result.Data!;
        if (!await IsTenantRoomManagerAsync(cancellationToken))
        {
            var accessibleRoleIds = await GetRoomScopedRoleIdsAsync((Guid)roomId, cancellationToken);
            roles = roles.Where(r => accessibleRoleIds.Contains(r.Id)).ToList();
        }

        return Ok(roles.Select(RoomRoleManagementService.ToDto).ToList());
    }

    /// <summary>
    /// IDでロールを取得
    /// </summary>
    [HttpGet("{id}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomRoleDto>> GetById(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid id,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, (Guid)id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _roleService.GetRoleByIdAsync((Guid)id, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return Ok(RoomRoleManagementService.ToDto(result.Data!));
    }

    /// <summary>
    /// ロールを作成
    /// </summary>
    [HttpPost]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomRoleDto>> Create(
        [FromRoute] MaskedGuid roomId,
        [FromBody] CreateRoomRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleService.CreateRoleAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return StatusCode(StatusCodes.Status201Created, RoomRoleManagementService.ToDto(result.Data));
    }

    /// <summary>
    /// ロールを更新
    /// </summary>
    [HttpPut("{id}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomRoleDto>> Update(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid id,
        [FromBody] UpdateRoomRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, (Guid)id, cancellationToken))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleService.UpdateRoleAsync((Guid)id, request, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return Ok(RoomRoleManagementService.ToDto(result.Data!));
    }

    /// <summary>
    /// ロールを削除
    /// </summary>
    [HttpDelete("{id}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Delete(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid id,
        CancellationToken cancellationToken)
    {
        if (!await RoomExistsAsync((Guid)roomId, cancellationToken))
        {
            return NotFound(new { message = "Room not found" });
        }

        if (!await CanManageRoleScopeAsync((Guid)roomId, (Guid)id, cancellationToken))
        {
            return Forbid();
        }

        var result = await _roleService.DeleteRoleAsync((Guid)id, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    private Task<bool> RoomExistsAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return _db.Rooms.AsNoTracking().AnyAsync(r => r.Id == roomId, cancellationToken);
    }

    private async Task<bool> CanManageRoleScopeAsync(Guid roomId, Guid roleId, CancellationToken cancellationToken)
    {
        if (await IsTenantRoomManagerAsync(cancellationToken))
        {
            return true;
        }

        var impactedRoomIds = await GetImpactedRoomIdsAsync(roleId, cancellationToken);
        return impactedRoomIds.Count == 0 || impactedRoomIds.All(id => id == roomId);
    }

    private async Task<bool> IsTenantRoomManagerAsync(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return false;
        }

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
            .AnyAsync(p => roleIds.Contains(p.RoleId) && p.Name == TenantPermissions.RoomManage, cancellationToken);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private async Task<HashSet<Guid>> GetRoomScopedRoleIdsAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var fromRoomUsers = await _db.RoomUserRoomRoles
            .AsNoTracking()
            .Join(_db.RoomUsers.AsNoTracking().Where(ru => ru.RoomId == roomId),
                rur => rur.RoomUserId,
                ru => ru.Id,
                (rur, _) => rur.RoomRoleId)
            .ToListAsync(cancellationToken);

        var fromTopics = await _db.TopicRolePermissions
            .AsNoTracking()
            .Join(_db.Topics.AsNoTracking().Where(t => t.RoomId == roomId),
                trp => trp.TopicId,
                t => t.Id,
                (trp, _) => trp.RoomRoleId)
            .ToListAsync(cancellationToken);

        return fromRoomUsers.Concat(fromTopics).ToHashSet();
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
