using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Helpers;
using TreeTopic.Services;
using TreeTopic.Permissions;
using TreeTopic.Dtos;
using TreeTopic.Common;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[RequireSetupToken]
public class SetupController : ControllerBase
{
    private readonly SetupTokenValidationService _tokenValidator;
    private readonly PermissionScanService _permissionScanService;
    private readonly UserManagementService _userManagementService;

    public SetupController(
        SetupTokenValidationService tokenValidator,
        PermissionScanService permissionScanService,
        UserManagementService userManagementService)
    {
        _tokenValidator = tokenValidator;
        _permissionScanService = permissionScanService;
        _userManagementService = userManagementService;
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
