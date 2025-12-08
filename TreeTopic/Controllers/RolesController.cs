using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RolesController(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    public ActionResult<List<RoleDto>> List()
    {
        var roles = _roleManager.Roles.ToList();
        return Ok(roles.Select(r => new RoleDto { Id = r.Id, Name = r.Name }).ToList());
    }

    [HttpPost]
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

    [HttpDelete("{roleId:guid}")]
    public async Task<IActionResult> Delete(Guid roleId)
    {
        var role = await _roleManager.FindByIdAsync(roleId.ToString());
        if (role == null)
        {
            return NotFound(new { message = $"Role '{roleId}' not found" });
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return NoContent();
    }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public class RoleCreationRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
