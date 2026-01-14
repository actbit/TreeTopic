using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IBrainIdeaVoteRepository : IBaseRepository<BrainIdeaVote>
{
    Task<BrainIdeaVote?> GetVoteAsync(Guid ideaId, Guid? roomUserId, string voteType, CancellationToken cancellationToken = default);
    Task<List<BrainIdeaVote>> GetVotesByIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default);
    Task<List<BrainIdeaVote>> GetVotesByUserAsync(Guid roomUserId, CancellationToken cancellationToken = default);
}
