using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
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
    public async Task<ActionResult> List(CancellationToken cancellationToken)
    {
        var (success, permissions, errorMessage) = await _permissionManagementService.ListPermissionsAsync();

        if (!success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = errorMessage });
        }

        return Ok(permissions);
    }

    [HttpGet("{permissionId:guid}")]
    public async Task<ActionResult> Get(Guid permissionId, CancellationToken cancellationToken)
    {
        var (success, permission, errorMessage) = await _permissionManagementService.GetPermissionByIdAsync(permissionId);

        if (!success)
        {
            return NotFound(new { message = errorMessage });
        }

        return Ok(permission);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (success, permission, errorMessage) = await _permissionManagementService.CreatePermissionAsync(request);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("required") == true)
            {
                return BadRequest(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("already exists") == true)
            {
                return Conflict(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return CreatedAtAction(nameof(Get), new { permissionId = permission!.Id }, permission);
    }

    [HttpPut("{permissionId:guid}")]
    public async Task<ActionResult> Update(Guid permissionId, [FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (success, permission, errorMessage) = await _permissionManagementService.UpdatePermissionAsync(permissionId, request);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("already exists") == true)
            {
                return Conflict(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return Ok(permission);
    }
}
