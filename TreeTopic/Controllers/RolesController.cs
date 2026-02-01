using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RolesController(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    [RequirePermission(IdentityPermissions.RoleRead)]
    public ActionResult<List<RoleDto>> List()
    {
        var roles = _roleManager.Roles.ToList();
        return Ok(roles.Select(r => new RoleDto { Id = r.Id, Name = r.Name }).ToList());
    }

    [HttpPost]
    [RequirePermission(IdentityPermissions.RoleManage)]
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

        return CreatedAtAction(nameof(List), new { id = role.Id }, new RoleDto { Id = role.Id, Name = role.Name });
    }

    [HttpDelete("{roleName}")]
    [RequirePermission(IdentityPermissions.RoleManage)]
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
}
