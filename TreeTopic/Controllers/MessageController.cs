using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
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
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}")]
    public async Task<IActionResult> GetByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessagesByTopicAsync((Guid)topicId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}/after/{messageId}")]
    public async Task<IActionResult> GetAfter(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid messageId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageManagementService.GetMessagesAfterAsync((Guid)topicId, (Guid)messageId, take, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}/before/{messageId}")]
    public async Task<IActionResult> GetBefore(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid messageId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageManagementService.GetMessagesBeforeAsync((Guid)topicId, (Guid)messageId, take, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{messageId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessageByIdAsync((Guid)messageId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateWithFiles([FromForm] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{messageId}")]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid messageId, [FromBody] UpdateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.UpdateMessageAsync((Guid)messageId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{messageId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.DeleteMessageAsync(messageId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

}




