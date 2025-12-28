using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly IMessageManagementService _messageManagementService;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public MessageController(
        IMessageManagementService messageManagementService,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _messageManagementService = messageManagementService;
        _tenantAccessor = tenantAccessor;
    }

    private Guid CurrentTenantId => Guid.Parse(_tenantAccessor.MultiTenantContext?.TenantInfo?.Id ?? Guid.Empty.ToString());
    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetAllMessagesAsync(CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("topic/{topicId:guid}")]
    public async Task<IActionResult> GetByTopic(Guid topicId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessagesByTopicAsync(topicId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{messageId:guid}")]
    public async Task<IActionResult> GetById(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.GetMessageByIdAsync(messageId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.CreateMessageAsync(request, CurrentUserId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{messageId:guid}")]
    public async Task<IActionResult> Update(Guid messageId, [FromBody] UpdateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _messageManagementService.UpdateMessageAsync(messageId, request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{messageId:guid}")]
    public async Task<IActionResult> Delete(Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _messageManagementService.DeleteMessageAsync(messageId, CurrentTenantId, cancellationToken);
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




