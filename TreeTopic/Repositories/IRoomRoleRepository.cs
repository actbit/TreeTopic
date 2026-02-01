using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IRoomRoleRepository
{
    Task<List<RoomRole>> ListAsync(CancellationToken cancellationToken = default);
    Task<RoomRole?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RoomRole?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
    Task<RoomRole> CreateAsync(RoomRole role, CancellationToken cancellationToken = default);
    Task<RoomRole> UpdateAsync(RoomRole role, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
