using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IRoomRepository : IBaseRepository<Room>
{
    Task<List<Room>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<Room?> GetWithCreatorAsync(Guid roomId, CancellationToken cancellationToken = default);
}
