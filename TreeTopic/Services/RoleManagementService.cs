using Microsoft.AspNetCore.Identity;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// ロール管理サービス
/// ロール作成・削除・パーミッション管理を統括
/// </summary>
public class RoleManagementService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SetupTokenValidationService _setupTokenValidator;
    private readonly ILogger<RoleManagementService> _logger;

    public RoleManagementService(
        RoleManager<ApplicationRole> roleManager,
        SetupTokenValidationService setupTokenValidator,
        ILogger<RoleManagementService> logger)
    {
        _roleManager = roleManager;
        _setupTokenValidator = setupTokenValidator;
        _logger = logger;
    }

    /// <summary>
    /// SetupToken検証を伴うロール作成
    /// </summary>
    public async Task<(bool Success, ApplicationRole? Role, string? ErrorMessage)> CreateRoleAsync(
        string tenant, SetupRoleCreationRequest request)
    {
        try
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
                return (false, null, "Invalid or expired setup token");
            }

            var cleanName = request.Name.Trim();

            // ロールが既に存在するかチェック
            if (await _roleManager.RoleExistsAsync(cleanName))
            {
                _logger.LogWarning("Attempt to create existing role {RoleName} for tenant {TenantId}", cleanName, tenant);
                return (false, null, $"Role '{cleanName}' already exists");
            }

            var role = new ApplicationRole(cleanName);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create role {RoleName} for tenant {TenantId}: {Errors}",
                    cleanName, tenant, errors);
                return (false, null, $"Failed to create role: {errors}");
            }

            _logger.LogInformation("Role {RoleName} created for tenant {TenantId}", cleanName, tenant);
            return (true, role, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role for tenant {TenantId}", tenant);
            return (false, null, "An error occurred while creating the role");
        }
    }

    /// <summary>
    /// SetupToken検証を伴うロール削除
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> DeleteRoleAsync(
        string tenant, SetupRoleDeletionRequest request)
    {
        try
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
                return (false, "Invalid or expired setup token");
            }

            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenant);
                return (false, $"Role '{request.RoleId}' not found");
            }

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to delete role {RoleId} for tenant {TenantId}: {Errors}",
                    request.RoleId, tenant, errors);
                return (false, $"Failed to delete role: {errors}");
            }

            _logger.LogInformation("Role {RoleId} deleted for tenant {TenantId}", request.RoleId, tenant);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId} for tenant {TenantId}", request.RoleId, tenant);
            return (false, "An error occurred while deleting the role");
        }
    }

    /// <summary>
    /// SetupToken検証を伴うロールへのパーミッション追加
    /// </summary>
    public async Task<(bool Success, Permission? Permission, string? ErrorMessage)> AddPermissionToRoleAsync(
        string tenant, SetupPermissionRequest request)
    {
        try
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
                return (false, null, "Invalid or expired setup token");
            }

            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenant);
                return (false, null, $"Role '{request.RoleId}' not found");
            }

            // パーミッションが既に存在するかチェック
            if (role.Authorities?.Any(a => a.Name == request.PermissionName) ?? false)
            {
                _logger.LogWarning("Permission {PermissionName} already exists for role {RoleId}",
                    request.PermissionName, request.RoleId);
                return (false, null, $"Permission '{request.PermissionName}' already exists for this role");
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
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError("Failed to add permission {PermissionName} to role {RoleId}: {Errors}",
                    request.PermissionName, request.RoleId, errors);
                return (false, null, $"Failed to add permission: {errors}");
            }

            _logger.LogInformation("Permission {PermissionName} added to role {RoleId} for tenant {TenantId}",
                request.PermissionName, request.RoleId, tenant);
            return (true, permission, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding permission {PermissionName} to role {RoleId} for tenant {TenantId}",
                request.PermissionName, request.RoleId, tenant);
            return (false, null, "An error occurred while adding the permission");
        }
    }

    /// <summary>
    /// SetupToken検証を伴うロールからのパーミッション削除
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> DeletePermissionFromRoleAsync(
        string tenant, SetupPermissionDeletionRequest request)
    {
        try
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
                return (false, "Invalid or expired setup token");
            }

            // パーミッション削除ロジック（ApplicationDbContext が必要な場合は、
            // PermissionManagementService で実装）
            _logger.LogWarning("Delete permission endpoint not fully implemented - requires ApplicationDbContext access");
            return (false, "Delete permission functionality requires additional setup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission {PermissionId} for tenant {TenantId}",
                request.PermissionId, tenant);
            return (false, "An error occurred while deleting the permission");
        }
    }

    /// <summary>
    /// SetupToken検証を伴うデフォルトロール設定
    /// </summary>
    public async Task<(bool Success, RoleSetupCompletionResponse Response, string? ErrorMessage)> SetupDefaultRoleAsync(
        string tenant, SetupDefaultRoleRequest request)
    {
        try
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                _logger.LogWarning("Invalid or expired SetupToken provided for tenant {TenantId}", tenant);
                var errorResponse = new RoleSetupCompletionResponse
                {
                    Success = false,
                    Message = "Invalid or expired setup token"
                };
                return (false, errorResponse, "Invalid or expired setup token");
            }

            var cleanName = request.DefaultRoleName.Trim();

            // デフォルトロールが既に存在するかチェック
            if (await _roleManager.RoleExistsAsync(cleanName))
            {
                _logger.LogWarning("Default role {RoleName} already exists for tenant {TenantId}", cleanName, tenant);
                var conflictResponse = new RoleSetupCompletionResponse
                {
                    Success = false,
                    Message = $"Default role '{cleanName}' already exists"
                };
                return (false, conflictResponse, $"Default role '{cleanName}' already exists");
            }

            // デフォルトロールを作成
            var role = new ApplicationRole(cleanName);
            var createResult = await _roleManager.CreateAsync(role);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to create default role {RoleName}: {Errors}",
                    cleanName, errors);
                var failResponse = new RoleSetupCompletionResponse
                {
                    Success = false,
                    Message = $"Failed to create default role: {errors}"
                };
                return (false, failResponse, $"Failed to create default role: {errors}");
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

            var response = new RoleSetupCompletionResponse
            {
                Success = true,
                Message = $"Default role '{cleanName}' configured successfully",
                DefaultRoleName = cleanName,
                PermissionsAdded = permissionsAdded
            };
            return (true, response, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default role for tenant {TenantId}", tenant);
            var errorResponse = new RoleSetupCompletionResponse
            {
                Success = false,
                Message = "An error occurred while setting the default role"
            };
            return (false, errorResponse, "An error occurred while setting the default role");
        }
    }
}
