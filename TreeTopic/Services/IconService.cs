using System.Security.Cryptography;
using Finbuckle.MultiTenant.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using TreeTopic.Models;

namespace TreeTopic.Services;

public class IconService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private const string DefaultTenantFolder = "default";

    public IconService(
        IWebHostEnvironment environment,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _environment = environment;
        _tenantAccessor = tenantAccessor;
    }

    public string? GetUserIconUrl(ApplicationUser? user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.IconFileName))
            return null;

        var tenantFolder = GetTenantUploadsFolderName();
        var tenantPath = GetUserIconPath(tenantFolder, user.IconFileName);
        if (System.IO.File.Exists(tenantPath))
            return BuildUserIconUrl(tenantFolder, user.IconFileName);

        var defaultPath = GetUserIconPath(DefaultTenantFolder, user.IconFileName);
        if (System.IO.File.Exists(defaultPath))
            return BuildUserIconUrl(DefaultTenantFolder, user.IconFileName);

        if (TryMigrateLegacyIcon(tenantFolder, "users", user.IconFileName, tenantPath))
            return BuildUserIconUrl(tenantFolder, user.IconFileName);

        if (TryMigrateLegacyIcon(DefaultTenantFolder, "users", user.IconFileName, defaultPath))
            return BuildUserIconUrl(DefaultTenantFolder, user.IconFileName);

        return null;
    }

    public string? GetRoomUserIconUrl(RoomUser? roomUser)
    {
        if (roomUser == null)
            return null;

        if (roomUser.UseMainIcon)
            return GetUserIconUrl(roomUser.ApplicationUser);

        if (string.IsNullOrWhiteSpace(roomUser.IconFileName))
            return null;

        var tenantFolder = GetTenantUploadsFolderName();
        var roomPath = GetRoomUserIconPath(roomUser.IconFileName);
        if (System.IO.File.Exists(roomPath))
            return BuildRoomUserIconUrl(roomUser.IconFileName);

        if (TryMigrateLegacyIcon(tenantFolder, "room-users", roomUser.IconFileName, roomPath))
            return BuildRoomUserIconUrl(roomUser.IconFileName);

        return null;
    }

    public async Task<string?> EnsureDefaultUserIconAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(user.IconFileName))
            return user.IconFileName;

        var initials = GetInitials(user.DisplayName ?? user.UserName ?? user.Email ?? "U");
        var fileName = $"{user.Id:N}.png";
        var path = GetUserIconPath(GetTenantUploadsFolderName(), fileName);
        await GenerateIconAsync(initials, user.Id.ToString("N"), path, _environment, cancellationToken);
        return fileName;
    }

    public async Task<string> SaveUserIconAsync(ApplicationUser user, IFormFile file, CancellationToken cancellationToken = default)
    {
        var fileName = $"{user.Id:N}{Path.GetExtension(file.FileName)}";
        var path = GetUserIconPath(GetTenantUploadsFolderName(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var stream = System.IO.File.Create(path))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return fileName;
    }

    public async Task<string> SaveRoomUserIconAsync(RoomUser roomUser, IFormFile file, CancellationToken cancellationToken = default)
    {
        var fileName = $"{roomUser.Id:N}{Path.GetExtension(file.FileName)}";
        var path = GetRoomUserIconPath(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using (var stream = System.IO.File.Create(path))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return fileName;
    }

    public async Task<string> EnsureDefaultRoomUserIconAsync(RoomUser roomUser, string seedSource, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(roomUser.IconFileName))
            return roomUser.IconFileName!;

        var initials = GetInitials(seedSource);
        var fileName = $"{roomUser.Id:N}.png";
        var path = GetRoomUserIconPath(fileName);
        await GenerateIconAsync(initials, roomUser.Id.ToString("N"), path, _environment, cancellationToken);
        return fileName;
    }

    public Task DeleteUserIconAsync(ApplicationUser user, string fileName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var tenantFolder = GetTenantUploadsFolderName();
                var path = GetUserIconPath(tenantFolder, fileName);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // 削除に失敗しても無視
            }
        }, cancellationToken);
    }

    public Task DeleteRoomUserIconAsync(RoomUser roomUser, string fileName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var path = GetRoomUserIconPath(fileName);
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // 削除に失敗しても無視
            }
        }, cancellationToken);
    }

    private string GetTenantUploadsFolderName()
    {
        var tenantInfo = _tenantAccessor.MultiTenantContext?.TenantInfo;
        return tenantInfo?.Identifier
               ?? tenantInfo?.Id
               ?? "default";
    }

    private string GetUploadsRootPath(string tenantFolder)
    {
        var contentRoot = _environment.ContentRootPath;
        return Path.Combine(contentRoot, "uploads", tenantFolder, "icons");
    }

    private string GetUserIconPath(string tenantFolder, string fileName)
    {
        return Path.Combine(GetUploadsRootPath(tenantFolder), "users", fileName);
    }

    private string GetRoomUserIconPath(string fileName)
    {
        return Path.Combine(GetUploadsRootPath(GetTenantUploadsFolderName()), "room-users", fileName);
    }

    private string BuildUserIconUrl(string tenantFolder, string fileName)
    {
        var tenantRoute = GetTenantRouteIdentifier();
        return $"/{tenantRoute}/api/icons/users/{fileName}".Replace("\\", "/");
    }

    private string BuildRoomUserIconUrl(string fileName)
    {
        var tenantRoute = GetTenantRouteIdentifier();
        return $"/{tenantRoute}/api/icons/room-users/{fileName}".Replace("\\", "/");
    }

    private string GetTenantRouteIdentifier()
    {
        var tenantInfo = _tenantAccessor.MultiTenantContext?.TenantInfo;
        return tenantInfo?.Identifier
               ?? tenantInfo?.Id
               ?? DefaultTenantFolder;
    }

    private static string GetInitials(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "U";

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return string.Concat(parts[0][0], parts[1][0]).ToUpperInvariant();
        }

        return trimmed.Length >= 2
            ? trimmed.Substring(0, 2).ToUpperInvariant()
            : trimmed.Substring(0, 1).ToUpperInvariant();
    }

    private static Color PickColor(string seed)
    {
        var palette = new[]
        {
            Color.FromRgb(0xEF, 0x53, 0x50),
            Color.FromRgb(0xAB, 0x47, 0xBC),
            Color.FromRgb(0x5C, 0x6B, 0xC0),
            Color.FromRgb(0x29, 0xB6, 0xF6),
            Color.FromRgb(0x26, 0xA6, 0x9A),
            Color.FromRgb(0x66, 0xBB, 0x6A),
            Color.FromRgb(0xFF, 0xCA, 0x28),
            Color.FromRgb(0xFF, 0xA7, 0x26),
            Color.FromRgb(0x8D, 0x6E, 0x63),
            Color.FromRgb(0x78, 0x90, 0x9C)
        };

        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        var index = hash[0] % palette.Length;
        return palette[index];
    }

    private static Task GenerateIconAsync(string initials, string seed, string path, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        const int size = 256;
        var bg = PickColor(seed);

        using var image = new Image<Rgba32>(size, size);
        image.Mutate(x => x.Fill(bg));

        // フォントを読み込む
        var fontPath = Path.Combine(environment.ContentRootPath, "Fonts", "NotoSansJP-Bold.ttf");
        var fontCollection = new FontCollection();
        var fontFamily = fontCollection.Add(fontPath);
        var font = fontFamily.CreateFont(size * 0.45f);

        var textOptions = new RichTextOptions(font)
        {
            Origin = new PointF(size / 2f, size / 2f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        image.Mutate(x => x.DrawText(textOptions, initials, Color.White));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        image.SaveAsPng(path);
        return Task.CompletedTask;
    }

    private bool TryMigrateLegacyIcon(string tenantFolder, string iconFolder, string fileName, string targetPath)
    {
        var legacyRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(legacyRoot))
            return false;

        var legacyPath = Path.Combine(legacyRoot, "uploads", tenantFolder, "icons", iconFolder, fileName);
        if (!System.IO.File.Exists(legacyPath))
            return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            System.IO.File.Copy(legacyPath, targetPath, overwrite: false);
            return System.IO.File.Exists(targetPath);
        }
        catch
        {
            return false;
        }
    }
}
