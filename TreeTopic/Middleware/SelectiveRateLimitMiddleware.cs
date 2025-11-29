namespace TreeTopic.Middleware;

/// <summary>
/// テナント作成エンドポイントに対するレート制限ミドルウェア
/// </summary>
public class SelectiveRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Dictionary<string, List<DateTime>> _requests = new();
    private readonly ILogger<SelectiveRateLimitMiddleware> _logger;

    public SelectiveRateLimitMiddleware(RequestDelegate next, ILogger<SelectiveRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // テナント作成エンドポイントのみ制限
        if (context.Request.Method == "POST" &&
            context.Request.Path.StartsWithSegments("/api/tenant/register"))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            if (!_requests.ContainsKey(ip))
                _requests[ip] = new();

            // 1時間以内のリクエストのみカウント
            _requests[ip] = _requests[ip]
                .Where(t => (now - t).TotalSeconds < 3600)
                .ToList();

            // 1時間に10回まで
            if (_requests[ip].Count >= 10)
            {
                _logger.LogWarning("Rate limit exceeded for tenant creation from IP: {IpAddress}", ip);
                context.Response.StatusCode = 429;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Too many tenant creation requests. Please try again later." });
                return;
            }

            _requests[ip].Add(now);
            _logger.LogDebug("Tenant creation request from IP: {IpAddress}, Count: {Count}/10 in 1 hour",
                ip, _requests[ip].Count);
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
}
