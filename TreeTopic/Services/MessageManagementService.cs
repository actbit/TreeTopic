using System.Linq;
using TreeTopic.Dtos;
using TreeTopic.Hubs;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.SignalR;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Hubs;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Services;

public interface IMessageManagementService
{
    Task<Result<List<MessageDto>>> GetAllMessagesAsync(CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesAfterAsync(Guid topicId, Guid anchorMessageId, int take = 50, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesBeforeAsync(Guid topicId, Guid anchorMessageId, int take = 50, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> CreateMessageAsync(CreateMessageRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> MoveMessagesBeforeAsync(Guid sourceTopicId, Guid targetTopicId, Guid anchorMessageId, bool includeAnchorMessage = false, bool includeEarlierMessages = true, CancellationToken cancellationToken = default);
}

public class MessageManagementService : BaseService, IMessageManagementService
{
    private readonly IMessageRepository _messageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly IconService _iconService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IHubContext<MessageHub, IMessageHubClient> _messageHub;
    private readonly IHubContext<RoomTopicHub, IRoomTopicHubClient> _roomTopicHub;
    private readonly IMaskedUUIDService _maskedUuidService;

    public MessageManagementService(
        IMessageRepository messageRepository,
        ITopicRepository topicRepository,
        IFileRepository fileRepository,
        IRoomUserRepository roomUserRepository,
        IconService iconService,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment webHostEnvironment,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IHubContext<MessageHub, IMessageHubClient> messageHub,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IMaskedUUIDService maskedUuidService,
        ILogger<MessageManagementService> logger) : base(logger)
    {
        _messageRepository = messageRepository;
        _topicRepository = topicRepository;
        _fileRepository = fileRepository;
        _roomUserRepository = roomUserRepository;
        _iconService = iconService;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
        _tenantAccessor = tenantAccessor;
        _messageHub = messageHub;
        _roomTopicHub = roomTopicHub;
        _maskedUuidService = maskedUuidService;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
    private string? CurrentTenantIdentifier => _tenantAccessor.MultiTenantContext?.TenantInfo?.Identifier;

    private string GetTenantUploadsFolderName()
    {
        // Prefer the tenant identifier (matches route segment), fallback to internal tenant id.
        return CurrentTenantIdentifier
               ?? CurrentTenantId
               ?? "default";
    }

    private string GetUploadsRootPath()
    {
        var contentRoot = _webHostEnvironment.ContentRootPath;
        return Path.Combine(contentRoot, "uploads", GetTenantUploadsFolderName());
    }

    private string GetMessageUploadsPath(Guid userId, Guid messageId)
    {
        return Path.Combine(GetUploadsRootPath(), "messages", userId.ToString(), messageId.ToString());
    }

    private string BuildMessageUploadUrl(Guid userId, Guid messageId, string savedFileName)
    {
        var folder = GetTenantUploadsFolderName();
        return $"/uploads/{folder}/messages/{userId}/{messageId}/{savedFileName}".Replace("\\", "/");
    }

    private string BuildLegacyUploadUrl(string savedFileName)
    {
        var folder = GetTenantUploadsFolderName();
        return $"/uploads/{folder}/{savedFileName}".Replace("\\", "/");
    }

    private string? GetTopicGroupName(Guid topicId)
    {
        try
        {
            var maskedTopicId = _maskedUuidService.EncodeSynchronous(topicId);
            return MessageHubGroups.Topic(string.Empty, maskedTopicId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve SignalR group for topic {TopicId}", topicId);
            return null;
        }
    }

    private Task BroadcastMessageCreatedAsync(MessageDto dto)
    {
        var groupName = GetTopicGroupName(dto.TopicId);
        if (string.IsNullOrEmpty(groupName))
            return Task.CompletedTask;

        Logger.LogInformation("[MessageHub] Broadcast MessageCreated message={MessageId} topic={TopicId} group={Group}", dto.Id, dto.TopicId, groupName);
        var realtime = MapToRealtimeDto(dto);
        return _messageHub.Clients.Group(groupName).MessageCreated(realtime);
    }

    private Task BroadcastMessageUpdatedAsync(MessageDto dto)
    {
        var groupName = GetTopicGroupName(dto.TopicId);
        if (string.IsNullOrEmpty(groupName))
            return Task.CompletedTask;

        Logger.LogInformation("[MessageHub] Broadcast MessageUpdated message={MessageId} topic={TopicId} group={Group}", dto.Id, dto.TopicId, groupName);
        var realtime = MapToRealtimeDto(dto);
        return _messageHub.Clients.Group(groupName).MessageUpdated(realtime);
    }

    private Task BroadcastMessageDeletedAsync(Guid messageId, Guid topicId)
    {
        var groupName = GetTopicGroupName(topicId);
        if (string.IsNullOrEmpty(groupName))
            return Task.CompletedTask;

        Logger.LogInformation("[MessageHub] Broadcast MessageDeleted message={MessageId} topic={TopicId} group={Group}", messageId, topicId, groupName);
        var payload = new MessageDeletedEvent(
            _maskedUuidService.EncodeSynchronous(messageId),
            _maskedUuidService.EncodeSynchronous(topicId));
        return _messageHub.Clients.Group(groupName).MessageDeleted(payload);
    }

    private TopicDto MapTopicDto(Topic topic)
    {
        var hasChildren = _topicRepository.Query().Any(t => t.ParentId == topic.Id);

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

    private TopicRealtimeDto MapTopicRealtime(TopicDto dto)
    {
        var id = (Guid)dto.Id;
        var roomId = (Guid)dto.RoomId;
        var parentId = dto.ParentId.HasValue ? (Guid)dto.ParentId.Value : Guid.Empty;
        var sourceMessageId = dto.SourceMessageId.HasValue ? (Guid)dto.SourceMessageId.Value : Guid.Empty;

        var maskedParent = dto.ParentId.HasValue && parentId != Guid.Empty
            ? _maskedUuidService.EncodeSynchronous(parentId)
            : null;
        var maskedSource = dto.SourceMessageId.HasValue && sourceMessageId != Guid.Empty
            ? _maskedUuidService.EncodeSynchronous(sourceMessageId)
            : null;

        return new TopicRealtimeDto(
            id == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(id),
            roomId == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(roomId),
            maskedParent,
            dto.Title,
            dto.Description,
            dto.HasChildren,
            maskedSource,
            dto.CreatedAt,
            dto.UpdatedAt);
    }

    private Task BroadcastTopicCreatedAsync(TopicDto dto)
    {
        var roomId = (Guid)dto.RoomId;
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous(roomId));
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicCreated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        var payload = MapTopicRealtime(dto);
        return _roomTopicHub.Clients.Group(groupName).TopicCreated(payload);
    }

    private Task BroadcastTopicUpdatedAsync(TopicDto dto)
    {
        var roomId = (Guid)dto.RoomId;
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous(roomId));
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicUpdated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        var payload = MapTopicRealtime(dto);
        return _roomTopicHub.Clients.Group(groupName).TopicUpdated(payload);
    }

    private MessageRealtimeDto MapToRealtimeDto(MessageDto dto)
    {
        var id = (Guid)dto.Id;
        var topicId = (Guid)dto.TopicId;
        var roomUserId = (Guid)dto.RoomUserId;
        var replyId = dto.ReplyId.HasValue ? (Guid)dto.ReplyId.Value : Guid.Empty;
        var childTopicId = dto.ChildTopicId.HasValue ? (Guid)dto.ChildTopicId.Value : Guid.Empty;

        return new MessageRealtimeDto
        {
            Id = id == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(id),
            TopicId = topicId == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(topicId),
            RoomUserId = roomUserId == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous(roomUserId),
            UserName = dto.UserName,
            UserAvatar = dto.UserAvatar,
            Header = dto.Header,
            Body = dto.Body,
            ReplyId = replyId != Guid.Empty
                ? _maskedUuidService.EncodeSynchronous(replyId)
                : null,
            ChildTopicId = childTopicId != Guid.Empty
                ? _maskedUuidService.EncodeSynchronous(childTopicId)
                : null,
            ChildTopicTitle = dto.ChildTopicTitle,
            Files = dto.Files?.Select(f => new FileRealtimeDto
            {
                Id = ((Guid)f.Id) == Guid.Empty ? string.Empty : _maskedUuidService.EncodeSynchronous((Guid)f.Id),
                SourceFileId = f.SourceFileId.HasValue && (Guid)f.SourceFileId.Value != Guid.Empty
                    ? _maskedUuidService.EncodeSynchronous((Guid)f.SourceFileId.Value)
                    : null,
                MessageId = f.MessageId.HasValue && (Guid)f.MessageId.Value != Guid.Empty
                    ? _maskedUuidService.EncodeSynchronous((Guid)f.MessageId.Value)
                    : null,
                FileName = f.FileName,
                SaveFileName = f.SaveFileName,
                FileType = f.FileType,
                Size = f.Size,
                Url = f.Url,
                IsLatest = f.IsLatest,
                CreatedAt = f.CreatedAt,
                UpdatedAt = f.UpdatedAt
            }).ToList(),
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt
        };
    }

    public async Task<Result<List<MessageDto>>> GetAllMessagesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .ToListAsync(cancellationToken);

            var dtos = new List<MessageDto>();
            foreach (var message in messages)
            {
                var dto = await MapToDtoAsync(message, cancellationToken);
                dtos.Add(dto);
            }
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetAllMessagesAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .ToListAsync(cancellationToken);

            var dtos = new List<MessageDto>();
            foreach (var message in messages)
            {
                var dto = await MapToDtoAsync(message, cancellationToken);
                dtos.Add(dto);
            }
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesByTopicAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesAfterAsync(
        Guid topicId,
        Guid anchorMessageId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (take <= 0) take = 50;

            var anchor = await _messageRepository.Query()
                .Where(m => m.Id == anchorMessageId)
                .Select(m => new { m.Id, m.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (anchor == null)
            {
                anchor = await _messageRepository.Query()
                    .Where(m => m.TopicId == topicId)
                    .OrderBy(m => m.CreatedAt)
                    .ThenBy(m => m.Id)
                    .Select(m => new { m.Id, m.CreatedAt })
                    .FirstOrDefaultAsync(cancellationToken);

                if (anchor == null)
                {
                    return Result<List<MessageDto>>.Success(new List<MessageDto>());
                }
            }

            var messages = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId)
                .Where(m => m.CreatedAt > anchor.CreatedAt || (m.CreatedAt == anchor.CreatedAt && m.Id >= anchor.Id))
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .Take(take)
                .ToListAsync(cancellationToken);

            var dtos = new List<MessageDto>();
            foreach (var message in messages)
            {
                var dto = await MapToDtoAsync(message, cancellationToken);
                dtos.Add(dto);
            }
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesAfterAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesBeforeAsync(
        Guid topicId,
        Guid anchorMessageId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (take <= 0) take = 50;

            var anchor = await _messageRepository.Query()
                .Where(m => m.Id == anchorMessageId)
                .Select(m => new { m.Id, m.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (anchor == null)
            {
                return Result<List<MessageDto>>.Success(new List<MessageDto>());
            }

            var messages = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId)
                .Where(m => m.CreatedAt < anchor.CreatedAt || (m.CreatedAt == anchor.CreatedAt && m.Id < anchor.Id))
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .Take(take)
                .ToListAsync(cancellationToken);

            messages.Reverse();
            var dtos = new List<MessageDto>();
            foreach (var message in messages)
            {
                var dto = await MapToDtoAsync(message, cancellationToken);
                dtos.Add(dto);
            }
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesBeforeAsync));
    }

    public async Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.Query()
                .Where(m => m.Id == messageId)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .FirstOrDefaultAsync(cancellationToken);

            if (message == null)
                return Result<MessageDto>.NotFound("Message not found");

            var dto = await MapToDtoAsync(message, cancellationToken);
            return Result<MessageDto>.Success(dto);
        }, nameof(GetMessageByIdAsync));
    }

    public async Task<Result<MessageDto>> CreateMessageAsync(
        CreateMessageRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
            if (topic == null)
                return Result<MessageDto>.NotFound("Topic not found");

            if (request.ReplyId.HasValue && request.ReplyId != Guid.Empty)
            {
                var replyTo = await _messageRepository.GetByIdAsync(request.ReplyId.Value, cancellationToken);
                if (replyTo == null)
                    return Result<MessageDto>.NotFound("Reply message not found");

                if (replyTo.TopicId != (Guid)request.TopicId)
                    return Result<MessageDto>.BadRequest("Reply message must be in the same topic");
            }

            var roomUser = await ResolveRoomUserAsync(topic.RoomId, userId, cancellationToken);
            if (roomUser == null)
                return Result<MessageDto>.BadRequest("Room user not found");

            var message = new Message
            {
                TopicId = request.TopicId,
                RoomUserId = roomUser.Id,
                Header = request.Header ?? string.Empty,
                Body = request.Body,
                ReplyId = request.ReplyId.HasValue && request.ReplyId != Guid.Empty
                    ? request.ReplyId
                    : null
            };

            await _messageRepository.AddAsync(message, cancellationToken);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            message.RoomUser = roomUser;

            if (request.Files != null && request.Files.Count > 0)
            {
                await ProcessUploadedFilesAsync(message, request.Files, cancellationToken);
            }

            // Ensure files are available on response DTO.
            message.Files = await _fileRepository.Query()
                .Where(f => f.MessageId == message.Id)
                .ToListAsync(cancellationToken);

            var dto = await MapToDtoAsync(message, cancellationToken);

            TopicDto? createdTopicDto = null;
            if (request.ChildTopic != null)
            {
                var childRequest = request.ChildTopic;
                var title = childRequest.Title?.Trim() ?? string.Empty;
                if (title.Length < 2)
                {
                    return Result<MessageDto>.BadRequest("Child topic title must be at least 2 characters long");
                }

                var parentId = childRequest.ParentId.HasValue
                    ? (Guid)childRequest.ParentId.Value
                    : topic.Id;

                var childTopic = new Topic
                {
                    RoomId = topic.RoomId,
                    ParentId = parentId,
                    Title = title,
                    Description = childRequest.Description?.Trim(),
                    SourceMessageId = message.Id
                };

                await _topicRepository.AddAsync(childTopic, cancellationToken);
                await _topicRepository.SaveChangesAsync(cancellationToken);

                // メッセージから既存のChildTopicsを取得して、親を新しく作られたTopicに変更
                var existingChildTopics = await _topicRepository.Query()
                    .Where(t => t.SourceMessageId == message.Id && t.Id != childTopic.Id)
                    .ToListAsync(cancellationToken);

                if (existingChildTopics.Count > 0)
                {
                    foreach (var existingChild in existingChildTopics)
                    {
                        existingChild.ParentId = childTopic.Id;
                    }
                    await _topicRepository.SaveChangesAsync(cancellationToken);
                }

                createdTopicDto = MapTopicDto(childTopic);
                await BroadcastTopicCreatedAsync(createdTopicDto);

                var selectedMessageIds = childRequest.SelectedMessageIds?
                    .Select(id => (Guid)id)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                Result<List<MessageDto>>? moveResult = null;
                if (selectedMessageIds != null && selectedMessageIds.Count > 0)
                {
                    moveResult = await MoveMessagesByIdsInternalAsync(
                        topic.Id,
                        childTopic.Id,
                        selectedMessageIds,
                        cancellationToken);
                }

                if (moveResult != null && !moveResult.IsSuccess)
                {
                    Logger.LogWarning("Failed to move selected messages into child topic: {Error}", moveResult.Error?.Message);
                }
            }

            if (createdTopicDto != null)
            {
                dto.ChildTopicId = createdTopicDto.Id;
                dto.ChildTopicTitle = createdTopicDto.Title;
            }

            try
            {
                await BroadcastMessageCreatedAsync(dto);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to broadcast message created for message {MessageId}", message.Id);
            }

            return Result<MessageDto>.Success(dto, 201);
        }, nameof(CreateMessageAsync));
    }

    public async Task<Result<MessageDto>> UpdateMessageAsync(
        Guid messageId,
        UpdateMessageRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.Query()
                .Include(m => m.RoomUser)
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
                return Result<MessageDto>.NotFound("Message not found");

            if (message.RoomUser == null || message.RoomUser.ApplicationUserId != userId)
                return Result<MessageDto>.Forbidden("You are not allowed to edit this message");

            if (!string.IsNullOrEmpty(request.Header))
                message.Header = request.Header;

            if (!string.IsNullOrEmpty(request.Body))
                message.Body = request.Body;

            message.UpdatedAt = DateTime.UtcNow;
            _messageRepository.Update(message);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            var updated = await _messageRepository.Query()
                .Where(m => m.Id == messageId)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .FirstOrDefaultAsync(cancellationToken);

            var dto = await MapToDtoAsync(updated ?? message, cancellationToken);
            try
            {
                await BroadcastMessageUpdatedAsync(dto);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to broadcast message updated for message {MessageId}", messageId);
            }
            return Result<MessageDto>.Success(dto);
        }, nameof(UpdateMessageAsync));
    }

    public async Task<Result> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.Query()
                .Include(m => m.RoomUser)
                .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

            if (message == null)
                return Result.NotFound("Message not found");

            if (message.RoomUser == null || message.RoomUser.ApplicationUserId != userId)
                return Result.Forbidden("You are not allowed to delete this message");

            _messageRepository.Delete(message);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            try
            {
                await BroadcastMessageDeletedAsync(message.Id, message.TopicId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to broadcast message deleted for message {MessageId}", message.Id);
            }

            return Result.Success();
        }, nameof(DeleteMessageAsync));
    }

    public async Task<Result<List<MessageDto>>> MoveMessagesBeforeAsync(
        Guid sourceTopicId,
        Guid targetTopicId,
        Guid anchorMessageId,
        bool includeAnchorMessage = false,
        bool includeEarlierMessages = true,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            () => MoveMessagesBeforeInternalAsync(
                sourceTopicId,
                targetTopicId,
                anchorMessageId,
                includeAnchorMessage,
                includeEarlierMessages,
                cancellationToken),
            nameof(MoveMessagesBeforeAsync));
    }

    private async Task<Result<List<MessageDto>>> MoveMessagesBeforeInternalAsync(
        Guid sourceTopicId,
        Guid targetTopicId,
        Guid anchorMessageId,
        bool includeAnchorMessage,
        bool includeEarlierMessages,
        CancellationToken cancellationToken)
    {
        var anchor = await _messageRepository.Query()
            .Where(m => m.Id == anchorMessageId)
            .Select(m => new { m.Id, m.TopicId, m.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (anchor == null)
            return Result<List<MessageDto>>.NotFound("Anchor message not found");

        if (anchor.TopicId != sourceTopicId)
            return Result<List<MessageDto>>.BadRequest("Anchor message must belong to the source topic");

        var cutoffCreatedAt = anchor.CreatedAt;
        var cutoffId = anchor.Id;

        var messages = new List<Message>();

        if (includeEarlierMessages)
        {
            var earlier = await _messageRepository.Query()
                .Where(m => m.TopicId == sourceTopicId)
                .Where(m =>
                    m.CreatedAt < cutoffCreatedAt ||
                    (m.CreatedAt == cutoffCreatedAt && m.Id < cutoffId))
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Id)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .ToListAsync(cancellationToken);
            messages.AddRange(earlier);
        }

        if (includeAnchorMessage)
        {
            var anchorMessage = await _messageRepository.Query()
                .Where(m => m.TopicId == sourceTopicId && m.Id == anchorMessageId)
                .Include(m => m.RoomUser)
                .ThenInclude(ru => ru.ApplicationUser)
                .Include(m => m.Files)
                .FirstOrDefaultAsync(cancellationToken);
            if (anchorMessage != null && !messages.Any(m => m.Id == anchorMessage.Id))
            {
                messages.Add(anchorMessage);
            }
        }

        if (messages.Count == 0)
            return Result<List<MessageDto>>.Success(new List<MessageDto>());

        foreach (var message in messages)
        {
            message.TopicId = targetTopicId;
            // Note: UpdatedAt is not changed because moving a message is not an edit
        }

        await _messageRepository.SaveChangesAsync(cancellationToken);

        // メッセージに紐づいた子Topicの親を移動先Topicに変更
        var movedMessageIds = messages.Select(m => m.Id).ToList();
        var childTopics = await _topicRepository.Query()
            .Where(t => movedMessageIds.Contains(t.SourceMessageId!.Value))
            .ToListAsync(cancellationToken);

        if (childTopics.Count > 0)
        {
            foreach (var childTopic in childTopics)
            {
                childTopic.ParentId = targetTopicId;
            }
            await _topicRepository.SaveChangesAsync(cancellationToken);

            // 子Topicの更新をブロードキャスト
            foreach (var childTopic in childTopics)
            {
                var topicDto = MapTopicDto(childTopic);
                await BroadcastTopicUpdatedAsync(topicDto);
            }
        }

        var dtos = new List<MessageDto>();
        foreach (var message in messages)
        {
            await BroadcastMessageDeletedAsync(message.Id, sourceTopicId);
            var dto = await MapToDtoAsync(message, cancellationToken);
            dtos.Add(dto);
        }

        foreach (var dto in dtos)
        {
            await BroadcastMessageCreatedAsync(dto);
        }

        return Result<List<MessageDto>>.Success(dtos);
    }

    private async Task<Result<List<MessageDto>>> MoveMessagesByIdsInternalAsync(
        Guid sourceTopicId,
        Guid targetTopicId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        var ids = messageIds.ToList();
        if (ids.Count == 0)
            return Result<List<MessageDto>>.Success(new List<MessageDto>());

        var messages = await _messageRepository.Query()
            .Where(m => ids.Contains(m.Id) && m.TopicId == sourceTopicId)
            .Include(m => m.RoomUser)
            .ThenInclude(ru => ru.ApplicationUser)
            .Include(m => m.Files)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
            return Result<List<MessageDto>>.Success(new List<MessageDto>());

        foreach (var message in messages)
        {
            message.TopicId = targetTopicId;
            // Note: UpdatedAt is not changed because moving a message is not an edit
        }

        await _messageRepository.SaveChangesAsync(cancellationToken);

        // メッセージに紐づいた子Topicの親を移動先Topicに変更
        var movedMessageIds = messages.Select(m => m.Id).ToList();
        var childTopics = await _topicRepository.Query()
            .Where(t => movedMessageIds.Contains(t.SourceMessageId!.Value))
            .ToListAsync(cancellationToken);

        if (childTopics.Count > 0)
        {
            foreach (var childTopic in childTopics)
            {
                childTopic.ParentId = targetTopicId;
            }
            await _topicRepository.SaveChangesAsync(cancellationToken);

            // 子Topicの更新をブロードキャスト
            foreach (var childTopic in childTopics)
            {
                var topicDto = MapTopicDto(childTopic);
                await BroadcastTopicUpdatedAsync(topicDto);
            }
        }

        var dtos = new List<MessageDto>();
        foreach (var message in messages)
        {
            await BroadcastMessageDeletedAsync(message.Id, sourceTopicId);
            var dto = await MapToDtoAsync(message, cancellationToken);
            dtos.Add(dto);
        }

        foreach (var dto in dtos)
        {
            await BroadcastMessageCreatedAsync(dto);
        }

        return Result<List<MessageDto>>.Success(dtos);
    }

    private async Task ProcessUploadedFilesAsync(
        Message message,
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        try
        {
            var applicationUserId = message.RoomUser?.ApplicationUserId ?? Guid.Empty;
            var uploadPath = GetMessageUploadsPath(applicationUserId, message.Id);
            Directory.CreateDirectory(uploadPath);

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileExtension = Path.GetExtension(file.FileName);
                    var savedFileName = $"{Guid.CreateVersion7()}_{DateTime.UtcNow.Ticks}{fileExtension}";
                    var filePath = Path.Combine(uploadPath, savedFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream, cancellationToken);
                    }

                    var fileEntity = new FileModel
                    {
                        FileName = file.FileName,
                        SaveFileName = savedFileName,
                        FileType = file.ContentType ?? "application/octet-stream",
                        MessageId = message.Id,
                        SourceFileId = null,
                        SourceFile = null,
                        IsLatast = true
                    };

                    await _fileRepository.AddAsync(fileEntity, cancellationToken);
                }
            }

            await _fileRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing uploaded files for message {MessageId}", message.Id);
        }
    }

    private async Task<RoomUser?> ResolveRoomUserAsync(Guid roomId, Guid applicationUserId, CancellationToken cancellationToken)
    {
        var existing = await _roomUserRepository.GetByRoomAndUserAsync(roomId, applicationUserId, cancellationToken);
        if (existing != null)
            return existing;

        var user = await _userManager.FindByIdAsync(applicationUserId.ToString());
        if (user == null)
            return null;

        var roomUser = new RoomUser
        {
            ApplicationUserId = applicationUserId,
            RoomId = roomId,
            Name = RoomUserNameHelper.DefaultUserToken,
            UseMainName = true,
            UseMainIcon = true
        };

        await _roomUserRepository.AddAsync(roomUser, cancellationToken);
        await _roomUserRepository.SaveChangesAsync(cancellationToken);
        roomUser.ApplicationUser = user;

        return roomUser;
    }

    private async Task<MessageDto> MapToDtoAsync(Message message, CancellationToken cancellationToken = default)
    {
        // 最新の子Topicを取得
        var childTopic = await _topicRepository.Query()
            .Where(t => t.SourceMessageId == message.Id)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new MessageDto
        {
            Id = message.Id,
            TopicId = message.TopicId,
            RoomUserId = message.RoomUserId,
            UserName = RoomUserNameHelper.ResolveDisplayName(message.RoomUser),
            UserAvatar = _iconService.GetRoomUserIconUrl(message.RoomUser),
            Header = message.Header,
            Body = message.Body,
            ReplyId = message.ReplyId != Guid.Empty ? message.ReplyId : null,
            ChildTopicId = childTopic?.Id,
            ChildTopicTitle = childTopic?.Title,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            Files = message.Files?.Select(f =>
            {
                var applicationUserId = message.RoomUser?.ApplicationUserId ?? Guid.Empty;
                var newPath = Path.Combine(GetMessageUploadsPath(applicationUserId, message.Id), f.SaveFileName);
                var legacyPath = Path.Combine(GetUploadsRootPath(), f.SaveFileName);
                long size = 0;
                try
                {
                    if (System.IO.File.Exists(newPath))
                        size = new FileInfo(newPath).Length;
                    else if (System.IO.File.Exists(legacyPath))
                        size = new FileInfo(legacyPath).Length;
                }
                catch
                {
                    size = 0;
                }

                return new FileDto
                {
                    Id = f.Id,
                    FileName = f.FileName,
                    SaveFileName = f.SaveFileName,
                    FileType = f.FileType,
                    MessageId = f.MessageId != Guid.Empty ? f.MessageId : null,
                    SourceFileId = f.SourceFileId != Guid.Empty ? f.SourceFileId : null,
                    IsLatest = f.IsLatast,
                    CreatedAt = f.CreatedAt,
                    UpdatedAt = f.UpdatedAt,
                    Size = size,
                    Url = System.IO.File.Exists(newPath)
                        ? BuildMessageUploadUrl(applicationUserId, message.Id, f.SaveFileName)
                        : BuildLegacyUploadUrl(f.SaveFileName)
                };
            }).ToList()
        };
    }
}
