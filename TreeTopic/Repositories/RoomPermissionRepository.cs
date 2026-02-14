using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class RoomPermissionRepository : BaseRepository<RoomPermission>, IRoomPermissionRepository
{
    public RoomPermissionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<RoomPermission>> GetForRoomUserAsync(Guid roomUserId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(rp => rp.RoomUserId == roomUserId)
            .Include(rp => rp.RoomUser)
            .ToListAsync(cancellationToken);
    }

    public Task<RoomPermission?> GetWithUserAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(rp => rp.RoomUser)
            .ThenInclude(ru => ru.Room)
            .FirstOrDefaultAsync(rp => rp.Id == permissionId, cancellationToken);
    }
}
