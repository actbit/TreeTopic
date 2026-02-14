using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Services;

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

    public async Task<List<RoomRole>>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomRole?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<RoomRole?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<bool> RoleExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.RoomRoles
            .AnyAsync(r => r.Name == name, cancellationToken);
    }

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

    public async Task<RoomRole> UpdateAsync(
        RoomRole role,
        CancellationToken cancellationToken = default)
    {
        _context.RoomRoles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("RoomRole updated: {Name} ({Id})", role.Name, role.Id);
        return role;
    }

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

    public async Task<RoomRolePermission> AddPermissionAsync(
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        var role = await _context.RoomRoles.FindAsync(new object[] { roleId }, cancellationToken);
        if (role == null)
        {
            throw new InvalidOperationException($"RoomRole with ID '{roleId}' not found.");
        }

        var permission = new RoomRolePermission
        {
            Id = Guid.CreateVersion7(),
            RoomRoleId = roleId,
            PermissionName = permissionName
        };

        return await PermissionHelper.AddWithTransactionAsync(
            _context,
            _context.RoomRolePermissions,
            permission,
            async (ctx, ct) => await ctx.RoomRolePermissions
                .FirstOrDefaultAsync(p => p.RoomRoleId == roleId && p.PermissionName == permissionName, ct),
            _logger,
            $"Permission added to RoomRole: {permissionName} -> {roleId}",
            $"Failed to add permission to RoomRole: {permissionName} -> {roleId}",
            cancellationToken);
    }

    public async Task<bool> RemovePermissionAsync(
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        return await PermissionHelper.RemoveWithTransactionAsync(
            _context,
            _context.RoomRolePermissions,
            async (ctx, ct) => await ctx.RoomRolePermissions
                .FindAsync(new object[] { permissionId }, ct),
            _logger,
            $"Permission removed from RoomRole: {permissionId}",
            $"Failed to remove permission from RoomRole: {permissionId}",
            cancellationToken);
    }

    public async Task<List<string>>> GetPermissionNamesAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RoomRolePermissions
            .Where(p => p.RoomRoleId == roleId)
            .Select(p => p.PermissionName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasPermissionAsync(
        Guid roleId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        return await _context.RoomRolePermissions
            .AnyAsync(p => p.RoomRoleId == roleId && p.PermissionName == permissionName, cancellationToken);
    }
}
