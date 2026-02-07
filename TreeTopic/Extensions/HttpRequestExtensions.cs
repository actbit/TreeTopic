using Microsoft.AspNetCore.Http;
using TreeTopic.Constants;

namespace TreeTopic.Extensions;

/// <summary>
/// HTTP リクエストに関する拡張メソッド
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// リクエストが API リクエストかどうかを判定
    /// </summary>
    /// <param name="request">HTTP リクエスト</param>
    /// <returns>API リクエストの場合は true</returns>
    public static bool IsApiRequest(this HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.EndsWith("/auth/me", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/auth/check", StringComparison.OrdinalIgnoreCase))
            return true;

        if (AuthenticationConstants.Paths.IsApiPath(path))
            return true;

        // SignalR の negotiate / WebSocket エンドポイントも API リクエストとして扱う。
        // （401/403 を返し、ログイン/OIDC リダイレクトは行わない）
        if (IsHubPath(path) || request.Query.ContainsKey("negotiateVersion"))
            return true;

        var accept = request.Headers["Accept"].ToString();
        if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return true;

        var xRequestedWith = request.Headers["X-Requested-With"].ToString();
        if (string.Equals(xRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsHubPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var trimmed = path.Trim('/');
        if (trimmed.Length == 0)
            return false;

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Equals(segments[0], "hubs", StringComparison.OrdinalIgnoreCase) ||
               (segments.Length >= 2 && string.Equals(segments[1], "hubs", StringComparison.OrdinalIgnoreCase));
    }
}
