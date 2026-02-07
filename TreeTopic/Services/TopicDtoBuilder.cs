using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using Microsoft.Extensions.Logging;

namespace TreeTopic.Services;

/// <summary>
/// TopicDtoビルダー
/// </summary>
public class TopicDtoBuilder
{
    private readonly List<Topic> _topics;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITopicRepository _topicRepository;
    private readonly ILogger<TopicDtoBuilder>? _logger;
    private HashSet<Guid>? _hasChildrenSet;
    private Dictionary<Guid, int>? _unreadCountsMap;

    public TopicDtoBuilder(
        List<Topic> topics,
        ApplicationDbContext dbContext,
        ITopicRepository topicRepository,
        ILogger<TopicDtoBuilder>? logger = null)
    {
        _topics = topics;
        _dbContext = dbContext;
        _topicRepository = topicRepository;
        _logger = logger;
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
        if (_topics.Count == 0 || !userId.HasValue || userId.Value == Guid.Empty)
        {
            return this;
        }

        var topicIds = _topics.Select(t => t.Id).ToList();
        _unreadCountsMap = topicIds.ToDictionary(topicId => topicId, _ => 0);

        try
        {
            var unreadCounts = await UnreadCountQueryHelper.GetUnreadCountsByTopicAsync(
                _dbContext,
                topicIds,
                userId.Value,
                cancellationToken);

            foreach (var item in unreadCounts)
            {
                _unreadCountsMap[item.Key] = item.Value;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to calculate unread counts in {Builder}", nameof(TopicDtoBuilder));
        }

        return this;
    }

    /// <summary>
    /// 基本DTOで構築
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
    /// ツリー用DTOで構築
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
}
