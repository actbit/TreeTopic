using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
using TreeTopic.Repositories;

namespace TreeTopic.Services;

/// <summary>
/// RoomUserの管理と権限チェックを行うマネージャー
/// UserManagerと同様のパターンで実装
/// </summary>
public class RoomUserManager
{
    private readonly ApplicationDbContext _context;
    private readonly RoomRoleManager _roleManager;
    private readonly IRoomPermissionRepository _permissionRepository;
    private readonly ILogger<RoomUserManager> _logger;

    public RoomUserManager(
        ApplicationDbContext context,
        RoomRoleManager roleManager,
        IRoomPermissionRepository permissionRepository,
        ILogger<RoomUserManager> logger)
    {
        _context = context;
        _roleManager = roleManager;
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    /// <summary>
    /// RoomUserを取得（権限・ロール込み）
    /// </summary>
    public async Task<RoomUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RoomUsers
            .Include(ru => ru.RoomRole)
                .ThenInclude(rr => rr!.Permissions)
            .Include(ru => ru.RoomPermission)
            .FirstOrDefaultAsync(ru => ru.Id == id, cancellationToken);
    }

    /// <summary>
    /// 部屋とユーザーでRoomUserを取得
    /// </summary>
    public async Task<RoomUser?> FindByRoomAndUserAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RoomUsers
            .Include(ru => ru.RoomRole)
                .ThenInclude(rr => rr!.Permissions)
            .Include(ru => ru.RoomPermission)
            .FirstOrDefaultAsync(ru => ru.RoomId == roomId && ru.ApplicationUserId == userId, cancellationToken);
    }

    /// <summary>
    /// RoomUserを作成
    /// </summary>
    public async Task<RoomUser> CreateAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        if (roomUser.Id == Guid.Empty)
        {
            roomUser.Id = Guid.CreateVersion7();
        }

        _context.RoomUsers.Add(roomUser);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomUser created: RoomId={RoomId}, UserId={UserId}, RoleId={RoleId}",
            roomUser.RoomId, roomUser.ApplicationUserId, roomUser.RoomRoleId);
        return roomUser;
    }

    /// <summary>
    /// RoomUserを更新
    /// </summary>
    public async Task<RoomUser> UpdateAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        _context.RoomUsers.Update(roomUser);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomUser updated: {Id}", roomUser.Id);
        return roomUser;
    }

    /// <summary>
    /// RoomUserのロールを設定
    /// </summary>
    public async Task<RoomUser> SetRoleAsync(
        RoomUser roomUser,
        Guid? roleId,
        CancellationToken cancellationToken = default)
    {
        roomUser.RoomRoleId = roleId;
        return await UpdateAsync(roomUser, cancellationToken);
    }

    /// <summary>
    /// RoomUserを削除
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roomUser = await _context.RoomUsers.FindAsync(new object[] { id }, cancellationToken);
        if (roomUser == null)
        {
            return false;
        }

        _context.RoomUsers.Remove(roomUser);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomUser deleted: {Id}", id);
        return true;
    }

    /// <summary>
    /// ユーザーの権限を取得（ロール + 個別設定）
    /// </summary>
    public async Task<HashSet<string>> GetPermissionsAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        var permissions = new HashSet<string>();

        // 1. ロールから基本権限を取得
        if (roomUser.RoomRole != null)
        {
            foreach (var perm in roomUser.RoomRole.Permissions)
            {
                permissions.Add(perm.PermissionName);
            }
        }

        // 2. 個別設定で上書き（ある場合）
        // ※個別設定は「上書き」として扱うか「追加」として扱うか要検討
        // ここでは「追加」として扱う
        var roomUserWithPerms = await FindByIdAsync(roomUser.Id, cancellationToken);
        if (roomUserWithPerms != null)
        {
            foreach (var perm in roomUserWithPerms.RoomPermission)
            {
                permissions.Add(perm.Name);
            }
        }

        return permissions;
    }

    /// <summary>
    /// ユーザーが特定の権限を持っているか確認
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        RoomUser roomUser,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var permissions = await GetPermissionsAsync(roomUser, cancellationToken);
        return permissions.Contains(permissionName);
    }

    /// <summary>
    /// ユーザーが読み取り権限を持っているか
    /// </summary>
    public async Task<bool> CanReadAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "read", cancellationToken);
    }

    /// <summary>
    /// ユーザーが書き込み権限を持っているか
    /// </summary>
    public async Task<bool> CanWriteAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "write", cancellationToken);
    }

    /// <summary>
    /// ユーザーが削除権限を持っているか
    /// </summary>
    public async Task<bool> CanDeleteAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "delete", cancellationToken);
    }

    /// <summary>
    /// ユーザーが管理権限を持っているか
    /// </summary>
    public async Task<bool> CanManageAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "manage", cancellationToken);
    }

    // ========== Topic権限チェック ==========

    /// <summary>
    /// ユーザーがトピック読み取り権限を持っているか
    /// </summary>
    public async Task<bool> CanReadTopicAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "topic.read", cancellationToken);
    }

    /// <summary>
    /// ユーザーがトピック書き込み権限を持っているか
    /// </summary>
    public async Task<bool> CanWriteTopicAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "topic.write", cancellationToken);
    }

    /// <summary>
    /// ユーザーがトピック削除権限を持っているか
    /// </summary>
    public async Task<bool> CanDeleteTopicAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "topic.delete", cancellationToken);
    }

    /// <summary>
    /// ユーザーがトピック管理権限を持っているか
    /// </summary>
    public async Task<bool> CanManageTopicAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        return await HasPermissionAsync(roomUser, "topic.manage", cancellationToken);
    }

    /// <summary>
    /// 個別権限を追加（上書き用）
    /// </summary>
    public async Task<RoomPermission> AddPermissionAsync(
        RoomUser roomUser,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var permission = new RoomPermission
        {
            Id = Guid.CreateVersion7(),
            RoomUserId = roomUser.Id,
            Name = permissionName
        };

        _context.RoomPermissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission added to RoomUser: {Permission} -> {RoomUserId}", permissionName, roomUser.Id);
        return permission;
    }

    /// <summary>
    /// 個別権限を削除
    /// </summary>
    public async Task<bool> RemovePermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var permission = await _context.RoomPermissions
            .FindAsync(new object[] { permissionId }, cancellationToken);

        if (permission == null)
        {
            return false;
        }

        _context.RoomPermissions.Remove(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission removed from RoomUser: {PermissionId}", permissionId);
        return true;
    }

    /// <summary>
    /// 個別権限をすべてクリア
    /// </summary>
    public async Task ClearPermissionsAsync(
        RoomUser roomUser,
        CancellationToken cancellationToken = default)
    {
        var permissions = await _context.RoomPermissions
            .Where(p => p.RoomUserId == roomUser.Id)
            .ToListAsync(cancellationToken);

        _context.RoomPermissions.RemoveRange(permissions);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All permissions cleared from RoomUser: {RoomUserId}", roomUser.Id);
    }
}
