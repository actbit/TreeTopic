using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Services;
using System.Security.Claims;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly IMessageManagementService _messageManagementService;

    public MessageController(
        IMessageManagementService messageManagementService)
    {
        _messageManagementService = messageManagementService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetAllMessagesAsync(cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("topic/{topicId}")]
    public async Task<IActionResult> GetByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessagesByTopicAsync((Guid)topicId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{messageId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessageByIdAsync((Guid)messageId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{messageId}")]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid messageId, [FromBody] UpdateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.UpdateMessageAsync((Guid)messageId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.DeleteMessageAsync(messageId, cancellationToken);
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




