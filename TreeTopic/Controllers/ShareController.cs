using System.Security.Claims;
using MaskedUUID.AspNetCore.Services;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
using TreeTopic.Common;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using FileModel = TreeTopic.Models.File;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class ShareController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IMaskedUUIDService _maskedUuidService;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public ShareController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IMaskedUUIDService maskedUuidService)
    {
        _db = db;
        _environment = environment;
        _maskedUuidService = maskedUuidService;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    private string CurrentUserName =>
        User.FindFirst(ClaimTypes.Name)?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? User.Identity?.Name
        ?? "Unknown";

    private string GetTenantIdentifier()
        => RouteData.Values["tenant"]?.ToString() ?? "default";

    private string GetShareItemFolder(string tenant, Guid shareItemId)
    {
        var webRoot = _environment.ContentRootPath;
        return Path.Combine(webRoot, "uploads", tenant, "share", shareItemId.ToString());
    }

    private string BuildShareFileUrl(string tenant, Guid shareItemId, string savedFileName)
        => $"/uploads/{tenant}/share/{shareItemId}/{savedFileName}".Replace("\\", "/");

    private string BuildBrainstormUrl(string tenant, Guid boardId)
    {
        var masked = _maskedUuidService.EncodeSynchronous(boardId);
        return $"/{tenant}/brainstorm/{masked}".Replace("\\", "/");
    }

    private static string NormalizeKind(string? kind, string mimeType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(kind))
            return kind.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(mimeType) &&
            mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        var ext = Path.GetExtension(fileName);
        if (string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            return "document";

        return "document";
    }

    private async Task<RoomUser?> GetRoomUserAsync(Guid roomId, CancellationToken cancellationToken)
    {
        return await _db.RoomUsers
            .Include(ru => ru.ApplicationUser)
            .FirstOrDefaultAsync(
                ru => ru.RoomId == roomId && ru.ApplicationUserId == CurrentUserId,
                cancellationToken);
    }

    private async Task<string> GetRoomUserDisplayNameAsync(Guid roomId, CancellationToken cancellationToken)
    {
        var roomUser = await GetRoomUserAsync(roomId, cancellationToken);
        if (roomUser == null)
            return CurrentUserName;

        return RoomUserNameHelper.ResolveDisplayName(roomUser);
    }

    private sealed record ShareItemUserDto(
        string Id,
        string? Name,
        string? DisplayName
    );

    private sealed record ShareItemMessageDto(
        string Id,
        string Header
    );

    private sealed record ShareItemFileDto(
        string Id,
        string FileName
    );

    private sealed record ShareItemShareDto(
        string Id,
        string Title
    );

    private sealed record ShareItemDto(
        string Id,
        string RoomId,
        string? TopicId,
        string Kind,
        string? BoardId,
        string Title,
        string FileName,
        string MimeType,
        long Size,
        string Url,
        DateTime CreatedAt,
        ShareItemUserDto? CreatedByUser,
        string CreatedByName,
        ShareItemMessageDto? SourceMessage,
        ShareItemFileDto? SourceFile,
        ShareItemShareDto? SourceShareItem
    );

    public sealed record CreateBrainstormShareRequest(
        MaskedGuid? TopicId,
        MaskedGuid BoardId,
        string? Title
    );

    private ShareItemDto ToDto(
        ShareItem share,
        string tenant,
        (FileModel file, string url, long size)? currentFile)
    {
        var id = _maskedUuidService.EncodeSynchronous(share.Id);
        var roomId = _maskedUuidService.EncodeSynchronous(share.RoomId);
        var topicId = share.TopicId.HasValue ? _maskedUuidService.EncodeSynchronous(share.TopicId.Value) : null;
        var boardId = share.BrainBoardId.HasValue ? _maskedUuidService.EncodeSynchronous(share.BrainBoardId.Value) : null;

        var fileName = currentFile?.file.FileName ?? string.Empty;
        var mimeType = currentFile?.file.FileType ?? string.Empty;
        var size = currentFile?.size ?? 0;
        var url = currentFile?.url
                  ?? (share.Kind == "brainstorm" && share.BrainBoardId.HasValue
                      ? BuildBrainstormUrl(tenant, share.BrainBoardId.Value)
                      : string.Empty);

        ShareItemUserDto? createdByUser = null;
        if (share.CreatedByRoomUser != null)
        {
            createdByUser = new ShareItemUserDto(
                Id: _maskedUuidService.EncodeSynchronous(share.CreatedByRoomUser.Id),
                Name: share.CreatedByRoomUser.Name,
                DisplayName: RoomUserNameHelper.ResolveDisplayName(share.CreatedByRoomUser)
            );
        }

        ShareItemMessageDto? sourceMessage = null;
        if (share.SourceMessage != null)
        {
            sourceMessage = new ShareItemMessageDto(
                Id: _maskedUuidService.EncodeSynchronous(share.SourceMessage.Id),
                Header: share.SourceMessage.Header
            );
        }

        ShareItemFileDto? sourceFile = null;
        if (share.SourceFile != null)
        {
            sourceFile = new ShareItemFileDto(
                Id: _maskedUuidService.EncodeSynchronous(share.SourceFile.Id),
                FileName: share.SourceFile.FileName
            );
        }

        ShareItemShareDto? sourceShare = null;
        if (share.SourceShareItem != null)
        {
            sourceShare = new ShareItemShareDto(
                Id: _maskedUuidService.EncodeSynchronous(share.SourceShareItem.Id),
                Title: share.SourceShareItem.Title
            );
        }

        return new ShareItemDto(
            Id: id,
            RoomId: roomId,
            TopicId: topicId,
            Kind: share.Kind,
            BoardId: share.Kind == "brainstorm" ? boardId : null,
            Title: share.Title,
            FileName: fileName,
            MimeType: mimeType,
            Size: size,
            Url: url,
            CreatedAt: share.CreatedAt,
            CreatedByUser: createdByUser,
            CreatedByName: share.CreatedByName,
            SourceMessage: sourceMessage,
            SourceFile: sourceFile,
            SourceShareItem: sourceShare
        );
    }

    [HttpGet("room/{roomId}")]
    [RequirePermission(RoomPermissions.Join)]
    public async Task<IActionResult> GetByRoom(
        [FromRoute] MaskedGuid roomId,
        [FromQuery] MaskedGuid? topicId,
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        var tenant = GetTenantIdentifier();
        var roomGuid = (Guid)roomId;
        var topicGuid = topicId.HasValue && (Guid)topicId.Value != Guid.Empty ? (Guid)topicId.Value : (Guid?)null;
        var normalizedKind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().ToLowerInvariant();

        var shareQuery = _db.ShareItems.AsNoTracking().Where(s => s.RoomId == roomGuid);
        if (topicGuid.HasValue)
            shareQuery = shareQuery.Where(s => s.TopicId == topicGuid);
        if (!string.IsNullOrWhiteSpace(normalizedKind))
            shareQuery = shareQuery.Where(s => s.Kind == normalizedKind);

        var shares = await shareQuery
            .Include(s => s.CreatedByRoomUser)
            .ThenInclude(ru => ru.ApplicationUser)
            .Include(s => s.SourceMessage)
            .Include(s => s.SourceFile)
            .Include(s => s.SourceShareItem)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var shareIds = shares.Select(s => s.Id).ToList();

        var currentLinks = await _db.ShareItemFiles.AsNoTracking()
            .Where(x => shareIds.Contains(x.ShareItemId) && x.IsCurrent)
            .ToListAsync(cancellationToken);

        var fileIds = currentLinks.Select(x => x.FileId).Distinct().ToList();
        var files = await _db.Files.AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(cancellationToken);

        var fileMap = files.ToDictionary(f => f.Id, f => f);
        var linkMap = currentLinks.ToDictionary(l => l.ShareItemId, l => l);

        var dtos = shares.Select(share =>
        {
            if (linkMap.TryGetValue(share.Id, out var link) && fileMap.TryGetValue(link.FileId, out var file))
            {
                var dir = GetShareItemFolder(tenant, share.Id);
                var path = Path.Combine(dir, file.SaveFileName);
                long size = 0;
                try
                {
                    if (System.IO.File.Exists(path))
                        size = new FileInfo(path).Length;
                }
                catch
                {
                    size = 0;
                }

                var url = BuildShareFileUrl(tenant, share.Id, file.SaveFileName);
                return ToDto(share, tenant, (file, url, size));
            }

            return ToDto(share, tenant, null);
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("room/{roomId}")]
    [Consumes("multipart/form-data")]
    [RequirePermission(RoomPermissions.Write)]
    public async Task<IActionResult> UploadToRoom(
        [FromRoute] MaskedGuid roomId,
        [FromForm] IFormFile file,
        [FromForm] MaskedGuid? topicId,
        [FromForm] string? kind,
        [FromForm] string? title,
        [FromForm] MaskedGuid? shareId,
        [FromForm] bool? updateShare,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var tenant = GetTenantIdentifier();
        var roomGuid = (Guid)roomId;
        var topicGuid = topicId.HasValue && (Guid)topicId.Value != Guid.Empty ? (Guid)topicId.Value : (Guid?)null;

        var originalFileName = Path.GetFileName(file.FileName);

        var mime = !string.IsNullOrWhiteSpace(file.ContentType)
            ? file.ContentType
            : (_contentTypeProvider.TryGetContentType(originalFileName, out var ct)
                ? ct
                : "application/octet-stream");

        var normalizedKind = NormalizeKind(kind, mime, originalFileName);
        if (normalizedKind is not ("image" or "document"))
            normalizedKind = "document";

        var shouldUpdateShare = updateShare ?? true;

        ShareItem targetShare;
        ShareItem? baseShare = null;
        if (shareId.HasValue && (Guid)shareId.Value != Guid.Empty)
        {
            baseShare = await _db.ShareItems.FirstOrDefaultAsync(s => s.Id == (Guid)shareId.Value, cancellationToken);
            if (baseShare == null)
                return NotFound(new { message = "Share not found." });

            if (!string.Equals(baseShare.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Share kind mismatch." });
        }

        if (baseShare != null && !shouldUpdateShare)
        {
            var roomUser = await GetRoomUserAsync(baseShare.RoomId, cancellationToken);
            if (roomUser == null)
                return Unauthorized(new { message = "Room user not found." });

            var roomUserName = RoomUserNameHelper.ResolveDisplayName(roomUser);
            // Create a new share entry for the new version, copying metadata from the selected share.
            targetShare = new ShareItem
            {
                RoomId = baseShare.RoomId,
                TopicId = baseShare.TopicId,
                Kind = baseShare.Kind,
                Title = string.IsNullOrWhiteSpace(title) ? baseShare.Title : title.Trim(),
                CreatedByRoomUserId = roomUser.Id,
                CreatedByName = roomUserName,
                SourceMessageId = baseShare.SourceMessageId,
                SourceFileId = baseShare.SourceFileId,
                SourceShareItemId = baseShare.SourceShareItemId
            };
            _db.ShareItems.Add(targetShare);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else if (baseShare != null)
        {
            targetShare = baseShare;
            if (!string.IsNullOrWhiteSpace(title))
                targetShare.Title = title.Trim();
            targetShare.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var roomUser = await GetRoomUserAsync(roomGuid, cancellationToken);
            if (roomUser == null)
                return Unauthorized(new { message = "Room user not found." });

            var roomUserName = RoomUserNameHelper.ResolveDisplayName(roomUser);
            targetShare = new ShareItem
            {
                RoomId = roomGuid,
                TopicId = topicGuid,
                Kind = normalizedKind,
                Title = string.IsNullOrWhiteSpace(title) ? originalFileName : title.Trim(),
                CreatedByRoomUserId = roomUser.Id,
                CreatedByName = roomUserName,
            };
            _db.ShareItems.Add(targetShare);
            await _db.SaveChangesAsync(cancellationToken);
        }

        // Resolve current file for version chain (if any).
        FileModel? currentFile = null;
        if (baseShare != null)
        {
            var currentLink = await _db.ShareItemFiles
                .Where(x => x.ShareItemId == baseShare.Id && x.IsCurrent)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentLink != null)
                currentFile = await _db.Files.FirstOrDefaultAsync(f => f.Id == currentLink.FileId, cancellationToken);
        }

        var fileId = Guid.CreateVersion7();
        var savedFileName = $"{fileId:N}_{originalFileName}";

        var shareDir = GetShareItemFolder(tenant, targetShare.Id);
        Directory.CreateDirectory(shareDir);
        var savePath = Path.Combine(shareDir, savedFileName);

        await using (var stream = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var fileEntity = new FileModel
        {
            Id = fileId,
            FileName = originalFileName,
            SaveFileName = savedFileName,
            FileType = mime,
            MessageId = null,
            SourceFileId = currentFile?.Id,
            SourceFile = null,
            IsLatast = true
        };

        if (currentFile != null)
            currentFile.IsLatast = false;

        _db.Files.Add(fileEntity);

        if (shouldUpdateShare)
        {
            var existingCurrentLinks = await _db.ShareItemFiles
                .Where(x => x.ShareItemId == targetShare.Id && x.IsCurrent)
                .ToListAsync(cancellationToken);

            foreach (var link in existingCurrentLinks)
            {
                link.IsCurrent = false;
                link.UpdatedAt = DateTime.UtcNow;
            }
        }

        var linkEntity = new ShareItemFile
        {
            ShareItemId = targetShare.Id,
            FileId = fileEntity.Id,
            ShareItem = null,
            File = null,
            IsCurrent = true
        };
        _db.ShareItemFiles.Add(linkEntity);

        await _db.SaveChangesAsync(cancellationToken);

        var url = BuildShareFileUrl(tenant, targetShare.Id, fileEntity.SaveFileName);
        var dto = ToDto(targetShare, tenant, (fileEntity, url, file.Length));
        return Ok(dto);
    }

    [HttpPost("room/{roomId}/brainstorm")]
    [RequirePermission(RoomPermissions.Write)]
    public async Task<IActionResult> ShareBrainstorm(
        [FromRoute] MaskedGuid roomId,
        [FromBody] CreateBrainstormShareRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = GetTenantIdentifier();
        var roomUser = await GetRoomUserAsync((Guid)roomId, cancellationToken);
        if (roomUser == null)
            return Unauthorized(new { message = "Room user not found." });

        var share = new ShareItem
        {
            RoomId = (Guid)roomId,
            TopicId = request.TopicId.HasValue && (Guid)request.TopicId.Value != Guid.Empty ? (Guid)request.TopicId.Value : null,
            Kind = "brainstorm",
            BrainBoardId = (Guid)request.BoardId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Brainstorm" : request.Title.Trim(),
            CreatedByRoomUserId = roomUser.Id,
            CreatedByName = RoomUserNameHelper.ResolveDisplayName(roomUser)
        };

        _db.ShareItems.Add(share);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(share, tenant, null));
    }

    [HttpDelete("room/{roomId}/{shareId}")]
    [RequirePermission(RoomPermissions.Delete)]
    public async Task<IActionResult> DeleteShare(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid shareId,
        CancellationToken cancellationToken)
    {
        var shareGuid = (Guid)shareId;
        var share = await _db.ShareItems.FirstOrDefaultAsync(s => s.Id == shareGuid, cancellationToken);
        if (share == null)
            return NotFound(new { message = "Share not found." });

        if (share.RoomId != (Guid)roomId)
            return NotFound(new { message = "Share not found." });

        _db.ShareItems.Remove(share);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var tenant = GetTenantIdentifier();
            var dir = GetShareItemFolder(tenant, shareGuid);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // ignore
        }

        return NoContent();
    }
}
