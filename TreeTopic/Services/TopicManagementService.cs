using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.SignalR;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Hubs;
using TreeTopic;

namespace TreeTopic.Services;

public interface ITopicManagementService
{
    Task<Result<List<TopicDto>>> GetAllTopicsAsync(Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetRootTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByParentAsync(Guid parentId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> UpdateTopicAsync(Guid topicId, UpdateTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteTopicAsync(Guid topicId, TopicDeleteStrategy strategy = TopicDeleteStrategy.Cascade, CancellationToken cancellationToken = default);
}

public class TopicManagementService : BaseService, ITopicManagementService
{
    private readonly ITopicRepository _topicRepository;
    private readonly IRoomRepository _roomRepository;
    private readonly IHubContext<RoomTopicHub, IRoomTopicHubClient> _roomTopicHub;
    private readonly IMaskedUUIDService _maskedUuidService;
    private readonly ApplicationDbContext _dbContext;

    public TopicManagementService(
        ITopicRepository topicRepository,
        IRoomRepository roomRepository,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        ApplicationDbContext dbContext,
        ILogger<TopicManagementService> logger) : base(logger)
    {
        _topicRepository = topicRepository;
        _roomRepository = roomRepository;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
        _dbContext = dbContext;
    }

    public async Task<Result<List<TopicDto>>> GetAllTopicsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(t => MapToDto(t, userId)).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetAllTopicsAsync));
    }

    public async Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId)
                .ToListAsync(cancellationToken);

            var dtos = await MapToDtosAsync(topics, userId, cancellationToken);
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicDto>>> GetRootTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[GetRootTopicsByRoom] START roomId={RoomId} userId={UserId}", roomId, userId);
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId && t.ParentId == null)
                .ToListAsync(cancellationToken);

            Logger.LogInformation("[GetRootTopicsByRoom] Found {Count} topics", topics.Count);

            // 暫定対応：未読数計算をスキップして速度を確認
            var dtos = topics.Select(t => MapToDto(t, userId)).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetRootTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicDto>>> GetTopicsByParentAsync(Guid parentId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.ParentId == parentId)
                .ToListAsync(cancellationToken);

            var dtos = await MapToDtosAsync(topics, userId, cancellationToken);
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetTopicsByParentAsync));
    }

    public async Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result<TopicDto>.NotFound("Topic not found");

            var dto = MapToDto(topic, userId);
            return Result<TopicDto>.Success(dto);
        }, nameof(GetTopicByIdAsync));
    }

    public async Task<Result<TopicDto>> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(request.RoomId, cancellationToken);
            if (room == null)
                return Result<TopicDto>.NotFound("Room not found");

            Guid? parentId = request.ParentId.HasValue ? (Guid)request.ParentId.Value : null;

            if (parentId.HasValue)
            {
                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent == null)
                    return Result<TopicDto>.NotFound("Parent topic not found");
            }

            Guid? sourceMessageId = request.SourceMessageId.HasValue ? (Guid)request.SourceMessageId.Value : null;

            var topic = new Topic
            {
                RoomId = request.RoomId,
                ParentId = parentId,
                SourceMessageId = sourceMessageId
            };
            topic.Title = request.Title?.Trim() ?? string.Empty;
            topic.Description = request.Description?.Trim();

            await _topicRepository.AddAsync(topic, cancellationToken);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(topic);
            await BroadcastTopicCreatedAsync(dto);

            // 親トピックのhasChildrenを更新してブロードキャスト
            if (parentId.HasValue)
            {
                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent != null)
                {
                    var parentDto = MapToDto(parent);
                    await BroadcastTopicUpdatedAsync(parentDto);
                }
            }

            return Result<TopicDto>.Success(dto, 201);
        }, nameof(CreateTopicAsync));
    }

    public async Task<Result<TopicDto>> UpdateTopicAsync(
        Guid topicId,
        UpdateTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result<TopicDto>.NotFound("Topic not found");

            var oldParentId = topic.ParentId;
            Guid? parentId = request.ParentId.HasValue ? (Guid)request.ParentId.Value : null;
            if (parentId.HasValue)
            {
                if (parentId.Value == topicId)
                    return Result<TopicDto>.BadRequest("A topic cannot be its own parent");

                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent == null)
                    return Result<TopicDto>.NotFound("Parent topic not found");

                if (parent.RoomId != topic.RoomId)
                    return Result<TopicDto>.BadRequest("Parent topic must be in the same room");

                // Prevent cycles: the new parent cannot be a descendant of the topic.
                var cursor = parent;
                var visited = new HashSet<Guid> { parent.Id };
                while (cursor.ParentId.HasValue)
                {
                    var nextId = cursor.ParentId.Value;
                    if (nextId == topicId)
                        return Result<TopicDto>.BadRequest("Cannot move a topic under its descendant");

                    if (!visited.Add(nextId))
                        break; // Defensive: existing cycle in DB

                    var next = await _topicRepository.GetByIdAsync(nextId, cancellationToken);
                    if (next == null)
                        break;

                    cursor = next;
                }

                topic.ParentId = parentId;
            }
            else
            {
                topic.ParentId = null;
            }

            if (request.Title != null)
            {
                topic.Title = request.Title.Trim();
            }

            if (request.Description != null)
            {
                topic.Description = request.Description.Trim();
            }

            topic.UpdatedAt = DateTime.UtcNow;
            _topicRepository.Update(topic);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(topic);
            await BroadcastTopicUpdatedAsync(dto);

            // 親が変更された場合、古い親と新しい親のhasChildrenを更新してブロードキャスト
            if (oldParentId != topic.ParentId)
            {
                // 古い親を更新
                if (oldParentId.HasValue)
                {
                    var oldParent = await _topicRepository.GetByIdAsync(oldParentId.Value, cancellationToken);
                    if (oldParent != null)
                    {
                        var oldParentDto = MapToDto(oldParent);
                        await BroadcastTopicUpdatedAsync(oldParentDto);
                    }
                }

                // 新しい親を更新
                if (topic.ParentId.HasValue)
                {
                    var newParent = await _topicRepository.GetByIdAsync(topic.ParentId.Value, cancellationToken);
                    if (newParent != null)
                    {
                        var newParentDto = MapToDto(newParent);
                        await BroadcastTopicUpdatedAsync(newParentDto);
                    }
                }
            }

            return Result<TopicDto>.Success(dto);
        }, nameof(UpdateTopicAsync));
    }

    public async Task<Result> DeleteTopicAsync(Guid topicId, TopicDeleteStrategy strategy = TopicDeleteStrategy.Cascade, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result.NotFound("Topic not found");

            var oldParentId = topic.ParentId;

            if (strategy == TopicDeleteStrategy.ReparentToParent)
            {
                var children = await _topicRepository.Query()
                    .Where(t => t.ParentId == topic.Id)
                    .ToListAsync(cancellationToken);

                foreach (var child in children)
                {
                    child.ParentId = topic.ParentId;
                    child.UpdatedAt = DateTime.UtcNow;
                    _topicRepository.Update(child);
                }

                await _topicRepository.SaveChangesAsync(cancellationToken);
            }

            _topicRepository.Delete(topic);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            await BroadcastTopicDeletedAsync(topic);

            // 親トピックのhasChildrenを更新してブロードキャスト
            if (oldParentId.HasValue)
            {
                var oldParent = await _topicRepository.GetByIdAsync(oldParentId.Value, cancellationToken);
                if (oldParent != null)
                {
                    var oldParentDto = MapToDto(oldParent);
                    await BroadcastTopicUpdatedAsync(oldParentDto);
                }
            }

            return Result.Success();
        }, nameof(DeleteTopicAsync));
    }

    private TopicRealtimeDto MapToRealtime(TopicDto dto)
    {
        var id = (Guid)dto.Id;
        var roomId = (Guid)dto.RoomId;
        var parentId = dto.ParentId.HasValue ? (Guid)dto.ParentId.Value : Guid.Empty;

            var sourceMessageEncoded = dto.SourceMessageId.HasValue && (Guid)dto.SourceMessageId.Value != Guid.Empty
                ? _maskedUuidService.EncodeSynchronous((Guid)dto.SourceMessageId.Value)
                : null;

            return new TopicRealtimeDto(
                id == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(id),
                roomId == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(roomId),
                dto.ParentId.HasValue && parentId != Guid.Empty ? _maskedUuidService.EncodeSynchronous(parentId) : null,
                dto.Title,
                dto.Description,
                dto.HasChildren,
                sourceMessageEncoded,
                dto.UnreadCount,
                dto.CreatedAt,
                dto.UpdatedAt);
    }

    private Task BroadcastTopicCreatedAsync(TopicDto dto)
    {
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous((Guid)dto.RoomId));
        var payload = MapToRealtime(dto);
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicCreated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).TopicCreated(payload);
    }

    private Task BroadcastTopicUpdatedAsync(TopicDto dto)
    {
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous((Guid)dto.RoomId));
        var payload = MapToRealtime(dto);
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicUpdated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).TopicUpdated(payload);
    }

    private Task BroadcastTopicDeletedAsync(Topic topic)
    {
        var roomId = topic.RoomId;
        var topicId = topic.Id;
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous(roomId));
        var payload = new TopicDeletedEvent(
            _maskedUuidService.EncodeSynchronous(topicId),
            _maskedUuidService.EncodeSynchronous(roomId),
            topic.ParentId.HasValue ? _maskedUuidService.EncodeSynchronous(topic.ParentId.Value) : null);
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicDeleted topic={TopicId} room={RoomId} group={Group}", topicId, roomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).TopicDeleted(payload);
    }

    private async Task<List<TopicDto>> MapToDtosAsync(List<Topic> topics, Guid? userId, CancellationToken cancellationToken)
    {
        if (topics.Count == 0)
            return new List<TopicDto>();

        var topicIds = topics.Select(t => t.Id).ToList();

        // hasChildrenを一括チェック
        var childTopicParentIds = await _topicRepository.Query()
            .Where(t => topicIds.Contains(t.ParentId.Value))
            .Select(t => t.ParentId.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var hasChildrenSet = childTopicParentIds.ToHashSet();

        // 未読数を計算（一括で取得）
        Dictionary<Guid, int> unreadCountsMap = new();
        Dictionary<Guid, MaskedGuid?> lastReadMessageIdsMap = new();

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            try
            {
                // UserTopicsを一括取得
                var userTopics = await _dbContext.UserTopics
                    .Where(ut => topicIds.Contains(ut.TopicId) && ut.UserId == userId.Value)
                    .ToListAsync(cancellationToken);

                var userTopicsMap = userTopics.ToDictionary(ut => ut.TopicId, ut => ut);

                // 各トピックの全メッセージ数を一括取得
                var messageCountsByTopic = await _dbContext.Messages
                    .Where(m => topicIds.Contains(m.TopicId))
                    .GroupBy(m => m.TopicId)
                    .Select(g => new { TopicId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TopicId, x => x.Count, cancellationToken);

                // LastReadMessageIdがあるトピックの未読数を計算
                var topicsWithLastRead = userTopicsMap
                    .Where(kvp => kvp.Value.LastReadMessageId.HasValue)
                    .Select(kvp => new { TopicId = kvp.Key, LastReadMessageId = kvp.Value.LastReadMessageId!.Value })
                    .ToList();

                if (topicsWithLastRead.Count > 0)
                {
                    // 一括で未読件数を取得
                    // トピックごとのLastReadMessageIdを条件に含めてカウント
                    var unreadCounts = await _dbContext.Messages
                        .Where(m => topicsWithLastRead.Select(x => x.TopicId).Contains(m.TopicId))
                        .GroupBy(m => m.TopicId)
                        .Select(g => new
                        {
                            TopicId = g.Key,
                            UnreadCount = g.Count(m => m.Id > topicsWithLastRead.First(x => x.TopicId == g.Key).LastReadMessageId)
                        })
                        .ToDictionaryAsync(x => x.TopicId, x => x.UnreadCount, cancellationToken);

                    foreach (var item in topicsWithLastRead)
                    {
                        lastReadMessageIdsMap[item.TopicId] = item.LastReadMessageId;
                        unreadCountsMap[item.TopicId] = unreadCounts.GetValueOrDefault(item.TopicId, 0);
                    }
                }

                // UserTopicがないトピックは全メッセージが未読
                foreach (var topicId in topicIds)
                {
                    if (!unreadCountsMap.ContainsKey(topicId))
                    {
                        unreadCountsMap[topicId] = messageCountsByTopic.GetValueOrDefault(topicId, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to calculate unread counts for {TopicCount} topics", topics.Count);
            }
        }

        // DTOを作成
        return topics.Select(topic => new TopicDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = hasChildrenSet.Contains(topic.Id),
            UnreadCount = unreadCountsMap.GetValueOrDefault(topic.Id),
            LastReadMessageId = lastReadMessageIdsMap.GetValueOrDefault(topic.Id),
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        }).ToList();
    }

    private TopicDto MapToDto(Topic topic, Guid? userId = null)
    {
        // Check if this topic has any children
        var hasChildren = _topicRepository.Query()
            .Any(t => t.ParentId == topic.Id);

        // TODO: 未読数計算はパフォーマンスに影響するため一時的に無効化
        int unreadCount = 0;
        MaskedGuid? lastReadMessageId = null;

        return new TopicDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = hasChildren,
            UnreadCount = unreadCount,
            LastReadMessageId = lastReadMessageId,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }
}
