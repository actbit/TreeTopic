using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using MaskedUUID.AspNetCore.Services;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Permissions;
using System.Linq;

namespace TreeTopic.Filters;

/// <summary>
/// すべての権限を持っているかチェック（AND条件）
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAllAttribute : Attribute, IAsyncActionFilter
{
    private readonly PermissionRequirement[] _requirements;

    /// <summary>
    /// トピックIDのアクションパラメーター名（TopicPermissionチェック用）
    /// </summary>
    public string TopicIdKey { get; set; } = "topicId";

    /// <summary>
    /// ルームIDのアクションパラメーター名（RoomPermissionチェック用）
    /// </summary>
    public string RoomIdKey { get; set; } = "roomId";

    /// <summary>
    /// ルームユーザーIDのアクションパラメーター名（RoomPermissionチェックのRoomId解決用）
    /// </summary>
    public string RoomUserIdKey { get; set; } = "roomUserId";

    /// <summary>
    /// IDをルートパラメーターからも取得するか
    /// </summary>
    public bool FallbackToRoute { get; set; } = true;

    /// <summary>
    /// ルートパラメーターからRoomIdが取得できない場合、TopicからRoomIdを取得するか
    /// </summary>
    public bool ResolveRoomIdFromTopic { get; set; } = true;

    /// <summary>
    /// ルートパラメーターからRoomIdが取得できない場合、RoomUserからRoomIdを取得するか
    /// </summary>
    public bool ResolveRoomIdFromRoomUser { get; set; } = true;

    /// <summary>
    /// 文字列配列からPermissionRequirementを構築するコンストラクター
    /// </summary>
    public RequireAllAttribute(params string[] permissions)
    {
        _requirements = permissions?.Select(ParsePermissionRequirement).ToArray()
            ?? Array.Empty<PermissionRequirement>();
    }

    private static PermissionRequirement ParsePermissionRequirement(string permission)
    {
        if (permission.StartsWith("identity.") || permission.StartsWith("tenant."))
            return new PermissionRequirement(PermissionScope.Role, permission);
        if (permission.StartsWith("room."))
            return new PermissionRequirement(PermissionScope.Room, permission);
        if (permission.StartsWith("topic."))
            return new PermissionRequirement(PermissionScope.Topic, permission);
        // デフォルトはRoleスコープ
        return new PermissionRequirement(PermissionScope.Role, permission);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAllAttribute>>();
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var roomUserManager = httpContext.RequestServices.GetRequiredService<RoomUserManager>();
        var topicPermissionManager = httpContext.RequestServices.GetRequiredService<TopicPermissionManager>();
        var maskedUuidService = httpContext.RequestServices.GetRequiredService<IMaskedUUIDService>();

        // 認証済みユーザーを取得
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
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

        // ユーザーのロールを取得（Identity管理ロール + claimsロールをマージ）
        var identityRoles = await userManager.GetRolesAsync(appUser);
        var claimRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var roles = new HashSet<string>(identityRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var claimRole in claimRoles)
        {
            if (!string.IsNullOrWhiteSpace(claimRole))
            {
                roles.Add(claimRole);
            }
        }

        // RoomId/TopicIdを事前に取得（必要な場合のみ）
        var roomId = GetRoomId(context, maskedUuidService);
        var topicId = GetTopicId(context, maskedUuidService);
        var roomUserId = GetRoomUserId(context, maskedUuidService);

        // RoomIdがない場合、Topicから解決
        if (!roomId.HasValue && topicId.HasValue && ResolveRoomIdFromTopic)
        {
            var topic = await dbContext.Topics
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == topicId.Value, httpContext.RequestAborted);
            if (topic != null)
            {
                roomId = topic.RoomId;
            }
        }

        // RoomIdがない場合、RoomUserから解決
        if (!roomId.HasValue && roomUserId.HasValue && ResolveRoomIdFromRoomUser)
        {
            roomId = await dbContext.RoomUsers
                .AsNoTracking()
                .Where(ru => ru.Id == roomUserId.Value)
                .Select(ru => (Guid?)ru.RoomId)
                .FirstOrDefaultAsync(httpContext.RequestAborted);
        }

        RoomUser? roomUser = null;
        if (roomId.HasValue)
        {
            roomUser = await roomUserManager.FindByRoomAndUserAsync(
                roomId.Value, userId, httpContext.RequestAborted);
        }

        // 各要件をチェック（すべて満たす必要がある）
        var missingPermissions = new List<PermissionRequirement>();

        foreach (var requirement in _requirements)
        {
            var hasPermission = await CheckPermissionAsync(
                requirement, roles, roomUser, roomId, topicId,
                dbContext, roomUserManager, topicPermissionManager,
                httpContext.RequestAborted);

            if (!hasPermission)
            {
                missingPermissions.Add(requirement);
            }
        }

        // 不足している権限があれば拒否
        if (missingPermissions.Any())
        {
            logger.LogWarning(
                "[RequireAll] Permission denied: UserId={UserId}, Missing={Missing}, Required={Required}",
                userId,
                string.Join(", ", missingPermissions.Select(r => r.ToString())),
                string.Join(", ", _requirements.Select(r => r.ToString())));
            context.Result = new ForbidResult();
            return;
        }

        // すべての権限を持っている
        logger.LogDebug(
            "[RequireAll] All permissions granted: UserId={UserId}, Requirements={Requirements}",
            userId, string.Join(", ", _requirements.Select(r => r.ToString())));
        await next();
    }

    private static async Task<bool> CheckPermissionAsync(
        PermissionRequirement requirement,
        ISet<string> roles,
        RoomUser? roomUser,
        Guid? roomId,
        Guid? topicId,
        ApplicationDbContext dbContext,
        RoomUserManager roomUserManager,
        TopicPermissionManager topicPermissionManager,
        CancellationToken cancellationToken)
    {
        switch (requirement.Scope)
        {
            case PermissionScope.Role:
                // Role（グローバル）権限のみチェック
                return await CheckRolePermissionAsync(requirement.Name, roles, dbContext, cancellationToken);

            case PermissionScope.Room:
                // Room権限のみチェック
                if (roomUser == null) return false;
                var roomPermissions = await roomUserManager.GetPermissionsAsync(roomUser, cancellationToken);
                return roomPermissions.Contains(requirement.Name);

            case PermissionScope.Topic:
                // Topic権限のみチェック
                if (roomUser == null || !topicId.HasValue) return false;
                return await topicPermissionManager.HasPermissionAsync(
                    roomUser, topicId.Value, requirement.Name, cancellationToken);

            default:
                return false;
        }
    }

    private static async Task<bool> CheckRolePermissionAsync(
        string permissionName,
        ISet<string> roles,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var hasPermission = await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p => p.Role != null &&
                          roles.Contains(p.Role.Name) &&
                          p.Name == permissionName,
                      cancellationToken);
        return hasPermission;
    }

    private Guid? GetRoomId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        // アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(RoomIdKey, out var roomIdObj))
        {
            if (roomIdObj is Guid guid) return guid;
            if (roomIdObj is MaskedGuid maskedGuid) return maskedGuid;
            if (Guid.TryParse(roomIdObj?.ToString(), out var parsedRoomId)) return parsedRoomId;
            if (roomIdObj?.ToString() is string roomIdStr && !string.IsNullOrWhiteSpace(roomIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(roomIdStr); }
                catch { /* decode failed */ }
            }
        }

        // ルートパラメータから取得
        if (FallbackToRoute &&
            context.RouteData.Values.TryGetValue(RoomIdKey, out var routeRoomIdObj))
        {
            if (routeRoomIdObj is Guid routeGuid) return routeGuid;
            if (Guid.TryParse(routeRoomIdObj?.ToString(), out var routeRoomId)) return routeRoomId;
            if (routeRoomIdObj?.ToString() is string routeRoomIdStr && !string.IsNullOrWhiteSpace(routeRoomIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(routeRoomIdStr); }
                catch { /* decode failed */ }
            }
        }

        return null;
    }

    private Guid? GetTopicId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        // アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(TopicIdKey, out var topicIdObj))
        {
            if (topicIdObj is Guid guid) return guid;
            if (topicIdObj is MaskedGuid maskedGuid) return maskedGuid;
            if (Guid.TryParse(topicIdObj?.ToString(), out var parsedTopicId)) return parsedTopicId;
            if (topicIdObj?.ToString() is string topicIdStr && !string.IsNullOrWhiteSpace(topicIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(topicIdStr); }
                catch { /* decode failed */ }
            }
        }

        // ルートパラメータから取得
        if (FallbackToRoute &&
            context.RouteData.Values.TryGetValue(TopicIdKey, out var routeTopicIdObj))
        {
            if (routeTopicIdObj is Guid routeGuid) return routeGuid;
            if (Guid.TryParse(routeTopicIdObj?.ToString(), out var routeTopicId)) return routeTopicId;
            if (routeTopicIdObj?.ToString() is string routeTopicIdStr && !string.IsNullOrWhiteSpace(routeTopicIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(routeTopicIdStr); }
                catch { /* decode failed */ }
            }
        }

        return null;
    }

    private Guid? GetRoomUserId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        // アクションパラメータから取得
        if (context.ActionArguments.TryGetValue(RoomUserIdKey, out var roomUserIdObj))
        {
            if (roomUserIdObj is Guid guid) return guid;
            if (roomUserIdObj is MaskedGuid maskedGuid) return maskedGuid;
            if (Guid.TryParse(roomUserIdObj?.ToString(), out var parsedRoomUserId)) return parsedRoomUserId;
            if (roomUserIdObj?.ToString() is string roomUserIdStr && !string.IsNullOrWhiteSpace(roomUserIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(roomUserIdStr); }
                catch { /* decode failed */ }
            }
        }

        // ルートパラメータから取得
        if (FallbackToRoute &&
            context.RouteData.Values.TryGetValue(RoomUserIdKey, out var routeRoomUserIdObj))
        {
            if (routeRoomUserIdObj is Guid routeGuid) return routeGuid;
            if (Guid.TryParse(routeRoomUserIdObj?.ToString(), out var routeRoomUserId)) return routeRoomUserId;
            if (routeRoomUserIdObj?.ToString() is string routeRoomUserIdStr && !string.IsNullOrWhiteSpace(routeRoomUserIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(routeRoomUserIdStr); }
                catch { /* decode failed */ }
            }
        }

        return null;
    }
}
