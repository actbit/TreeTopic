using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Services;

public interface IMessageManagementService
{
    Task<Result<List<MessageDto>>> GetAllMessagesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> CreateMessageAsync(CreateMessageRequest request, Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> DeleteMessageAsync(Guid messageId, Guid tenantId, CancellationToken cancellationToken = default);
}

public class MessageManagementService : BaseService, IMessageManagementService
{
    private readonly IMessageRepository _messageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IFileRepository _fileRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public MessageManagementService(
        IMessageRepository messageRepository,
        ITopicRepository topicRepository,
        IFileRepository fileRepository,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment webHostEnvironment,
        ILogger<MessageManagementService> logger) : base(logger)
    {
        _messageRepository = messageRepository;
        _topicRepository = topicRepository;
        _fileRepository = fileRepository;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<Result<List<MessageDto>>> GetAllMessagesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Where(m => m.TenantId == tenantId.ToString())
                .Include(m => m.ApplicationUser)
                .ToListAsync(cancellationToken);

            var dtos = messages.Select(MapToDto).ToList();
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetAllMessagesAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId && m.TenantId == tenantId.ToString())
                .Include(m => m.ApplicationUser)
                .ToListAsync(cancellationToken);

            var dtos = messages.Select(MapToDto).ToList();
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesByTopicAsync));
    }

    public async Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null || message.TenantId != tenantId.ToString())
                return Result<MessageDto>.NotFound("Message not found");

            var dto = MapToDto(message);
            return Result<MessageDto>.Success(dto);
        }, nameof(GetMessageByIdAsync));
    }

    public async Task<Result<MessageDto>> CreateMessageAsync(
        CreateMessageRequest request,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
            if (topic == null || topic.TenantId != tenantId.ToString())
                return Result<MessageDto>.NotFound("Topic not found");

            if (request.ReplyId.HasValue && request.ReplyId != Guid.Empty)
            {
                var replyTo = await _messageRepository.GetByIdAsync(request.ReplyId.Value, cancellationToken);
                if (replyTo == null || replyTo.TenantId != tenantId.ToString())
                    return Result<MessageDto>.NotFound("Reply message not found");
            }

            var message = new Message
            {
                TopicId = request.TopicId,
                ApplicationUserId = userId,
                Header = request.Header,
                Body = request.Body,
                ReplyId = request.ReplyId ?? Guid.Empty,
                TenantId = tenantId.ToString()
            };

            await _messageRepository.AddAsync(message, cancellationToken);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            // ファイルがアップロードされている場合、処理する
            if (request.Files != null && request.Files.Count > 0)
            {
                await ProcessUploadedFilesAsync(message, request.Files, tenantId, cancellationToken);
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            message.ApplicationUser = user;

            var dto = MapToDto(message);
            return Result<MessageDto>.Success(dto, 201);
        }, nameof(CreateMessageAsync));
    }

    public async Task<Result<MessageDto>> UpdateMessageAsync(
        Guid messageId,
        UpdateMessageRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null || message.TenantId != tenantId.ToString())
                return Result<MessageDto>.NotFound("Message not found");

            if (!string.IsNullOrEmpty(request.Header))
                message.Header = request.Header;

            if (!string.IsNullOrEmpty(request.Body))
                message.Body = request.Body;

            message.UpdatedAt = DateTime.UtcNow;
            _messageRepository.Update(message);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(message);
            return Result<MessageDto>.Success(dto);
        }, nameof(UpdateMessageAsync));
    }

    public async Task<Result> DeleteMessageAsync(Guid messageId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null || message.TenantId != tenantId.ToString())
                return Result.NotFound("Message not found");

            _messageRepository.Delete(message);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }, nameof(DeleteMessageAsync));
    }

    private async Task ProcessUploadedFilesAsync(
        Message message,
        List<IFormFile> files,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var uploadPath = Path.Combine(_webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath, "uploads", tenantId.ToString());

            // ディレクトリが存在しない場合は作成
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    // ファイル名を生成（衝突を避けるため、タイムスタンプとGUIDを使用）
                    var fileExtension = Path.GetExtension(file.FileName);
                    var savedFileName = $"{Guid.NewGuid()}_{DateTime.UtcNow.Ticks}{fileExtension}";
                    var filePath = Path.Combine(uploadPath, savedFileName);

                    // ファイルを保存
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream, cancellationToken);
                    }

                    // ファイルエンティティを作成
                    var fileEntity = new FileModel
                    {
                        FileName = file.FileName,
                        SaveFileName = savedFileName,
                        FileType = file.ContentType ?? "application/octet-stream",
                        MessageId = message.Id,
                        SourceFileId = null,
                        SourceFile = null,
                        IsLatast = true,
                        TenantId = tenantId.ToString()
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

    private static MessageDto MapToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            TenantId = Guid.Parse(message.TenantId),
            TopicId = message.TopicId,
            ApplicationUserId = message.ApplicationUserId,
            UserName = message.ApplicationUser?.UserName,
            Header = message.Header,
            Body = message.Body,
            ReplyId = message.ReplyId != Guid.Empty ? message.ReplyId : null,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt
        };
    }
}
