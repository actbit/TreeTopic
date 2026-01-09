using System.Security.Claims;
using System.Text.Json;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class ShareController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public ShareController(IWebHostEnvironment environment)
    {
        _environment = environment;
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

    private string GetManifestDir(string tenant, string roomId)
        => Path.Combine(_environment.ContentRootPath, ".share", tenant, roomId);

    private string GetRoomShareDir(string tenant, string roomId)
    {
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads", tenant, roomId, "share");
    }

    private string GetManifestPath(string tenant, string roomId)
        => Path.Combine(GetManifestDir(tenant, roomId), "share-manifest.json");

    private sealed record ShareManifestItem(
        string Id,
        string RoomId,
        string? TopicId,
        string Kind,
        string? BoardId,
        string Title,
        string FileName,
        string SavedFileName,
        string MimeType,
        long Size,
        string Url,
        DateTime CreatedAt,
        string CreatedBy,
        string CreatedByName,
        string? SourceId
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
        string CreatedBy,
        string CreatedByName,
        string? SourceId
    );

    public sealed record CreateBrainstormShareRequest(
        string RoomId,
        string? TopicId,
        string BoardId,
        string? Title
    );

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

    private async Task<List<ShareManifestItem>> ReadManifestAsync(string tenant, string roomId, CancellationToken ct)
    {
        var path = GetManifestPath(tenant, roomId);
        if (!System.IO.File.Exists(path))
            return [];

        await using var stream = System.IO.File.OpenRead(path);
        var list = await JsonSerializer.DeserializeAsync<List<ShareManifestItem>>(stream, _jsonOptions, ct);
        return list ?? [];
    }

    private async Task WriteManifestAsync(string tenant, string roomId, List<ShareManifestItem> items, CancellationToken ct)
    {
        var dir = GetManifestDir(tenant, roomId);
        Directory.CreateDirectory(dir);

        var path = GetManifestPath(tenant, roomId);
        await using var stream = System.IO.File.Create(path);
        await JsonSerializer.SerializeAsync(stream, items, _jsonOptions, ct);
    }

    private ShareItemDto ToDto(ShareManifestItem item)
        => new(
            Id: item.Id,
            RoomId: item.RoomId,
            TopicId: item.TopicId,
            Kind: item.Kind,
            BoardId: item.BoardId,
            Title: item.Title,
            FileName: item.FileName,
            MimeType: item.MimeType,
            Size: item.Size,
            Url: item.Url,
            CreatedAt: item.CreatedAt,
            CreatedBy: item.CreatedBy,
            CreatedByName: item.CreatedByName,
            SourceId: item.SourceId
        );

    private string BuildShareFileUrl(string tenant, string roomId, string savedFileName)
        => $"/uploads/{tenant}/{roomId}/share/{savedFileName}".Replace("\\", "/");

    private string BuildBrainstormUrl(string tenant, string boardId)
        => $"/{tenant}/brainstorm/{boardId}".Replace("\\", "/");

    [HttpGet("room/{roomId}")]
    public async Task<IActionResult> GetByRoom(
        [FromRoute] MaskedGuid roomId,
        [FromQuery] MaskedGuid? topicId,
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        var tenant = GetTenantIdentifier();
        var list = await ReadManifestAsync(tenant, roomId.ToString(), cancellationToken);

        if (topicId.HasValue && (Guid)topicId.Value != Guid.Empty)
        {
            var tid = topicId.Value.ToString();
            list = list.Where(x => string.Equals(x.TopicId, tid, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToLowerInvariant();
            list = list.Where(x => string.Equals(x.Kind, k, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var dtos = list
            .OrderByDescending(x => x.CreatedAt)
            .Select(ToDto)
            .ToList();

        return Ok(dtos);
    }

    [HttpPost("room/{roomId}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadToRoom(
        [FromRoute] MaskedGuid roomId,
        [FromForm] IFormFile file,
        [FromForm] MaskedGuid? topicId,
        [FromForm] string? kind,
        [FromForm] string? title,
        [FromForm] string? sourceId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var tenant = GetTenantIdentifier();
        var dir = GetRoomShareDir(tenant, roomId.ToString());
        Directory.CreateDirectory(dir);

        var originalFileName = Path.GetFileName(file.FileName);
        var id = Guid.NewGuid();
        var savedFileName = $"{id:N}_{originalFileName}";
        var savePath = Path.Combine(dir, savedFileName);

        await using (var stream = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var mime = !string.IsNullOrWhiteSpace(file.ContentType)
            ? file.ContentType
            : (_contentTypeProvider.TryGetContentType(originalFileName, out var ct)
                ? ct
                : "application/octet-stream");

        var normalizedKind = NormalizeKind(kind, mime, originalFileName);
        if (normalizedKind is not ("image" or "document"))
            normalizedKind = "document";

        var createdAt = DateTime.UtcNow;
        var topic = topicId.HasValue && (Guid)topicId.Value != Guid.Empty ? topicId.Value.ToString() : null;
        var url = BuildShareFileUrl(tenant, roomId.ToString(), savedFileName);

        var item = new ShareManifestItem(
            Id: id.ToString(),
            RoomId: roomId.ToString(),
            TopicId: topic,
            Kind: normalizedKind,
            BoardId: null,
            Title: string.IsNullOrWhiteSpace(title) ? originalFileName : title.Trim(),
            FileName: originalFileName,
            SavedFileName: savedFileName,
            MimeType: mime,
            Size: file.Length,
            Url: url,
            CreatedAt: createdAt,
            CreatedBy: CurrentUserId.ToString(),
            CreatedByName: CurrentUserName,
            SourceId: string.IsNullOrWhiteSpace(sourceId) ? null : sourceId.Trim()
        );

        var list = await ReadManifestAsync(tenant, roomId.ToString(), cancellationToken);
        list.Add(item);
        await WriteManifestAsync(tenant, roomId.ToString(), list, cancellationToken);

        return Ok(ToDto(item));
    }

    [HttpPost("room/{roomId}/brainstorm")]
    public async Task<IActionResult> ShareBrainstorm(
        [FromRoute] MaskedGuid roomId,
        [FromBody] CreateBrainstormShareRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BoardId))
            return BadRequest(new { message = "BoardId is required." });

        var normalizedBoardId = request.BoardId.Trim();
        if (string.Equals(normalizedBoardId, "undefined", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedBoardId, "null", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "BoardId is invalid." });
        }

        var tenant = GetTenantIdentifier();
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var item = new ShareManifestItem(
            Id: id.ToString(),
            RoomId: roomId.ToString(),
            TopicId: string.IsNullOrWhiteSpace(request.TopicId) ? null : request.TopicId.Trim(),
            Kind: "brainstorm",
            BoardId: normalizedBoardId,
            Title: string.IsNullOrWhiteSpace(request.Title) ? "Brainstorm" : request.Title.Trim(),
            FileName: string.Empty,
            SavedFileName: string.Empty,
            MimeType: string.Empty,
            Size: 0,
            Url: BuildBrainstormUrl(tenant, normalizedBoardId),
            CreatedAt: createdAt,
            CreatedBy: CurrentUserId.ToString(),
            CreatedByName: CurrentUserName,
            SourceId: null
        );

        var list = await ReadManifestAsync(tenant, roomId.ToString(), cancellationToken);
        list.Add(item);
        await WriteManifestAsync(tenant, roomId.ToString(), list, cancellationToken);

        return Ok(ToDto(item));
    }

    [HttpDelete("room/{roomId}/{shareId}")]
    public async Task<IActionResult> DeleteShare(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string shareId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shareId))
            return BadRequest(new { message = "ShareId is required." });

        var tenant = GetTenantIdentifier();
        var list = await ReadManifestAsync(tenant, roomId.ToString(), cancellationToken);
        var idx = list.FindIndex(x => string.Equals(x.Id, shareId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
            return NotFound(new { message = "Share not found." });

        var item = list[idx];
        list.RemoveAt(idx);
        await WriteManifestAsync(tenant, roomId.ToString(), list, cancellationToken);

        if (!string.IsNullOrWhiteSpace(item.SavedFileName))
        {
            try
            {
                var primaryPath = Path.Combine(GetRoomShareDir(tenant, roomId.ToString()), item.SavedFileName);
                if (System.IO.File.Exists(primaryPath))
                    System.IO.File.Delete(primaryPath);
            }
            catch
            {
                // ignore
            }
        }

        return NoContent();
    }
}
