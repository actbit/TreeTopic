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
            .Include(ru => ru.RoomUserRoomRoles)
                .ThenInclude(rur => rur.RoomRole)
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
            .Include(ru => ru.RoomUserRoomRoles)
                .ThenInclude(rur => rur.RoomRole)
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

        _logger.LogInformation("RoomUser created: RoomId={RoomId}, UserId={UserId}",
            roomUser.RoomId, roomUser.ApplicationUserId);
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
    /// RoomUserにロールを追加
    /// </summary>
    public async Task AddRoleAsync(
        RoomUser roomUser,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        // 既に同じロールが割り当てられているか確認
        var existing = await _context.RoomUserRoomRoles
            .AnyAsync(rur => rur.RoomUserId == roomUser.Id && rur.RoomRoleId == roleId, cancellationToken);

        if (!existing)
        {
            var mapping = new RoomUserRoomRole
            {
                Id = Guid.CreateVersion7(),
                RoomUserId = roomUser.Id,
                RoomRoleId = roleId
            };
            _context.RoomUserRoomRoles.Add(mapping);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Role added to RoomUser: RoomUserId={RoomUserId}, RoleId={RoleId}",
                roomUser.Id, roleId);
        }
    }

    /// <summary>
    /// RoomUserからロールを削除
    /// </summary>
    public async Task RemoveRoleAsync(
        RoomUser roomUser,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var mapping = await _context.RoomUserRoomRoles
            .FirstOrDefaultAsync(rur => rur.RoomUserId == roomUser.Id && rur.RoomRoleId == roleId, cancellationToken);

        if (mapping != null)
        {
            _context.RoomUserRoomRoles.Remove(mapping);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Role removed from RoomUser: RoomUserId={RoomUserId}, RoleId={RoleId}",
                roomUser.Id, roleId);
        }
    }

    /// <summary>
    /// RoomUserのすべてのロールを設定（置き換え）
    /// </summary>
    public async Task SetRolesAsync(
        RoomUser roomUser,
        List<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        // 既存のマッピングを削除
        var existingMappings = await _context.RoomUserRoomRoles
            .Where(rur => rur.RoomUserId == roomUser.Id)
            .ToListAsync(cancellationToken);
        _context.RoomUserRoomRoles.RemoveRange(existingMappings);

        // 新しいマッピングを追加
        foreach (var roleId in roleIds)
        {
            var mapping = new RoomUserRoomRole
            {
                Id = Guid.CreateVersion7(),
                RoomUserId = roomUser.Id,
                RoomRoleId = roleId
            };
            _context.RoomUserRoomRoles.Add(mapping);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Roles set for RoomUser: RoomUserId={RoomUserId}, RoleCount={RoleCount}",
            roomUser.Id, roleIds.Count);
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

        // 1. ロールから基本権限を取得（多対多関係）
        if (roomUser.RoomUserRoomRoles != null)
        {
            foreach (var userRole in roomUser.RoomUserRoomRoles)
            {
                if (userRole.RoomRole?.Permissions != null)
                {
                    foreach (var perm in userRole.RoomRole.Permissions)
                    {
                        permissions.Add(perm.PermissionName);
                    }
                }
            }
        }

        // ロール権限がロードされていない場合はクエリ実行
        if (permissions.Count == 0)
        {
            var rolePerms = await _context.RoomUserRoomRoles
                .Where(rur => rur.RoomUserId == roomUser.Id)
                .Join(_context.RoomRolePermissions,
                    rur => rur.RoomRoleId,
                    rp => rp.RoomRoleId,
                    (rur, rp) => rp.PermissionName)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var perm in rolePerms)
            {
                permissions.Add(perm);
            }
        }

        // 2. 個別設定で追加
        // N+1問題を回避するため、roomUser.RoomPermissionが既にロードされているかチェック
        if (roomUser.RoomPermission != null)
        {
            foreach (var perm in roomUser.RoomPermission)
            {
                permissions.Add(perm.Name);
            }
        }
        else
        {
            // 個別設定を直接クエリ
            var userPerms = await _context.RoomPermissions
                .Where(rp => rp.RoomUserId == roomUser.Id)
                .Select(rp => rp.Name)
                .ToListAsync(cancellationToken);
            foreach (var perm in userPerms)
            {
                permissions.Add(perm);
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

        return await PermissionHelper.AddWithTransactionAsync(
            _context,
            _context.RoomPermissions,
            permission,
            async (ctx, ct) => await ctx.RoomPermissions
                .FirstOrDefaultAsync(rp => rp.RoomUserId == roomUser.Id && rp.Name == permissionName, ct),
            _logger,
            $"Permission added to RoomUser: {permissionName} -> {roomUser.Id}",
            $"Failed to add permission to RoomUser: {permissionName} -> {roomUser.Id}",
            cancellationToken);
    }

    /// <summary>
    /// 個別権限を削除
    /// </summary>
    public async Task<bool> RemovePermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        return await PermissionHelper.RemoveWithTransactionAsync(
            _context,
            _context.RoomPermissions,
            async (ctx, ct) => await ctx.RoomPermissions
                .FindAsync(new object[] { permissionId }, ct),
            _logger,
            $"Permission removed from RoomUser: {permissionId}",
            $"Failed to remove permission from RoomUser: {permissionId}",
            cancellationToken);
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
