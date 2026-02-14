using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using TreeTopic.Constants;

namespace TreeTopic.Authentication;

/// <summary>
/// テナント固有のCookie名を使用するカスタムCookieマネージャー
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
    /// テナント固有のCookie名を取得
    /// </summary>
    private string GetCookieName(string baseCookieName)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return baseCookieName;
            }

            // 静的ファイルは tenant 判定をスキップ
            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.StaticFilePaths.SvelteKitAssets) == true)
            {
                return baseCookieName;
            }

            string? tenant = null;
            // OIDC コールバックパスでは query parameter から tenant を取得
            if (context.Request?.Path.StartsWithSegments(AuthenticationConstants.Paths.OidcCallbackPath) == true)
            {
                tenant = context.Request?.Query[AuthenticationConstants.TenantClaimType].ToString();
            }
            else
            {
                // 1. HttpContext.Items から tenant を取得（OnSigningIn で設定）
                if (context.Items?.TryGetValue(AuthenticationConstants.Cookie.TenantForCookieKey, out var tenantObj) == true)
                {
                    tenant = tenantObj?.ToString();
                }

                // 2. Items から取得できない場合、route から tenant を取得
                if (string.IsNullOrEmpty(tenant))
                {
                    tenant = context.GetRouteValue(AuthenticationConstants.TenantClaimType)?.ToString();
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
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(tenant))
            {
                return baseCookieName;
            }

            var tenantCookieName = $"{baseCookieName}{AuthenticationConstants.Cookie.TenantCookieNameSeparator}{tenant}{AuthenticationConstants.Cookie.TenantCookieSuffix}";
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
                return null;
            }

            var tenantAwareName = GetCookieName(key);
            var tenantCookie = _innerManager.GetRequestCookie(context, tenantAwareName);
            if (!string.IsNullOrEmpty(tenantCookie))
            {
                return tenantCookie;
            }

            // テナント固有のCookieがない場合はベース名でフォールバック
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
