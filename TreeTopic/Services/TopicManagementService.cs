using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Services;

public interface ITopicManagementService
{
    Task<Result<List<TopicDto>>> GetAllTopicsAsync(CancellationToken cancellationToken = default);
    Task<Result<List<TopicDto>>> GetTopicsByRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> GetTopicByIdAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> CreateTopicAsync(CreateTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result<TopicDto>> UpdateTopicAsync(Guid topicId, UpdateTopicRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteTopicAsync(Guid topicId, CancellationToken cancellationToken = default);
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

            var topic = new Topic
            {
                RoomId = request.RoomId,
                ParentId = parentId
            };
            topic.Title = request.Title?.Trim() ?? string.Empty;
            topic.Description = request.Description?.Trim();

            await _topicRepository.AddAsync(topic, cancellationToken);
            await _topicRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(topic);
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

            Guid? parentId = request.ParentId.HasValue ? (Guid)request.ParentId.Value : null;
            if (parentId.HasValue)
            {
                var parent = await _topicRepository.GetByIdAsync(parentId.Value, cancellationToken);
                if (parent == null)
                    return Result<TopicDto>.NotFound("Parent topic not found");

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
            return Result<TopicDto>.Success(dto);
        }, nameof(UpdateTopicAsync));
    }

    public async Task<Result> DeleteTopicAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);

            if (topic == null)
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
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            Title = topic.Title,
            Description = topic.Description,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }
}
