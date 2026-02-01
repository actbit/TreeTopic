using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
public class TopicController : ControllerBase
{
    private readonly ITopicManagementService _topicManagementService;

    public TopicController(
        ITopicManagementService topicManagementService)
    {
        _topicManagementService = topicManagementService;
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
    [RequirePermission(TopicPermissions.Read)]
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
    [RequirePermission(TopicPermissions.Read)]
    public async Task<IActionResult> GetByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}/root")]
    [RequirePermission(TopicPermissions.Read)]
    public async Task<IActionResult> GetRootByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetRootTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("parent/{parentId}")]
    [RequirePermission(TopicPermissions.Read)]
    public async Task<IActionResult> GetByParent([FromRoute] MaskedGuid parentId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByParentAsync((Guid)parentId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{topicId}")]
    [RequirePermission(TopicPermissions.Read)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync((Guid)topicId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    [RequirePermission(TopicPermissions.Write)]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.CreateTopicAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{topicId}")]
    [RequirePermission(TopicPermissions.Write)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.UpdateTopicAsync((Guid)topicId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{topicId}")]
    [RequirePermission(TopicPermissions.Delete)]
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
    /// 複数トピックの統計情報を一括取得（N+1問題を解決）
    /// </summary>
    [HttpGet("room/{roomId}/stats")]
    [RequirePermission(TopicPermissions.Read)]
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
    /// 単一トピックの統計情報を取得（N+1問題を解決）
    /// </summary>
    [HttpGet("{topicId}/stats")]
    [RequirePermission(TopicPermissions.Read)]
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
    /// ルームのルートトピックに未読カウントを含めて取得（N+1問題を解決）
    /// </summary>
    [HttpGet("room/{roomId}/root-with-unread")]
    [RequirePermission(TopicPermissions.Read)]
    public async Task<IActionResult> GetRootTopicsWithUnread(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[TopicController] GetRootTopicsWithUnread START - roomId: {roomId}");
        var userId = CurrentUserId;
        Console.WriteLine($"[TopicController] GetRootTopicsWithUnread - userId: {userId}");

        if (!userId.HasValue)
        {
            Console.WriteLine($"[TopicController] GetRootTopicsWithUnread - userId is null, returning Unauthorized");
            return Unauthorized();
        }

        var result = await _topicManagementService.GetRootTopicsWithUnreadAsync((Guid)roomId, userId.Value, cancellationToken);
        Console.WriteLine($"[TopicController] GetRootTopicsWithUnread END - result: {result.IsSuccess}, count: {result.Data?.Count ?? 0}");
        return result.ToApiResult();
    }

    /// <summary>
    /// ルーム内の全トピックを未読カウント付きで一括取得（N+1問題を解決）
    /// </summary>
    [HttpGet("room/{roomId}/all-with-unread")]
    [RequirePermission(TopicPermissions.Read)]
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




