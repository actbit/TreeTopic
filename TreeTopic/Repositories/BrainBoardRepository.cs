using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class BrainBoardRepository : BaseRepository<BrainBoard>, IBrainBoardRepository
{
    public BrainBoardRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<BrainBoard>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(bb => bb.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    public Task<BrainBoard?> GetWithIdeasAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(bb => bb.BrainIdeas)
            .FirstOrDefaultAsync(bb => bb.Id == id, cancellationToken);
    }

    public Task<BrainBoard?> GetWithTopicAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(bb => bb.Topic)
            .FirstOrDefaultAsync(bb => bb.Id == id, cancellationToken);
    }
}
