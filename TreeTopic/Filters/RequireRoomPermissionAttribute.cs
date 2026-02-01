using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TreeTopic.Services;

namespace TreeTopic.Filters;

/// <summary>
/// ルームに対する権限チェックを行うActionFilter
/// モデルバインド後に実行されます
///
/// 使用例:
/// [RequireRoomPermission("room.read")]
/// [RequireRoomPermission("room.manage")]
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireRoomPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _permissionName;

    /// <summary>
    /// ルームIDのアクションパラメーター名（デフォルト: "roomId"）
    /// </summary>
    public string RoomIdKey { get; set; } = "roomId";

    /// <summary>
    /// ルームIDをルートパラメーターからも取得するか
    /// </summary>
    public bool FallbackToRoute { get; set; } = true;

    /// <summary>
    /// ルームIDのルートパラメーター名（フォールバック用）
    /// </summary>
    public string RoomIdRouteKey { get; set; } = "roomId";

    public RequireRoomPermissionAttribute(string permissionName)
    {
        _permissionName = permissionName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireRoomPermissionAttribute>>();
        var roomUserManager = httpContext.RequestServices.GetRequiredService<RoomUserManager>();

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
            logger.LogWarning("[RequireRoomPermission] Invalid UserId claim: {UserIdClaim}", userIdClaim);
            context.Result = new UnauthorizedResult();
            return;
        }

        // RoomIdを取得（モデルバインド済みのパラメータから）
        Guid? roomId = null;

        // 1. アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(RoomIdKey, out var roomIdObj))
        {
            if (roomIdObj is Guid guid)
            {
                roomId = guid;
            }
            else if (Guid.TryParse(roomIdObj?.ToString(), out var parsedRoomId))
            {
                roomId = parsedRoomId;
            }
        }

        // 2. ルートパラメータから取得（フォールバック）
        if (!roomId.HasValue && FallbackToRoute)
        {
            if (context.RouteData.Values.TryGetValue(RoomIdRouteKey, out var routeRoomIdObj) &&
                Guid.TryParse(routeRoomIdObj?.ToString(), out var routeRoomId))
            {
                roomId = routeRoomId;
            }
        }

        if (!roomId.HasValue)
        {
            logger.LogWarning("[RequireRoomPermission] RoomId not found in action parameter '{RoomIdKey}' or route '{RoomIdRouteKey}'",
                RoomIdKey, RoomIdRouteKey);
            context.Result = new BadRequestObjectResult(new { message = $"RoomId required" });
            return;
        }

        // RoomUserを取得
        var roomUser = await roomUserManager.FindByRoomAndUserAsync(
            roomId.Value,
            userId,
            httpContext.RequestAborted);

        if (roomUser == null)
        {
            logger.LogWarning("[RequireRoomPermission] RoomUser not found: UserId={UserId}, RoomId={RoomId}",
                userId, roomId.Value);
            context.Result = new ForbidResult();
            return;
        }

        // 権限チェック
        var hasPermission = await roomUserManager.HasPermissionAsync(
            roomUser,
            _permissionName,
            httpContext.RequestAborted);

        if (!hasPermission)
        {
            logger.LogWarning("[RequireRoomPermission] Permission denied: UserId={UserId}, RoomId={RoomId}, Permission={Permission}",
                userId, roomId.Value, _permissionName);
            context.Result = new ForbidResult();
            return;
        }

        logger.LogDebug("[RequireRoomPermission] Permission granted: UserId={UserId}, RoomId={RoomId}, Permission={Permission}",
            userId, roomId.Value, _permissionName);

        await next(); // アクションを実行
    }
}
