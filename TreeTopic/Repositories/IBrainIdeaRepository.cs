using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IBrainIdeaRepository : IBaseRepository<BrainIdea>
{
    Task<List<BrainIdea>> GetByBrainBoardIdAsync(Guid brainBoardId, CancellationToken cancellationToken = default);
    Task<List<BrainIdea>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<List<BrainIdea>> GetByUserIdAsync(Guid roomUserId, CancellationToken cancellationToken = default);
    Task<BrainIdea?> GetWithBoardAsync(Guid id, CancellationToken cancellationToken = default);
}
