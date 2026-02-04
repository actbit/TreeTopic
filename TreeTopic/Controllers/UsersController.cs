using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using TreeTopic.Data;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserManagementService _userManagementService;
    private readonly IconService _iconService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SetupTokenValidationService _setupTokenValidator;
    private readonly TenantCatalogDbContext _tenantDb;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManagementService userManagementService,
        IconService iconService,
        UserManager<ApplicationUser> userManager,
        SetupTokenValidationService setupTokenValidator,
        TenantCatalogDbContext tenantDb,
        ILogger<UsersController> logger)
    {
        _userManagementService = userManagementService;
        _iconService = iconService;
        _userManager = userManager;
        _setupTokenValidator = setupTokenValidator;
        _tenantDb = tenantDb;
        _logger = logger;
    }

    /// <summary>
    /// SetupToken の Authorization ヘッダーから検証
    /// </summary>
    private async Task<bool> ValidateSetupTokenFromHeader(string tenant)
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Bearer "))
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            // テナント識別子からテナントIDを解決
            var tenantInfo = await _tenantDb.Tenants.FirstOrDefaultAsync(t => t.Identifier == tenant);
            if (tenantInfo == null)
            {
                return false;
            }

            return await _setupTokenValidator.ValidateSetupTokenAsync(tenantInfo.Id, token);
        }
        return false;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll([FromRoute] string tenant, CancellationToken cancellationToken)
    {
        // Allow access for all logged-in users OR setupToken
        var hasSetupToken = await ValidateSetupTokenFromHeader(tenant);
        var isAuthenticated = User.Identity?.IsAuthenticated == true;

        if (!isAuthenticated && !hasSetupToken)
            return Unauthorized();

        var result = await _userManagementService.GetAllUsersAsync();

        if (result.IsFailure)
        {
            return result.ToActionResult(userList => userList.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList());
        }

        var userDtos = result.Data!.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList();
        return Ok(userDtos);
    }

    [HttpGet("{userId}")]
    [RequireAny(IdentityPermissions.UserRead)]
    public async Task<ActionResult<UserSummaryDto>> GetById([FromRoute] MaskedGuid userId)
    {
        var result = await _userManagementService.GetUserByIdAsync((Guid)userId);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApplicationUserDto>> GetMe(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(guid.ToString());
        if (user == null)
            return NotFound();

        var dto = new ApplicationUserDto
        {
            Id = user.Id.ToString(),
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IconFileName = user.IconFileName,
            IconUrl = _iconService.GetUserIconUrl(user)
        };

        return Ok(dto);
    }

    [HttpPost("{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> AddRole(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        // Allow access with either permission OR setupToken
        var hasPermission = User.HasClaim(c =>
            c.Type == "permission" && c.Value == IdentityPermissions.UserManage);
        var hasSetupToken = await ValidateSetupTokenFromHeader(tenant);

        if (!hasPermission && !hasSetupToken)
            return Forbid();

        var result = await _userManagementService.AddRoleToUserAsync((Guid)userId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    [HttpDelete("{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> RemoveRole(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        // Allow access with either permission OR setupToken
        var hasPermission = User.HasClaim(c =>
            c.Type == "permission" && c.Value == IdentityPermissions.UserManage);
        var hasSetupToken = await ValidateSetupTokenFromHeader(tenant);

        if (!hasPermission && !hasSetupToken)
            return Forbid();

        var result = await _userManagementService.RemoveRoleFromUserAsync(userId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

   
    [HttpPut("me")]
    public async Task<ActionResult<ApplicationUserDto>> UpdateMe([FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(guid.ToString());
        if (user == null)
            return NotFound();

        // 表示名の更新
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "表示名の更新に失敗しました" });
            }
        }

        var dto = new ApplicationUserDto
        {
            Id = user.Id.ToString(),
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IconFileName = user.IconFileName,
            IconUrl = _iconService.GetUserIconUrl(user)
        };

        return Ok(dto);
    }

    [HttpPost("me/icon")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyIcon([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(guid.ToString());
        if (user == null)
            return NotFound();

        var fileName = await _iconService.SaveUserIconAsync(user, file, cancellationToken);
        user.IconFileName = fileName;
        await _userManager.UpdateAsync(user);

        return Ok(new { iconUrl = _iconService.GetUserIconUrl(user), iconFileName = fileName });
    }

    /// <summary>
    /// Create a new user (for OIDC default mode - only allowed when RoleClaimName is not set)
    /// </summary>
    [HttpPost]
    [Authorize]
    [RequireAny(IdentityPermissions.UserManagement)]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(
        [FromRoute] string tenant,
        [FromBody] CreateUserRequest request)
    {
        // Check if RoleClaimName is set - if so, user creation is not allowed
        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo?.Detail?.RoleClaimName != null &&
            tenantInfo.Detail.RoleClaimName.Trim() != string.Empty)
        {
            return BadRequest(new
            {
                message = "User creation is not allowed when OIDC role claim is configured. " +
                          "Users are automatically managed by the OIDC provider."
            });
        }

        var result = await _userManagementService.CreateUserAsync(request);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return CreatedAtAction(nameof(GetById), new { tenant, userId = user.Id }, dto);
    }

    /// <summary>
    /// Ban a user
    /// </summary>
    [HttpPost("{userId}/ban")]
    [Authorize]
    [RequireAny(IdentityPermissions.UserManagement)]
    public async Task<ActionResult<UserSummaryDto>> BanUser(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] BanUserRequest request)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        var result = await _userManagementService.BanUserAsync((Guid)userId, request, currentUserId);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    /// <summary>
    /// Unban a user
    /// </summary>
    [HttpDelete("{userId}/ban")]
    [Authorize]
    [RequireAny(IdentityPermissions.UserManagement)]
    public async Task<ActionResult<UserSummaryDto>> UnbanUser(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId)
    {
        var result = await _userManagementService.UnbanUserAsync((Guid)userId);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    private UserSummaryDto UserToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IconUrl = _iconService.GetUserIconUrl(user),
            Roles = roles,
            IsBanned = user.IsBanned,
            BannedAt = user.BannedAt?.ToString("o"),
            BannedBy = user.BannedBy,
            BanReason = user.BanReason
        };
    }
}
