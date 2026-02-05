using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
public class TopicController : ControllerBase
{
    private readonly ITopicManagementService _topicManagementService;
    private readonly ApplicationDbContext _db;
    private readonly TopicPermissionManager _topicPermissionManager;

    public TopicController(
        ITopicManagementService topicManagementService,
        ApplicationDbContext db,
        TopicPermissionManager topicPermissionManager)
    {
        _topicManagementService = topicManagementService;
        _db = db;
        _topicPermissionManager = topicPermissionManager;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(userIdStr, out var userId))
                return userId;
            return null;
        }
    }

    [HttpGet]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetAll([FromQuery] MaskedGuid? roomId, CancellationToken cancellationToken)
    {
        if (!roomId.HasValue)
        {
            return BadRequest(new { message = "roomId is required. Use /{tenant}/api/Topic/room/{roomId} or /{tenant}/api/Topic?roomId=..." });
        }

        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId.Value, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}/root")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetRootByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetRootTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("parent/{parentId}")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetByParent([FromRoute] MaskedGuid parentId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByParentAsync((Guid)parentId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{topicId}")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync((Guid)topicId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// 現在のユーザーのトピック権限一覧を取得
    /// </summary>
    [HttpGet("{topicId}/my/permissions")]
    public async Task<IActionResult> GetMyPermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var userId = CurrentUserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        // トピックのルームIDを取得
        var topic = await _db.Topics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topicGuid, cancellationToken);

        if (topic == null)
        {
            return NotFound(new { message = "Topic not found" });
        }

        // RoomUserを取得
        var roomUser = await _db.RoomUsers
            .Include(ru => ru.RoomUserRoomRoles)
                .ThenInclude(rur => rur.RoomRole)
                    .ThenInclude(rr => rr!.Permissions)
            .Include(ru => ru.RoomPermission)
            .FirstOrDefaultAsync(ru => ru.RoomId == topic.RoomId && ru.ApplicationUserId == userId.Value, cancellationToken);

        if (roomUser == null)
        {
            return Ok(new TopicPermissionsResponse
            {
                Permissions = new List<string>()
            });
        }

        var permissions = new List<string>();

        // RoomRoleの権限を収集
        foreach (var userRole in roomUser.RoomUserRoomRoles)
        {
            if (userRole.RoomRole?.Permissions != null)
            {
                foreach (var permission in userRole.RoomRole.Permissions)
                {
                    permissions.Add(permission.PermissionName);
                }
            }
        }

        // 直接付与されたRoom権限を追加
        if (roomUser.RoomPermission != null && roomUser.RoomPermission.Count > 0)
        {
            foreach (var perm in roomUser.RoomPermission)
            {
                permissions.Add(perm.Name);
            }
        }

        // Topicレベルでの直接権限を追加
        var topicPermissions = await _topicPermissionManager.GetUserPermissionsAsync(
            topicGuid, roomUser.Id, cancellationToken);
        permissions.AddRange(topicPermissions.Select(p => p.Name));

        return Ok(new TopicPermissionsResponse
        {
            Permissions = permissions.Distinct().ToList()
        });
    }

    [HttpPost]
    [RequireAny(TopicPermissions.Write, TenantPermissions.TopicManage)]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.CreateTopicAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{topicId}")]
    [RequireAny(TopicPermissions.Write, TenantPermissions.TopicManage)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.UpdateTopicAsync((Guid)topicId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{topicId}")]
    [RequireAny(TopicPermissions.Delete, TenantPermissions.TopicManage)]
    public async Task<IActionResult> Delete(
        [FromRoute] MaskedGuid topicId,
        [FromQuery] string? strategy,
        CancellationToken cancellationToken)
    {
        TopicDeleteStrategy deleteStrategy = TopicDeleteStrategy.Cascade;

        if (!string.IsNullOrWhiteSpace(strategy))
        {
            if (!Enum.TryParse<TopicDeleteStrategy>(strategy, ignoreCase: true, out deleteStrategy))
            {
                return BadRequest(new { message = $"Invalid strategy '{strategy}'. Use 'Cascade' or 'ReparentToParent'." });
            }
        }

        var result = await _topicManagementService.DeleteTopicAsync((Guid)topicId, deleteStrategy, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// 複数トピックの統計情報を一括取得
    /// </summary>
    [HttpGet("room/{roomId}/stats")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetTopicsWithStats(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
            return Unauthorized();

        var result = await _topicManagementService.GetTopicsWithStatsAsync((Guid)roomId, userId.Value, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// 単一トピックの統計情報を取得
    /// </summary>
    [HttpGet("{topicId}/stats")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetTopicWithStats(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
            return Unauthorized();

        var result = await _topicManagementService.GetTopicWithStatsByIdAsync((Guid)topicId, userId.Value, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// ルームのルートトピックを未読カウント付きで取得
    /// </summary>
    [HttpGet("room/{roomId}/root-with-unread")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetRootTopicsWithUnread(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;

        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _topicManagementService.GetRootTopicsWithUnreadAsync((Guid)roomId, userId.Value, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// ルーム内の全トピックを未読カウント付きで取得
    /// </summary>
    [HttpGet("room/{roomId}/all-with-unread")]
    [RequireAny(TopicPermissions.Read, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetAllTopicsWithUnread(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId;
        if (!userId.HasValue)
            return Unauthorized();

        var result = await _topicManagementService.GetAllTopicsWithUnreadAsync((Guid)roomId, userId.Value, cancellationToken);
        return result.ToApiResult();
    }
}
