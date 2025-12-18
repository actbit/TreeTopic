using Microsoft.AspNetCore.Identity;
using TreeTopic.Common;
using TreeTopic.Common.Helpers;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

public class RoleManagementService : BaseService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SetupTokenValidationService _setupTokenValidator;

    public RoleManagementService(
        RoleManager<ApplicationRole> roleManager,
        SetupTokenValidationService setupTokenValidator,
        ILogger<RoleManagementService> logger) : base(logger)
    {
        _roleManager = roleManager;
        _setupTokenValidator = setupTokenValidator;
    }

    public async Task<Result<ApplicationRole>> CreateRoleAsync(
        string tenant, SetupRoleCreationRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                return Result<ApplicationRole>.Unauthorized("Invalid or expired setup token");
            }

            // Validate role name is not empty
            var nameValidation = ValidationHelper.ValidateRequired(request.Name, "Role name");
            if (nameValidation.IsFailure)
            {
                return Result<ApplicationRole>.BadRequest(nameValidation.Error!.Message);
            }

            var cleanName = request.Name.Trim();

            // Check if role already exists
            if (await _roleManager.RoleExistsAsync(cleanName))
            {
                return Result<ApplicationRole>.Conflict($"Role '{cleanName}' already exists");
            }

            var role = new ApplicationRole(cleanName);
            var result = await _roleManager.CreateAsync(role);

            var identityResult = result.ToResult(role);
            if (identityResult.IsFailure)
            {
                return Result<ApplicationRole>.BadRequest(identityResult.Error!.Message);
            }

            return Result<ApplicationRole>.Success(role, 201);
        }, nameof(CreateRoleAsync));
    }

    public async Task<Result> DeleteRoleAsync(
        string tenant, SetupRoleDeletionRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                return Result.Unauthorized("Invalid or expired setup token");
            }

            // ロール名を検証
            var roleNameValidation = ValidationHelper.ValidateRequired(request.RoleName, "Role name");
            if (roleNameValidation.IsFailure)
            {
                return Result.BadRequest(roleNameValidation.Error!.Message);
            }

            // ロール名で検索
            var role = await _roleManager.FindByNameAsync(request.RoleName.Trim());
            if (role == null)
            {
                return Result.NotFound($"Role '{request.RoleName}' not found");
            }

            // ロールを削除
            var deleteResult = await _roleManager.DeleteAsync(role);
            var identityResult = deleteResult.ToResult();
            if (identityResult.IsFailure)
            {
                return Result.BadRequest(identityResult.Error!.Message);
            }

            return Result.NoContent();
        }, nameof(DeleteRoleAsync));
    }

    public async Task<Result<Permission>> AddPermissionToRoleAsync(
        string tenant, SetupPermissionRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                return Result<Permission>.Unauthorized("Invalid or expired setup token");
            }

            // ロール名を検証
            var roleNameValidation = ValidationHelper.ValidateRequired(request.RoleName, "Role name");
            if (roleNameValidation.IsFailure)
            {
                return Result<Permission>.BadRequest(roleNameValidation.Error!.Message);
            }

            // ロール名で検索
            var role = await _roleManager.FindByNameAsync(request.RoleName.Trim());
            if (role == null)
            {
                return Result<Permission>.NotFound($"Role '{request.RoleName}' not found");
            }

            // Validate permission name is not empty
            var permissionNameValidation = ValidationHelper.ValidateRequired(request.PermissionName, "Permission name");
            if (permissionNameValidation.IsFailure)
            {
                return Result<Permission>.BadRequest(permissionNameValidation.Error!.Message);
            }

            // Check if permission already exists for this role
            if (role.Authorities?.Any(a => a.Name == request.PermissionName) ?? false)
            {
                return Result<Permission>.Conflict($"Permission '{request.PermissionName}' already exists for this role");
            }

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = request.PermissionName,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow
            };

            if (role.Authorities == null)
            {
                role.Authorities = new List<Permission>();
            }
            role.Authorities.Add(permission);
            var result = await _roleManager.UpdateAsync(role);

            var identityResult = result.ToResult(permission);
            if (identityResult.IsFailure)
            {
                return Result<Permission>.BadRequest(identityResult.Error!.Message);
            }

            return Result<Permission>.Success(permission, 201);
        }, nameof(AddPermissionToRoleAsync));
    }

    public async Task<Result> DeletePermissionFromRoleAsync(
        string tenant, SetupPermissionDeletionRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                return Result.Unauthorized("Invalid or expired setup token");
            }

            // ロール名を検証
            var roleNameValidation = ValidationHelper.ValidateRequired(request.RoleName, "Role name");
            if (roleNameValidation.IsFailure)
            {
                return Result.BadRequest(roleNameValidation.Error!.Message);
            }

            // ロール名で検索
            var role = await _roleManager.FindByNameAsync(request.RoleName.Trim());
            if (role == null)
            {
                return Result.NotFound($"Role '{request.RoleName}' not found");
            }

            // パーミッション名を検証
            var permissionNameValidation = ValidationHelper.ValidateRequired(request.PermissionName, "Permission name");
            if (permissionNameValidation.IsFailure)
            {
                return Result.BadRequest(permissionNameValidation.Error!.Message);
            }

            // ロールからパーミッションを削除
            var permission = role.Authorities?.FirstOrDefault(a => a.Name == request.PermissionName.Trim());
            if (permission == null)
            {
                return Result.NotFound($"Permission '{request.PermissionName}' not found for role '{request.RoleName}'");
            }

            role.Authorities!.Remove(permission);
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return Result.BadRequest("Failed to delete permission from role");
            }

            return Result.NoContent();
        }, nameof(DeletePermissionFromRoleAsync));
    }

    /// <summary>
    /// SetupToken検証を伴うデフォルトロール設定
    /// </summary>
    public async Task<Result<RoleSetupCompletionResponse>> SetupDefaultRoleAsync(
        string tenant, SetupDefaultRoleRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // SetupToken の検証
            if (!await _setupTokenValidator.ValidateSetupTokenAsync(tenant, request.SetupToken))
            {
                return Result<RoleSetupCompletionResponse>.Unauthorized("Invalid or expired setup token");
            }

            // Validate role name is not empty
            var nameValidation = ValidationHelper.ValidateRequired(request.DefaultRoleName, "Default role name");
            if (nameValidation.IsFailure)
            {
                return Result<RoleSetupCompletionResponse>.BadRequest(nameValidation.Error!.Message);
            }

            var cleanName = request.DefaultRoleName.Trim();

            // Check if default role already exists
            if (await _roleManager.RoleExistsAsync(cleanName))
            {
                return Result<RoleSetupCompletionResponse>.Conflict($"Default role '{cleanName}' already exists");
            }

            // Create default role
            var role = new ApplicationRole(cleanName);
            var createResult = await _roleManager.CreateAsync(role);

            var identityResult = createResult.ToResult<RoleSetupCompletionResponse>(null);
            if (identityResult.IsFailure)
            {
                return Result<RoleSetupCompletionResponse>.BadRequest(identityResult.Error!.Message);
            }

            // Add default permissions
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
                        // Log warning but continue - permissions may have been partially added
                    }
                }
            }

            var response = new RoleSetupCompletionResponse
            {
                Success = true,
                Message = $"Default role '{cleanName}' configured successfully",
                DefaultRoleName = cleanName,
                PermissionsAdded = permissionsAdded
            };

            return Result<RoleSetupCompletionResponse>.Success(response, 201);
        }, nameof(SetupDefaultRoleAsync));
    }
}
