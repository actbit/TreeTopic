using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using TreeTopic.Constants;

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
        try
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                _logger.LogDebug("HttpContext is null, using base cookie name");
                return baseCookieName;
            }

            // 静的ファイルは tenant 判定をスキップ
            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.StaticFilePaths.SvelteKitAssets) == true)
            {
                _logger.LogDebug("Static file request, using base cookie name");
                return baseCookieName;
            }

            string? tenant = null;
            if(context.Request?.Path.Value.Contains("me") == true){

            }
            // OIDC コールバックパスでは query parameter から tenant を取得
            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.Paths.OidcCallbackPath) == true)
            {
                tenant = context.Request?.Query[AuthenticationConstants.TenantClaimType].ToString();
                _logger.LogDebug("OIDC callback path, tenant from query: {Tenant}", tenant ?? "NULL");
            }
            else
            {
                // 1. HttpContext.Items から tenant を取得（OnSigningIn で設定されている）
                if (context.Items?.TryGetValue(AuthenticationConstants.Cookie.TenantForCookieKey, out var tenantObj) == true)
                {
                    tenant = tenantObj?.ToString();
                    _logger.LogDebug("Regular path, tenant from HttpContext.Items: {Tenant}", tenant ?? "NULL");
                }

                // 2. Items から取得できない場合、route から tenant を取得
                if (string.IsNullOrEmpty(tenant))
                {
                    tenant = context.GetRouteValue(AuthenticationConstants.TenantClaimType)?.ToString();
                    _logger.LogDebug("Regular path, tenant from route: {Tenant}", tenant ?? "NULL");
                }

                // 3. Route から取得できない場合、パスから tenant を抽出（フォールバック）
                if (string.IsNullOrEmpty(tenant))
                {
                    var pathValue = context.Request?.Path.Value;
                    if (!string.IsNullOrEmpty(pathValue))
                    {
                        var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length > 0)
                        {
                            var firstSegment = segments[0];
                            if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                            {
                                tenant = firstSegment;
                                _logger.LogDebug("Regular path, tenant from path (fallback): {Tenant}", tenant);
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(tenant))
            {
                _logger.LogDebug("Tenant not resolved, using base cookie name");
                return baseCookieName;
            }

            var tenantCookieName = $"{baseCookieName}{AuthenticationConstants.Cookie.TenantCookieNameSeparator}{tenant}{AuthenticationConstants.Cookie.TenantCookieSuffix}";
            _logger.LogDebug("Using tenant-specific cookie: {CookieName}", tenantCookieName);
            return tenantCookieName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCookieName, using base cookie name");
            return baseCookieName;
        }
    }

    public string? GetRequestCookie(HttpContext context, string key)
    {
        try
        {
            if (context == null)
            {
                _logger.LogWarning("GetRequestCookie: HttpContext is null");
                return null;
            }

            var tenantAwareName = GetCookieName(key);
            _logger.LogDebug("GetRequestCookie: base={Base}, resolved={Resolved}, path={Path}", key, tenantAwareName, context.Request?.Path.Value);
            var tenantCookie = _innerManager.GetRequestCookie(context, tenantAwareName);
            if (!string.IsNullOrEmpty(tenantCookie))
            {
                _logger.LogDebug("GetRequestCookie: found cookie for {Resolved}", tenantAwareName);
                return tenantCookie;
            }

            // Fallback to base cookie name when tenant-specific cookie is not present
            _logger.LogDebug("GetRequestCookie: not found for {Resolved}, fallback to base {Base}", tenantAwareName, key);
            return _innerManager.GetRequestCookie(context, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRequestCookie");
            return null;
        }
    }

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        try
        {
            if (context == null || options == null)
            {
                _logger.LogWarning("AppendResponseCookie: HttpContext or CookieOptions is null");
                return;
            }

            var tenantAwareName = GetCookieName(key);
            _innerManager.AppendResponseCookie(context, tenantAwareName, value, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AppendResponseCookie");
        }
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        try
        {
            if (context == null || options == null)
            {
                _logger.LogWarning("DeleteCookie: HttpContext or CookieOptions is null");
                return;
            }

            var tenantAwareName = GetCookieName(key);
            _innerManager.DeleteCookie(context, tenantAwareName, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteCookie");
        }
    }
}
