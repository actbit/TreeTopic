using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly PermissionManagementService _permissionManagementService;

    public PermissionsController(PermissionManagementService permissionManagementService)
    {
        _permissionManagementService = permissionManagementService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PermissionDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _permissionManagementService.ListPermissionsAsync();

        if (result.IsFailure)
        {
            return result.ToActionResult(p => p.Select(PermissionToDto).ToList());
        }

        var permissionDtos = result.Data!.Select(PermissionToDto).ToList();
        return Ok(permissionDtos);
    }

    [HttpGet("{permissionId}")]
    public async Task<ActionResult<PermissionDto>> Get([FromRoute] MaskedGuid permissionId, CancellationToken cancellationToken)
    {
        var result = await _permissionManagementService.GetPermissionByIdAsync((Guid)permissionId);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<PermissionDto>> Create([FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _permissionManagementService.CreatePermissionAsync(request);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return CreatedAtAction(nameof(Get), new { permissionId = dto.Id }, dto);
    }

    [HttpPut("{permissionId}")]
    public async Task<ActionResult<PermissionDto>> Update([FromRoute] MaskedGuid permissionId, [FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _permissionManagementService.UpdatePermissionAsync((Guid)permissionId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return Ok(dto);
    }

    [HttpDelete("{permissionId}")]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid permissionId)
    {
        var result = await _permissionManagementService.DeletePermissionAsync(permissionId);

        if (result.IsFailure)
        {
            return result.ToApiResult();
        }

        return NoContent();
    }

    private static PermissionDto PermissionToDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
    }
}
