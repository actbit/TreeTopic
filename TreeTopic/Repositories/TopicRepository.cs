using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class TopicRepository : BaseRepository<Topic>, ITopicRepository
{
    public TopicRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<Topic>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(t => t.RoomId == roomId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Topic>> GetChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(t => t.ParentId == parentId)
            .ToListAsync(cancellationToken);
    }

    public Task<Topic?> GetWithRoomAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(t => t.Room)
            .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
    }
}
