using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// SetupToken を使用したロール初期設定API
/// テナント初期化時のみ使用可能
/// </summary>
[ApiController]
[Route("{tenant}/api/setup/[controller]")]
public class RoleSetupController : ControllerBase
{
    private readonly RoleManagementService _roleManagementService;
    private readonly ILogger<RoleSetupController> _logger;

    public RoleSetupController(
        RoleManagementService roleManagementService,
        ILogger<RoleSetupController> logger)
    {
        _roleManagementService = roleManagementService;
        _logger = logger;
    }

    /// <summary>
    /// ロールを作成（SetupToken経由）
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<RoleDto>> CreateRole(string tenant, [FromBody] SetupRoleCreationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (success, role, errorMessage) = await _roleManagementService.CreateRoleAsync(tenant, request);

        if (!success)
        {
            // SetupToken検証エラーか一般的なエラーかで判定
            if (errorMessage?.Contains("Invalid or expired") == true)
            {
                return Unauthorized(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("already exists") == true)
            {
                return Conflict(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return Ok(new RoleDto { Id = role!.Id, Name = role.Name });
    }

    /// <summary>
    /// ロールを削除（SetupToken経由）
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteRole(string tenant, [FromBody] SetupRoleDeletionRequest request)
    {
        var (success, errorMessage) = await _roleManagementService.DeleteRoleAsync(tenant, request);

        if (!success)
        {
            // SetupToken検証エラーか一般的なエラーかで判定
            if (errorMessage?.Contains("Invalid or expired") == true)
            {
                return Unauthorized(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// パーミッションを追加（SetupToken経由）
    /// </summary>
    [HttpPost("permissions/add")]
    public async Task<ActionResult<PermissionDto>> AddPermission(string tenant, [FromBody] SetupPermissionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (success, permission, errorMessage) = await _roleManagementService.AddPermissionToRoleAsync(tenant, request);

        if (!success)
        {
            // SetupToken検証エラーか一般的なエラーかで判定
            if (errorMessage?.Contains("Invalid or expired") == true)
            {
                return Unauthorized(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("already exists") == true)
            {
                return Conflict(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return Ok(permission);
    }

    /// <summary>
    /// パーミッションを削除（SetupToken経由）
    /// </summary>
    [HttpPost("permissions/delete")]
    public async Task<IActionResult> DeletePermission(string tenant, [FromBody] SetupPermissionDeletionRequest request)
    {
        var (success, errorMessage) = await _roleManagementService.DeletePermissionFromRoleAsync(tenant, request);

        if (!success)
        {
            // SetupToken検証エラーか一般的なエラーかで判定
            if (errorMessage?.Contains("Invalid or expired") == true)
            {
                return Unauthorized(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("not fully implemented") == true)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// デフォルトロール（新規ユーザーに自動付与されるロール）を設定
    /// </summary>
    [HttpPost("default")]
    public async Task<IActionResult> SetDefaultRole(string tenant, [FromBody] SetupDefaultRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (success, response, errorMessage) = await _roleManagementService.SetupDefaultRoleAsync(tenant, request);

        if (!success)
        {
            // SetupToken検証エラーか一般的なエラーかで判定
            if (errorMessage?.Contains("Invalid or expired") == true)
            {
                return Unauthorized(response);
            }
            else if (errorMessage?.Contains("already exists") == true)
            {
                return Conflict(response);
            }
            return BadRequest(response);
        }

        return Ok(response);
    }
}
