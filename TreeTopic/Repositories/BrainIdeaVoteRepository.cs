using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class BrainIdeaVoteRepository : BaseRepository<BrainIdeaVote>, IBrainIdeaVoteRepository
{
    public BrainIdeaVoteRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<BrainIdeaVote?> GetVoteAsync(Guid ideaId, Guid? roomUserId, string voteType, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.BrainIdeaId == ideaId && v.RoomUserId == roomUserId && v.VoteType == voteType)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<BrainIdeaVote>> GetVotesByIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.BrainIdeaId == ideaId)
            .Include(v => v.RoomUser)
            .ThenInclude(ru => ru.ApplicationUser)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BrainIdeaVote>> GetVotesByUserAsync(Guid roomUserId, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(v => v.RoomUserId == roomUserId)
            .Include(v => v.BrainIdea)
            .ToListAsync(cancellationToken);
    }
}
