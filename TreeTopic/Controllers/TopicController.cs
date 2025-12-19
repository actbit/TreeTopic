using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class TopicController : ControllerBase
{
    private readonly ITopicManagementService _topicManagementService;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public TopicController(
        ITopicManagementService topicManagementService,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _topicManagementService = topicManagementService;
        _tenantAccessor = tenantAccessor;
    }

    private Guid CurrentTenantId => Guid.Parse(_tenantAccessor.MultiTenantContext?.TenantInfo?.Id ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetAllTopicsAsync(CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("room/{roomId:guid}")]
    public async Task<IActionResult> GetByRoom(Guid roomId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicsByRoomAsync(roomId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{topicId:guid}")]
    public async Task<IActionResult> GetById(Guid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.GetTopicByIdAsync(topicId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.CreateTopicAsync(request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{topicId:guid}")]
    public async Task<IActionResult> Update(Guid topicId, [FromBody] UpdateTopicRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _topicManagementService.UpdateTopicAsync(topicId, request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{topicId:guid}")]
    public async Task<IActionResult> Delete(Guid topicId, CancellationToken cancellationToken)
    {
        var result = await _topicManagementService.DeleteTopicAsync(topicId, CurrentTenantId, cancellationToken);
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
