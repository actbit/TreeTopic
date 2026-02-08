using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Permissions;

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
    public string MessageIdKey { get; set; } = "messageId";
    public string FileIdKey { get; set; } = "fileId";
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
        if (permission.StartsWith("tenant."))
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
        var roomUserManager = httpContext.RequestServices.GetRequiredService<RoomUserManager>();
        var topicPermissionManager = httpContext.RequestServices.GetRequiredService<TopicPermissionManager>();

        // TopicId → RoomId の順に解決
        var resolvedTopicId = await PermissionFilterHelper.ResolveTopicIdFromContextAsync(
            context,
            TopicIdKey, BoardIdKey, MessageIdKey, FileIdKey,
            FallbackToRoute, ResolveTopicIdFromBoard,
            httpContext.RequestAborted);

        var resolvedRoomId = await PermissionFilterHelper.ResolveRoomIdFromContextAsync(
            context,
            RoomIdKey, RoomUserIdKey, resolvedTopicId,
            FallbackToRoute, ResolveRoomIdFromTopic, ResolveRoomIdFromRoomUser,
            httpContext.RequestAborted);

        // 共通メソッドでコンテキストを構築
        var (permContext, errorResult) = await PermissionFilterHelper.InitializePermissionContextAsync(
            context,
            resolvedTopicId,
            resolvedRoomId,
            roomUserManager,
            httpContext.RequestAborted);

        if (errorResult != null || permContext == null)
        {
            context.Result = errorResult ?? new UnauthorizedResult();
            return;
        }

        // Topic scope の権限を一括取得（DBアクセスは最大1回）
        var topicPermissions = await PermissionFilterHelper.ResolveTopicPermissionsAsync(
            _requirements,
            permContext.RoomUser,
            permContext.TopicId,
            topicPermissionManager,
            httpContext.RequestAborted);

        foreach (var requirement in _requirements)
        {
            var hasPermission = PermissionFilterHelper.CheckPermission(
                requirement,
                permContext.RolePermissions,
                permContext.RoomUser,
                permContext.RoomPermissions,
                topicPermissions);

            if (hasPermission)
            {
                logger.LogDebug(
                    "[RequireAny] Permission granted: UserId={UserId}, Requirement={Requirement}",
                    permContext.UserId, requirement);
                await next();
                return;
            }
        }

        logger.LogWarning(
            "[RequireAny] Permission denied: UserId={UserId}, Requirements={Requirements}",
            permContext.UserId, string.Join(", ", _requirements.Select(r => r.ToString())));
        context.Result = new ForbidResult();
    }
}
