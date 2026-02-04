using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Data;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/setup/[controller]")]
[AllowAnonymous]
[RequireSetupToken]
public class DefaultUserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IconService _iconService;
    private readonly ILogger<DefaultUserController> _logger;
    private readonly TenantCatalogDbContext _tenantDb;

    public DefaultUserController(
        UserManager<ApplicationUser> userManager,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IconService iconService,
        ILogger<DefaultUserController> logger,
        TenantCatalogDbContext tenantDb)
    {
        _userManager = userManager;
        _tenantAccessor = tenantAccessor;
        _iconService = iconService;
        _logger = logger;
        _tenantDb = tenantDb;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromRoute] string tenant, [FromBody] CreateDefaultUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // OIDC設定がある場合はエラー
        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo?.Detail?.HasOidcSettings() ?? false)
        {
            return BadRequest(new { message = "User creation through setup is not allowed when OIDC is configured." });
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

        var iconFileName = await _iconService.EnsureDefaultUserIconAsync(user, CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            user.IconFileName = iconFileName;
            await _userManager.UpdateAsync(user);
        }

        _logger.LogInformation("User created through setup: {Email}", user.Email);
        return CreatedAtAction(nameof(CreateUser), new { tenant, user.Id, user.Email }, new { user.Id, user.Email });
    }

}




