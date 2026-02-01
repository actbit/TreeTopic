using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

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
        // 重複チェック
        var existing = await _context.TopicRolePermissions
            .FirstOrDefaultAsync(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId && trp.Name == permissionName, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var permission = new TopicRolePermission
        {
            Id = Guid.CreateVersion7(),
            TopicId = topicId,
            RoomRoleId = roleId,
            Name = permissionName
        };

        _context.TopicRolePermissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TopicRolePermission added: TopicId={TopicId}, RoleId={RoleId}, Permission={Permission}",
            topicId, roleId, permissionName);
        return permission;
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
        var permission = await _context.TopicRolePermissions
            .FirstOrDefaultAsync(trp => trp.TopicId == topicId && trp.RoomRoleId == roleId && trp.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return false;
        }

        _context.TopicRolePermissions.Remove(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TopicRolePermission removed: TopicId={TopicId}, RoleId={RoleId}, Permission={Permission}",
            topicId, roleId, permissionName);
        return true;
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
        // 重複チェック
        var existing = await _context.TopicUserPermissions
            .FirstOrDefaultAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId && tup.Name == permissionName, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var permission = new TopicUserPermission
        {
            Id = Guid.CreateVersion7(),
            TopicId = topicId,
            RoomUserId = roomUserId,
            Name = permissionName
        };

        _context.TopicUserPermissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TopicUserPermission added: TopicId={TopicId}, RoomUserId={RoomUserId}, Permission={Permission}",
            topicId, roomUserId, permissionName);
        return permission;
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
        var permission = await _context.TopicUserPermissions
            .FirstOrDefaultAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUserId && tup.Name == permissionName, cancellationToken);

        if (permission == null)
        {
            return false;
        }

        _context.TopicUserPermissions.Remove(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("TopicUserPermission removed: TopicId={TopicId}, RoomUserId={RoomUserId}, Permission={Permission}",
            topicId, roomUserId, permissionName);
        return true;
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

        // 1. ロールから権限を取得
        if (roomUser.RoomRole != null)
        {
            var rolePermissions = await _context.TopicRolePermissions
                .Where(trp => trp.TopicId == topicId && trp.RoomRoleId == roomUser.RoomRole.Id)
                .Select(trp => trp.Name)
                .ToListAsync(cancellationToken);

            foreach (var perm in rolePermissions)
            {
                permissions.Add(perm);
            }
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
        // 1. ロール権限を確認
        if (roomUser.RoomRole != null)
        {
            var hasRolePermission = await _context.TopicRolePermissions
                .AnyAsync(trp => trp.TopicId == topicId && trp.RoomRoleId == roomUser.RoomRole.Id && trp.Name == permissionName, cancellationToken);

            if (hasRolePermission)
            {
                return true;
            }
        }

        // 2. 個別ユーザー権限を確認
        var hasUserPermission = await _context.TopicUserPermissions
            .AnyAsync(tup => tup.TopicId == topicId && tup.RoomUserId == roomUser.Id && tup.Name == permissionName, cancellationToken);

        return hasUserPermission;
    }

    #endregion
}
