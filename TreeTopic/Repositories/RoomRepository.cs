using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class RoomRepository : BaseRepository<Room>, IRoomRepository
{
    public RoomRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<Room>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(r => r.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<Room?> GetWithCreatorAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(r => r.CreatedUser)
            .FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken);
    }
}
