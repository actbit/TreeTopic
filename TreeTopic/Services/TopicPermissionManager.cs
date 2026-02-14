using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Services;

public class TopicPermissionManager
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TopicPermissionManager> _logger;
    private readonly PermissionScanService _permissionScanService;

    public TopicPermissionManager(
        ApplicationDbContext context,
        ILogger<TopicPermissionManager> logger,
        PermissionScanService permissionScanService)
    {
        _context = context;
        _logger = logger;
        _permissionScanService = permissionScanService;
    }


    public async Task<List<TopicRolePermission>> GetRolePermissionsAsync(
        Guid topicId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicRolePermissions
            .Where(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TopicRolePermission>> GetTopicRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicRolePermissions
            .Include(trp => trp.RoomRole)
            .Where(trp => trp.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TopicRolePermission> AddRolePermissionAsync(
        Guid topicId,
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var permission = new TopicRolePermission
        {
            Id = Guid.CreateVersion7(),
            TopicId = topicId,
            RoomRoleId = roleId,
            Name = permissionName
        };

        return await PermissionHelper.AddWithTransactionAsync(
            _context,
            _context.TopicRolePermissions,
            permission,
            async (ctx, ct) => await ctx.TopicRolePermissions
                .FirstOrDefaultAsync(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId && trp.Name == permissionName, ct),
            _logger,
            $"TopicRolePermission added: TopicId={topicId}, RoleId={roleId}, Permission={permissionName}",
            $"Failed to add role permission: TopicId={topicId}, RoleId={roleId}, Permission={permissionName}",
            cancellationToken);
    }

    public async Task<bool> RemoveRolePermissionAsync(
        Guid topicId,
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await PermissionHelper.RemoveWithTransactionAsync(
            _context,
            _context.TopicRolePermissions,
            async (ctx, ct) => await ctx.TopicRolePermissions
                .FirstOrDefaultAsync(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId && trp.Name == permissionName, ct),
            _logger,
            $"TopicRolePermission removed: TopicId={topicId}, RoleId={roleId}, Permission={permissionName}",
            $"Failed to remove role permission: TopicId={topicId}, RoleId={roleId}, Permission={permissionName}",
            cancellationToken);
    }

    public async Task ClearRolePermissionsAsync(
        Guid topicId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _context.TopicRolePermissions
            .Where(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId)
            .ToListAsync(cancellationToken);

        _context.TopicRolePermissions.RemoveRange(permissions);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All TopicRolePermissions cleared: TopicId={TopicId}, RoleId={RoleId}", topicId, roleId);
    }


    public async Task<List<TopicUserPermission>> GetUserPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicUserPermissions
            .Where(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TopicUserPermission>> GetTopicUserPermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicUserPermissions
            .Include(tup => tup.RoomUser)
                .ThenInclude(ru => ru!.ApplicationUser)
            .Where(tup => tup.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TopicUserPermission> AddUserPermissionAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var permission = new TopicUserPermission
        {
            Id = Guid.CreateVersion7(),
            TopicId = topicId,
            RoomUserId = roomUserId,
            Name = permissionName
        };

        return await PermissionHelper.AddWithTransactionAsync(
            _context,
            _context.TopicUserPermissions,
            permission,
            async (ctx, ct) => await ctx.TopicUserPermissions
                .FirstOrDefaultAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId && tup.Name == permissionName, ct),
            _logger,
            $"TopicUserPermission added: TopicId={topicId}, RoomUserId={roomUserId}, Permission={permissionName}",
            $"Failed to add user permission: TopicId={topicId}, RoomUserId={roomUserId}, Permission={permissionName}",
            cancellationToken);
    }

    public async Task<bool> RemoveUserPermissionAsync(
        Guid topicId,
        Guid roomUserId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await PermissionHelper.RemoveWithTransactionAsync(
            _context,
            _context.TopicUserPermissions,
            async (ctx, ct) => await ctx.TopicUserPermissions
                .FirstOrDefaultAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId && tup.Name == permissionName, ct),
            _logger,
            $"TopicUserPermission removed: TopicId={topicId}, RoomUserId={roomUserId}, Permission={permissionName}",
            $"Failed to remove user permission: TopicId={topicId}, RoomUserId={roomUserId}, Permission={permissionName}",
            cancellationToken);
    }

    public async Task ClearUserPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _context.TopicUserPermissions
            .Where(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId)
            .ToListAsync(cancellationToken);

        _context.TopicUserPermissions.RemoveRange(permissions);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All TopicUserPermissions cleared: TopicId={TopicId}, RoomUserId={RoomUserId}", topicId, roomUserId);
    }


    public async Task CopyPermissionsAsync(
        Guid parentTopicId,
        Guid childTopicId,
        CancellationToken cancellationToken = default)
    {
        var hasExistingTransaction = _context.Database.CurrentTransaction != null;
        var transaction = hasExistingTransaction
            ? null
            : await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var parentRolePermissions = await _context.TopicRolePermissions
                .Where(trp => trp.TopicId == parentTopicId)
                .ToListAsync(cancellationToken);

            foreach (var parentPerm in parentRolePermissions)
            {
                var childPerm = new TopicRolePermission
                {
                    Id = Guid.CreateVersion7(),
                    TopicId = childTopicId,
                    RoomRoleId = parentPerm.RoomRoleId,
                    Name = parentPerm.Name
                };
                _context.TopicRolePermissions.Add(childPerm);
            }

            var parentUserPermissions = await _context.TopicUserPermissions
                .Where(tup => tup.TopicId == parentTopicId)
                .ToListAsync(cancellationToken);

            foreach (var parentPerm in parentUserPermissions)
            {
                var childPerm = new TopicUserPermission
                {
                    Id = Guid.CreateVersion7(),
                    TopicId = childTopicId,
                    RoomUserId = parentPerm.RoomUserId,
                    Name = parentPerm.Name
                };
                _context.TopicUserPermissions.Add(childPerm);
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (transaction != null) await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Permissions copied from parent topic {ParentTopicId} to child topic {ChildTopicId}: {RoleCount} role permissions, {UserCount} user permissions",
                parentTopicId, childTopicId, parentRolePermissions.Count, parentUserPermissions.Count);
        }
        catch (Exception ex)
        {
            if (transaction != null) await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to copy permissions from parent topic {ParentTopicId} to child topic {ChildTopicId}. Transaction rolled back.",
                parentTopicId, childTopicId);
            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    public async Task GrantCreatorPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        var existingPermissions = await _context.TopicUserPermissions
            .Where(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId)
            .Select(tup => tup.Name)
            .ToHashSetAsync(cancellationToken);

        var allTopicPermissions = _permissionScanService.GetTopicPermissions();

        var addedCount = 0;
        foreach (var permission in allTopicPermissions)
        {
            if (existingPermissions.Contains(permission.Name))
                continue;

            var userPermission = new TopicUserPermission
            {
                Id = Guid.CreateVersion7(),
                TopicId = topicId,
                RoomUserId = roomUserId,
                Name = permission.Name
            };
            _context.TopicUserPermissions.Add(userPermission);
            addedCount++;
        }

        if (addedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Creator permissions granted to RoomUser {RoomUserId} for topic {TopicId}: {AddedCount} new permissions added",
            roomUserId, topicId, addedCount);
    }


    public async Task<HashSet<string>> GetPermissionsAsync(
        RoomUser roomUser,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var permissions = new HashSet<string>();

        var rolePermissions = await _context.RoomUserRoomRoles
            .Where(rur => rur.RoomUserId == roomUser.Id)
            .Join(_context.TopicRolePermissions,
                rur => rur.RoomRoleId,
                trp => trp.RoomRoleId,
                (rur, trp) => new { trp.TopicId, trp.Name })
            .Where(x => x.TopicId == topicId)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var perm in rolePermissions)
        {
            permissions.Add(perm);
        }

        var userPermissions = await _context.TopicUserPermissions
            .Where(tup => tup.TopicId == topicId && tup.RoomUserId == roomUser.Id)
            .Select(tup => tup.Name)
            .ToListAsync(cancellationToken);

        foreach (var perm in userPermissions)
        {
            permissions.Add(perm);
        }

        return permissions;
    }

    public async Task<bool> HasPermissionAsync(
        RoomUser roomUser,
        Guid topicId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var hasRolePermission = await _context.RoomUserRoomRoles
            .Where(rur => rur.RoomUserId == roomUser.Id)
            .Join(_context.TopicRolePermissions,
                rur => rur.RoomRoleId,
                trp => trp.RoomRoleId,
                (rur, trp) => new { trp.TopicId, trp.Name })
            .AnyAsync(x => x.TopicId == topicId && x.Name == permissionName, cancellationToken);

        if (hasRolePermission)
        {
            return true;
        }

        var hasUserPermission = await _context.TopicUserPermissions
            .AnyAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUser.Id && tup.Name == permissionName, cancellationToken);

        return hasUserPermission;
    }
}
