using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using Microsoft.Extensions.Logging;

namespace TreeTopic.Services;

/// <summary>
/// TopicDtoビルダー - メソッドチェーンで必要なデータを追加
/// </summary>
public class TopicDtoBuilder
{
    private readonly List<Topic> _topics;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITopicRepository _topicRepository;
    private HashSet<Guid>? _hasChildrenSet;
    private Dictionary<Guid, int>? _unreadCountsMap;

    public TopicDtoBuilder(List<Topic> topics, ApplicationDbContext dbContext, ITopicRepository topicRepository)
    {
        _topics = topics;
        _dbContext = dbContext;
        _topicRepository = topicRepository;
    }

    public async Task<TopicDtoBuilder> WithHasChildren(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0) return this;

        var topicIds = _topics.Select(t => t.Id).ToList();
        var childTopicParentIds = await _topicRepository.Query()
            .Where(t => t.ParentId.HasValue && topicIds.Contains(t.ParentId.Value))
            .Select(t => new { t.ParentId, t.Id })
            .ToListAsync(cancellationToken);

        _hasChildrenSet = childTopicParentIds.Select(x => x.ParentId.Value).ToHashSet();
        return this;
    }

    public async Task<TopicDtoBuilder> WithUnread(Guid? userId, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[TopicDtoBuilder.WithUnread] START - topics: {_topics.Count}, userId: {userId}");

        if (_topics.Count == 0 || !userId.HasValue || userId.Value == Guid.Empty)
        {
            Console.WriteLine($"[TopicDtoBuilder.WithUnread] EARLY RETURN - topics: {_topics.Count}, userId: {userId}");
            return this;
        }

        var topicIds = _topics.Select(t => t.Id).ToList();
        _unreadCountsMap = new Dictionary<Guid, int>();

        try
        {
            // UserTopicsを一括取得
            var userTopics = await _dbContext.UserTopics
                .Where(ut => topicIds.Contains(ut.TopicId) && ut.UserId == userId.Value)
                .ToListAsync(cancellationToken);

            Console.WriteLine($"[TopicDtoBuilder.WithUnread] UserTopics found: {userTopics.Count}");

            var userTopicsMap = userTopics.ToDictionary(ut => ut.TopicId, ut => ut);

            // 各トピックの全メッセージ数を一括取得
            var messageCountsByTopic = await _dbContext.Messages
                .Where(m => topicIds.Contains(m.TopicId))
                .GroupBy(m => m.TopicId)
                .Select(g => new { TopicId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TopicId, x => x.Count, cancellationToken);

            Console.WriteLine($"[TopicDtoBuilder.WithUnread] Message counts: {string.Join(", ", messageCountsByTopic.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // LastReadMessageIdがあるトピックの未読数を計算
            var lastReadMessageMap = userTopicsMap
                .Where(kvp => kvp.Value.LastReadMessageId.HasValue)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.LastReadMessageId!.Value);

            Console.WriteLine($"[TopicDtoBuilder.WithUnread] Topics with LastReadMessageId: {lastReadMessageMap.Count}");

            if (lastReadMessageMap.Count > 0)
            {
                // まずメッセージをメモリに取得してからフィルタリング（SQL変換エラーを回避）
                var messages = await _dbContext.Messages
                    .Where(m => lastReadMessageMap.Keys.Contains(m.TopicId))
                    .ToListAsync(cancellationToken);

                var unreadCounts = messages
                    .GroupBy(m => m.TopicId)
                    .Select(g => new
                    {
                        TopicId = g.Key,
                        UnreadCount = g.Count(m => m.Id > lastReadMessageMap[g.Key])
                    })
                    .ToDictionary(x => x.TopicId, x => x.UnreadCount);

                foreach (var item in lastReadMessageMap)
                {
                    _unreadCountsMap[item.Key] = unreadCounts.GetValueOrDefault(item.Key, 0);
                }
            }

            // UserTopicがないトピックは全メッセージが未読
            foreach (var topicId in topicIds)
            {
                if (!_unreadCountsMap.ContainsKey(topicId))
                {
                    _unreadCountsMap[topicId] = messageCountsByTopic.GetValueOrDefault(topicId, 0);
                }
            }

            Console.WriteLine($"[TopicDtoBuilder.WithUnread] Final unread counts: {string.Join(", ", _unreadCountsMap.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        }
        catch (Exception ex)
        {
            // エラー時は空のマップを返す
            Console.WriteLine($"[TopicDtoBuilder.WithUnread] ERROR occurred: {ex.Message}");
            Console.WriteLine($"[TopicDtoBuilder.WithUnread] ERROR StackTrace: {ex.StackTrace}");
        }

        return this;
    }

    /// <summary>
    /// 基本DTO（最小限）で構築
    /// </summary>
    public Task<List<TopicBasicDto>> BuildBasicAsync(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0)
            return Task.FromResult(new List<TopicBasicDto>());

        return Task.FromResult(_topics.Select(topic => new TopicBasicDto
        {
            Id = topic.Id,
            Title = topic.Title,
            ParentId = topic.ParentId
        }).ToList());
    }

    /// <summary>
    /// ツリー用DTO（hasChildrenと未読数付き）で構築
    /// </summary>
    public Task<List<TopicTreeDto>> BuildTreeAsync(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0)
            return Task.FromResult(new List<TopicTreeDto>());

        return Task.FromResult(_topics.Select(topic => new TopicTreeDto
        {
            Id = topic.Id,
            Title = topic.Title,
            ParentId = topic.ParentId,
            RoomId = topic.RoomId,
            HasChildren = _hasChildrenSet?.Contains(topic.Id) ?? false,
            UnreadCount = _unreadCountsMap?.GetValueOrDefault(topic.Id) ?? 0
        }).ToList());
    }

    /// <summary>
    /// 詳細DTOで構築
    /// </summary>
    public Task<List<TopicDetailDto>> BuildDetailAsync(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0)
            return Task.FromResult(new List<TopicDetailDto>());

        return Task.FromResult(_topics.Select(topic => new TopicDetailDto
        {
            Id = topic.Id,
            Title = topic.Title,
            ParentId = topic.ParentId,
            RoomId = topic.RoomId,
            SourceMessageId = topic.SourceMessageId,
            Description = topic.Description,
            HasChildren = _hasChildrenSet?.Contains(topic.Id) ?? false,
            UnreadCount = _unreadCountsMap?.GetValueOrDefault(topic.Id) ?? 0,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        }).ToList());
    }

    /// <summary>
    /// 互換性のために残す古いビルドメソッド（新規コードでは使用禁止）
    /// TODO: 移行完了後に削除
    /// </summary>
    [Obsolete("Use BuildBasicAsync, BuildTreeAsync, or BuildDetailAsync instead")]
    public Task<List<TopicDto>> BuildAsync(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0)
            return Task.FromResult(new List<TopicDto>());

        return Task.FromResult(_topics.Select(topic => new TopicDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = _hasChildrenSet?.Contains(topic.Id) ?? false,
            ChildIds = new List<MaskedGuid>(),
            UnreadCount = _unreadCountsMap?.GetValueOrDefault(topic.Id) ?? 0,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        }).ToList());
    }
}
