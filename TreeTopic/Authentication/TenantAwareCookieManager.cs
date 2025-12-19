using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace TreeTopic.Authentication;

/// <summary>
/// Custom cookie manager that uses tenant-specific cookie names
/// </summary>
public class TenantAwareCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager _innerManager = new ChunkingCookieManager();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantAwareCookieManager> _logger;

    public TenantAwareCookieManager(IHttpContextAccessor httpContextAccessor, ILogger<TenantAwareCookieManager> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Get the tenant-specific cookie name
    /// </summary>
    private string GetCookieName(string baseCookieName)
    {
        var tenant = _httpContextAccessor.HttpContext?.GetRouteValue("tenant")?.ToString();
        if (string.IsNullOrEmpty(tenant))
        {
            return baseCookieName;
        }

        var tenantCookieName = $"{baseCookieName}_{tenant}";
        _logger.LogDebug("Using tenant-specific cookie name: {CookieName}", tenantCookieName);
        return tenantCookieName;
    }

    public string? GetRequestCookie(HttpContext context, string key)
    {
        var tenantAwareName = GetCookieName(key);
        return _innerManager.GetRequestCookie(context, tenantAwareName);
    }

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        var tenantAwareName = GetCookieName(key);
        _innerManager.AppendResponseCookie(context, tenantAwareName, value, options);
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        var tenantAwareName = GetCookieName(key);
        _innerManager.DeleteCookie(context, tenantAwareName, options);
    }
}
