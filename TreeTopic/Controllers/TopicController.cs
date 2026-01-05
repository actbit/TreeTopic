using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Policy = "Topic:read")]
public class TopicController : ControllerBase
{
    private readonly ITopicManagementService _topicManagementService;

    public TopicController(
        ITopicManagementService topicManagementService)
    {
        _topicManagementService = topicManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetAllTopicsAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}/root")]
    public async Task<IActionResult> GetRootByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetRootTopicsByRoomAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("parent/{parentId}")]
    public async Task<IActionResult> GetByParent([FromRoute] MaskedGuid parentId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByParentAsync((Guid)parentId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{topicId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync((Guid)topicId, cancellationToken);
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
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.DeleteTopicAsync((Guid)topicId, cancellationToken);
        return result.ToApiResult();
    }

}




