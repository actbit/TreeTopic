using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IRoomUserRepository : IBaseRepository<RoomUser>
{
    Task<List<RoomUser>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<List<RoomUser>> GetByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken = default);
    Task<RoomUser?> GetWithPermissionAsync(Guid roomUserId, CancellationToken cancellationToken = default);
}
