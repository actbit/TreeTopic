using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class BrainIdeaVoteRepository : BaseRepository<BrainIdeaVote>, IBrainIdeaVoteRepository
{
    public BrainIdeaVoteRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<BrainIdeaVote?> GetVoteAsync(Guid ideaId, Guid? userId, string voteType, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.BrainIdeaId == ideaId && v.ApplicationUserId == userId && v.VoteType == voteType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<BrainIdeaVote>> GetVotesByIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.BrainIdeaId == ideaId)
            .Include(v => v.ApplicationUser)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BrainIdeaVote>> GetVotesByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.ApplicationUserId == userId)
            .Include(v => v.BrainIdea)
            .ToListAsync(cancellationToken);
    }
}
