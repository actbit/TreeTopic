using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Services;

internal static class UnreadCountQueryHelper
{
    public static async Task<int> CountUnreadAsync(
        ApplicationDbContext dbContext,
        Guid topicId,
        Guid? lastReadMessageId,
        CancellationToken cancellationToken)
    {
        var topicMessages = dbContext.Messages.Where(m => m.TopicId == topicId);
        if (!lastReadMessageId.HasValue)
        {
            return await topicMessages.CountAsync(cancellationToken);
        }

        var readAnchor = await dbContext.Messages
            .Where(m => m.TopicId == topicId && m.Id == lastReadMessageId.Value)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (readAnchor == null)
        {
            return await topicMessages.CountAsync(cancellationToken);
        }

        return await topicMessages.CountAsync(
            m => m.CreatedAt > readAnchor.CreatedAt
                || (m.CreatedAt == readAnchor.CreatedAt && m.Id > readAnchor.Id),
            cancellationToken);
    }

    public static Task<Dictionary<Guid, int>> GetUnreadCountsByTopicAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<Guid> topicIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (topicIds.Count == 0 || userId == Guid.Empty)
        {
            return Task.FromResult(new Dictionary<Guid, int>());
        }

        return (
            from m in dbContext.Messages
            where topicIds.Contains(m.TopicId)
            join ut in dbContext.UserTopics.Where(ut => ut.UserId == userId && topicIds.Contains(ut.TopicId))
                on m.TopicId equals ut.TopicId into utJoin
            from ut in utJoin.DefaultIfEmpty()
            join readMessage in dbContext.Messages
                on ut.LastReadMessageId equals readMessage.Id into readJoin
            from readMessage in readJoin.DefaultIfEmpty()
            where ut == null
                || !ut.LastReadMessageId.HasValue
                || readMessage == null
                || m.CreatedAt > readMessage.CreatedAt
                || (m.CreatedAt == readMessage.CreatedAt && m.Id > readMessage.Id)
            group m by m.TopicId into g
            select new { TopicId = g.Key, UnreadCount = g.Count() }
        ).ToDictionaryAsync(x => x.TopicId, x => x.UnreadCount, cancellationToken);
    }
}
