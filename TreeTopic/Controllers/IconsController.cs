using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public sealed class IconsController : ControllerBase
{
    private const string DefaultTenantFolder = "default";

    private readonly IWebHostEnvironment _environment;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public IconsController(
        IWebHostEnvironment environment,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor)
    {
        _environment = environment;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("users/{fileName}")]
    public IActionResult GetUserIcon([FromRoute] string tenant, [FromRoute] string fileName)
    {
        if (!IsSafeFileName(fileName))
            return BadRequest(new { message = "Invalid file name." });

        var tenantFolder = GetTenantUploadsFolderName(tenant);
        var filePath = Path.Combine(_environment.ContentRootPath, "uploads", tenantFolder, "icons", "users", fileName);
        if (!System.IO.File.Exists(filePath))
        {
            var defaultPath = Path.Combine(_environment.ContentRootPath, "uploads", DefaultTenantFolder, "icons", "users", fileName);
            if (!System.IO.File.Exists(defaultPath))
                return NotFound();
            filePath = defaultPath;
        }

        return PhysicalFile(filePath, ResolveContentType(fileName));
    }

    [HttpGet("room-users/{fileName}")]
    public IActionResult GetRoomUserIcon([FromRoute] string tenant, [FromRoute] string fileName)
    {
        if (!IsSafeFileName(fileName))
            return BadRequest(new { message = "Invalid file name." });

        var tenantFolder = GetTenantUploadsFolderName(tenant);
        var filePath = Path.Combine(_environment.ContentRootPath, "uploads", tenantFolder, "icons", "room-users", fileName);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        return PhysicalFile(filePath, ResolveContentType(fileName));
    }

    private string GetTenantUploadsFolderName(string? routeTenant = null)
    {
        if (IsSafeTenantSegment(routeTenant))
            return routeTenant!;

        var tenantInfo = _tenantAccessor.MultiTenantContext?.TenantInfo;
        return tenantInfo?.Identifier
               ?? tenantInfo?.Id
               ?? DefaultTenantFolder;
    }

    private string ResolveContentType(string fileName)
    {
        return _contentTypeProvider.TryGetContentType(fileName, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static bool IsSafeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        if (fileName.Contains("..", StringComparison.Ordinal))
            return false;

        var onlyName = Path.GetFileName(fileName);
        return string.Equals(fileName, onlyName, StringComparison.Ordinal);
    }

    private static bool IsSafeTenantSegment(string? tenant)
    {
        if (string.IsNullOrWhiteSpace(tenant))
            return false;

        if (tenant.Contains("..", StringComparison.Ordinal))
            return false;

        var onlyName = Path.GetFileName(tenant);
        return string.Equals(tenant, onlyName, StringComparison.Ordinal);
    }
}
