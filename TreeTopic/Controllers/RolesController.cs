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
[Route("{tenant}/api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RolesController(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<RoleDto>>> List()
    {
        // Allow access with either RoleRead OR UserManagement permission
        var hasPermission = User.HasClaim(c =>
            c.Type == "permission" && (
                c.Value == TenantPermissions.RoleRead ||
                c.Value == TenantPermissions.UserManagement));

        if (!hasPermission)
            return Forbid();

        var roles = await _roleManager.Roles
            .Include(r => r.Authorities)
            .ToListAsync();

        return Ok(roles.Select(MapRoleToDto).ToList());
    }

    [HttpPost]
    [RequireAny(TenantPermissions.RoleManage)]
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
    [RequireAny(TenantPermissions.RoleManage)]
    public async Task<IActionResult> Delete(string roleName)
    {
        // OIDCロール同期が有効な場合はロール管理を禁止
        var tenant = HttpContext.GetRouteValue("tenant")?.ToString();
        if (!string.IsNullOrEmpty(tenant))
        {
            var tenantInfo = await _tenantDb.Tenants
                .Include(t => t.Detail)
                .FirstOrDefaultAsync(t => t.Identifier == tenant);

            if (!tenantInfo?.Detail.CanManageRoles() ?? false)
            {
                return BadRequest(new
                {
                    message = "Role management is not allowed when OIDC role claim is configured. " +
                              "Roles are automatically managed by the OIDC provider."
                });
            }
        }

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
