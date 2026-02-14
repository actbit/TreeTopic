using System;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IBrainBoardRepository : IBaseRepository<BrainBoard>
{
    Task<List<BrainBoard>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<BrainBoard?> GetWithIdeasAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BrainBoard?> GetWithTopicAsync(Guid id, CancellationToken cancellationToken = default);
}
