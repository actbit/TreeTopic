using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IRoomPermissionRepository : IBaseRepository<RoomPermission>
{
    Task<List<RoomPermission>> GetForRoomUserAsync(Guid roomUserId, CancellationToken cancellationToken = default);
    Task<RoomPermission?> GetWithUserAsync(Guid permissionId, CancellationToken cancellationToken = default);
}
