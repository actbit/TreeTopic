using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class RoomRolesController : ControllerBase
{
    private readonly RoomRoleManagementService _roleService;
    private readonly ILogger<RoomRolesController> _logger;

    public RoomRolesController(
        RoomRoleManagementService roleService,
        ILogger<RoomRolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    /// <summary>
    /// すべてのロールを取得
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RoomRoleDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _roleService.ListRolesAsync(cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => r.Select(RoomRoleManagementService.ToDto).ToList());
        }

        return Ok(result.Data!.Select(RoomRoleManagementService.ToDto).ToList());
    }

    /// <summary>
    /// IDでロールを取得
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<RoomRoleDto>> GetById([FromRoute] MaskedGuid id, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetRoleByIdAsync((Guid)id, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return Ok(RoomRoleManagementService.ToDto(result.Data!));
    }

    /// <summary>
    /// ロールを作成
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RoomRoleDto>> Create([FromBody] CreateRoomRoleRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleService.CreateRoleAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Data!.Id },
            RoomRoleManagementService.ToDto(result.Data));
    }

    /// <summary>
    /// ロールを更新
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<RoomRoleDto>> Update(
        [FromRoute] MaskedGuid id,
        [FromBody] UpdateRoomRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleService.UpdateRoleAsync((Guid)id, request, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult(RoomRoleManagementService.ToDto);
        }

        return Ok(RoomRoleManagementService.ToDto(result.Data!));
    }

    /// <summary>
    /// ロールを削除
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid id, CancellationToken cancellationToken)
    {
        var result = await _roleService.DeleteRoleAsync((Guid)id, cancellationToken);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }
}
