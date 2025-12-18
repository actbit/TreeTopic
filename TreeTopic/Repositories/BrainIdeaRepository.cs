using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class BrainIdeaRepository : BaseRepository<BrainIdea>, IBrainIdeaRepository
{
    public BrainIdeaRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<BrainIdea>> GetByBrainBoardIdAsync(Guid brainBoardId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(bi => bi.BrainBoardId == brainBoardId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<BrainIdea>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(bi => bi.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<BrainIdea>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(bi => bi.ApplicationUserId == userId)
            .ToListAsync(cancellationToken);
    }

    public Task<BrainIdea?> GetWithBoardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(bi => bi.BrainBoard)
            .FirstOrDefaultAsync(bi => bi.Id == id, cancellationToken);
    }
}
