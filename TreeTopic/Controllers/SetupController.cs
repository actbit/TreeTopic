using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Helpers;
using TreeTopic.Services;
using TreeTopic.Permissions;
using TreeTopic.Dtos;
using TreeTopic.Common;
using TreeTopic.Models;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[RequireSetupToken]
public class SetupController : ControllerBase
{
    private static readonly string[] RequiredPermissionsForCompletion =
    [
        TenantPermissions.PermissionRead,
        TenantPermissions.RoleManage
    ];

    private readonly SetupTokenValidationService _tokenValidator;
    private readonly PermissionScanService _permissionScanService;
    private readonly UserManagementService _userManagementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public SetupController(
        SetupTokenValidationService tokenValidator,
        PermissionScanService permissionScanService,
        UserManagementService userManagementService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _tokenValidator = tokenValidator;
        _permissionScanService = permissionScanService;
        _userManagementService = userManagementService;
        _userManager = userManager;
        _db = db;
    }

    /// <summary>
    /// SetupToken を無効化（使用済みとしてマーク）
    /// </summary>
    [HttpPost("token/invalidate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateToken()
    {
        var tenantId = HttpContext.Items["ValidatedTenantId"]?.ToString();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { message = "Authentication required to complete setup" });
        }

        var appUser = await _userManager.FindByIdAsync(userGuid.ToString());
        if (appUser == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        var identityRoles = await _userManager.GetRolesAsync(appUser);
        var claimRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var roles = new HashSet<string>(identityRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var claimRole in claimRoles)
        {
            if (!string.IsNullOrWhiteSpace(claimRole))
            {
                roles.Add(claimRole);
            }
        }

        var userPermissions = await _db.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .Where(p => p.Role != null && roles.Contains(p.Role.Name))
            .Select(p => p.Name)
            .Distinct()
            .ToListAsync(HttpContext.RequestAborted);

        var missingPermissions = RequiredPermissionsForCompletion
            .Where(required => !userPermissions.Contains(required))
            .ToList();

        if (missingPermissions.Count > 0)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Missing required permissions to complete setup",
                requiredPermissions = RequiredPermissionsForCompletion,
                missingPermissions
            });
        }

        // AuthorizationヘッダーからSetupトークンを取得
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return BadRequest(new { message = "Bearer token required" });
        }

        var setupToken = authHeader.Substring("Bearer ".Length).Trim();

        var success = await _tokenValidator.InvalidateSetupTokenAsync(
            tenantId,
            setupToken);

        if (!success)
            return BadRequest(new { message = "Failed to invalidate token" });

        return Ok();
    }

    /// <summary>
    /// 利用可能な権限一覧を取得（Setup用）
    /// </summary>
    [HttpGet("permissions/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAvailablePermissions()
    {
        var permissions = _permissionScanService.GetPermissionsByCategory();

        var result = new
        {
            tenant = permissions["tenant"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            }),
            topic = permissions["topic"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            }),
            room = permissions["room"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            })
        };

        return Ok(result);
    }

    /// <summary>
    /// UserにRoleを割り当て（Setup用）
    /// </summary>
    [HttpPost("users/{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> AddRoleToUser(
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        var result = await _userManagementService.AddRoleToUserAsync((Guid)userId, request);

        if (result.IsFailure)
        {
            var errorResponse = new ErrorResponse(result.Error!);
            return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
        }

        var (user, roles) = result.Data!;
        return Ok(new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles
        });
    }

    /// <summary>
    /// UserからRoleを削除（Setup用）
    /// </summary>
    [HttpDelete("users/{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> RemoveRoleFromUser(
        [FromRoute] MaskedGuid userId,
        [FromBody] RoleAssignmentRequest request)
    {
        var result = await _userManagementService.RemoveRoleFromUserAsync(userId, request);

        if (result.IsFailure)
        {
            var errorResponse = new ErrorResponse(result.Error!);
            return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
        }

        var (user, roles) = result.Data!;
        return Ok(new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles
        });
    }
}
