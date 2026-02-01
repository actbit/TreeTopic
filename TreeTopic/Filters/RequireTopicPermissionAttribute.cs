using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Filters;

/// <summary>
/// トピックに対する権限チェックを行うActionFilter
///
/// 使用例:
/// [RequireTopicPermission("topic.read")]
/// [RequireTopicPermission("topic.write", TopicIdSource = TopicIdSource.Body)]
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireTopicPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private ILogger<RequireTopicPermissionAttribute> _logger = null!;
    private ApplicationDbContext _context = null!;
    private TopicPermissionManager _permissionManager = null!;

    /// <summary>
    /// 必要な権限名
    /// </summary>
    public string PermissionName { get; set; } = string.Empty;

    /// <summary>
    /// TopicIdの取得先
    /// </summary>
    public TopicIdSource TopicIdSource { get; set; } = TopicIdSource.Route;

    /// <summary>
    /// トピックIDのルートパラメーター名（デフォルト: "topicId"）
    /// </summary>
    public string TopicIdRouteKey { get; set; } = "topicId";

    /// <summary>
    /// トピックIDのクエリパラメーター名（デフォルト: "topicId"）
    /// </summary>
    public string TopicIdQueryKey { get; set; } = "topicId";

    /// <summary>
    /// トピックIDのボディプロパティ名（デフォルト: "topicId"）
    /// </summary>
    public string TopicIdBodyKey { get; set; } = "topicId";

    /// <summary>
    /// ルームIDのルートパラメーター名（RoomUserを取得するため）
    /// </summary>
    public string RoomIdRouteKey { get; set; } = "roomId";

    /// <summary>
    /// ルートパラメーターからRoomIdが取得できない場合、TopicからRoomIdを取得するか
    /// </summary>
    public bool ResolveRoomIdFromTopic { get; set; } = true;

    public RequireTopicPermissionAttribute(string permissionName)
    {
        PermissionName = permissionName;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;
        var serviceProvider = httpContext.RequestServices;

        _logger = serviceProvider.GetRequiredService<ILogger<RequireTopicPermissionAttribute>>();
        _context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        _permissionManager = serviceProvider.GetRequiredService<TopicPermissionManager>();

        // 認証済みユーザーを取得
        var user = httpContext.User;
        if (user == null || user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // ユーザーIDを取得
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("[RequireTopicPermission] Invalid UserId claim: {UserIdClaim}", userIdClaim);
            context.Result = new UnauthorizedResult();
            return;
        }

        // TopicIdを取得
        var topicId = await GetTopicIdAsync(context, httpContext);
        if (!topicId.HasValue)
        {
            _logger.LogWarning("[RequireTopicPermission] TopicId not found");
            context.Result = new BadRequestObjectResult(new { message = "TopicId required" });
            return;
        }

        // RoomIdを取得
        Guid? roomId = null;
        if (context.RouteData.Values.TryGetValue(RoomIdRouteKey, out var roomIdObj) &&
            Guid.TryParse(roomIdObj?.ToString(), out var parsedRoomId))
        {
            roomId = parsedRoomId;
        }

        // RoomIdがルートにない場合、Topicから取得
        if (!roomId.HasValue && ResolveRoomIdFromTopic)
        {
            var topic = await _context.Topics
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == topicId.Value, httpContext.RequestAborted);

            if (topic != null)
            {
                roomId = topic.RoomId;
            }
        }

        if (!roomId.HasValue)
        {
            _logger.LogWarning("[RequireTopicPermission] RoomId not found: tried route '{RouteKey}' and topic resolve", RoomIdRouteKey);
            context.Result = new BadRequestObjectResult(new { message = $"RoomId required in route parameter '{RoomIdRouteKey}'" });
            return;
        }

        // RoomUserを取得
        var roomUser = await _context.RoomUsers
            .Include(ru => ru.RoomRole)
            .FirstOrDefaultAsync(ru => ru.RoomId == roomId.Value && ru.ApplicationUserId == userId, httpContext.RequestAborted);

        if (roomUser == null)
        {
            _logger.LogWarning("[RequireTopicPermission] RoomUser not found: UserId={UserId}, RoomId={RoomId}", userId, roomId.Value);
            context.Result = new ForbidResult();
            return;
        }

        // 権限チェック
        var hasPermission = await _permissionManager.HasPermissionAsync(
            roomUser,
            topicId.Value,
            PermissionName,
            httpContext.RequestAborted);

        if (!hasPermission)
        {
            _logger.LogWarning("[RequireTopicPermission] Permission denied: UserId={UserId}, TopicId={TopicId}, Permission={Permission}",
                userId, topicId.Value, PermissionName);
            context.Result = new ForbidResult();
            return;
        }

        _logger.LogDebug("[RequireTopicPermission] Permission granted: UserId={UserId}, TopicId={TopicId}, Permission={Permission}",
            userId, topicId.Value, PermissionName);
    }

    /// <summary>
    /// 設定されたソースからTopicIdを取得
    /// </summary>
    private async Task<Guid?> GetTopicIdAsync(AuthorizationFilterContext context, HttpContext httpContext)
    {
        return TopicIdSource switch
        {
            TopicIdSource.Route => GetTopicIdFromRoute(context),
            TopicIdSource.Query => GetTopicIdFromQuery(context),
            TopicIdSource.Body => await GetTopicIdFromBodyAsync(context, httpContext),
            TopicIdSource.RouteOrQuery => GetTopicIdFromRoute(context) ?? GetTopicIdFromQuery(context),
            TopicIdSource.RouteOrBody => GetTopicIdFromRoute(context) ?? await GetTopicIdFromBodyAsync(context, httpContext),
            TopicIdSource.QueryOrBody => GetTopicIdFromQuery(context) ?? await GetTopicIdFromBodyAsync(context, httpContext),
            TopicIdSource.RouteOrQueryOrBody => GetTopicIdFromRoute(context) ?? GetTopicIdFromQuery(context) ?? await GetTopicIdFromBodyAsync(context, httpContext),
            _ => GetTopicIdFromRoute(context)
        };
    }

    private Guid? GetTopicIdFromRoute(AuthorizationFilterContext context)
    {
        if (context.RouteData.Values.TryGetValue(TopicIdRouteKey, out var topicIdObj) &&
            Guid.TryParse(topicIdObj?.ToString(), out var topicId))
        {
            return topicId;
        }
        return null;
    }

    private Guid? GetTopicIdFromQuery(AuthorizationFilterContext context)
    {
        var queryValue = context.HttpContext.Request.Query[TopicIdQueryKey].ToString();
        if (Guid.TryParse(queryValue, out var topicId))
        {
            return topicId;
        }
        return null;
    }

    private async Task<Guid?> GetTopicIdFromBodyAsync(AuthorizationFilterContext context, HttpContext httpContext)
    {
        try
        {
            // リクエストボディを読み取り
            httpContext.Request.EnableBuffering();
            var body = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
            httpContext.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            // JSONをパースして指定されたプロパティからTopicIdを取得
            using var jsonDoc = JsonDocument.Parse(body);
            if (jsonDoc.RootElement.TryGetProperty(TopicIdBodyKey, out var topicIdElement))
            {
                var topicIdStr = topicIdElement.ToString();
                if (Guid.TryParse(topicIdStr, out var topicId))
                {
                    return topicId;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RequireTopicPermission] Failed to read TopicId from body");
        }

        return null;
    }
}

/// <summary>
/// TopicIdの取得先
/// </summary>
public enum TopicIdSource
{
    /// <summary>
    /// ルートパラメーターのみ
    /// </summary>
    Route,

    /// <summary>
    /// クエリパラメーターのみ
    /// </summary>
    Query,

    /// <summary>
    /// リクエストボディのみ
    /// </summary>
    Body,

    /// <summary>
    /// ルート→クエリの順で試行
    /// </summary>
    RouteOrQuery,

    /// <summary>
    /// ルート→ボディの順で試行
    /// </summary>
    RouteOrBody,

    /// <summary>
    /// クエリ→ボディの順で試行
    /// </summary>
    QueryOrBody,

    /// <summary>
    /// ルート→クエリ→ボディの順で試行
    /// </summary>
    RouteOrQueryOrBody,
}
