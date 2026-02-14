using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Repositories;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Services;

public interface IFileManagementService
{
    Task<Result<List<FileDto>>> GetAllFilesAsync(CancellationToken cancellationToken = default);
    Task<Result<List<FileDto>>> GetFilesByMessageAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task<Result<FileDto>> GetFileByIdAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task<Result<FileDto>> CreateFileAsync(CreateFileRequest request, CancellationToken cancellationToken = default);
    Task<Result<FileDto>> UpdateFileAsync(Guid fileId, UpdateFileRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public class FileManagementService : BaseService, IFileManagementService
{
    private readonly IFileRepository _fileRepository;
    private readonly IMessageRepository _messageRepository;

    public FileManagementService(
        IFileRepository fileRepository,
        IMessageRepository messageRepository,
        ILogger<FileManagementService> logger) : base(logger)
    {
        _fileRepository = fileRepository;
        _messageRepository = messageRepository;
    }

    public async Task<Result<List<FileDto>>> GetAllFilesAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var files = await _fileRepository.Query()
                .ToListAsync(cancellationToken);

            var dtos = files.Select(MapToDto).ToList();
            return Result<List<FileDto>>.Success(dtos);
        }, nameof(GetAllFilesAsync));
    }

    public async Task<Result<List<FileDto>>> GetFilesByMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var files = await _fileRepository.Query()
                .Where(f => f.MessageId == messageId)
                .ToListAsync(cancellationToken);

            var dtos = files.Select(MapToDto).ToList();
            return Result<List<FileDto>>.Success(dtos);
        }, nameof(GetFilesByMessageAsync));
    }

    public async Task<Result<FileDto>> GetFileByIdAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);

            if (file == null)
                return Result<FileDto>.NotFound("File not found");

            var dto = MapToDto(file);
            return Result<FileDto>.Success(dto);
        }, nameof(GetFileByIdAsync));
    }

    public async Task<Result<FileDto>> CreateFileAsync(
        CreateFileRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            if (request.MessageId.HasValue && request.MessageId != Guid.Empty)
            {
                var message = await _messageRepository.GetByIdAsync(request.MessageId.Value, cancellationToken);
                if (message == null)
                    return Result<FileDto>.NotFound("Message not found");
            }

            if (request.SourceFileId.HasValue && request.SourceFileId != Guid.Empty)
            {
                var sourceFile = await _fileRepository.GetByIdAsync(request.SourceFileId.Value, cancellationToken);
                if (sourceFile == null)
                    return Result<FileDto>.NotFound("Source file not found");
            }

            var file = new FileModel
            {
                FileName = request.FileName,
                SaveFileName = request.SaveFileName,
                FileType = request.FileType,
                MessageId = request.MessageId ?? Guid.Empty,
                SourceFileId = request.SourceFileId ?? Guid.Empty,
                SourceFile = null,
                IsLatest = true
            };

            await _fileRepository.AddAsync(file, cancellationToken);
            await _fileRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(file);
            return Result<FileDto>.Success(dto, 201);
        }, nameof(CreateFileAsync));
    }

    public async Task<Result<FileDto>> UpdateFileAsync(
        Guid fileId,
        UpdateFileRequest request,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);

            if (file == null)
                return Result<FileDto>.NotFound("File not found");

            if (!string.IsNullOrEmpty(request.FileName))
                file.FileName = request.FileName;

            if (!string.IsNullOrEmpty(request.FileType))
                file.FileType = request.FileType;

            file.UpdatedAt = DateTime.UtcNow;
            _fileRepository.Update(file);
            await _fileRepository.SaveChangesAsync(cancellationToken);

            var dto = MapToDto(file);
            return Result<FileDto>.Success(dto);
        }, nameof(UpdateFileAsync));
    }

    public async Task<Result> DeleteFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var file = await _fileRepository.GetByIdAsync(fileId, cancellationToken);

            if (file == null)
                return Result.NotFound("File not found");

            _fileRepository.Delete(file);
            await _fileRepository.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }, nameof(DeleteFileAsync));
    }

    private static FileDto MapToDto(FileModel file)
    {
        return new FileDto
        {
            Id = file.Id,
            FileName = file.FileName,
            SaveFileName = file.SaveFileName,
            FileType = file.FileType,
            MessageId = file.MessageId != Guid.Empty ? file.MessageId : null,
            SourceFileId = file.SourceFileId != Guid.Empty ? file.SourceFileId : null,
            IsLatest = file.IsLatest,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt,
            Size = 0,
            Url = string.Empty
        };
    }
}
