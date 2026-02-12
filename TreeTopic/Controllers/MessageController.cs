using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
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

    private Guid CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or has invalid user ID.");
            }
            return userId;
        }
    }

    [HttpGet]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetAllMessagesAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessagesByTopicAsync((Guid)topicId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}/search")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> SearchByTopic(
        [FromRoute] MaskedGuid topicId,
        [FromQuery(Name = "q")] string query,
        [FromQuery] MessageSearchMode mode = MessageSearchMode.Contains,
        [FromQuery] bool caseSensitive = false,
        [FromQuery][Range(1, 200)] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageManagementService.SearchMessagesByTopicAsync(
            (Guid)topicId,
            query,
            mode,
            caseSensitive,
            take,
            cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}/after/{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetAfter(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid messageId,
        [FromQuery][Range(1, 200)] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageManagementService.GetMessagesAfterAsync((Guid)topicId, (Guid)messageId, CurrentUserId, take, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}/before/{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetBefore(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid messageId,
        [FromQuery][Range(1, 200)] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _messageManagementService.GetMessagesBeforeAsync((Guid)topicId, (Guid)messageId, CurrentUserId, take, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessageByIdAsync((Guid)messageId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageWrite)]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, TenantPermissions.TopicManage, RoomPermissions.TopicWrite)]
    public async Task<IActionResult> Create([FromBody] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageWrite)]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, TenantPermissions.TopicManage, RoomPermissions.TopicWrite)]
    public async Task<IActionResult> CreateWithFiles([FromForm] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageManage)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid messageId, [FromBody] UpdateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.UpdateMessageAsync((Guid)messageId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("move")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageManage)]
    public async Task<IActionResult> MoveMessages([FromBody] MoveMessagesRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.MoveMessagesBeforeAsync(
            (Guid)request.SourceTopicId,
            (Guid)request.TargetTopicId,
            (Guid)request.AnchorMessageId,
            request.IncludeAnchorMessage,
            includeEarlierMessages: true,
            cancellationToken);

        return result.ToApiResult();
    }

    [HttpDelete("{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageManage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.DeleteMessageAsync(messageId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("topic/{topicId}/markAsRead")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> MarkTopicAsRead([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.MarkTopicAsReadAsync((Guid)topicId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }
}
