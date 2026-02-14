using TreeTopic.Models;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Repositories;

public class RoomUserRoleRepository : IRoomUserRoleRepository
{
    private readonly ApplicationDbContext _context;

    public RoomUserRoleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<RoomUserRoomRole>> GetByRoomUserAsync(Guid roomUserId, CancellationToken cancellationToken = default)
    {
        return await _context.RoomUserRoomRoles
            .AsNoTracking()
            .Include(rurr => rurr.RoomUser)
            .Include(rurr => rurr.RoomRole)
            .Where(rurr => rurr.RoomUserId == roomUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomUserRoomRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RoomUserRoomRoles
            .AsNoTracking()
            .Include(rurr => rurr.RoomUser)
            .Include(rurr => rurr.RoomRole)
            .FirstOrDefaultAsync(rurr => rurr.Id == id, cancellationToken);
    }

    public async Task<RoomUserRoomRole> AddAsync(RoomUserRoomRole mapping, CancellationToken cancellationToken = default)
    {
        _context.RoomUserRoomRoles.Add(mapping);
        await _context.SaveChangesAsync(cancellationToken);
        return mapping;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mapping = await _context.RoomUserRoomRoles
            .FirstOrDefaultAsync(rurr => rurr.Id == id, cancellationToken);

        if (mapping != null)
        {
            _context.RoomUserRoomRoles.Remove(mapping);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid roomUserId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return await _context.RoomUserRoomRoles
            .AnyAsync(rurr => rurr.RoomUserId == roomUserId && rurr.RoomRoleId == roleId, cancellationToken);
    }
}
