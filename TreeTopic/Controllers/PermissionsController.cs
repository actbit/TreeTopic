using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
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

    [HttpGet("{permissionId:guid}")]
    public async Task<ActionResult<PermissionDto>> Get(Guid permissionId, CancellationToken cancellationToken)
    {
        var result = await _permissionManagementService.GetPermissionByIdAsync(permissionId);

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

    [HttpPut("{permissionId:guid}")]
    public async Task<ActionResult<PermissionDto>> Update(Guid permissionId, [FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _permissionManagementService.UpdatePermissionAsync(permissionId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return Ok(dto);
    }

    [HttpDelete("{permissionId:guid}")]
    public async Task<IActionResult> Delete(Guid permissionId)
    {
        var result = await _permissionManagementService.DeletePermissionAsync(permissionId);

        if (result.IsFailure)
        {
            return result.ToActionResult();
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
