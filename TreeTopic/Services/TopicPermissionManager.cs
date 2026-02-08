using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Services;

/// <summary>
/// トピックレベルの権限管理を行うマネージャー
/// RoomUserManagerと同じパターンで権限名ベースの管理
/// </summary>
public class TopicPermissionManager
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TopicPermissionManager> _logger;

    public TopicPermissionManager(
        ApplicationDbContext context,
        ILogger<TopicPermissionManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region TopicRolePermission 管理

    /// <summary>
    /// トピックとロールの権限設定を取得
    /// </summary>
    public async Task<List<TopicRolePermission>> GetRolePermissionsAsync(
        Guid topicId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicRolePermissions
            .Where(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// トピックのすべてのロール権限を取得
    /// </summary>
    public async Task<List<TopicRolePermission>> GetTopicRolePermissionsAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicRolePermissions
            .Include(trp => trp.RoomRole)
            .Where(trp => trp.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// ロール権限を追加
    /// </summary>
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

    /// <summary>
    /// ロール権限を削除
    /// </summary>
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

    /// <summary>
    /// ロールのトピック権限をすべてクリア
    /// </summary>
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

    #endregion

    #region TopicUserPermission 管理

    /// <summary>
    /// トピックとユーザーの権限設定を取得
    /// </summary>
    public async Task<List<TopicUserPermission>> GetUserPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TopicUserPermissions
            .Where(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// トピックのすべてのユーザー権限を取得
    /// </summary>
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

    /// <summary>
    /// ユーザー権限を追加
    /// </summary>
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

    /// <summary>
    /// ユーザー権限を削除
    /// </summary>
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

    /// <summary>
    /// ユーザーのトピック権限をすべてクリア
    /// </summary>
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

    #endregion

    #region 権限コピー

    /// <summary>
    /// 親トピックの権限を子トピックにコピー
    /// </summary>
    public async Task CopyPermissionsAsync(
        Guid parentTopicId,
        Guid childTopicId,
        CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. TopicRolePermissionをコピー
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

            // 2. TopicUserPermissionをコピー
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
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Permissions copied from parent topic {ParentTopicId} to child topic {ChildTopicId}: {RoleCount} role permissions, {UserCount} user permissions",
                parentTopicId, childTopicId, parentRolePermissions.Count, parentUserPermissions.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to copy permissions from parent topic {ParentTopicId} to child topic {ChildTopicId}. Transaction rolled back.",
                parentTopicId, childTopicId);
            throw;
        }
    }

    /// <summary>
    /// トピック作成者に管理者権限を付与
    /// </summary>
    public async Task GrantCreatorPermissionsAsync(
        Guid topicId,
        Guid roomUserId,
        CancellationToken cancellationToken = default)
    {
        // Topic作成者に全権限を付与
        var permissions = new[]
        {
            TopicPermissions.Read,
            TopicPermissions.Write,
            TopicPermissions.Delete,
            TopicPermissions.Manage,
            TopicPermissions.ReadMessages,
            TopicPermissions.WriteMessages
        };

        foreach (var permissionName in permissions)
        {
            var permission = new TopicUserPermission
            {
                Id = Guid.CreateVersion7(),
                TopicId = topicId,
                RoomUserId = roomUserId,
                Name = permissionName
            };
            _context.TopicUserPermissions.Add(permission);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Creator permissions granted to RoomUser {RoomUserId} for topic {TopicId}",
            roomUserId, topicId);
    }

    #endregion

    #region 権限チェック

    /// <summary>
    /// ユーザーのトピック権限をすべて取得（ロール + 個別設定）
    /// </summary>
    public async Task<HashSet<string>> GetPermissionsAsync(
        RoomUser roomUser,
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var permissions = new HashSet<string>();

        // 1. ロールから権限を取得（多対多関係）
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

        // 2. 個別ユーザー権限を追加（ロール権限に追加）
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

    /// <summary>
    /// ユーザーが特定の権限を持っているか確認
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        RoomUser roomUser,
        Guid topicId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        // 1. ロール権限を確認（多対多関係）
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

        // 2. 個別ユーザー権限を確認
        var hasUserPermission = await _context.TopicUserPermissions
            .AnyAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUser.Id && tup.Name == permissionName, cancellationToken);

        return hasUserPermission;
    }

    #endregion
}
