using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/setup/[controller]")]
public class RoleSetupController : ControllerBase
{
    private readonly RoleManagementService _roleManagementService;

    public RoleSetupController(RoleManagementService roleManagementService)
    {
        _roleManagementService = roleManagementService;
    }

    [HttpPost("create")]
    public async Task<ActionResult<RoleDto>> CreateRole(string tenant, [FromBody] SetupRoleCreationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleManagementService.CreateRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => new RoleDto { Id = r.Id, Name = r.Name });
        }

        var role = result.Data!;
        return Ok(new RoleDto { Id = role.Id, Name = role.Name });
    }

    [HttpDelete("{roleName}")]
    public async Task<IActionResult> DeleteRole(string tenant, string roleName, [FromBody] SetupTokenRequest request)
    {
        var deletionRequest = new SetupRoleDeletionRequest
        {
            SetupToken = request.SetupToken,
            RoleName = roleName
        };

        var result = await _roleManagementService.DeleteRoleAsync(tenant, deletionRequest);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    [HttpPost("permissions/add")]
    public async Task<ActionResult<PermissionDto>> AddPermission(string tenant, [FromBody] SetupPermissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleManagementService.AddPermissionToRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                RoleId = p.RoleId,
                RoleName = p.Role?.Name
            });
        }

        var permission = result.Data!;
        var dto = new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
        return Ok(dto);
    }

    [HttpPost("permissions/delete")]
    public async Task<IActionResult> DeletePermission(string tenant, [FromBody] SetupPermissionDeletionRequest request)
    {
        var result = await _roleManagementService.DeletePermissionFromRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        return NoContent();
    }

    [HttpPost("default")]
    public async Task<ActionResult<RoleSetupCompletionResponse>> SetDefaultRole(string tenant, [FromBody] SetupDefaultRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleManagementService.SetupDefaultRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => r);
        }

        return Ok(result.Data!);
    }
}
