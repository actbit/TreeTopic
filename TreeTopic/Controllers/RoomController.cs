using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Dtos;
using TreeTopic.Services;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class RoomController : ControllerBase
{
    private readonly IRoomManagementService _roomManagementService;

    public RoomController(
        IRoomManagementService roomManagementService)
    {
        _roomManagementService = roomManagementService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetAllRoomsAsync(cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{roomId}")]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetRoomByIdAsync((Guid)roomId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.CreateRoomAsync(request, CurrentUserId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{roomId}")]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid roomId, [FromBody] UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.UpdateRoomAsync((Guid)roomId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{roomId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.DeleteRoomAsync((Guid)roomId, cancellationToken);
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




