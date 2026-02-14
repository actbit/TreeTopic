using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

public class RoleManagementService : BaseService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SetupTokenValidationService _setupTokenValidator;
    private readonly ApplicationDbContext _context;

    public RoleManagementService(
        RoleManager<ApplicationRole> roleManager,
        SetupTokenValidationService setupTokenValidator,
        ApplicationDbContext context,
        ILogger<RoleManagementService> logger) : base(logger)
    {
        _roleManager = roleManager;
        _setupTokenValidator = setupTokenValidator;
        _context = context;
    }

    public async Task<Result<IEnumerable<ApplicationRole>>> GetAllRolesAsync(string tenant)
    {
        return await ExecuteAsync(async () =>
        {
            var allRoles = await _roleManager.Roles
                .Include(r => r.Authorities)
                .ToListAsync();

            return Result<IEnumerable<ApplicationRole>>.Success(allRoles);
        }, nameof(GetAllRolesAsync));
    }

    public async Task<Result<ApplicationRole>> CreateRoleAsync(
        string tenant, SetupRoleCreationRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var cleanName = request.Name!.Trim();

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
            var role = await _roleManager.FindByNameAsync(request.RoleName!.Trim());
            if (role == null)
            {
                return Result.NotFound($"Role '{request.RoleName}' not found");
            }

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
            var normalizedName = _roleManager.NormalizeKey(request.RoleName!.Trim());

            var role = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName);

            if (role == null)
            {
                return Result<Permission>.NotFound($"Role '{request.RoleName}' not found");
            }

            var existingPermission = await _context.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.RoleId == role.Id && p.Name == request.PermissionName);

            if (existingPermission != null)
            {
                return Result<Permission>.Conflict($"Permission '{request.PermissionName}' already exists for this role");
            }

            var permission = new Permission
            {
                Id = Guid.CreateVersion7(),
                Name = request.PermissionName,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();

            return Result<Permission>.Success(permission, 201);
        }, nameof(AddPermissionToRoleAsync));
    }

    public async Task<Result> DeletePermissionFromRoleAsync(
        string tenant, SetupPermissionDeletionRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var normalizedName = _roleManager.NormalizeKey(request.RoleName!.Trim());

            var role = await _context.Roles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.NormalizedName == normalizedName);

            if (role == null)
            {
                return Result.NotFound($"Role '{request.RoleName}' not found");
            }

            var permission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.RoleId == role.Id && p.Name == request.PermissionName.Trim());

            if (permission == null)
            {
                return Result.NotFound($"Permission '{request.PermissionName}' not found for role '{request.RoleName}'");
            }

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();

            return Result.NoContent();
        }, nameof(DeletePermissionFromRoleAsync));
    }

    public async Task<Result<RoleSetupCompletionResponse>> SetupDefaultRoleAsync(
        string tenant, SetupDefaultRoleRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var cleanName = request.DefaultRoleName!.Trim();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (await _roleManager.RoleExistsAsync(cleanName))
                {
                    return Result<RoleSetupCompletionResponse>.Conflict($"Default role '{cleanName}' already exists");
                }

                var role = new ApplicationRole(cleanName);
                var createResult = await _roleManager.CreateAsync(role);

                var identityResult = createResult.ToResult<RoleSetupCompletionResponse>(null);
                if (identityResult.IsFailure)
                {
                    return Result<RoleSetupCompletionResponse>.BadRequest(identityResult.Error!.Message);
                }

                int permissionsAdded = 0;
                if (request.DefaultPermissions?.Count > 0)
                {
                    foreach (var permissionName in request.DefaultPermissions)
                    {
                        if (string.IsNullOrWhiteSpace(permissionName))
                            continue;

                        var permission = new Permission
                        {
                            Id = Guid.CreateVersion7(),
                            Name = permissionName.Trim(),
                            RoleId = role.Id,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Permissions.Add(permission);
                        permissionsAdded++;
                    }

                    if (permissionsAdded > 0)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                var response = new RoleSetupCompletionResponse
                {
                    Success = true,
                    Message = $"Default role '{cleanName}' configured successfully",
                    DefaultRoleName = cleanName,
                    PermissionsAdded = permissionsAdded
                };

                return Result<RoleSetupCompletionResponse>.Success(response, 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Logger.LogError(ex, "Failed to setup default role '{RoleName}'. Transaction rolled back.", cleanName);
                throw;
            }
        }, nameof(SetupDefaultRoleAsync));
    }
}
