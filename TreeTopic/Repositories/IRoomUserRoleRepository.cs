using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IRoomUserRoleRepository
{
    Task<List<RoomUserRoomRole>> GetByRoomUserAsync(Guid roomUserId, CancellationToken cancellationToken = default);
    Task<RoomUserRoomRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoomUserRoomRole> AddAsync(RoomUserRoomRole mapping, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid roomUserId, Guid roleId, CancellationToken cancellationToken = default);
}
