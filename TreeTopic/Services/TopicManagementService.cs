using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.SignalR;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Hubs;

namespace TreeTopic.Services;

public interface ITopicManagementService
{
    Task<Result<List<TopicDto>>> GetAllTopicsAsync(CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetRootTopicsByRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByParentAsync(Guid parentId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, CancellationToken cancellationToken = default);
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

    public TopicManagementService(
        ITopicRepository topicRepository,
        IRoomRepository roomRepository,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        ILogger<TopicManagementService> logger) : base(logger)
    {
        _topicRepository = topicRepository;
        _roomRepository = roomRepository;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
    }

    public async Task<Result<List<TopicDto>>> GetAllTopicsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetAllTopicsAsync));
    }

    public async Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId)
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicDto>>> GetRootTopicsByRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId && t.ParentId == null)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetRootTopicsByRoomAsync));
    }

    public async Task<Result<List<TopicDto>>> GetTopicsByParentAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.ParentId == parentId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetTopicsByParentAsync));
    }

    public async Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
                return Result<TopicDto>.NotFound("Topic not found");

            var dto = MapToDto(topic);
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

    private TopicDto MapToDto(Topic topic)
    {
        // Check if this topic has any children
        var hasChildren = _topicRepository.Query()
            .Any(t => t.ParentId == topic.Id);

            return new TopicDto
            {
                Id = topic.Id,
                RoomId = topic.RoomId,
                ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
                SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
                Title = topic.Title,
                Description = topic.Description,
                HasChildren = hasChildren,
                CreatedAt = topic.CreatedAt,
                UpdatedAt = topic.UpdatedAt
            };
    }
}
