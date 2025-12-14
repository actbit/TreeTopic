using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// パーミッション管理サービス
/// パーミッションのCRUD操作を統括
/// </summary>
public class PermissionManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly ILogger<PermissionManagementService> _logger;

    public PermissionManagementService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<PermissionManagementService> logger)
    {
        _context = context;
        _roleManager = roleManager;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

    /// <summary>
    /// パーミッション一覧を取得（テナント別）
    /// </summary>
    public async Task<(bool Success, List<PermissionDto>? Permissions, string? ErrorMessage)> ListPermissionsAsync()
    {
        try
        {
            var query = _context.Permissions.Include(p => p.Role).AsQueryable();
            var tenantId = CurrentTenantId;
            if (!string.IsNullOrEmpty(tenantId))
            {
                query = query.Where(p => p.TenantId == tenantId);
            }

            var permissions = await query.ToListAsync();
            var mapped = permissions.Select(PermissionToDto).ToList();
            return (true, mapped, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing permissions");
            return (false, null, "An error occurred while listing permissions");
        }
    }

    /// <summary>
    /// パーミッションをIDで取得
    /// </summary>
    public async Task<(bool Success, PermissionDto? Permission, string? ErrorMessage)> GetPermissionByIdAsync(Guid permissionId)
    {
        try
        {
            var permission = await _context.Permissions
                .Include(p => p.Role)
                .FirstOrDefaultAsync(p => p.Id == permissionId);

            if (permission == null)
            {
                _logger.LogWarning("Permission {PermissionId} not found", permissionId);
                return (false, null, $"Permission '{permissionId}' not found");
            }

            return (true, PermissionToDto(permission), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving permission {PermissionId}", permissionId);
            return (false, null, "An error occurred while retrieving the permission");
        }
    }

    /// <summary>
    /// パーミッションを作成
    /// </summary>
    public async Task<(bool Success, PermissionDto? Permission, string? ErrorMessage)> CreatePermissionAsync(
        PermissionModificationRequest request)
    {
        try
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found", request.RoleId);
                return (false, null, $"Role '{request.RoleId}' not found");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (false, null, "Name is required");
            }

            // 同じロール内で同じ名前のパーミッションが既に存在するかチェック
            var existingPermission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.RoleId == request.RoleId && p.Name == request.Name.Trim());

            if (existingPermission != null)
            {
                _logger.LogWarning("Permission {PermissionName} already exists for role {RoleId}",
                    request.Name, request.RoleId);
                return (false, null, $"Permission '{request.Name}' already exists for this role");
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
            _logger.LogInformation("Permission {PermissionName} created for role {RoleId}", request.Name, request.RoleId);
            return (true, PermissionToDto(permission), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission {PermissionName}", request.Name);
            return (false, null, "An error occurred while creating the permission");
        }
    }

    /// <summary>
    /// パーミッションを更新
    /// </summary>
    public async Task<(bool Success, PermissionDto? Permission, string? ErrorMessage)> UpdatePermissionAsync(
        Guid permissionId, PermissionModificationRequest request)
    {
        try
        {
            var permission = await _context.Permissions.FindAsync(permissionId);
            if (permission == null)
            {
                _logger.LogWarning("Permission {PermissionId} not found", permissionId);
                return (false, null, $"Permission '{permissionId}' not found");
            }

            var role = await _roleManager.FindByIdAsync(request.RoleId.ToString());
            if (role == null)
            {
                _logger.LogWarning("Role {RoleId} not found", request.RoleId);
                return (false, null, $"Role '{request.RoleId}' not found");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return (false, null, "Name is required");
            }

            // 同じロール内で同じ名前のパーミッションが既に存在するかチェック（同じパーミッション除外）
            var existingPermission = await _context.Permissions
                .FirstOrDefaultAsync(p => p.Id != permissionId &&
                                        p.RoleId == request.RoleId &&
                                        p.Name == request.Name.Trim());

            if (existingPermission != null)
            {
                _logger.LogWarning("Permission {PermissionName} already exists for role {RoleId}",
                    request.Name, request.RoleId);
                return (false, null, $"Permission '{request.Name}' already exists for this role");
            }

            permission.Name = request.Name.Trim();
            permission.RoleId = request.RoleId;
            await _context.SaveChangesAsync();
            await _context.Entry(permission).Reference(p => p.Role).LoadAsync();

            _logger.LogInformation("Permission {PermissionId} updated", permissionId);
            return (true, PermissionToDto(permission), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating permission {PermissionId}", permissionId);
            return (false, null, "An error occurred while updating the permission");
        }
    }

    /// <summary>
    /// パーミッションを削除
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> DeletePermissionAsync(Guid permissionId)
    {
        try
        {
            var permission = await _context.Permissions.FindAsync(permissionId);
            if (permission == null)
            {
                _logger.LogWarning("Permission {PermissionId} not found", permissionId);
                return (false, $"Permission '{permissionId}' not found");
            }

            _context.Permissions.Remove(permission);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Permission {PermissionId} deleted", permissionId);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting permission {PermissionId}", permissionId);
            return (false, "An error occurred while deleting the permission");
        }
    }

    /// <summary>
    /// パーミッションオブジェクトからDtoに変換
    /// </summary>
    private static PermissionDto PermissionToDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
    }
}
