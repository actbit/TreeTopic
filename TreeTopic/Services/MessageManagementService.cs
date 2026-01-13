using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Services;

public interface IMessageManagementService
{
    Task<Result<List<MessageDto>>> GetAllMessagesAsync(CancellationToken cancellationToken = default);
    Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> CreateMessageAsync(CreateMessageRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<MessageDto>> UpdateMessageAsync(Guid messageId, UpdateMessageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
}

public class MessageManagementService : BaseService, IMessageManagementService
{
    private readonly IMessageRepository _messageRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IFileRepository _fileRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public MessageManagementService(
        IMessageRepository messageRepository,
        ITopicRepository topicRepository,
        IFileRepository fileRepository,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment webHostEnvironment,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<MessageManagementService> logger) : base(logger)
    {
        _messageRepository = messageRepository;
        _topicRepository = topicRepository;
        _fileRepository = fileRepository;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
        _tenantAccessor = tenantAccessor;
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
        var webRoot = _webHostEnvironment.WebRootPath ?? _webHostEnvironment.ContentRootPath;
        return Path.Combine(webRoot, "uploads", GetTenantUploadsFolderName());
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

    public async Task<Result<List<MessageDto>>> GetAllMessagesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Include(m => m.ApplicationUser)
                .Include(m => m.Files)
                .ToListAsync(cancellationToken);

            var dtos = messages.Select(MapToDto).ToList();
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetAllMessagesAsync));
    }

    public async Task<Result<List<MessageDto>>> GetMessagesByTopicAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var messages = await _messageRepository.Query()
                .Where(m => m.TopicId == topicId)
                .Include(m => m.ApplicationUser)
                .Include(m => m.Files)
                .ToListAsync(cancellationToken);

            var dtos = messages.Select(MapToDto).ToList();
            return Result<List<MessageDto>>.Success(dtos);
        }, nameof(GetMessagesByTopicAsync));
    }

    public async Task<Result<MessageDto>> GetMessageByIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.Query()
                .Where(m => m.Id == messageId)
                .Include(m => m.ApplicationUser)
                .Include(m => m.Files)
                .FirstOrDefaultAsync(cancellationToken);

            if (message == null)
                return Result<MessageDto>.NotFound("Message not found");

            var dto = MapToDto(message);
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

            var message = new Message
            {
                TopicId = request.TopicId,
                ApplicationUserId = userId,
                Header = request.Header,
                Body = request.Body,
                ReplyId = request.ReplyId.HasValue && request.ReplyId != Guid.Empty
                    ? request.ReplyId
                    : null
            };

            await _messageRepository.AddAsync(message, cancellationToken);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            if (request.Files != null && request.Files.Count > 0)
            {
                await ProcessUploadedFilesAsync(message, request.Files, cancellationToken);
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            message.ApplicationUser = user;

            // Ensure files are available on response DTO.
            message.Files = await _fileRepository.Query()
                .Where(f => f.MessageId == message.Id)
                .ToListAsync(cancellationToken);

            var dto = MapToDto(message);
            return Result<MessageDto>.Success(dto, 201);
        }, nameof(CreateMessageAsync));
    }

    public async Task<Result<MessageDto>> UpdateMessageAsync(
        Guid messageId,
        UpdateMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null)
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

    public async Task<Result> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var message = await _messageRepository.GetByIdAsync(messageId, cancellationToken);

            if (message == null)
                return Result.NotFound("Message not found");

            _messageRepository.Delete(message);
            await _messageRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }, nameof(DeleteMessageAsync));
    }

    private async Task ProcessUploadedFilesAsync(
        Message message,
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        try
        {
            var uploadPath = GetMessageUploadsPath(message.ApplicationUserId, message.Id);
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

    private MessageDto MapToDto(Message message)
    {
        return new MessageDto
        {
            Id = message.Id,
            TopicId = message.TopicId,
            ApplicationUserId = message.ApplicationUserId,
            UserName = message.ApplicationUser?.UserName,
            Header = message.Header,
            Body = message.Body,
            ReplyId = message.ReplyId != Guid.Empty ? message.ReplyId : null,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt,
            Files = message.Files?.Select(f =>
            {
                var newPath = Path.Combine(GetMessageUploadsPath(message.ApplicationUserId, message.Id), f.SaveFileName);
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
                        ? BuildMessageUploadUrl(message.ApplicationUserId, message.Id, f.SaveFileName)
                        : BuildLegacyUploadUrl(f.SaveFileName)
                };
            }).ToList()
        };
    }
}
