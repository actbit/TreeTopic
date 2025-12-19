using Finbuckle.MultiTenant.Abstractions;
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
public class RoomController : ControllerBase
{
    private readonly IRoomManagementService _roomManagementService;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public RoomController(
        IRoomManagementService roomManagementService,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _roomManagementService = roomManagementService;
        _tenantAccessor = tenantAccessor;
    }

    private Guid CurrentTenantId => Guid.Parse(_tenantAccessor.MultiTenantContext?.TenantInfo?.Id ?? Guid.Empty.ToString());
    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetAllRoomsAsync(CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{roomId:guid}")]
    public async Task<IActionResult> GetById(Guid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetRoomByIdAsync(roomId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.CreateRoomAsync(request, CurrentUserId, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{roomId:guid}")]
    public async Task<IActionResult> Update(Guid roomId, [FromBody] UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.UpdateRoomAsync(roomId, request, CurrentTenantId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{roomId:guid}")]
    public async Task<IActionResult> Delete(Guid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.DeleteRoomAsync(roomId, CurrentTenantId, cancellationToken);
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
