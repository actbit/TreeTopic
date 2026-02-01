using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MaskedUUID.AspNetCore.Types;
using MaskedUUID.AspNetCore.Services;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Filters;

/// <summary>
/// 権限チェックを行うActionFilter
/// ASP.NET Core Identity Permission、RoomPermission、TopicPermissionのいずれかの権限があればOK
///
/// 使用例:
/// [RequirePermission("user.manage")]  // 単一権限
/// [RequirePermission("room.manage", "topic.manage")]  // 複数権限（OR条件）
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissionNames;

    /// <summary>
    /// トピックIDのアクションパラメーター名（TopicPermissionチェック用）
    /// </summary>
    public string TopicIdKey { get; set; } = "topicId";

    /// <summary>
    /// ルームIDのアクションパラメーター名（RoomPermissionチェック用）
    /// </summary>
    public string RoomIdKey { get; set; } = "roomId";

    /// <summary>
    /// IDをルートパラメーターからも取得するか
    /// </summary>
    public bool FallbackToRoute { get; set; } = true;

    /// <summary>
    /// ルートパラメーターからRoomIdが取得できない場合、TopicからRoomIdを取得するか
    /// </summary>
    public bool ResolveRoomIdFromTopic { get; set; } = true;

    public RequirePermissionAttribute(params string[] permissionNames)
    {
        _permissionNames = permissionNames;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequirePermissionAttribute>>();
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var roomUserManager = httpContext.RequestServices.GetRequiredService<RoomUserManager>();
        var topicPermissionManager = httpContext.RequestServices.GetRequiredService<TopicPermissionManager>();
        var maskedUuidService = httpContext.RequestServices.GetRequiredService<IMaskedUUIDService>();

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
            context.Result = new UnauthorizedResult();
            return;
        }

        // ApplicationUserを取得
        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // ユーザーのロールを取得
        var roles = await userManager.GetRolesAsync(appUser);

        // ============ 1. ASP.NET Core Identity Permissionをチェック ============
        var identityPermissions = await dbContext.Permissions
            .Where(p => roles.Contains(p.Role.Name) && _permissionNames.Contains(p.Name))
            .Select(p => p.Name)
            .ToListAsync(httpContext.RequestAborted);

        if (identityPermissions.Any())
        {
            logger.LogDebug("[RequirePermission] Permission granted via Identity: UserId={UserId}, Permissions={Permissions}",
                userId, string.Join(", ", identityPermissions));
            await next();
            return;
        }

        // ============ 2. RoomPermissionをチェック ============
        // RoomIdを取得
        Guid? roomId = GetRoomId(context, maskedUuidService);
        if (roomId.HasValue)
        {
            var roomUser = await roomUserManager.FindByRoomAndUserAsync(
                roomId.Value,
                userId,
                httpContext.RequestAborted);

            if (roomUser != null)
            {
                var roomPermissions = await roomUserManager.GetPermissionsAsync(roomUser, httpContext.RequestAborted);
                var matchingRoomPermissions = roomPermissions.Where(p => _permissionNames.Contains(p)).ToList();

                if (matchingRoomPermissions.Any())
                {
                    logger.LogDebug("[RequirePermission] Permission granted via Room: UserId={UserId}, RoomId={RoomId}, Permissions={Permissions}",
                        userId, roomId.Value, string.Join(", ", matchingRoomPermissions));
                    await next();
                    return;
                }
            }
        }

        // ============ 3. TopicPermissionをチェック ============
        // TopicIdを取得
        Guid? topicId = GetTopicId(context, maskedUuidService);
        if (topicId.HasValue)
        {
            // RoomIdがまだ取得できない場合はTopicから取得
            if (!roomId.HasValue && ResolveRoomIdFromTopic)
            {
                var topic = await dbContext.Topics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == topicId.Value, httpContext.RequestAborted);
                if (topic != null)
                {
                    roomId = topic.RoomId;
                }
            }

            if (roomId.HasValue)
            {
                var roomUser = await roomUserManager.FindByRoomAndUserAsync(
                    roomId.Value,
                    userId,
                    httpContext.RequestAborted);

                if (roomUser != null)
                {
                    var topicPermissions = await topicPermissionManager.GetPermissionsAsync(
                        roomUser,
                        topicId.Value,
                        httpContext.RequestAborted);

                    var matchingTopicPermissions = topicPermissions.Where(p => _permissionNames.Contains(p)).ToList();

                    if (matchingTopicPermissions.Any())
                    {
                        logger.LogDebug("[RequirePermission] Permission granted via Topic: UserId={UserId}, TopicId={TopicId}, Permissions={Permissions}",
                            userId, topicId.Value, string.Join(", ", matchingTopicPermissions));
                        await next();
                        return;
                    }
                }
            }
        }

        // ============ すべてのチェックで権限なし ============
        logger.LogWarning("[RequirePermission] Permission denied: UserId={UserId}, Permissions={Permissions}",
            userId, string.Join(", ", _permissionNames));
        context.Result = new ForbidResult();
    }

    private Guid? GetRoomId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        // アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(RoomIdKey, out var roomIdObj))
        {
            if (roomIdObj is Guid guid)
            {
                return guid;
            }
            if (roomIdObj is MaskedGuid maskedGuid)
            {
                return maskedGuid;
            }
            if (Guid.TryParse(roomIdObj?.ToString(), out var parsedRoomId))
            {
                return parsedRoomId;
            }
            // MaskedUUIDとしてデコードを試みる
            if (roomIdObj?.ToString() is string roomIdStr && !string.IsNullOrWhiteSpace(roomIdStr))
            {
                try
                {
                    return maskedUuidService.DecodeSynchronous(roomIdStr);
                }
                catch
                {
                    // デコード失敗は無視
                }
            }
        }

        // ルートパラメータから取得（フォールバック）
        if (FallbackToRoute)
        {
            if (context.RouteData.Values.TryGetValue(RoomIdKey, out var routeRoomIdObj))
            {
                if (routeRoomIdObj is Guid routeGuid)
                {
                    return routeGuid;
                }
                if (Guid.TryParse(routeRoomIdObj?.ToString(), out var routeRoomId))
                {
                    return routeRoomId;
                }
                // MaskedUUIDとしてデコードを試みる
                if (routeRoomIdObj?.ToString() is string routeRoomIdStr && !string.IsNullOrWhiteSpace(routeRoomIdStr))
                {
                    try
                    {
                        return maskedUuidService.DecodeSynchronous(routeRoomIdStr);
                    }
                    catch
                    {
                        // デコード失敗は無視
                    }
                }
            }
        }

        return null;
    }

    private Guid? GetTopicId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        // アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(TopicIdKey, out var topicIdObj))
        {
            if (topicIdObj is Guid guid)
            {
                return guid;
            }
            if (topicIdObj is MaskedGuid maskedGuid)
            {
                return maskedGuid;
            }
            if (Guid.TryParse(topicIdObj?.ToString(), out var parsedTopicId))
            {
                return parsedTopicId;
            }
            // MaskedUUIDとしてデコードを試みる
            if (topicIdObj?.ToString() is string topicIdStr && !string.IsNullOrWhiteSpace(topicIdStr))
            {
                try
                {
                    return maskedUuidService.DecodeSynchronous(topicIdStr);
                }
                catch
                {
                    // デコード失敗は無視
                }
            }
        }

        // ルートパラメータから取得（フォールバック）
        if (FallbackToRoute)
        {
            if (context.RouteData.Values.TryGetValue(TopicIdKey, out var routeTopicIdObj))
            {
                if (routeTopicIdObj is Guid routeGuid)
                {
                    return routeGuid;
                }
                if (Guid.TryParse(routeTopicIdObj?.ToString(), out var routeTopicId))
                {
                    return routeTopicId;
                }
                // MaskedUUIDとしてデコードを試みる
                if (routeTopicIdObj?.ToString() is string routeTopicIdStr && !string.IsNullOrWhiteSpace(routeTopicIdStr))
                {
                    try
                    {
                        return maskedUuidService.DecodeSynchronous(routeTopicIdStr);
                    }
                    catch
                    {
                        // デコード失敗は無視
                    }
                }
            }
        }

        return null;
    }
}
