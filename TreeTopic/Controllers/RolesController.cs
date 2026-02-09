using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Authorize]
[Route("{tenant}/api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RolesController(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    [RequireAny(PermissionScope.Role, TenantPermissions.RoleRead, TenantPermissions.UserAdmin)]
    public async Task<ActionResult<List<RoleDto>>> List()
    {
        var roles = await _roleManager.Roles
            .Include(r => r.Authorities)
            .ToListAsync();

        return Ok(roles.Select(MapRoleToDto).ToList());
    }

    [HttpPost]
    [RequireAny(PermissionScope.Role, TenantPermissions.RoleManage)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] RoleCreationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var cleanName = request.Name.Trim();
        if (await _roleManager.RoleExistsAsync(cleanName))
        {
            return Conflict(new { message = $"Role '{cleanName}' already exists" });
        }

        var role = new ApplicationRole(cleanName);
        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return CreatedAtAction(nameof(List), new { id = role.Id }, MapRoleToDto(role));
    }

    [HttpDelete("{roleName}")]
    [RequireAny(PermissionScope.Role, TenantPermissions.RoleManage)]
    public async Task<IActionResult> Delete(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return BadRequest(new { message = "Role name cannot be empty" });
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return NotFound(new { message = $"Role '{roleName}' not found" });
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return NoContent();
    }

    private static RoleDto MapRoleToDto(ApplicationRole role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Permissions = role.Authorities?.Select(a => a.Name).ToList()
        };
    }
}
