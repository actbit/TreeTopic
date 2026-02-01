using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// RoomRoleとRoomRolePermissionの管理を行うマネージャー
/// RoleManager/UserManagerと同様のパターンで実装
/// </summary>
public class RoomRoleManager
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RoomRoleManager> _logger;

    public RoomRoleManager(
        ApplicationDbContext context,
        ILogger<RoomRoleManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// すべてのロールを取得
    /// </summary>
    public async Task<List<RoomRole>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// IDでロールを取得
    /// </summary>
    public async Task<RoomRole?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <summary>
    /// 名前でロールを取得
    /// </summary>
    public async Task<RoomRole?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    /// <summary>
    /// ロールが存在するか確認
    /// </summary>
    public async Task<bool> RoleExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .AnyAsync(r => r.Name == name, cancellationToken);
    }

    /// <summary>
    /// ロールを作成
    /// </summary>
    public async Task<RoomRole> CreateAsync(
        RoomRole role,
        CancellationToken cancellationToken = default)
    {
        if (role.Id == Guid.Empty)
        {
            role.Id = Guid.CreateVersion7();
        }

        _context.RoomRoles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomRole created: {Name} ({Id})", role.Name, role.Id);
        return role;
    }

    /// <summary>
    /// ロールを更新
    /// </summary>
    public async Task<RoomRole> UpdateAsync(
        RoomRole role,
        CancellationToken cancellationToken = default)
    {
        _context.RoomRoles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomRole updated: {Name} ({Id})", role.Name, role.Id);
        return role;
    }

    /// <summary>
    /// ロールを削除
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await FindByIdAsync(id, cancellationToken);
        if (role == null)
        {
            return false;
        }

        _context.RoomRoles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomRole deleted: {Name} ({Id})", role.Name, id);
        return true;
    }

    /// <summary>
    /// ロールに権限を追加
    /// </summary>
    public async Task<RoomRolePermission> AddPermissionAsync(
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var permission = new RoomRolePermission
        {
            Id = Guid.CreateVersion7(),
            RoomRoleId = roleId,
            PermissionName = permissionName
        };

        _context.RoomRolePermissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission added to RoomRole: {Permission} -> {RoleId}", permissionName, roleId);
        return permission;
    }

    /// <summary>
    /// ロールから権限を削除
    /// </summary>
    public async Task<bool> RemovePermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var permission = await _context.RoomRolePermissions
            .FindAsync(new object[] { permissionId }, cancellationToken);

        if (permission == null)
        {
            return false;
        }

        _context.RoomRolePermissions.Remove(permission);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Permission removed from RoomRole: {PermissionId}", permissionId);
        return true;
    }

    /// <summary>
    /// ロールの権限を取得
    /// </summary>
    public async Task<List<string>> GetPermissionNamesAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RoomRolePermissions
            .Where(p => p.RoomRoleId == roleId)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// ロールに権限が含まれているか確認
    /// </summary>
    public async Task<bool> HasPermissionAsync(
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await _context.RoomRolePermissions
            .AnyAsync(p => p.RoomRoleId == roleId && p.PermissionName == permissionName, cancellationToken);
    }
}
