using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Repositories;
using TreeTopic.Filters;
using TreeTopic.Permissions;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class FileController : ControllerBase
{
    private readonly IFileManagementService _fileManagementService;
    private readonly IWebHostEnvironment _environment;
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public FileController(
        IFileManagementService fileManagementService,
        IWebHostEnvironment environment,
        IRoomUserRepository roomUserRepository)
    {
        _fileManagementService = fileManagementService;
        _environment = environment;
        _roomUserRepository = roomUserRepository;
    }

    private Guid CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or has invalid user ID.");
            }
            return userId;
        }
    }

    private string CurrentUserName =>
        User.FindFirst(ClaimTypes.Name)?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? User.Identity?.Name
        ?? "Unknown";

    private async Task<string> GetRoomUserDisplayNameAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var roomUser = await _roomUserRepository.GetByRoomAndUserAsync(roomId, CurrentUserId, cancellationToken);
        if (roomUser == null)
            return CurrentUserName;

        return RoomUserNameHelper.ResolveDisplayName(roomUser);
    }

    private static string GetFileType(string fileName, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        var ext = Path.GetExtension(fileName);
        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";

        var docExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".rtf", ".csv"
        };

        if (docExts.Contains(ext))
            return "document";

        return "other";
    }

    private sealed record RoomMaterialDto(
        string Id,
        string RoomId,
        string? MessageId,
        string FileName,
        string OriginalFileName,
        string MimeType,
        long Size,
        string Url,
        string FileType,
        DateTime UploadedAt,
        string UploadedBy,
        string UploadedByName,
        IReadOnlyList<object> Versions,
        bool IsArchived
    );

    [HttpGet]
    [RequireAny(PermissionScope.Role, TenantPermissions.RoomRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetAllFilesAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("message/{messageId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetByMessage([FromRoute] MaskedGuid messageId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFilesByMessageAsync((Guid)messageId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{fileId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.ReadMessages, TenantPermissions.TopicReadMessages, RoomPermissions.TopicMessageRead)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.GetFileByIdAsync((Guid)fileId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("room/{roomId}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Read, TenantPermissions.RoomRead)]
    public IActionResult GetByRoom([FromRoute] MaskedGuid roomId)
    {
        var tenant = RouteData.Values["tenant"]?.ToString() ?? "default";
        var webRoot = _environment.ContentRootPath;
        var roomDir = Path.Combine(webRoot, "uploads", tenant, roomId.ToString());

        if (!Directory.Exists(roomDir))
            return Ok(Array.Empty<RoomMaterialDto>());

        var list = Directory.EnumerateFiles(roomDir)
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Select(fileInfo =>
            {
                var storedName = fileInfo.Name;
                var id = Guid.NewGuid().ToString();
                var originalFileName = storedName;

                if (storedName.Length > 33 && storedName[32] == '_' &&
                    Guid.TryParseExact(storedName.Substring(0, 32), "N", out var parsedId))
                {
                    id = parsedId.ToString();
                    originalFileName = storedName.Substring(33);
                }

                var mime = _contentTypeProvider.TryGetContentType(originalFileName, out var ct)
                    ? ct
                    : "application/octet-stream";

                // 認可付きダウンロードエンドポイントを使用
                var url = $"/{tenant}/api/file/download/{roomId}/{storedName}".Replace("\\", "/");

                return new RoomMaterialDto(
                    Id: id,
                    RoomId: roomId.ToString(),
                    MessageId: null,
                    FileName: originalFileName,
                    OriginalFileName: originalFileName,
                    MimeType: mime,
                    Size: fileInfo.Length,
                    Url: url,
                    FileType: GetFileType(originalFileName, mime),
                    UploadedAt: fileInfo.CreationTimeUtc,
                    UploadedBy: Guid.Empty.ToString(),
                    UploadedByName: "Unknown",
                    Versions: Array.Empty<object>(),
                    IsArchived: false
                );
            })
            .ToList();

        return Ok(list);
    }

    [HttpPost("room/{roomId}")]
    [Consumes("multipart/form-data")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Write, TenantPermissions.RoomManage)]
    public async Task<IActionResult> UploadToRoom(
        [FromRoute] MaskedGuid roomId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var tenant = RouteData.Values["tenant"]?.ToString() ?? "default";
        var webRoot = _environment.ContentRootPath;
        var roomDir = Path.Combine(webRoot, "uploads", tenant, roomId.ToString());
        Directory.CreateDirectory(roomDir);

        var originalFileName = Path.GetFileName(file.FileName);
        var id = Guid.NewGuid();
        var savedFileName = $"{id:N}_{originalFileName}";
        var savePath = Path.Combine(roomDir, savedFileName);

        // ファイル保存前にパスを記録（エラー時のクリア用）
        var fileCreated = false;
        try
        {
            await using (var stream = System.IO.File.Create(savePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
            fileCreated = true;

            var mime = !string.IsNullOrWhiteSpace(file.ContentType)
                ? file.ContentType
                : (_contentTypeProvider.TryGetContentType(originalFileName, out var ct)
                    ? ct
                    : "application/octet-stream");

            // 認可付きダウンロードエンドポイントを使用
            var url = $"/{tenant}/api/file/download/{roomId}/{savedFileName}".Replace("\\", "/");

            var roomUserName = await GetRoomUserDisplayNameAsync((Guid)roomId, cancellationToken);
            var dto = new RoomMaterialDto(
                Id: id.ToString(),
                RoomId: roomId.ToString(),
                MessageId: null,
                FileName: originalFileName,
                OriginalFileName: originalFileName,
                MimeType: mime,
                Size: file.Length,
                Url: url,
                FileType: GetFileType(originalFileName, mime),
                UploadedAt: DateTime.UtcNow,
                UploadedBy: CurrentUserId.ToString(),
                UploadedByName: roomUserName,
                Versions: Array.Empty<object>(),
                IsArchived: false
            );

            return Ok(dto);
        }
        catch
        {
            // DB保存失敗時やエラー時にファイルを削除
            if (fileCreated && System.IO.File.Exists(savePath))
            {
                try
                {
                    System.IO.File.Delete(savePath);
                }
                catch
                {
                    // ファイル削除に失敗しても無視
                }
            }
            throw;
        }
    }

    [HttpPost]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageWrite)]
    public async Task<IActionResult> Create([FromBody] CreateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.CreateFileAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{fileId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageManage)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid fileId, [FromBody] UpdateFileRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _fileManagementService.UpdateFileAsync((Guid)fileId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{fileId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.WriteMessages, TenantPermissions.TopicWriteMessages, RoomPermissions.TopicMessageManage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid fileId, CancellationToken cancellationToken)
    {
        var result = await _fileManagementService.DeleteFileAsync(fileId, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// 認可付きファイルダウンロードエンドポイント
    /// /uploads 静的配信の代わりに使用
    /// </summary>
    [HttpGet("download/{roomId}/{fileName}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Read, TenantPermissions.RoomRead)]
    public async Task<IActionResult> DownloadFile(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string fileName,
        CancellationToken cancellationToken)
    {
        var tenant = RouteData.Values["tenant"]?.ToString() ?? "default";
        var webRoot = _environment.ContentRootPath;
        var roomRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads", tenant, roomId.ToString()));
        var safeFileName = Path.GetFileName(fileName);
        if (!string.Equals(fileName, safeFileName, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Invalid file name." });
        }

        if (!roomRoot.EndsWith(Path.DirectorySeparatorChar))
            roomRoot += Path.DirectorySeparatorChar;
        var filePath = Path.GetFullPath(Path.Combine(roomRoot, safeFileName));
        if (!filePath.StartsWith(roomRoot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Invalid file path." });
        }

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "File not found." });
        }

        // Content-Type を決定
        string contentType;
        if (_contentTypeProvider.TryGetContentType(safeFileName, out var providerContentType))
        {
            contentType = providerContentType;
        }
        else
        {
            contentType = "application/octet-stream";
        }

        // ファイルをストリームで返す
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(fileStream, contentType);
    }
}
