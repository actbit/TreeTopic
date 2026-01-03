using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Dtos;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
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
        return HandleResult(result);
    }

    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync((Guid)roomId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{topicId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync((Guid)topicId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.CreateTopicAsync(request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{topicId}")]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.UpdateTopicAsync((Guid)topicId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{topicId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.DeleteTopicAsync((Guid)topicId, cancellationToken);
        return HandleResult(result);
    }

    private IActionResult HandleResult<T>(Common.Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, result.Data);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }

    private IActionResult HandleResult(Common.Result result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }
}




