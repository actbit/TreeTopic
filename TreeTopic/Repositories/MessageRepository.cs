using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public class MessageRepository : BaseRepository<Message>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext context) : base(context)
    {
    }

    public Task<List<Message>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(m => m.TopicId == topicId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Message>> GetRepliesAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Where(m => m.ReplyId == messageId)
            .ToListAsync(cancellationToken);
    }

    public Task<Message?> GetWithTopicAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return Query()
            .Include(m => m.Topic)
            .Include(m => m.Replies)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
    }
}
