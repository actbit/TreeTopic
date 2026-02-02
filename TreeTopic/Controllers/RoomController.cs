using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
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
    [RequireAny(RoomPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetAllRoomsAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{roomId}")]
    [RequireAny(RoomPermissions.Join)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetRoomByIdAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    [RequireAny(RoomPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.CreateRoomAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{roomId}")]
    [RequireAny(RoomPermissions.Manage)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid roomId, [FromBody] UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.UpdateRoomAsync((Guid)roomId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{roomId}")]
    [RequireAny(RoomPermissions.Manage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.DeleteRoomAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

}




