using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class RoomUserRepository : BaseRepository<RoomUser>, IRoomUserRepository
{
    public RoomUserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<RoomUser>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(ru => ru.RoomId == roomId)
            .Include(ru => ru.RoomPermission)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RoomUser>> GetByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(ru => ru.ApplicationUserId == applicationUserId)
            .Include(ru => ru.Room)
            .ToListAsync(cancellationToken);
    }

    public Task<RoomUser?> GetWithPermissionAsync(Guid roomUserId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(ru => ru.RoomPermission)
            .FirstOrDefaultAsync(ru => ru.Id == roomUserId, cancellationToken);
    }
}
