using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

        var userWithRoles = await Task.WhenAll(users.Select(async user =>
        {
            var roles = await _userManager.GetRolesAsync(user);
            return (user, roles);
        }));

        var summaries = userWithRoles.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList();
        return Ok(summaries);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserSummaryDto>> GetById(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound(new { message = $"User '{userId}' not found" });
        }

        return Ok(await BuildUserDtoAsync(user));
    }

    [HttpPost("{userId:guid}/roles")]
    public async Task<ActionResult<UserSummaryDto>> AddRole(Guid userId, [FromBody] RoleAssignmentRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound(new { message = $"User '{userId}' not found" });
        }

        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest(new { message = "RoleName is required" });
        }

        var roleName = request.RoleName.Trim();
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return NotFound(new { message = $"Role '{roleName}' does not exist" });
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return Ok(await BuildUserDtoAsync(user));
    }

    [HttpDelete("{userId:guid}/roles")]
    public async Task<ActionResult<UserSummaryDto>> RemoveRole(Guid userId, [FromBody] RoleAssignmentRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return NotFound(new { message = $"User '{userId}' not found" });
        }

        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return BadRequest(new { message = "RoleName is required" });
        }

        var roleName = request.RoleName.Trim();
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return NotFound(new { message = $"Role '{roleName}' does not exist" });
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!removeResult.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(removeResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        }

        return Ok(await BuildUserDtoAsync(user));
    }

    private async Task<UserSummaryDto> BuildUserDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return UserToDto(user, roles);
    }

    private static UserSummaryDto UserToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles
        };
    }
}
