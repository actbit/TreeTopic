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
    // 基本的なトピック情報（最小限）
    Task<Result<List<TopicBasicDto>>> GetAllTopicsAsync(Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicBasicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default);

    // ツリー表示用（hasChildrenと未読数付き）
    Task<Result<List<TopicTreeDto>>> GetRootTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicTreeDto>>> GetTopicsByParentAsync(Guid parentId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<List<TopicTreeDto>>> GetRootTopicsWithUnreadAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<List<TopicTreeDto>>> GetAllTopicsWithUnreadAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);

    // 詳細情報
    Task<Result<TopicDetailDto>> GetTopicByIdAsync(Guid topicId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<Result<TopicDetailDto>> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<TopicDetailDto>> UpdateTopicAsync(Guid topicId, UpdateTopicRequest request, CancellationToken cancellationToken = default);

    // 削除（戻り値なし）
    Task<Result> DeleteTopicAsync(Guid topicId, TopicDeleteStrategy strategy = TopicDeleteStrategy.Cascade, CancellationToken cancellationToken = default);

    // N+1問題を解決するための統計情報メソッド
    Task<Result<List<TopicWithStatsDto>>> GetTopicsWithStatsAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<TopicWithStatsDto>> GetTopicWithStatsByIdAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default);
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

    public async Task<Result<List<TopicBasicDto>>> GetAllTopicsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .ToListAsync(cancellationToken);

            var dtos = await MapToDtosBuilder(topics).BuildBasicAsync(cancellationToken);
            return Result<List<TopicBasicDto>>.Success(dtos);
        }, nameof(GetAllTopicsAsync));
    }

    public async Task<Result<List<TopicBasicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId)
                .ToListAsync(cancellationToken);

            var dtos = await MapToDtosBuilder(topics).BuildBasicAsync(cancellationToken);
            return Result<List<TopicBasicDto>>.Success(dtos);
        }, nameof(GetTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicTreeDto>>> GetRootTopicsByRoomAsync(Guid roomId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[GetRootTopicsByRoom] START roomId={RoomId} userId={UserId}", roomId, userId);
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId && t.ParentId == null)
                .ToListAsync(cancellationToken);

            Logger.LogInformation("[GetRootTopicsByRoom] Found {Count} topics", topics.Count);

            var builder = await MapToDtosBuilder(topics)
                .WithHasChildren(cancellationToken);

            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                builder = await builder.WithUnread(userId, cancellationToken);
            }

            var dtos = await builder.BuildTreeAsync(cancellationToken);
            return Result<List<TopicTreeDto>>.Success(dtos);
        }, nameof(GetRootTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicTreeDto>>> GetTopicsByParentAsync(Guid parentId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.ParentId == parentId)
                .ToListAsync(cancellationToken);

            var builder = await MapToDtosBuilder(topics)
                .WithHasChildren(cancellationToken);

            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                builder = await builder.WithUnread(userId, cancellationToken);
            }

            var dtos = await builder.BuildTreeAsync(cancellationToken);
            return Result<List<TopicTreeDto>>.Success(dtos);
        }, nameof(GetTopicsByParentAsync));
    }

    public async Task<Result<TopicDetailDto>> GetTopicByIdAsync(Guid topicId, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result<TopicDetailDto>.NotFound("Topic not found");

            var dto = await MapToTopicDetailDtoAsync(topic, userId, cancellationToken);
            return Result<TopicDetailDto>.Success(dto);
        }, nameof(GetTopicByIdAsync));
    }

    public async Task<Result<TopicDetailDto>> CreateTopicAsync(
        CreateTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(request.RoomId, cancellationToken);
            if (room == null)
                return Result<TopicDetailDto>.NotFound("Room not found");

            Guid? parentId = request.ParentId.HasValue ? (Guid)request.ParentId.Value : null;

            if (parentId.HasValue)
            {
                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent == null)
                    return Result<TopicDetailDto>.NotFound("Parent topic not found");
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

            var dto = await MapToTopicDetailDtoAsync(topic, null, cancellationToken);
            await BroadcastTopicCreatedAsync(dto);

            // 親トピックのhasChildrenを更新してブロードキャスト
            if (parentId.HasValue)
            {
                await BroadcastTopicHasChildrenUpdatedAsync(parentId.Value, true);
            }

            return Result<TopicDetailDto>.Success(dto, 201);
        }, nameof(CreateTopicAsync));
    }

    public async Task<Result<TopicDetailDto>> UpdateTopicAsync(
        Guid topicId,
        UpdateTopicRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result<TopicDetailDto>.NotFound("Topic not found");

            var oldParentId = topic.ParentId;
            Guid? parentId = request.ParentId.HasValue ? (Guid)request.ParentId.Value : null;
            if (parentId.HasValue)
            {
                if (parentId.Value == topicId)
                    return Result<TopicDetailDto>.BadRequest("A topic cannot be its own parent");

                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent == null)
                    return Result<TopicDetailDto>.NotFound("Parent topic not found");

                if (parent.RoomId != topic.RoomId)
                    return Result<TopicDetailDto>.BadRequest("Parent topic must be in the same room");

                // Prevent cycles: the new parent cannot be a descendant of the topic.
                var cursor = parent;
                var visited = new HashSet<Guid> { parent.Id };
                while (cursor.ParentId.HasValue)
                {
                    var nextId = cursor.ParentId.Value;
                    if (nextId == topicId)
                        return Result<TopicDetailDto>.BadRequest("Cannot move a topic under its descendant");

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

            var dto = await MapToTopicDetailDtoAsync(topic, null, cancellationToken);
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
                        var oldParentDto = await MapToTopicDetailDtoAsync(oldParent, null, cancellationToken);
                        await BroadcastTopicUpdatedAsync(oldParentDto);
                    }
                }

                // 新しい親を更新
                if (topic.ParentId.HasValue)
                {
                    var newParent = await _topicRepository.GetByIdAsync(topic.ParentId.Value, cancellationToken);
                    if (newParent != null)
                    {
                        var newParentDto = await MapToTopicDetailDtoAsync(newParent, null, cancellationToken);
                        await BroadcastTopicUpdatedAsync(newParentDto);
                    }
                }
            }

            return Result<TopicDetailDto>.Success(dto);
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
                    var oldParentDto = await MapToTopicDetailDtoAsync(oldParent, null, cancellationToken);
                    await BroadcastTopicUpdatedAsync(oldParentDto);
                }
            }

            return Result.Success();
        }, nameof(DeleteTopicAsync));
    }

    private TopicRealtimeDto MapToRealtime(TopicDetailDto dto)
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

    private Task BroadcastTopicCreatedAsync(TopicDetailDto dto)
    {
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous((Guid)dto.RoomId));
        var payload = MapToRealtime(dto);
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicCreated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        return _roomTopicHub.Clients.Group(groupName).TopicCreated(payload);
    }

    private Task BroadcastTopicUpdatedAsync(TopicDetailDto dto)
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

    /// <summary>
    /// 親トピックのhasChildrenのみを更新してブロードキャスト（子トピック作成時など）
    /// </summary>
    private async Task BroadcastTopicHasChildrenUpdatedAsync(Guid topicId, bool hasChildren)
    {
        var topic = await _topicRepository.GetByIdAsync(topicId);
        if (topic == null) return;

        var dto = await MapToTopicDetailDtoAsync(topic, null, CancellationToken.None);
        await BroadcastTopicUpdatedAsync(dto);
    }

    /// <summary>
    /// 複数トピックをDTOに変換するビルダーを作成
    /// </summary>
    private TopicDtoBuilder MapToDtosBuilder(List<Topic> topics)
    {
        return new TopicDtoBuilder(topics, _dbContext, _topicRepository);
    }

    /// <summary>
    /// 単一トピックをDTOに変換（基本版: hasChildrenも未読も含まない）
    /// </summary>
    private TopicDto MapToDtoBasic(Topic topic)
    {
        return new TopicDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = false,
            UnreadCount = 0,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }

    /// <summary>
    /// 単一トピックを詳細DTOに変換（hasChildrenチェックと未読数計算を含む）
    /// </summary>
    private async Task<TopicDetailDto> MapToTopicDetailDtoAsync(Topic topic, Guid? userId, CancellationToken cancellationToken)
    {
        // hasChildrenチェック
        var hasChildren = await _topicRepository.Query()
            .AnyAsync(t => t.ParentId == topic.Id, cancellationToken);

        // 未読数計算
        int unreadCount = 0;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            try
            {
                var userTopic = await _dbContext.UserTopics
                    .FirstOrDefaultAsync(ut => ut.TopicId == topic.Id && ut.UserId == userId.Value, cancellationToken);

                if (userTopic != null)
                {
                    if (userTopic.LastReadMessageId.HasValue)
                    {
                        unreadCount = await _dbContext.Messages
                            .CountAsync(m => m.TopicId == topic.Id && m.Id > userTopic.LastReadMessageId.Value, cancellationToken);
                    }
                }
                else
                {
                    // UserTopicがない場合は全メッセージが未読
                    unreadCount = await _dbContext.Messages
                        .CountAsync(m => m.TopicId == topic.Id, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to calculate unread count for topic {TopicId}", topic.Id);
            }
        }

        return new TopicDetailDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId,
            SourceMessageId = topic.SourceMessageId,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = hasChildren,
            UnreadCount = unreadCount,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }

    /// <summary>
    /// 複数トピックの統計情報を一括取得（N+1問題を解決）
    /// </summary>
    public async Task<Result<List<TopicWithStatsDto>>> GetTopicsWithStatsAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId)
                .ToListAsync(cancellationToken);

            if (topics.Count == 0)
                return Result<List<TopicWithStatsDto>>.Success(new List<TopicWithStatsDto>());

            var topicIds = topics.Select(t => t.Id).ToList();

            // 1. hasChildrenを一括チェック
            var childTopicParentIds = await _topicRepository.Query()
                .Where(t => topicIds.Contains(t.ParentId.Value))
                .Select(t => t.ParentId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var hasChildrenSet = childTopicParentIds.ToHashSet();

            // 2. UserTopicsを一括取得
            Dictionary<Guid, UserTopic> userTopicsMap = new();
            if (userId != Guid.Empty)
            {
                var userTopics = await _dbContext.UserTopics
                    .Where(ut => topicIds.Contains(ut.TopicId) && ut.UserId == userId)
                    .ToListAsync(cancellationToken);

                userTopicsMap = userTopics.ToDictionary(ut => ut.TopicId, ut => ut);
            }

            // 3. メッセージ統計を一括取得
            var messageStats = await _dbContext.Messages
                .Where(m => topicIds.Contains(m.TopicId))
                .GroupBy(m => m.TopicId)
                .Select(g => new
                {
                    TopicId = g.Key,
                    TotalCount = g.Count(),
                    LastUpdatedAt = g.Max(m => m.UpdatedAt)
                })
                .ToListAsync(cancellationToken);

            var messageStatsMap = messageStats.ToDictionary(x => x.TopicId, x => x);

            // 4. 未読数を一括計算
            Dictionary<Guid, int> unreadCountsMap = new();
            foreach (var topic in topics)
            {
                unreadCountsMap[topic.Id] = CalculateUnreadCount(topic.Id, userTopicsMap.GetValueOrDefault(topic.Id), messageStatsMap.GetValueOrDefault(topic.Id));
            }

            // 5. DTOを作成
            var dtos = topics.Select(topic => new TopicWithStatsDto
            {
                Id = topic.Id,
                RoomId = topic.RoomId,
                ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
                SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
                Title = topic.Title,
                Description = topic.Description,
                HasChildren = hasChildrenSet.Contains(topic.Id),
                UnreadCount = unreadCountsMap.GetValueOrDefault(topic.Id),
                TotalMessageCount = messageStatsMap.GetValueOrDefault(topic.Id)?.TotalCount ?? 0,
                LastMessageUpdatedAt = messageStatsMap.GetValueOrDefault(topic.Id)?.LastUpdatedAt,
                LastAccessAt = userTopicsMap.GetValueOrDefault(topic.Id)?.LastAccessAt,
                IsAccessible = userTopicsMap.GetValueOrDefault(topic.Id)?.IsAccessible,
                CreatedAt = topic.CreatedAt,
                UpdatedAt = topic.UpdatedAt
            }).ToList();

            return Result<List<TopicWithStatsDto>>.Success(dtos);
        }, nameof(GetTopicsWithStatsAsync));
    }

    /// <summary>
    /// 単一トピックの統計情報を取得（N+1問題を解決）
    /// </summary>
    public async Task<Result<TopicWithStatsDto>> GetTopicWithStatsByIdAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);
            if (topic == null)
                return Result<TopicWithStatsDto>.NotFound("Topic not found");

            // hasChildrenチェック
            var hasChildren = await _topicRepository.Query()
                .AnyAsync(t => t.ParentId == topic.Id, cancellationToken);

            // UserTopic取得
            UserTopic? userTopic = null;
            if (userId != Guid.Empty)
            {
                userTopic = await _dbContext.UserTopics
                    .FirstOrDefaultAsync(ut => ut.TopicId == topicId && ut.UserId == userId, cancellationToken);
            }

            // メッセージ統計取得
            var messageStats = await _dbContext.Messages
                .Where(m => m.TopicId == topicId)
                .GroupBy(m => m.TopicId)
                .Select(g => new
                {
                    TotalCount = g.Count(),
                    LastUpdatedAt = g.Max(m => m.UpdatedAt)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // 未読数計算
            var unreadCount = CalculateUnreadCount(topicId, userTopic, messageStats);

            var dto = new TopicWithStatsDto
            {
                Id = topic.Id,
                RoomId = topic.RoomId,
                ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
                SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
                Title = topic.Title,
                Description = topic.Description,
                HasChildren = hasChildren,
                UnreadCount = unreadCount,
                TotalMessageCount = messageStats?.TotalCount ?? 0,
                LastMessageUpdatedAt = messageStats?.LastUpdatedAt,
                LastAccessAt = userTopic?.LastAccessAt,
                IsAccessible = userTopic?.IsAccessible,
                CreatedAt = topic.CreatedAt,
                UpdatedAt = topic.UpdatedAt
            };

            return Result<TopicWithStatsDto>.Success(dto);
        }, nameof(GetTopicWithStatsByIdAsync));
    }

    /// <summary>
    /// ルートトピックに未読カウントを含めて取得（N+1問題を解決）
    /// </summary>
    public async Task<Result<List<TopicTreeDto>>> GetRootTopicsWithUnreadAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId && t.ParentId == null)
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync(cancellationToken);

            if (!topics.Any())
                return Result<List<TopicTreeDto>>.Success(new List<TopicTreeDto>());

            var builder = await MapToDtosBuilder(topics)
                .WithHasChildren(cancellationToken);

            builder = await builder.WithUnread(userId, cancellationToken);

            var dtos = await builder.BuildTreeAsync(cancellationToken);
            return Result<List<TopicTreeDto>>.Success(dtos);
        }, nameof(GetRootTopicsWithUnreadAsync));
    }

    /// <summary>
    /// 未読数を計算するヘルパーメソッド
    /// </summary>
    private int CalculateUnreadCount(Guid topicId, UserTopic? userTopic, dynamic? messageStats)
    {
        if (userTopic?.LastReadMessageId.HasValue == true)
        {
            if (messageStats != null)
            {
                // 一括取得した統計情報から未読数を計算
                var totalMessages = (int)messageStats.TotalCount;
                if (totalMessages > 0)
                {
                    // 最新メッセージIDを取得（※注意：この簡略化版では完全ではありません）
                    var latestMessageId = _dbContext.Messages
                        .Where(m => m.TopicId == topicId)
                        .Max(m => m.Id);

                    var readMessages = _dbContext.Messages
                        .Count(m => m.TopicId == topicId && m.Id <= userTopic.LastReadMessageId.Value);

                    return Math.Max(0, totalMessages - readMessages);
                }
            }
            return 0;
        }
        else if (userTopic == null)
        {
            // UserTopicがない場合は全メッセージが未読
            return messageStats != null ? (int)messageStats.TotalCount : 0;
        }

        return 0;
    }

    /// <summary>
    /// ルーム内の全トピックを未読カウント付きで一括取得（N+1問題を解決）
    /// </summary>
    public async Task<Result<List<TopicTreeDto>>> GetAllTopicsWithUnreadAsync(Guid roomId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId)
                .ToListAsync(cancellationToken);

            if (!topics.Any())
                return Result<List<TopicTreeDto>>.Success(new List<TopicTreeDto>());

            // hasChildren + 未読計算
            var builder = await MapToDtosBuilder(topics)
                .WithHasChildren(cancellationToken);

            builder = await builder.WithUnread(userId, cancellationToken);

            var dtos = await builder.BuildTreeAsync(cancellationToken);
            return Result<List<TopicTreeDto>>.Success(dtos);
        }, nameof(GetAllTopicsWithUnreadAsync));
    }
}