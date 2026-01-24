using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
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
    public async Task<IActionResult> GetByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}/root")]
    public async Task<IActionResult> GetRootByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetRootTopicsByRoomAsync((Guid)roomId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("parent/{parentId}")]
    public async Task<IActionResult> GetByParent([FromRoute] MaskedGuid parentId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByParentAsync((Guid)parentId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{topicId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync((Guid)topicId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.CreateTopicAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{topicId}")]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.UpdateTopicAsync((Guid)topicId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{topicId}")]
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

}




