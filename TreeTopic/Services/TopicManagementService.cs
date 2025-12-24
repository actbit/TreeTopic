using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Services;

public interface ITopicManagementService
{
    Task<Result<List<TopicDto>>> GetAllTopicsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> CreateTopicAsync(CreateTopicRequest request, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> UpdateTopicAsync(Guid topicId, UpdateTopicRequest request, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeleteTopicAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default);
}

public class TopicManagementService : BaseService, ITopicManagementService
{
    private readonly ITopicRepository _topicRepository;
    private readonly IRoomRepository _roomRepository;

    public TopicManagementService(
        ITopicRepository topicRepository,
        IRoomRepository roomRepository,
        ILogger<TopicManagementService> logger) : base(logger)
    {
        _topicRepository = topicRepository;
        _roomRepository = roomRepository;
    }

    public async Task<Result<List<TopicDto>>> GetAllTopicsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.TenantId == tenantId.ToString())
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetAllTopicsAsync));
    }

    public async Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topics = await _topicRepository.Query()
                .Where(t => t.RoomId == roomId && t.TenantId == tenantId.ToString())
                .ToListAsync(cancellationToken);

            var dtos = topics.Select(MapToDto).ToList();
            return Result<List<TopicDto>>.Success(dtos);
        }, nameof(GetTopicsByRoomAsync));
    }

    public async Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null || topic.TenantId != tenantId.ToString())
                return Result<TopicDto>.NotFound("Topic not found");

            var dto = MapToDto(topic);
            return Result<TopicDto>.Success(dto);
        }, nameof(GetTopicByIdAsync));
    }

    public async Task<Result<TopicDto>> CreateTopicAsync(
        CreateTopicRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var room = await _roomRepository.GetByIdAsync(request.RoomId, cancellationToken);
            if (room == null || room.TenantId != tenantId.ToString())
                return Result<TopicDto>.NotFound("Room not found");

            if (request.ParentId.HasValue)
            {
                var parent = await _topicRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
                if (parent == null || parent.TenantId != tenantId.ToString())
                    return Result<TopicDto>.NotFound("Parent topic not found");
            }

            var topic = new Topic
            {
                RoomId = request.RoomId,
                ParentId = request.ParentId ?? Guid.Empty,
                TenantId = tenantId.ToString()
            };

            await _topicRepository.AddAsync(topic, cancellationToken);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(topic);
            return Result<TopicDto>.Success(dto, 201);
        }, nameof(CreateTopicAsync));
    }

    public async Task<Result<TopicDto>> UpdateTopicAsync(
        Guid topicId,
        UpdateTopicRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null || topic.TenantId != tenantId.ToString())
                return Result<TopicDto>.NotFound("Topic not found");

            if (request.ParentId.HasValue && request.ParentId.Value != new MaskedGuid(Guid.Empty))
            {
                var parent = await _topicRepository.GetByIdAsync(request.ParentId.Value, cancellationToken);
                if (parent == null || parent.TenantId != tenantId.ToString())
                    return Result<TopicDto>.NotFound("Parent topic not found");

                topic.ParentId = request.ParentId.Value;
            }

            topic.UpdatedAt = DateTime.UtcNow;
            _topicRepository.Update(topic);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(topic);
            return Result<TopicDto>.Success(dto);
        }, nameof(UpdateTopicAsync));
    }

    public async Task<Result> DeleteTopicAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null || topic.TenantId != tenantId.ToString())
                return Result.NotFound("Topic not found");

            _topicRepository.Delete(topic);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }, nameof(DeleteTopicAsync));
    }

    private static TopicDto MapToDto(Topic topic)
    {
        return new TopicDto
        {
            Id = topic.Id,
            TenantId = Guid.Parse(topic.TenantId),
            RoomId = topic.RoomId,
            ParentId = topic.ParentId != Guid.Empty ? topic.ParentId : null,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }
}
