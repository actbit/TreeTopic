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
    private readonly TenantCatalogDbContext _tenantDb;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManagementService userManagementService,
        IconService iconService,
        UserManager<ApplicationUser> userManager,
        TenantCatalogDbContext tenantDb,
        ILogger<UsersController> logger)
    {
        _userManagementService = userManagementService;
        _iconService = iconService;
        _userManager = userManager;
        _tenantDb = tenantDb;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    [RequireAny(TenantPermissions.UserRead)]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll([FromRoute] string tenant, CancellationToken cancellationToken)
    {
        var result = await _userManagementService.GetAllUsersAsync();

        if (result.IsFailure)
        {
            return result.ToActionResult(userList => userList.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList());
        }

        var userDtos = result.Data!.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList();
        return Ok(userDtos);
    }

    [HttpGet("{userId}")]
    [RequireAny(TenantPermissions.UserRead)]
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
    [Authorize]
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
    [Authorize]
    [RequireAny(TenantPermissions.UserManage)]
    public async Task<ActionResult<UserSummaryDto>> AddRole(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        // OIDCロール同期が有効な場合はUserへのRole割り当てを禁止
        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo?.Detail?.CanAssignRolesToUsers() != true)
        {
            return BadRequest(new
            {
                message = "Role assignment is not allowed when OIDC role claim is configured. " +
                          "User roles are automatically managed by the OIDC provider."
            });
        }

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
    [Authorize]
    [RequireAny(TenantPermissions.UserManage)]
    public async Task<ActionResult<UserSummaryDto>> RemoveRole(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        // OIDCロール同期が有効な場合はUserへのRole割り当てを禁止
        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo?.Detail?.CanAssignRolesToUsers() != true)
        {
            return BadRequest(new
            {
                message = "Role assignment is not allowed when OIDC role claim is configured. " +
                          "User roles are automatically managed by the OIDC provider."
            });
        }

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
    [Authorize]
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
    [Authorize]
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

        string? newFileName = null;
        var fileCreated = false;
        try
        {
            newFileName = await _iconService.SaveUserIconAsync(user, file, cancellationToken);
            fileCreated = true;

            var oldFileName = user.IconFileName;
            user.IconFileName = newFileName;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                // DB更新失敗時にファイルを削除
                if (!string.IsNullOrEmpty(newFileName))
                {
                    await _iconService.DeleteUserIconAsync(user, newFileName, cancellationToken);
                }
                return BadRequest(new { message = "Failed to update user icon." });
            }

            // 古いアイコンファイルを削除
            if (!string.IsNullOrEmpty(oldFileName))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _iconService.DeleteUserIconAsync(user, oldFileName, cancellationToken);
                    }
                    catch
                    {
                        // 削除に失敗しても無視
                    }
                }, cancellationToken);
            }

            return Ok(new { iconUrl = _iconService.GetUserIconUrl(user), iconFileName = newFileName });
        }
        catch
        {
            // エラー時にファイルを削除
            if (fileCreated && !string.IsNullOrEmpty(newFileName))
            {
                try
                {
                    await _iconService.DeleteUserIconAsync(user, newFileName, cancellationToken);
                }
                catch
                {
                    // ファイル削除に失敗しても無視
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Create a new user (only allowed when OIDC is not configured)
    /// </summary>
    [HttpPost]
    [Authorize]
    [RequireAny(TenantPermissions.UserAdmin)]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(
        [FromRoute] string tenant,
        [FromBody] CreateUserRequest request)
    {
        // OIDC設定がある場合はユーザー作成を禁止
        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenant);

        if (tenantInfo?.Detail?.CanCreateUsers() != true)
        {
            var hasOidcRoleSync = tenantInfo?.Detail.HasOidcRoleSync() ?? false;
            var message = hasOidcRoleSync
                ? "User creation is not allowed when OIDC role claim is configured. " +
                  "Users are automatically managed by the OIDC provider."
                : "User creation is not allowed when OIDC is configured. " +
                  "Users are authenticated through the OIDC provider.";
            return BadRequest(new { message });
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
    [RequireAny(TenantPermissions.UserAdmin)]
    public async Task<ActionResult<UserSummaryDto>> BanUser(
        [FromRoute] string tenant,
        [FromRoute] MaskedGuid userId,
        [FromBody] BanUserRequest request)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
            return Unauthorized();

        // 自己BANを禁止
        if (Guid.TryParse(currentUserId, out var currentUserIdGuid) && currentUserIdGuid == (Guid)userId)
        {
            return BadRequest(new { message = "You cannot ban yourself." });
        }

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
    [RequireAny(TenantPermissions.UserAdmin)]
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
