using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Services;

public class RoomPermissionsService : BaseService, IRoomPermissionsService
{
    private readonly RoomRoleManager _roomRoleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RoomPermissionsService> _logger;

    public RoomPermissionsService(
        RoomRoleManager roomRoleManager,
        ApplicationDbContext dbContext,
        ILogger<RoomPermissionsService> logger) : base(logger)
    {
        _roomRoleManager = roomRoleManager;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<List<string>>> GetRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result<List<string>>.NotFound($"RoomRole '{roleName}' not found");
            }

            var permissions = await _roomRoleManager.GetPermissionNamesAsync(role.Id, cancellationToken);

            return Result<List<string>>.Success(permissions);
        }, nameof(GetRolePermissionsAsync));
    }

    public async Task<Result<RoomRolePermissionDto>> AddPermissionToRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result<RoomRolePermissionDto>.NotFound($"RoomRole '{roleName}' not found");
            }

            var permission = await _roomRoleManager.AddPermissionAsync(
                role.Id,
                permissionName,
                cancellationToken);

            var dto = new RoomRolePermissionDto(
                permission.Id,
                permission.RoomRoleId,
                roleName,
                permission.PermissionName);

            return Result<RoomRolePermissionDto>.Success(dto);
        }, nameof(AddPermissionToRoleAsync));
    }

    public async Task<Result> RemovePermissionFromRoleAsync(
        string roleName,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result.NotFound($"RoomRole '{roleName}' not found");
            }

            var permissions = await _roomRoleManager.GetPermissionNamesAsync(role.Id, cancellationToken);
            var targetPermission = permissions.FirstOrDefault(p => p == permissionName);

            if (targetPermission == null)
            {
                return Result.Success();
            }

            var success = await PermissionHelper.RemoveWithTransactionAsync(
                _dbContext,
                _dbContext.RoomRolePermissions,
                async (ctx, ct) => await ctx.RoomRolePermissions
                    .FirstOrDefaultAsync(p => p.RoomRoleId == role.Id && p.PermissionName == permissionName, ct),
                _logger,
                $"Permission removed from RoomRole: {permissionName} -> {roleName}",
                $"Failed to remove permission from RoomRole: {permissionName} -> {roleName}",
                cancellationToken);

            return success ? Result.Success() : Result.NotFound("Permission not found");
        }, nameof(RemovePermissionFromRoleAsync));
    }

    public async Task<Result> ClearRolePermissionsAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var role = await _roomRoleManager.FindByNameAsync(roleName, cancellationToken);
            if (role == null)
            {
                return Result.NotFound($"RoomRole '{roleName}' not found");
            }

            var permissionsToDelete = await _dbContext.RoomRolePermissions
                .Where(p => p.RoomRoleId == role.Id)
                .ToListAsync(cancellationToken);

            if (permissionsToDelete.Count > 0)
            {
                _dbContext.RoomRolePermissions.RemoveRange(permissionsToDelete);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("All permissions cleared from RoomRole: {RoleName} ({Count} permissions removed)", roleName, permissionsToDelete.Count);
            return Result.Success();
        }, nameof(ClearRolePermissionsAsync));
    }
}