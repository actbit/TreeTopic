using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Common.Helpers;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

public class PermissionManagementService : BaseService
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public PermissionManagementService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<PermissionManagementService> logger) : base(logger)
    {
        _context = context;
        _roleManager = roleManager;
        _tenantAccessor = tenantAccessor;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

    public async Task<Result<List<Permission>>> ListPermissionsAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var query = _context.Permissions.Include(p => p.Role).AsQueryable();
            var tenantId = CurrentTenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                query = query.Where(p => p.TenantId == tenantId);
            }

            var permissions = await query.ToListAsync();
            return Result<List<Permission>>.Success(permissions);
        }, nameof(ListPermissionsAsync));
    }

    public async Task<Result<Permission>> GetPermissionByIdAsync(Guid permissionId)
    {
        return await ExecuteAsync(async () =>
        {
            if (permissionId == Guid.Empty)
            {
                return Result<Permission>.BadRequest("Permission ID cannot be empty");
            }

            var permission = await _context.Permissions
                .Include(p => p.Role)
                .FirstOrDefaultAsync(p => p.Id == permissionId);

            if (permission == null)
            {
                return Result<Permission>.NotFound($"Permission '{permissionId}' not found");
            }

            return Result<Permission>.Success(permission);
        }, nameof(GetPermissionByIdAsync));
    }

    public async Task<Result<Permission>> CreatePermissionAsync(PermissionModificationRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            // Validate role exists
            var roleResult = await EntityHelper.FindRoleByIdOrNotFoundAsync(_roleManager, request.RoleId);
            if (roleResult.IsFailure)
            {
                return Result<Permission>.NotFound(roleResult.Error!.Message);
            }

            // Validate name is not empty
            var nameValidation = ValidationHelper.ValidateRequired(request.Name, "Name");
            if (nameValidation.IsFailure)
            {
                return Result<Permission>.BadRequest(nameValidation.Error!.Message);
            }

            // Check if permission already exists with same name in the same role
            var existingPermission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.RoleId == request.RoleId && p.Name == request.Name.Trim());

            if (existingPermission != null)
            {
                return Result<Permission>.Conflict($"Permission '{request.Name}' already exists for this role");
            }

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                RoleId = request.RoleId,
                TenantId = CurrentTenantId ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();

            await _context.Entry(permission).Reference(p => p.Role).LoadAsync();
            return Result<Permission>.Success(permission, 201);
        }, nameof(CreatePermissionAsync));
    }

    public async Task<Result<Permission>> UpdatePermissionAsync(
        Guid permissionId, PermissionModificationRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            if (permissionId == Guid.Empty)
            {
                return Result<Permission>.BadRequest("Permission ID cannot be empty");
            }

            var permission = await _context.Permissions.FindAsync(permissionId);
            if (permission == null)
            {
                return Result<Permission>.NotFound($"Permission '{permissionId}' not found");
            }

            // Validate role exists
            var roleResult = await EntityHelper.FindRoleByIdOrNotFoundAsync(_roleManager, request.RoleId);
            if (roleResult.IsFailure)
            {
                return Result<Permission>.NotFound(roleResult.Error!.Message);
            }

            // Validate name is not empty
            var nameValidation = ValidationHelper.ValidateRequired(request.Name, "Name");
            if (nameValidation.IsFailure)
            {
                return Result<Permission>.BadRequest(nameValidation.Error!.Message);
            }

            // Check if permission already exists with same name in the same role (excluding current permission)
            var existingPermission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id != permissionId &&
                                        p.RoleId == request.RoleId &&
                                        p.Name == request.Name.Trim());

            if (existingPermission != null)
            {
                return Result<Permission>.Conflict($"Permission '{request.Name}' already exists for this role");
            }

            permission.Name = request.Name.Trim();
            permission.RoleId = request.RoleId;
            await _context.SaveChangesAsync();
            await _context.Entry(permission).Reference(p => p.Role).LoadAsync();

            return Result<Permission>.Success(permission);
        }, nameof(UpdatePermissionAsync));
    }

    public async Task<Result> DeletePermissionAsync(Guid permissionId)
    {
        return await ExecuteAsync(async () =>
        {
            if (permissionId == Guid.Empty)
            {
                return Result.BadRequest("Permission ID cannot be empty");
            }

            var permission = await _context.Permissions.FindAsync(permissionId);
            if (permission == null)
            {
                return Result.NotFound($"Permission '{permissionId}' not found");
            }

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();

            return Result.NoContent();
        }, nameof(DeletePermissionAsync));
    }
}
