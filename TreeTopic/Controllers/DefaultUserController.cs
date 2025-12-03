using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Models;
using Finbuckle.MultiTenant.Abstractions;

namespace TreeTopic.Controllers;

[ApiController]
[Route("auth/users")]
[Authorize]
public class DefaultUserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly ILogger<DefaultUserController> _logger;

    public DefaultUserController(
        UserManager<ApplicationUser> userManager,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<DefaultUserController> logger)
    {
        _userManager = userManager;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateDefaultGoogleUser([FromBody] CreateDefaultUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (_tenantAccessor.MultiTenantContext?.TenantInfo != null)
        {
            return BadRequest(new { message = "Users can only be created for the default Google tenant." });
        }

        var email = request.Email.Trim();
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError(nameof(request.Email), "Email is required.");
            return ValidationProblem(ModelState);
        }

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Failed to create user",
                errors = result.Errors.Select(e => e.Description)
            });
        }

        _logger.LogInformation("Default Google user created: {Email}", user.Email);
        return CreatedAtAction(nameof(CreateDefaultGoogleUser), new { user.Id, user.Email }, new { user.Id, user.Email });
    }

}
