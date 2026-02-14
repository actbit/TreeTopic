using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface ITopicRepository : IBaseRepository<Topic>
{
    Task<List<Topic>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<List<Topic>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default);
    Task<Topic?> GetWithRoomAsync(Guid topicId, CancellationToken cancellationToken = default);
}
