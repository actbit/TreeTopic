using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Models;
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
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SetupTokenValidationService _setupTokenValidator;
    private readonly ILogger<RoleSetupController> _logger;

    public RoleSetupController(
        RoleManager<ApplicationRole> roleManager,
        SetupTokenValidationService setupTokenValidator,
        ILogger<RoleSetupController> logger)
    {
        _roleManager = roleManager;
        _setupTokenValidator = setupTokenValidator;
        _logger = logger;
    }

    /// <summary>
    /// ロールを作成（SetupToken経由）
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<RoleDto>> CreateRole(string tenant, [FromBody] SetupRoleCreationRequest request)
    {
        // SetupToken の検証
        if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
        {
            _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var cleanName = request.Name.Trim();

        // ロールが既に存在するかチェック
        if (await _roleManager.RoleExistsAsync(cleanName))
        {
            _logger.LogWarning("Attempt to create existing role {RoleName} for tenant {TenantId}", cleanName, tenant);
            return Conflict(new { message = $"Role '{cleanName}' already exists" });
        }

        try
        {
            var role = new ApplicationRole(cleanName);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create role {RoleName} for tenant {TenantId}: {Errors}",
                    cleanName, tenant, string.Join(", ", result.Errors.Select(e => e.Description)));
                return ValidationProblem(
                    new ValidationProblemDetails(
                        result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
            }

            _logger.LogInformation("Role {RoleName} created for tenant {TenantId}", cleanName, tenant);
            return Ok(new RoleDto { Id = role.Id, Name = role.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName} for tenant {TenantId}", cleanName, tenant);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the role" });
        }
    }

    /// <summary>
    /// ロールを削除（SetupToken経由）
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> DeleteRole(string tenant, [FromBody] SetupRoleDeletionRequest request)
    {
        // SetupToken の検証
        if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
        {
            _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        try
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenant);
                return NotFound(new { message = $"Role '{request.RoleId}' not found" });
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete role {RoleId} for tenant {TenantId}: {Errors}",
                    request.RoleId, tenant, string.Join(", ", result.Errors.Select(e => e.Description)));
                return ValidationProblem(
                    new ValidationProblemDetails(
                        result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
            }

            _logger.LogInformation("Role {RoleId} deleted for tenant {TenantId}", request.RoleId, tenant);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId} for tenant {TenantId}", request.RoleId, tenant);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while deleting the role" });
        }
    }

    /// <summary>
    /// パーミッションを追加（SetupToken経由）
    /// </summary>
    [HttpPost("permissions/add")]
    public async Task<ActionResult<PermissionDto>> AddPermission(string tenant, [FromBody] SetupPermissionRequest request)
    {
        // SetupToken の検証
        if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
        {
            _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenant);
                return NotFound(new { message = $"Role '{request.RoleId}' not found" });
            }

            // パーミッションが既に存在するかチェック
            if (role.Authorities?.Any(a => a.Name == request.PermissionName) ?? false)
            {
                _logger.LogWarning("Permission {PermissionName} already exists for role {RoleId}",
                    request.PermissionName, request.RoleId);
                return Conflict(new { message = $"Permission '{request.PermissionName}' already exists for this role" });
            }

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = request.PermissionName,
                RoleId = request.RoleId,
                CreatedAt = DateTime.UtcNow
            };

            if (role.Authorities == null)
            {
                role.Authorities = new List<Permission>();
            }
            role.Authorities.Add(permission);
            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to add permission {PermissionName} to role {RoleId}: {Errors}",
                    request.PermissionName, request.RoleId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return ValidationProblem(
                    new ValidationProblemDetails(
                        result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
            }

            _logger.LogInformation("Permission {PermissionName} added to role {RoleId} for tenant {TenantId}",
                request.PermissionName, request.RoleId, tenant);
            return Ok(new PermissionDto
            {
                Id = permission.Id,
                Name = permission.Name,
                RoleId = permission.RoleId,
                RoleName = role.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permission {PermissionName} to role {RoleId} for tenant {TenantId}",
                request.PermissionName, request.RoleId, tenant);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while adding the permission" });
        }
    }

    /// <summary>
    /// パーミッションを削除（SetupToken経由）
    /// </summary>
    [HttpPost("permissions/delete")]
    public async Task<IActionResult> DeletePermission(string tenant, [FromBody] SetupPermissionDeletionRequest request)
    {
        // SetupToken の検証
        if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
        {
            _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        try
        {
            // パーミッションを探す - これは複雑なので、ApplicationDbContext から直接削除する必要があります
            // ここでは簡単な実装として、PermissionsController で実装するか、
            // ApplicationDbContext への直接アクセスを通じて実装します

            _logger.LogWarning("Delete permission endpoint not fully implemented - requires ApplicationDbContext access");
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { message = "Delete permission functionality requires additional setup" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission {PermissionId} for tenant {TenantId}",
                request.PermissionId, tenant);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while deleting the permission" });
        }
    }

    /// <summary>
    /// デフォルトロール（新規ユーザーに自動付与されるロール）を設定
    /// </summary>
    [HttpPost("default")]
    public async Task<IActionResult> SetDefaultRole(string tenant, [FromBody] SetupDefaultRoleRequest request)
    {
        // SetupToken の検証
        if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
        {
            _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
            return Unauthorized(new { message = "Invalid or expired setup token" });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var cleanName = request.DefaultRoleName.Trim();

            // デフォルトロールが既に存在するかチェック
            if (await _roleManager.RoleExistsAsync(cleanName))
            {
                _logger.LogWarning("Default role {RoleName} already exists for tenant {TenantId}", cleanName, tenant);
                return Conflict(new { message = $"Default role '{cleanName}' already exists" });
            }

            // デフォルトロールを作成
            var role = new ApplicationRole(cleanName);
            var createResult = await _roleManager.CreateAsync(role);

            if (!createResult.Succeeded)
            {
                _logger.LogError("Failed to create default role {RoleName}: {Errors}",
                    cleanName, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return ValidationProblem(
                    new ValidationProblemDetails(
                        createResult.Errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
            }

            // デフォルトパーミッションを追加
            int permissionsAdded = 0;
            if (request.DefaultPermissions?.Count > 0)
            {
                foreach (var permissionName in request.DefaultPermissions)
                {
                    if (string.IsNullOrWhiteSpace(permissionName))
                        continue;

                    var permission = new Permission
                    {
                        Id = Guid.NewGuid(),
                        Name = permissionName.Trim(),
                        RoleId = role.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    role.Authorities.Add(permission);
                    permissionsAdded++;
                }

                if (permissionsAdded > 0)
                {
                    var updateResult = await _roleManager.UpdateAsync(role);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogWarning("Failed to add all default permissions to role {RoleName}",
                            cleanName);
                    }
                }
            }

            _logger.LogInformation(
                "Default role {RoleName} created with {PermissionCount} permissions for tenant {TenantId}",
                cleanName, permissionsAdded, tenant);

            return Ok(new RoleSetupCompletionResponse
            {
                Success = true,
                Message = $"Default role '{cleanName}' configured successfully",
                DefaultRoleName = cleanName,
                PermissionsAdded = permissionsAdded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default role for tenant {TenantId}", tenant);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while setting the default role" });
        }
    }
}
