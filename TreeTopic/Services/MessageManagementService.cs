using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using MaskedUUID.AspNetCore.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Threading;
using TreeTopic;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Hubs;
using TreeTopic.Models;
using TreeTopic.Repositories;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Services;

public interface IMessageManagementService
{
    Task<Result<List<MessageDto>>> GetAllMessagesAsync(CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesAfterAsync(Guid topicId, Guid anchorMessageId, Guid userId, int take = 50, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesBeforeAsync(Guid topicId, Guid anchorMessageId, Guid userId, int take = 50, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> SearchMessagesByTopicAsync(Guid topicId, string query, MessageSearchMode mode = MessageSearchMode.Contains, bool caseSensitive = false, int take = 100, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> CreateMessageAsync(CreateMessageRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> DeleteMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> MoveMessagesBeforeAsync(Guid sourceTopicId, Guid targetTopicId, Guid anchorMessageId, bool includeAnchorMessage = false, bool includeEarlierMessages = true, CancellationToken cancellationToken = default);
    Task<Result<int>> MarkTopicAsReadAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default);
}

public class MessageManagementService : BaseService, IMessageManagementService
{
    private readonly IMessageRepository _messageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IFileRepository _fileRepository;
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly RoomUserManager _roomUserManager;
    private readonly IconService _iconService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IHubContext<MessageHub, IMessageHubClient> _messageHub;
    private readonly IHubContext<RoomTopicHub, IRoomTopicHubClient> _roomTopicHub;
    private readonly IHubContext<RoomUserSyncHub, IRoomUserSyncHubClient> _roomUserSyncHub;
    private readonly IMaskedUUIDService _maskedUuidService;
    private readonly IPushService _pushService;
    private readonly IRegexSearchPatternConverter _regexSearchPatternConverter;
    private readonly ApplicationDbContext _dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TopicPermissionManager _topicPermissionManager;

    public MessageManagementService(
        IMessageRepository messageRepository,
        ITopicRepository topicRepository,
        IFileRepository fileRepository,
        IRoomUserRepository roomUserRepository,
        RoomUserManager roomUserManager,
        IconService iconService,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment webHostEnvironment,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IHubContext<MessageHub, IMessageHubClient> messageHub,
        IHubContext<RoomTopicHub, IRoomTopicHubClient> roomTopicHub,
        IHubContext<RoomUserSyncHub, IRoomUserSyncHubClient> roomUserSyncHub,
        IMaskedUUIDService maskedUuidService,
        IPushService pushService,
        IRegexSearchPatternConverter regexSearchPatternConverter,
        ApplicationDbContext dbContext,
        IServiceScopeFactory serviceScopeFactory,
        TopicPermissionManager topicPermissionManager,
        ILogger<MessageManagementService> logger) : base(logger)
    {
        _messageRepository = messageRepository;
        _topicRepository = topicRepository;
        _fileRepository = fileRepository;
        _roomUserRepository = roomUserRepository;
        _roomUserManager = roomUserManager;
        _iconService = iconService;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
        _tenantAccessor = tenantAccessor;
        _messageHub = messageHub;
        _roomTopicHub = roomTopicHub;
        _roomUserSyncHub = roomUserSyncHub;
        _maskedUuidService = maskedUuidService;
        _pushService = pushService;
        _regexSearchPatternConverter = regexSearchPatternConverter;
        _dbContext = dbContext;
        _serviceScopeFactory = serviceScopeFactory;
        _topicPermissionManager = topicPermissionManager;
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
        var payload = new MessageDeletedEvent(messageId, topicId);
        return _messageHub.Clients.Group(groupName).MessageDeleted(payload);
    }

    private async Task BroadcastTopicUnreadUpdatedAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default)
    {
        var topic = await _topicRepository.GetByIdAsync(topicId, cancellationToken);
        if (topic == null) return;

        // ユーザーの未読数を計算
        var userTopic = await _dbContext.UserTopics
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TopicId == topicId, cancellationToken);
        int unreadCount = 0;
        DateTime? lastReadAt = null;

        if (userTopic != null)
        {
            lastReadAt = userTopic.LastAccessAt;
        }
        unreadCount = await CountUnreadMessagesAsync(topicId, userTopic?.LastReadMessageId, cancellationToken);

        var groupName = RoomUserSyncHubGroups.RoomUser(
            _maskedUuidService.EncodeSynchronous(topic.RoomId),
            _maskedUuidService.EncodeSynchronous(userId));

        var payload = new TopicUnreadUpdateEvent(
            topic.RoomId,
            topicId,
            unreadCount,
            lastReadAt);

        Logger.LogInformation("[RoomUserSyncHub] Broadcast TopicUnreadUpdated topic={TopicId} user={UserId} unread={UnreadCount} group={Group}", topicId, userId, unreadCount, groupName);
        await _roomUserSyncHub.Clients.Group(groupName).TopicUnreadUpdated(payload);
    }

    private static bool IsMessageOrderAtOrAfter(
        DateTime lhsCreatedAt,
        Guid lhsId,
        DateTime rhsCreatedAt,
        Guid rhsId)
    {
        if (lhsCreatedAt != rhsCreatedAt)
            return lhsCreatedAt > rhsCreatedAt;

        return lhsId.CompareTo(rhsId) >= 0;
    }

    private async Task<(DateTime CreatedAt, Guid Id)?> GetMessageOrderKeyAsync(
        Guid topicId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var key = await _dbContext.Messages
            .Where(m => m.TopicId == topicId && m.Id == messageId)
            .Select(m => new { m.CreatedAt, m.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (key == null)
            return null;

        return (key.CreatedAt, key.Id);
    }

    private async Task<int> CountUnreadMessagesAsync(
        Guid topicId,
        Guid? lastReadMessageId,
        CancellationToken cancellationToken)
    {
        return await UnreadCountQueryHelper.CountUnreadAsync(_dbContext, topicId, lastReadMessageId, cancellationToken);
    }

    private static Guid GetLatestMessageId(IEnumerable<Message> messages)
    {
        return messages
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => m.Id)
            .First();
    }

    private TopicDetailDto MapTopicDto(Topic topic)
    {
        var hasChildren = _topicRepository.Query().Any(t => t.ParentId == topic.Id);

        return new TopicDetailDto
        {
            Id = topic.Id,
            RoomId = topic.RoomId,
            ParentId = topic.ParentId.HasValue ? topic.ParentId : null,
            SourceMessageId = topic.SourceMessageId.HasValue ? topic.SourceMessageId : null,
            Title = topic.Title,
            Description = topic.Description,
            HasChildren = hasChildren,
            UnreadCount = 0,
            CreatedAt = topic.CreatedAt,
            UpdatedAt = topic.UpdatedAt
        };
    }

    private async Task<TopicRealtimeDto> MapTopicRealtimeAsync(TopicDetailDto dto, CancellationToken cancellationToken = default)
    {
        var topicId = (Guid)dto.Id;

        // Get MessageCount
        var messageCount = await _dbContext.Messages
            .CountAsync(m => m.TopicId == topicId, cancellationToken);

        return new TopicRealtimeDto(
            dto.Id,
            dto.RoomId,
            dto.ParentId,
            dto.Title,
            dto.Description,
            dto.HasChildren,
            dto.SourceMessageId,
            dto.UnreadCount,
            messageCount,
            dto.CreatedAt,
            dto.UpdatedAt);
    }

    private async Task BroadcastTopicCreatedAsync(TopicDetailDto dto, CancellationToken cancellationToken = default)
    {
        var roomId = (Guid)dto.RoomId;
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous(roomId));
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicCreated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        var payload = await MapTopicRealtimeAsync(dto, cancellationToken);
        await _roomTopicHub.Clients.Group(groupName).TopicCreated(payload);
    }

    private async Task BroadcastTopicUpdatedAsync(TopicDetailDto dto, CancellationToken cancellationToken = default)
    {
        var roomId = (Guid)dto.RoomId;
        var groupName = RoomTopicHubGroups.Room(_maskedUuidService.EncodeSynchronous(roomId));
        Logger.LogInformation("[RoomTopicHub] Broadcast TopicUpdated topic={TopicId} room={RoomId} group={Group}", dto.Id, dto.RoomId, groupName);
        var payload = await MapTopicRealtimeAsync(dto, cancellationToken);
        await _roomTopicHub.Clients.Group(groupName).TopicUpdated(payload);
    }

    private MessageRealtimeDto MapToRealtimeDto(MessageDto dto)
    {
        return new MessageRealtimeDto
        {
            Id = dto.Id,
            TopicId = dto.TopicId,
            RoomUserId = dto.RoomUserId,
            UserName = dto.UserName,
            UserAvatar = dto.UserAvatar,
            Header = dto.Header,
            Body = dto.Body,
            ReplyId = dto.ReplyId,
            ChildTopicId = dto.ChildTopicId,
            ChildTopicTitle = dto.ChildTopicTitle,
            Files = dto.Files?.Select(f => new FileRealtimeDto
            {
                Id = f.Id,
                SourceFileId = f.SourceFileId,
                MessageId = f.MessageId,
                FileName = f.FileName,
                SaveFileName = f.SaveFileName,
                FileType = f.FileType,
                Size = f.Size,
                Url = f.Url,
                IsLatest = f.IsLatest,
                UploadedBy = null,
                UploadedByName = null,
                Versions = null,
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

    public async Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default)
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

            // 最新のメッセージIDでユーザーのLastReadMessageIdを更新
            if (messages.Count > 0 && userId != Guid.Empty)
            {
                var latestMessageId = GetLatestMessageId(messages);
                await UpdateUserTopicAccessAsync(topicId, userId, latestMessageId, cancellationToken);

                // 未読数更新を通知
                await BroadcastTopicUnreadUpdatedAsync(topicId, userId, cancellationToken);
            }

            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesByTopicAsync));
    }

    public async Task<Result<List<MessageDto>>> SearchMessagesByTopicAsync(
        Guid topicId,
        string query,
        MessageSearchMode mode = MessageSearchMode.Contains,
        bool caseSensitive = false,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<List<MessageDto>>.BadRequest("Search query is required.");
            }

            take = Math.Clamp(take, 1, 200);
            var normalizedQuery = query.Trim();
            List<Message> messages;

            if (mode == MessageSearchMode.Regex)
            {
                var ids = await SearchMessageIdsByRegexAsync(topicId, normalizedQuery, caseSensitive, take, cancellationToken);
                if (ids.Count == 0)
                {
                    return Result<List<MessageDto>>.Success(new List<MessageDto>());
                }

                var messagesById = await _messageRepository.Query()
                    .Where(m => ids.Contains(m.Id))
                    .Include(m => m.RoomUser)
                    .ThenInclude(ru => ru.ApplicationUser)
                    .Include(m => m.Files)
                    .ToListAsync(cancellationToken);

                var orderMap = ids
                    .Select((id, index) => new { id, index })
                    .ToDictionary(x => x.id, x => x.index);
                messages = messagesById
                    .OrderBy(m => orderMap.TryGetValue(m.Id, out var idx) ? idx : int.MaxValue)
                    .ToList();
            }
            else
            {
                var messageQuery = _messageRepository.Query()
                    .Where(m => m.TopicId == topicId);

                if (caseSensitive)
                {
                    messageQuery = messageQuery.Where(m =>
                        (m.Header != null && m.Header.Contains(normalizedQuery)) ||
                        m.Body.Contains(normalizedQuery));
                }
                else
                {
                    var lowered = normalizedQuery.ToLower();
                    messageQuery = messageQuery.Where(m =>
                        ((m.Header ?? string.Empty).ToLower().Contains(lowered)) ||
                        m.Body.ToLower().Contains(lowered));
                }

                messages = await messageQuery
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Include(m => m.RoomUser)
                    .ThenInclude(ru => ru.ApplicationUser)
                    .Include(m => m.Files)
                    .Take(take)
                    .ToListAsync(cancellationToken);
            }

            var dtos = new List<MessageDto>(messages.Count);
            foreach (var message in messages)
            {
                dtos.Add(await MapToDtoAsync(message, cancellationToken));
            }

            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(SearchMessagesByTopicAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesAfterAsync(
        Guid topicId,
        Guid anchorMessageId,
        Guid userId,
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

            // 未読更新処理
            if (messages.Count > 0 && userId != Guid.Empty)
            {
                var latestMessageId = GetLatestMessageId(messages);
                await UpdateUserTopicAccessAsync(topicId, userId, latestMessageId, cancellationToken);
                await BroadcastTopicUnreadUpdatedAsync(topicId, userId, cancellationToken);
            }

            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesAfterAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesBeforeAsync(
        Guid topicId,
        Guid anchorMessageId,
        Guid userId,
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

            // 未読更新処理
            if (messages.Count > 0 && userId != Guid.Empty)
            {
                var latestMessageId = GetLatestMessageId(messages);
                await UpdateUserTopicAccessAsync(topicId, userId, latestMessageId, cancellationToken);
                await BroadcastTopicUnreadUpdatedAsync(topicId, userId, cancellationToken);
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

    private async Task<List<Guid>> SearchMessageIdsByRegexAsync(
        Guid topicId,
        string pattern,
        bool caseSensitive,
        int take,
        CancellationToken cancellationToken)
    {
        var providerName = _dbContext.Database.ProviderName ?? string.Empty;
        var regexSpec = _regexSearchPatternConverter.Convert(pattern, caseSensitive, providerName);

        // 安全性検証: 演算子とマッチタイプが期待値であることを確認
        if (!regexSpec.IsValidOperator || !regexSpec.IsValidMatchType)
        {
            Logger.LogWarning("Invalid regex operator or match type detected. Operator: {Operator}, MatchType: {MatchType}",
                regexSpec.PostgresOperator, regexSpec.MySqlMatchType);
            throw new InvalidOperationException("Invalid regex search specification.");
        }

        var provider = providerName.ToLowerInvariant();

        if (provider.Contains("npgsql") || provider.Contains("postgres"))
        {
            // PostgreSQL専用のSQLテンプレート（演算子は条件分岐で決定）
            var sqlTemplate = regexSpec.PostgresOperator == RegexSearchSpec.PostgresCaseSensitive
                ? """
                  SELECT "Id"
                  FROM "Messages"
                  WHERE "TopicId" = @topicId
                    AND (
                      COALESCE("Header", '') ~ @pattern
                      OR COALESCE("Body", '') ~ @pattern
                    )
                  ORDER BY "CreatedAt" DESC, "Id" DESC
                  LIMIT @take
                  """
                : """
                  SELECT "Id"
                  FROM "Messages"
                  WHERE "TopicId" = @topicId
                    AND (
                      COALESCE("Header", '') ~* @pattern
                      OR COALESCE("Body", '') ~* @pattern
                    )
                  ORDER BY "CreatedAt" DESC, "Id" DESC
                  LIMIT @take
                  """;

            return await QueryRegexIdsSqlAsync(
                topicId,
                take,
                regexSpec.Pattern,
                null,  // MySQLでないためmySqlMatchTypeは不要
                sqlTemplate,
                cancellationToken);
        }

        if (provider.Contains("mysql"))
        {
            // MySQL専用のSQLテンプレート（matchTypeはパラメータとして渡す）
            return await QueryRegexIdsSqlAsync(
                topicId,
                take,
                regexSpec.Pattern,
                regexSpec.MySqlMatchType,
                """
                SELECT `Id`
                FROM `Messages`
                WHERE `TopicId` = @topicId
                  AND (
                    REGEXP_LIKE(COALESCE(`Header`, ''), @pattern, @matchType)
                    OR REGEXP_LIKE(COALESCE(`Body`, ''), @pattern, @matchType)
                  )
                ORDER BY `CreatedAt` DESC, `Id` DESC
                LIMIT @take
                """,
                cancellationToken);
        }

        // Provider fallback: search in memory after topic filtering.
        var regex = new Regex(
            regexSpec.Pattern,
            regexSpec.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(250));

        var candidates = await _messageRepository.Query()
            .Where(m => m.TopicId == topicId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new { m.Id, m.Header, m.Body })
            .Take(Math.Max(take * 10, 500))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(m => regex.IsMatch(m.Header ?? string.Empty) || regex.IsMatch(m.Body))
            .Take(take)
            .Select(m => m.Id)
            .ToList();
    }

    private async Task<List<Guid>> QueryRegexIdsSqlAsync(
        Guid topicId,
        int take,
        string pattern,
        string? mySqlMatchType,
        string sql,
        CancellationToken cancellationToken)
    {
        var ids = new List<Guid>();

        var connection = _dbContext.Database.GetDbConnection();
        var openedByMethod = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
            openedByMethod = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            var currentTransaction = _dbContext.Database.CurrentTransaction;
            if (currentTransaction != null)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            // パラメータを追加（SQLインジェクション対策）
            AddParameter(command, "@topicId", topicId, DbType.Guid);
            AddParameter(command, "@pattern", pattern, DbType.String);
            AddParameter(command, "@take", take, DbType.Int32);

            // MySQLの場合のみmatchTypeを追加
            if (!string.IsNullOrEmpty(mySqlMatchType))
            {
                AddParameter(command, "@matchType", mySqlMatchType, DbType.String);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var raw = reader.GetValue(0);
                if (raw is Guid guid)
                {
                    ids.Add(guid);
                }
                else if (Guid.TryParse(raw.ToString(), out var parsed))
                {
                    ids.Add(parsed);
                }
            }
        }
        finally
        {
            if (openedByMethod)
            {
                await connection.CloseAsync();
            }
        }

        return ids;
    }

    private static void AddParameter(DbCommand command, string name, object value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<Result<MessageDto>> CreateMessageAsync(
        CreateMessageRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // トランザクションを開始してデータ整合性を確保
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var topic = await _topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
                if (topic == null)
                    return Result<MessageDto>.NotFound("Topic not found");

            if (request.ChildTopic?.ParentId.HasValue == true)
            {
                var requestedParentId = (Guid)request.ChildTopic.ParentId.Value;
                var parentTopic = await _topicRepository.GetByIdAsync(requestedParentId, cancellationToken);
                if (parentTopic == null)
                    return Result<MessageDto>.BadRequest("Parent topic not found");

                if (parentTopic.RoomId != topic.RoomId)
                    return Result<MessageDto>.BadRequest("Parent topic must belong to the same room");
            }

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

            TopicDetailDto? createdTopicDto = null;
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

                // 親トピックの権限をコピー（オプション）
                if (childRequest.InheritPermissions)
                {
                    await _topicPermissionManager.CopyPermissionsAsync(parentId, childTopic.Id, cancellationToken);
                }

                // 作成者に管理者権限を付与
                await _topicPermissionManager.GrantCreatorPermissionsAsync(childTopic.Id, roomUser.Id, cancellationToken);

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
                await BroadcastTopicCreatedAsync(createdTopicDto, cancellationToken);

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

            // トランザクションをコミット（ブロードキャスト前に確定）
            await transaction.CommitAsync(cancellationToken);

            // SignalRブロードキャスト（トランザクション外で実行）
            try
            {
                await BroadcastMessageCreatedAsync(dto);
            }
            catch (Exception ex)
            {
                // ブロードキャスト失敗はログに記録するが、メッセージ作成処理は継続
                Logger.LogError(ex, "Failed to broadcast message created for message {MessageId}. SignalR broadcast failed but message was created successfully.", message.Id);
            }

            // プッシュ通知と未読カウント更新（fire-and-forget、トランザクション外で実行）
            _ = SendPushNotificationsAsync(message, roomUser, dto);

            return Result<MessageDto>.Success(dto, 201);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                Logger.LogError(ex, "Failed to create message. Transaction rolled back.");
                throw;
            }
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

            // SignalRブロードキャスト（失敗してもメッセージ更新自体は成功させる）
            try
            {
                await BroadcastMessageUpdatedAsync(dto);
            }
            catch (Exception ex)
            {
                // ブロードキャスト失敗はログに記録するが、メッセージ更新処理は継続
                Logger.LogError(ex, "Failed to broadcast message updated for message {MessageId}. SignalR broadcast failed but message was updated successfully.", messageId);
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

            // SignalRブロードキャスト（失敗してもメッセージ削除自体は成功させる）
            try
            {
                await BroadcastMessageDeletedAsync(message.Id, message.TopicId);
            }
            catch (Exception ex)
            {
                // ブロードキャスト失敗はログに記録するが、メッセージ削除処理は継続
                Logger.LogError(ex, "Failed to broadcast message deleted for message {MessageId}. SignalR broadcast failed but message was deleted successfully.", message.Id);
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

    public async Task<Result<int>> MarkTopicAsReadAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            // トピックの最新メッセージIDを取得
            var latestMessage = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => new { m.Id, m.CreatedAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestMessage == null)
                return Result<int>.Success(0);

            // 既にユーザーがこのメッセージより新しいものを読んでいる場合は未読数を返す
            var existingUserTopic = await _dbContext.UserTopics
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TopicId == topicId, cancellationToken);

            if (existingUserTopic?.LastReadMessageId.HasValue == true)
            {
                var lastReadOrder = await GetMessageOrderKeyAsync(topicId, existingUserTopic.LastReadMessageId.Value, cancellationToken);
                if (lastReadOrder.HasValue
                    && IsMessageOrderAtOrAfter(
                        lastReadOrder.Value.CreatedAt,
                        lastReadOrder.Value.Id,
                        latestMessage.CreatedAt,
                        latestMessage.Id))
                {
                    var unreadCount = await CountUnreadMessagesAsync(topicId, existingUserTopic.LastReadMessageId, cancellationToken);
                    Logger.LogInformation("User {UserId} already has topic {TopicId} marked as read (LastReadMessageId: {LastRead}), unread: {Unread}",
                        userId, topicId, existingUserTopic.LastReadMessageId, unreadCount);
                    return Result<int>.Success(unreadCount);
                }
            }

            // ユーザーのLastReadMessageIdを更新（内部でトランザクションを使用）
            await UpdateUserTopicAccessAsync(topicId, userId, latestMessage.Id, cancellationToken);

            Logger.LogInformation("Successfully marked topic {TopicId} as read for user {UserId} (LastReadMessageId: {Latest})",
                topicId, userId, latestMessage.Id);

            // 同じRoomの同じユーザーの他デバイスへ未読数更新を通知
            await BroadcastTopicUnreadUpdatedAsync(topicId, userId, cancellationToken);

            // 既読にしたので未読数は0
            return Result<int>.Success(0);
        }, nameof(MarkTopicAsReadAsync));
    }

    private async Task<Result<List<MessageDto>>> MoveMessagesBeforeInternalAsync(
        Guid sourceTopicId,
        Guid targetTopicId,
        Guid anchorMessageId,
        bool includeAnchorMessage,
        bool includeEarlierMessages,
        CancellationToken cancellationToken)
    {
        // トランザクションを使用してデータ整合性を確保
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
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
                message.UpdatedAt = DateTime.UtcNow;
                _messageRepository.Update(message);
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

                // トランザクションをコミット（ブロードキャスト前に確定）
                await transaction.CommitAsync(cancellationToken);

                // 子Topicの更新をブロードキャスト
                foreach (var childTopic in childTopics)
                {
                    var topicDto = MapTopicDto(childTopic);
                    await BroadcastTopicUpdatedAsync(topicDto, cancellationToken);
                }
            }
            else
            {
                // 子Topicがない場合もトランザクションをコミット
                await transaction.CommitAsync(cancellationToken);
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            Logger.LogError(ex, "Failed to move messages before anchor. SourceTopicId={SourceTopicId}, TargetTopicId={TargetTopicId}, AnchorMessageId={AnchorMessageId}",
                sourceTopicId, targetTopicId, anchorMessageId);
            throw;
        }
    }

    private async Task<Result<List<MessageDto>>> MoveMessagesByIdsInternalAsync(
        Guid sourceTopicId,
        Guid targetTopicId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        // トランザクションを使用してデータ整合性を確保
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
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
                message.UpdatedAt = DateTime.UtcNow;
                _messageRepository.Update(message);
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

                // トランザクションをコミット（ブロードキャスト前に確定）
                await transaction.CommitAsync(cancellationToken);

                // 子Topicの更新をブロードキャスト
                foreach (var childTopic in childTopics)
                {
                    var topicDto = MapTopicDto(childTopic);
                    await BroadcastTopicUpdatedAsync(topicDto, cancellationToken);
                }
            }
            else
            {
                // 子Topicがない場合もトランザクションをコミット
                await transaction.CommitAsync(cancellationToken);
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            Logger.LogError(ex, "Failed to move messages by IDs. SourceTopicId={SourceTopicId}, TargetTopicId={TargetTopicId}, MessageCount={MessageCount}",
                sourceTopicId, targetTopicId, messageIds.Count);
            throw;
        }
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
                        IsLatest = true
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

        try
        {
            await _roomUserManager.CreateMemberAsync(roomUser, cancellationToken);
            roomUser.ApplicationUser = user;
            return roomUser;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 一意制約違反: 誰かが先に作成したので、再取得して返す
            var retry = await _roomUserRepository.GetByRoomAndUserAsync(roomId, applicationUserId, cancellationToken);
            if (retry != null)
                return retry;
            // 再取得にも失敗した場合は元の例外を再スロー
            throw;
        }
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
            Header = message.Header ?? string.Empty,
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
                    IsLatest = f.IsLatest,
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

    /// <summary>
    /// プッシュ通知を送信（fire-and-forgetで実行）
    /// </summary>
    private async Task SendPushNotificationsAsync(Message message, RoomUser senderRoomUser, MessageDto messageDto)
    {
        Logger.LogInformation("[SendPushNotificationsAsync] Starting for message {MessageId}", message.Id);
        try
        {
            // 新しいスコープを作成してDbContextの破棄を防ぐ（すべてのDBアクセスをスコープ内で行う）
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var topicRepo = scope.ServiceProvider.GetRequiredService<ITopicRepository>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var pushService = scope.ServiceProvider.GetRequiredService<IPushService>();

                var topic = await topicRepo.GetByIdAsync(message.TopicId);
                if (topic == null) return;

                // Roomの全ユーザーを取得（今後はここで閲覧権限チェックを追加）
                var roomUserIds = await dbContext.RoomUsers
                    .Where(ru => ru.RoomId == topic.RoomId)
                    .Select(ru => ru.ApplicationUserId)
                    .Distinct()
                    .ToListAsync();

                // メッセージ送信者を除外
                roomUserIds.Remove(senderRoomUser.ApplicationUserId);

                if (roomUserIds.Count == 0) return;

                // 該当ユーザーのPushSubscriptionを取得
                var pushSubscriptions = await dbContext.PushSubscriptions
                    .Where(ps => roomUserIds.Contains(ps.UserId))
                    .ToListAsync();

                // テナント情報を取得
                var multiTenantContextAccessor = scope.ServiceProvider.GetRequiredService<IMultiTenantContextAccessor<ApplicationTenantInfo>>();
                var tenantId = multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.Identifier ?? "";

                foreach (var subscription in pushSubscriptions)
                {
                    try
                    {
                        var notification = new PushNotificationRequest
                        {
                            Title = $"{RoomUserNameHelper.ResolveDisplayName(senderRoomUser)}: {messageDto.Header?.Truncate(50) ?? "New message"}",
                            Body = messageDto.Body?.Truncate(200) ?? "",
                            Icon = "/pwa-192x192.png",
                            Data = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                tenant = tenantId,
                                topicId = message.TopicId,
                                roomId = topic.RoomId
                            })
                        };

                        var subscriptionDto = new PushSubscriptionDto
                        {
                            Endpoint = subscription.Endpoint,
                            Keys = new PushSubscriptionKeys
                            {
                                P256dh = subscription.P256dhKey,
                                Auth = subscription.AuthKey
                            }
                        };

                        await pushService.SendNotificationAsync(subscriptionDto, notification);

                        // LastUsedAtを更新
                        subscription.LastUsedAt = DateTime.UtcNow;
                    }
                    catch (TreeTopic.Services.SubscriptionExpiredException)
                    {
                        // 購読が無効 - データベースから削除
                        Logger.LogWarning("Invalid subscription {Endpoint}, removing from database", subscription.Endpoint);
                        dbContext.PushSubscriptions.Remove(subscription);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to send push notification to subscription {Endpoint}", subscription.Endpoint);
                    }
                }

                await dbContext.SaveChangesAsync();

                // 未読カウントを更新して通知（送信者以外のRoomUsers）
                var roomUserSyncHub = scope.ServiceProvider.GetRequiredService<IHubContext<RoomUserSyncHub, IRoomUserSyncHubClient>>();
                var maskedUuidService = scope.ServiceProvider.GetRequiredService<IMaskedUUIDService>();

                var roomUsersForUnread = await dbContext.RoomUsers
                    .Where(ru => ru.RoomId == topic.RoomId && ru.Id != senderRoomUser.Id)
                    .ToListAsync();

                var targetUserIds = roomUsersForUnread
                    .Select(ru => ru.ApplicationUserId)
                    .Distinct()
                    .ToList();

                var totalMessageCount = await dbContext.Messages
                    .CountAsync(m => m.TopicId == message.TopicId);

                var userTopics = await dbContext.UserTopics
                    .Where(ut => ut.TopicId == message.TopicId && targetUserIds.Contains(ut.UserId))
                    .ToListAsync();

                var userTopicMap = userTopics.ToDictionary(ut => ut.UserId, ut => ut);

                var validAnchorUserIds = (
                    await (
                        from ut in dbContext.UserTopics
                            .Where(ut =>
                                ut.TopicId == message.TopicId
                                && targetUserIds.Contains(ut.UserId)
                                && ut.LastReadMessageId.HasValue)
                        join anchor in dbContext.Messages
                            on ut.LastReadMessageId equals anchor.Id
                        where anchor.TopicId == message.TopicId
                        select ut.UserId
                    ).Distinct().ToListAsync()
                ).ToHashSet();

                var unreadCountsByUser = await (
                    from m in dbContext.Messages
                    where m.TopicId == message.TopicId
                    join ut in dbContext.UserTopics
                        .Where(ut =>
                            ut.TopicId == message.TopicId
                            && targetUserIds.Contains(ut.UserId)
                            && ut.LastReadMessageId.HasValue)
                        on m.TopicId equals ut.TopicId
                    join anchor in dbContext.Messages
                        on ut.LastReadMessageId equals anchor.Id
                    where anchor.TopicId == message.TopicId
                        && (
                            m.CreatedAt > anchor.CreatedAt
                            || (m.CreatedAt == anchor.CreatedAt && m.Id > anchor.Id)
                        )
                    group m by ut.UserId into g
                    select new { UserId = g.Key, UnreadCount = g.Count() }
                ).ToDictionaryAsync(x => x.UserId, x => x.UnreadCount);

                foreach (var ru in roomUsersForUnread)
                {
                    try
                    {
                        int unreadCount;
                        DateTime? lastReadAt = null;

                        if (userTopicMap.TryGetValue(ru.ApplicationUserId, out var userTopic))
                        {
                            lastReadAt = userTopic.LastAccessAt;

                            if (userTopic.LastReadMessageId.HasValue)
                            {
                                if (unreadCountsByUser.TryGetValue(ru.ApplicationUserId, out var unread))
                                {
                                    unreadCount = unread;
                                }
                                else if (validAnchorUserIds.Contains(ru.ApplicationUserId))
                                {
                                    // アンカーは有効で未読行が0件のケース
                                    unreadCount = 0;
                                }
                                else
                                {
                                    // LastReadMessageId が存在していてもアンカー行が消えていた場合は全件未読扱い
                                    unreadCount = totalMessageCount;
                                }
                            }
                            else
                            {
                                unreadCount = totalMessageCount;
                            }
                        }
                        else
                        {
                            unreadCount = totalMessageCount;
                        }

                        var groupName = RoomUserSyncHubGroups.RoomUser(
                            maskedUuidService.EncodeSynchronous(topic.RoomId),
                            maskedUuidService.EncodeSynchronous(ru.ApplicationUserId));

                        var payload = new TopicUnreadUpdateEvent(
                            topic.RoomId,
                            message.TopicId,
                            unreadCount,
                            lastReadAt);

                        await roomUserSyncHub.Clients.Group(groupName).TopicUnreadUpdated(payload);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to broadcast unread update for user {UserId} topic {TopicId}", ru.ApplicationUserId, message.TopicId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to send push notifications for message {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// ユーザーがトピックにアクセスしたとき、UserTopicを更新
    /// </summary>
    private async Task UpdateUserTopicAccessAsync(Guid topicId, Guid userId, Guid? messageId, CancellationToken cancellationToken)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // UserTopicを取得（追い出し可能ロックを使用）
            var userTopic = await _dbContext.UserTopics
                .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TopicId == topicId, cancellationToken);

            if (userTopic != null)
            {
                var nextLastReadMessageId = userTopic.LastReadMessageId;
                if (messageId.HasValue)
                {
                    if (!userTopic.LastReadMessageId.HasValue)
                    {
                        nextLastReadMessageId = messageId;
                    }
                    else if (userTopic.LastReadMessageId.Value == messageId.Value)
                    {
                        nextLastReadMessageId = messageId;
                    }
                    else
                    {
                        var currentOrder = await GetMessageOrderKeyAsync(topicId, userTopic.LastReadMessageId.Value, cancellationToken);
                        var candidateOrder = await GetMessageOrderKeyAsync(topicId, messageId.Value, cancellationToken);

                        if (candidateOrder.HasValue)
                        {
                            if (!currentOrder.HasValue
                                || IsMessageOrderAtOrAfter(
                                    candidateOrder.Value.CreatedAt,
                                    candidateOrder.Value.Id,
                                    currentOrder.Value.CreatedAt,
                                    currentOrder.Value.Id))
                            {
                                nextLastReadMessageId = messageId;
                            }
                        }
                    }
                }

                userTopic.LastReadMessageId = nextLastReadMessageId;
                userTopic.LastAccessAt = DateTime.UtcNow;
            }
            else
            {
                userTopic = new UserTopic
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    TopicId = topicId,
                    LastReadMessageId = messageId,
                    LastAccessAt = DateTime.UtcNow,
                    IsAccessible = null
                };
                _dbContext.UserTopics.Add(userTopic);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            Logger.LogError(ex, "Failed to update UserTopic access for topic {TopicId}, user {UserId}", topicId, userId);
            throw;
        }
    }

    /// <summary>
    /// DbUpdateExceptionがユニーク制約違反かどうかを判定
    /// PostgreSQL: 23505, MySQL: 1062
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? string.Empty;
        return message.Contains("23505") || message.Contains("1062");
    }
}

public static class StringExtensions
{
    public static string? Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
