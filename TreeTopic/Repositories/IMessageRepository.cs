using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TreeTopic.Models;

namespace TreeTopic.Repositories;

public interface IMessageRepository : IBaseRepository<Message>
{
    Task<List<Message>> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<List<Message>> GetRepliesAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<Message?> GetWithTopicAsync(Guid messageId, CancellationToken cancellationToken = default);
}
