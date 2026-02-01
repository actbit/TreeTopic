using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;
using TreeTopic.Common;

namespace TreeTopic.Controllers;

/// <summary>
/// トピックレベルの個別ユーザー権限管理API
/// </summary>
[ApiController]
[Route("{tenant}/api/topics/{topicId}/user-permissions")]
[Authorize]
public class TopicUserPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TopicUserPermissionsController> _logger;

    public TopicUserPermissionsController(
        ApplicationDbContext db,
        ILogger<TopicUserPermissionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// トピックに割り当てられているユーザー権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequirePermission(TopicPermissions.Manage)]
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
    [HttpGet("user/{roomUserId}")]
    [RequirePermission(TopicPermissions.Manage)]
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
    [HttpPost]
    [RequirePermission(TopicPermissions.Manage)]
    public async Task<IActionResult> AddPermissionToUser(
        [FromRoute] MaskedGuid topicId,
        [FromBody] AddUserPermissionRequest request,
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
    [HttpDelete("{permissionId}")]
    [RequirePermission(TopicPermissions.Manage)]
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
    [HttpDelete("user/{roomUserId}/{permissionName}")]
    [RequirePermission(TopicPermissions.Manage)]
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
}

/// <summary>
/// ユーザー権限割り当てリクエスト
/// </summary>
public record AddUserPermissionRequest(Guid RoomUserId, string PermissionName);
