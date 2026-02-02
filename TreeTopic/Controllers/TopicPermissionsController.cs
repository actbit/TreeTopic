using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Constants;
using TreeTopic.Common;

namespace TreeTopic.Controllers;

/// <summary>
/// Topic権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/topics/{topicId}/permissions")]
[Authorize]
public class TopicPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TopicPermissionsController> _logger;

    public TopicPermissionsController(
        ApplicationDbContext db,
        ILogger<TopicPermissionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Topic権限一覧を取得
    /// </summary>
    [HttpGet("available")]
    [RequireAny(IdentityPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions()
    {
        var permissions = Permissions.PermissionHelper.GetTopicPermissions();

        return Ok(permissions.Select(p => new
        {
            name = p,
            scope = "topic"
        }).ToList());
    }

    /// <summary>
    /// トピックに割り当てられているユーザー権限一覧を取得
    /// </summary>
    [HttpGet("users")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> GetTopicUserPermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;

        var permissions = await _db.TopicUserPermissions
            .AsNoTracking()
            .Include(p => p.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
            .Include(p => p.Topic)
            .Where(p => p.TopicId == topicGuid)
            .Select(p => new
            {
                p.Id,
                p.TopicId,
                p.RoomUserId,
                UserName = p.RoomUser.ApplicationUser != null ? p.RoomUser.ApplicationUser.UserName : "",
                DisplayName = p.RoomUser != null ? RoomUserNameHelper.ResolveDisplayName(p.RoomUser) : "",
                p.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(permissions);
    }

    /// <summary>
    /// 特定ユーザーのトピック権限を取得
    /// </summary>
    [HttpGet("users/{roomUserId}")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> GetUserTopicPermissions(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var roomUserGuid = (Guid)roomUserId;

        var permissions = await _db.TopicUserPermissions
            .AsNoTracking()
            .Where(p => p.TopicId == topicGuid && p.RoomUserId == roomUserGuid)
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        return Ok(new { topicId = topicGuid, roomUserId = roomUserGuid, permissions });
    }

    /// <summary>
    /// ユーザーにトピック権限を割り当て
    /// </summary>
    [HttpPost("users")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> AddPermissionToUser(
        [FromRoute] MaskedGuid topicId,
        [FromBody] AddTopicPermissionToUserRequest request,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;

        // TopicとRoomUserの存在確認
        var topic = await _db.Topics.FindAsync(new[] { topicGuid }, cancellationToken);
        if (topic == null)
        {
            return NotFound(new { message = "Topic not found" });
        }

        var roomUser = await _db.RoomUsers.FindAsync(new[] { request.RoomUserId }, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // RoomUserがトピックのルームに所属しているか検証
        if (roomUser.RoomId != topic.RoomId)
        {
            return BadRequest(new { message = "RoomUser does not belong to topic's room" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.TopicUserPermissions
            .AnyAsync(p => p.TopicId == topicGuid && p.RoomUserId == request.RoomUserId && p.Name == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned" });
        }

        var permission = new TopicUserPermission
        {
            TopicId = topicGuid,
            RoomUserId = request.RoomUserId,
            Name = request.PermissionName
        };

        _db.TopicUserPermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to User {RoomUserId} for Topic {TopicId}", request.PermissionName, request.RoomUserId, topicGuid);

        return Ok(new { permissionId = permission.Id, name = permission.Name });
    }

    /// <summary>
    /// ユーザーからトピック権限を削除
    /// </summary>
    [HttpDelete("users/{permissionId}")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> RemovePermissionFromUser(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid permissionId,
        CancellationToken cancellationToken)
    {
        var permissionGuid = (Guid)permissionId;

        var permission = await _db.TopicUserPermissions
            .FirstOrDefaultAsync(p => p.Id == permissionGuid, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.TopicUserPermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {PermissionId} removed", permissionGuid);

        return NoContent();
    }

    /// <summary>
    /// ユーザーからトピック権限を削除（権限名指定）
    /// </summary>
    [HttpDelete("users/{roomUserId}/{permissionName}")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> RemovePermissionFromUserByName(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roomUserId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var roomUserGuid = (Guid)roomUserId;

        var permission = await _db.TopicUserPermissions
            .FirstOrDefaultAsync(p => p.TopicId == topicGuid && p.RoomUserId == roomUserGuid && p.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.TopicUserPermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from User {RoomUserId} for Topic {TopicId}", permissionName, roomUserGuid, topicGuid);

        return NoContent();
    }

    /// <summary>
    /// トピックに割り当てられているTopicRolePermission（RoomRole権限）一覧を取得
    /// </summary>
    [HttpGet("role-permissions")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> GetTopicRolePermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;

        var permissions = await _db.TopicRolePermissions
            .AsNoTracking()
            .Include(p => p.RoomRole)
            .Include(p => p.Topic)
            .Where(p => p.TopicId == topicGuid)
            .Select(p => new
            {
                p.Id,
                p.TopicId,
                p.RoomRoleId,
                RoleName = p.RoomRole != null ? p.RoomRole.Name : "",
                RoleDescription = p.RoomRole != null ? p.RoomRole.Description : "",
                p.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(permissions);
    }

    /// <summary>
    /// トピックにRoomRole権限を割り当て（TopicRolePermissionとして追加）
    /// </summary>
    [HttpPost("role-permissions")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> AddTopicRolePermission(
        [FromRoute] MaskedGuid topicId,
        [FromBody] AddTopicRolePermissionRequest request,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;

        // TopicとRoomRoleの存在確認
        var topic = await _db.Topics.FindAsync(new[] { topicGuid }, cancellationToken);
        if (topic == null)
        {
            return NotFound(new { message = "Topic not found" });
        }

        var roomRole = await _db.RoomRoles.FindAsync(new[] { request.RoomRoleId }, cancellationToken);
        if (roomRole == null)
        {
            return NotFound(new { message = "RoomRole not found" });
        }

        // 既に割り当てられているか確認
        var existing = await _db.TopicRolePermissions
            .AnyAsync(p => p.TopicId == topicGuid && p.RoomRoleId == request.RoomRoleId && p.Name == request.PermissionName, cancellationToken);

        if (existing)
        {
            return Ok(new { message = "Permission already assigned to RoomRole" });
        }

        var permission = new TopicRolePermission
        {
            TopicId = topicGuid,
            RoomRoleId = request.RoomRoleId,
            Name = request.PermissionName
        };

        _db.TopicRolePermissions.Add(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} added to RoomRole {RoleId} for Topic {TopicId}", request.PermissionName, request.RoomRoleId, topicGuid);

        return Ok(new { permissionId = permission.Id, name = permission.Name });
    }

    /// <summary>
    /// トピックからRoomRole権限を削除
    /// </summary>
    [HttpDelete("role-permissions/{roleId}/{permissionName}")]
    [RequireAny(TopicPermissions.Manage)]
    public async Task<IActionResult> RemoveTopicRolePermission(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roleId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var roleGuid = (Guid)roleId;

        var permission = await _db.TopicRolePermissions
            .FirstOrDefaultAsync(p => p.TopicId == topicGuid && p.RoomRoleId == roleGuid && p.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return NotFound();
        }

        _db.TopicRolePermissions.Remove(permission);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission {Permission} removed from RoomRole {RoleId} for Topic {TopicId}", permissionName, roleGuid, topicGuid);

        return NoContent();
    }
}

/// <summary>
/// Topicユーザー権限割り当てリクエスト
/// </summary>
public record AddTopicPermissionToUserRequest(Guid RoomUserId, string PermissionName);

/// <summary>
/// TopicRolePermission割り当てリクエスト
/// </summary>
public record AddTopicRolePermissionRequest(Guid RoomRoleId, string PermissionName);
