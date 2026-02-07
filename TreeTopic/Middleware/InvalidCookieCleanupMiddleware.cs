using Microsoft.AspNetCore.Http;
using TreeTopic.Authentication;
using TreeTopic.Constants;

namespace TreeTopic.Middleware;

/// <summary>
/// 無効なクッキーを削除するミドルウェア
/// チャンクされたクッキーや無効な形式のクッキーを検出して削除する
/// </summary>
public class InvalidCookieCleanupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _baseCookieName;

    public InvalidCookieCleanupMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _baseCookieName = configuration["Authentication:CookieName"] ?? "TreeTopic.Cookie";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var deleteOptions = new CookieOptions
        {
            Path = AuthenticationConstants.Cookie.CookiePath,
            Secure = true,
            SameSite = SameSiteMode.None
        };

        void DeleteChunkedCookieSet(string cookieKey)
        {
            context.Response.Cookies.Delete(cookieKey, deleteOptions);
            for (var i = 1; i <= 5; i++)
            {
                var chunkName = $"{cookieKey}C{i}";
                context.Response.Cookies.Delete(chunkName, deleteOptions);
            }
        }

        var tenantSeparator = AuthenticationConstants.Cookie.TenantCookieNameSeparator;
        var tenantSuffix = AuthenticationConstants.Cookie.TenantCookieSuffix;

        foreach (var cookie in context.Request.Cookies)
        {
            var key = cookie.Key;
            if (!key.StartsWith(_baseCookieName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only handle base cookie or tenant-suffixed cookie: ".TreeTopic.Auth" or ".TreeTopic.Auth_{tenant}"
            var baseKey = key;
            if (!string.Equals(key, _baseCookieName, StringComparison.OrdinalIgnoreCase))
            {
                if (key.Length <= _baseCookieName.Length + 1 || key[_baseCookieName.Length] != tenantSeparator[0])
                    continue;

                // validate tenant suffix chars
                if (!key.EndsWith(tenantSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var tenantPart = key.Substring(_baseCookieName.Length + 1, key.Length - _baseCookieName.Length - 1 - tenantSuffix.Length);
                var tenantValid = tenantPart.Length > 0;
                if (tenantValid)
                {
                    foreach (var ch in tenantPart)
                    {
                        if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))
                        {
                            tenantValid = false;
                            break;
                        }
                    }
                }
                if (!tenantValid)
                    continue;
            }

            if (cookie.Value.StartsWith("chunks-", StringComparison.OrdinalIgnoreCase))
            {
                DeleteChunkedCookieSet(baseKey);
                continue;
            }

            // If this is a chunk cookie (ends with C + digits), delete the chunk set for its base.
            var lastIndex = key.LastIndexOf('C');
            if (lastIndex > _baseCookieName.Length &&
                lastIndex < key.Length - 1)
            {
                var digitOk = true;
                for (var i = lastIndex + 1; i < key.Length; i++)
                {
                    if (!char.IsDigit(key[i]))
                    {
                        digitOk = false;
                        break;
                    }
                }
                if (!digitOk)
                    continue;

                baseKey = key.Substring(0, lastIndex);
                // Ensure baseKey still matches base cookie pattern to avoid accidental deletions.
                if (string.Equals(baseKey, _baseCookieName, StringComparison.OrdinalIgnoreCase) ||
                    (baseKey.Length > _baseCookieName.Length + 1 &&
                     baseKey.StartsWith(_baseCookieName + tenantSeparator, StringComparison.OrdinalIgnoreCase) &&
                     baseKey.EndsWith(tenantSuffix, StringComparison.OrdinalIgnoreCase)))
                {
                    DeleteChunkedCookieSet(baseKey);
                }
            }
        }

        await _next(context);
    }
}
