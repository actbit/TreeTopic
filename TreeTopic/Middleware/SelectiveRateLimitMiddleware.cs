namespace TreeTopic.Middleware;

/// <summary>
/// テナント作成エンドポイントに対するレート制限ミドルウェア
/// </summary>
public class SelectiveRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Dictionary<string, List<DateTime>> _requests = new();
    private readonly ILogger<SelectiveRateLimitMiddleware> _logger;
    private readonly string _rateLimitEndpoint;
    private readonly int _maxRequestsPerHour;
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly object _lockObject = new();

    public SelectiveRateLimitMiddleware(RequestDelegate next, ILogger<SelectiveRateLimitMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _rateLimitEndpoint = configuration.GetValue<string>("RateLimit:TenantRegisterEndpoint", "/api/tenants/register") ?? "/api/tenants/register";
        _maxRequestsPerHour = configuration.GetValue<int>("RateLimit:MaxRequestsPerHour", 10);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 設定されたエンドポイントのみに制限を適用
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments(_rateLimitEndpoint))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            // 1時間ごとに古いエントリをクリーンアップ（ロック外で実行）
            if ((now - _lastCleanup).TotalHours >= 1)
            {
                lock (_lockObject)
                {
                    // ダブルチェック
                    if ((now - _lastCleanup).TotalHours >= 1)
                    {
                        CleanupOldEntries(now);
                        _lastCleanup = now;
                    }
                }
            }

            bool isRateLimited = false;

            lock (_lockObject)
            {
                if (!_requests.ContainsKey(ip))
                    _requests[ip] = new();

                // 1時間以内のリクエストのみカウント
                _requests[ip] = _requests[ip]
                    .Where(t => (now - t).TotalSeconds < 3600)
                    .ToList();

                // 設定値に達したら制限
                if (_requests[ip].Count >= _maxRequestsPerHour)
                {
                    isRateLimited = true;
                }
                else
                {
                    _requests[ip].Add(now);
                    _logger.LogDebug("Request to {Endpoint} from IP: {IpAddress}, Count: {Count}/{MaxRequests} in 1 hour",
                        _rateLimitEndpoint, ip, _requests[ip].Count, _maxRequestsPerHour);
                }
            }

            if (isRateLimited)
            {
                _logger.LogWarning("Rate limit exceeded for endpoint {Endpoint} from IP: {IpAddress}", _rateLimitEndpoint, ip);
                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new { message = $"Too many requests to {_rateLimitEndpoint}. Please try again later." });
                return;
            }
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception in downstream middleware. Path: {Path}, Method: {Method}",
                context.Request.Path, context.Request.Method);
            throw;
        }
    }

    private void CleanupOldEntries(DateTime now)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in _requests)
        {
            // このIPエントリの最新リクエスト時刻を取得
            if (kvp.Value.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
            else
            {
                var lastRequest = kvp.Value.Max();
                // 最後のリクエスト以降1時間以上経過している場合、エントリを削除
                if ((now - lastRequest).TotalSeconds >= 3600)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var key in keysToRemove)
        {
            _requests.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old rate limit entries", keysToRemove.Count);
        }
    }
}
