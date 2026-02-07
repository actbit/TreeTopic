using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Permissions;
using TreeTopic.Extensions;

namespace TreeTopic.Filters;

/// <summary>
/// 権限のいずれかを持っているかチェック（OR条件）
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAnyAttribute : Attribute, IAsyncActionFilter
{
    private readonly PermissionRequirement[] _requirements;

    public string TopicIdKey { get; set; } = "topicId";
    public string RoomIdKey { get; set; } = "roomId";
    public string RoomUserIdKey { get; set; } = "roomUserId";
    public string BoardIdKey { get; set; } = "boardId";
    public bool FallbackToRoute { get; set; } = true;
    public bool ResolveRoomIdFromTopic { get; set; } = true;
    public bool ResolveRoomIdFromRoomUser { get; set; } = true;
    public bool ResolveTopicIdFromBoard { get; set; } = true;

    public RequireAnyAttribute(params string[] permissions)
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
        return new PermissionRequirement(PermissionScope.Role, permission);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RequireAnyAttribute>>();
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var roomUserManager = httpContext.RequestServices.GetRequiredService<RoomUserManager>();
        var topicPermissionManager = httpContext.RequestServices.GetRequiredService<TopicPermissionManager>();
        var maskedUuidService = httpContext.RequestServices.GetRequiredService<IMaskedUUIDService>();

        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!PermissionFilterHelper.TryGetCurrentUserId(user, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // 【キャッシュ】ユーザー情報
        var appUser = await httpContext.GetOrCreateAsync(
            $"user_{userId}",
            async () => await userManager.FindByIdAsync(userId.ToString())
        );

        if (appUser == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // 【キャッシュ】マージされたロール
        var roles = await httpContext.GetOrCreateAsync(
            $"user_roles_{userId}",
            async () => await PermissionFilterHelper.GetMergedRolesAsync(user, userManager, appUser)
        );

        // 【キャッシュ】テナントロール権限（Userレベル）
        var rolePermissions = await httpContext.GetOrCreateAsync(
            "role_perms",
            async () => await PermissionFilterHelper.GetRolePermissionsFromDbAsync(roles, dbContext, httpContext.RequestAborted)
        );

        var roomId = PermissionFilterHelper.GetId(context, RoomIdKey, maskedUuidService, FallbackToRoute);
        var topicId = PermissionFilterHelper.GetId(context, TopicIdKey, maskedUuidService, FallbackToRoute);
        var roomUserId = PermissionFilterHelper.GetId(context, RoomUserIdKey, maskedUuidService, FallbackToRoute);
        var boardId = PermissionFilterHelper.GetId(context, BoardIdKey, maskedUuidService, FallbackToRoute);

        topicId = await PermissionFilterHelper.ResolveTopicIdAsync(
            topicId,
            boardId,
            ResolveTopicIdFromBoard,
            dbContext,
            httpContext.RequestAborted);

        roomId = await PermissionFilterHelper.ResolveRoomIdAsync(
            roomId,
            topicId,
            roomUserId,
            ResolveRoomIdFromTopic,
            ResolveRoomIdFromRoomUser,
            dbContext,
            httpContext.RequestAborted);

        RoomUser? roomUser = null;
        HashSet<string>? roomPermissions = null;

        if (roomId.HasValue)
        {
            // 【キャッシュ】RoomUser情報
            roomUser = await httpContext.GetOrCreateAsync(
                $"roomUser_{userId}_{roomId.Value}",
                async () => await roomUserManager.FindByRoomAndUserAsync(roomId.Value, userId, httpContext.RequestAborted)
            );

            // 【キャッシュ】Room権限（RoomUserレベル）
            if (roomUser != null)
            {
                roomPermissions = await httpContext.GetOrCreateAsync(
                    $"room_perms_{userId}_{roomId.Value}",
                    async () => await roomUserManager.GetPermissionsAsync(roomUser, httpContext.RequestAborted)
                );
            }
        }

        foreach (var requirement in _requirements)
        {
            var hasPermission = await PermissionFilterHelper.CheckPermissionAsync(
                requirement,
                rolePermissions,
                roomUser,
                roomPermissions,
                roomId,
                topicId,
                httpContext,
                dbContext,
                topicPermissionManager,
                httpContext.RequestAborted);

            if (hasPermission)
            {
                logger.LogDebug(
                    "[RequireAny] Permission granted: UserId={UserId}, Requirement={Requirement}",
                    userId, requirement);
                await next();
                return;
            }
        }

        logger.LogWarning(
            "[RequireAny] Permission denied: UserId={UserId}, Requirements={Requirements}",
            userId, string.Join(", ", _requirements.Select(r => r.ToString())));
        context.Result = new ForbidResult();
    }
}
