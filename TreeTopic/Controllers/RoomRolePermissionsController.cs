using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Services;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

/// <summary>
/// RoomRoleへの権限割り当て管理API
/// </summary>
[ApiController]
[Route("{tenant}/api/roomroles/{roleId}/permissions")]
[Authorize]
public class RoomRolePermissionsController : ControllerBase
{
    private readonly TopicPermissionManager _topicPermissionManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RoomRolePermissionsController> _logger;

    public RoomRolePermissionsController(
        TopicPermissionManager topicPermissionManager,
        ApplicationDbContext db,
        ILogger<RoomRolePermissionsController> logger)
    {
        _topicPermissionManager = topicPermissionManager;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequirePermission(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] MaskedGuid roleId,
        CancellationToken cancellationToken)
    {
        var roleGuid = (Guid)roleId;
        var permissions = await _db.TopicRolePermissions
            .AsNoTracking()
            .Where(p => p.RoomRoleId == roleGuid)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return Ok(new { roleId = roleGuid, permissions });
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequirePermission(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] MaskedGuid roleId,
        [FromBody] AddPermissionRequest request,
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
        var existing = await _db.TopicRolePermissions
            .AnyAsync(p => p.RoomRoleId == roleGuid && p.Name == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new TopicRolePermission
        {
            RoomRoleId = roleGuid,
            Name = request.PermissionName
        };

        _db.TopicRolePermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to RoomRole {RoleId}", request.PermissionName, roleGuid);

        return Ok(new { permissionId = permission.Id, name = permission.Name });
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequirePermission(RoomPermissions.ManageRoles)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] MaskedGuid roleId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var roleGuid = (Guid)roleId;

        var permission = await _db.TopicRolePermissions
            .FirstOrDefaultAsync(p => p.RoomRoleId == roleGuid && p.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.TopicRolePermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from RoomRole {RoleId}", permissionName, roleGuid);

        return NoContent();
    }
}

/// <summary>
/// 権限割り当てリクエスト
/// </summary>
public record AddPermissionRequest(string PermissionName);
