using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Permissions;
using TreeTopic.Extensions;
using System.Linq;

namespace TreeTopic.Filters;

/// <summary>
/// すべての権限を持っているかチェック（AND条件）
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequireAllAttribute : Attribute, IAsyncActionFilter
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
            async () => await GetRolePermissionsFromDbAsync(roles, dbContext, httpContext.RequestAborted)
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

        var missingPermissions = new List<PermissionRequirement>();

        foreach (var requirement in _requirements)
        {
            var hasPermission = await CheckPermissionWithCacheAsync(
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

            if (!hasPermission)
            {
                missingPermissions.Add(requirement);
            }
        }

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

        logger.LogDebug(
            "[RequireAll] All permissions granted: UserId={UserId}, Requirements={Requirements}",
            userId, string.Join(", ", _requirements.Select(r => r.ToString())));
        await next();
    }

    /// <summary>
    /// HttpContextキャッシュを使用した権限チェック
    /// </summary>
    private static async Task<bool> CheckPermissionWithCacheAsync(
        PermissionRequirement requirement,
        HashSet<string> rolePermissions,
        RoomUser? roomUser,
        HashSet<string>? roomPermissions,
        Guid? roomId,
        Guid? topicId,
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        TopicPermissionManager topicPermissionManager,
        CancellationToken cancellationToken)
    {
        switch (requirement.Scope)
        {
            case PermissionScope.Role:
                // テナントトピック権限の場合はRoomアクセス権も確認
                if (requirement.Name.StartsWith("tenant.topic.", StringComparison.OrdinalIgnoreCase) && roomId.HasValue)
                {
                    // Room権限でチェック
                    if (roomPermissions != null && roomPermissions.Contains(RoomPermissions.Member))
                        return rolePermissions.Contains(requirement.Name);

                    // テナント権限でチェック
                    if (rolePermissions.Contains(TenantPermissions.RoomRead) ||
                        rolePermissions.Contains(TenantPermissions.RoomManage))
                        return rolePermissions.Contains(requirement.Name);

                    return false;
                }
                // キャッシュ済みのロール権限を使用
                return rolePermissions.Contains(requirement.Name);

            case PermissionScope.Room:
                if (roomUser == null || roomPermissions == null)
                    return false;

                if (string.Equals(requirement.Name, RoomPermissions.Member, StringComparison.OrdinalIgnoreCase))
                    return true;

                // キャッシュ済みのRoom権限を使用
                return roomPermissions.Contains(requirement.Name);

            case PermissionScope.Topic:
                if (roomUser == null || !roomId.HasValue)
                    return false;

                // 【最適化】まずRoom権限でチェック可能か確認（DBアクセスなし）
                if (CanSatisfyTopicPermissionViaRoomPermission(requirement.Name, roomPermissions))
                    return true;

                // Room権限で満たせない場合のみ、Topic権限を取得（キャッシュしない）
                if (topicId.HasValue)
                {
                    return await topicPermissionManager.HasPermissionAsync(
                        roomUser, topicId.Value, requirement.Name, cancellationToken);
                }

                if (roomId.HasValue)
                {
                    return await HasTopicPermissionInRoomAsync(roomUser, roomId.Value, requirement.Name, dbContext, cancellationToken);
                }

                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Room権限でTopic権限を満たせるか判定（DBアクセスなし）
    /// </summary>
    private static bool CanSatisfyTopicPermissionViaRoomPermission(
        string topicPermission,
        HashSet<string>? roomPermissions)
    {
        if (roomPermissions == null) return false;

        return topicPermission switch
        {
            // room.topic.read → topic.read, topic.readMessages
            TopicPermissions.Read or TopicPermissions.ReadMessages
                when roomPermissions.Contains(RoomPermissions.TopicRead) => true,

            // room.topic.write → topic.write, topic.writeMessages
            TopicPermissions.Write or TopicPermissions.WriteMessages
                when roomPermissions.Contains(RoomPermissions.TopicWrite) => true,

            // room.topic.manage → topic.delete, topic.manage
            TopicPermissions.Delete or TopicPermissions.Manage
                when roomPermissions.Contains(RoomPermissions.TopicManage) => true,

            _ => false
        };
    }

    /// <summary>
    /// Room内のトピック権限チェック（ロールベース）
    /// </summary>
    private static async Task<bool> HasTopicPermissionInRoomAsync(
        RoomUser roomUser,
        Guid roomId,
        string permissionName,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var roleIds = await dbContext.RoomUserRoomRoles
            .AsNoTracking()
            .Where(rur => rur.RoomUserId == roomUser.Id)
            .Select(rur => rur.RoomRoleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var hasRolePermission = roleIds.Count > 0 && await dbContext.TopicRolePermissions
            .AsNoTracking()
            .Where(trp => trp.Name == permissionName && roleIds.Contains(trp.RoomRoleId))
            .Join(dbContext.Topics.AsNoTracking(),
                trp => trp.TopicId,
                t => t.Id,
                (_, t) => t.RoomId)
            .AnyAsync(rId => rId == roomId, cancellationToken);

        if (hasRolePermission)
        {
            return true;
        }

        return await dbContext.TopicUserPermissions
            .AsNoTracking()
            .Where(tup => tup.RoomUserId == roomUser.Id && tup.Name == permissionName)
            .Join(dbContext.Topics.AsNoTracking(),
                tup => tup.TopicId,
                t => t.Id,
                (_, t) => t.RoomId)
            .AnyAsync(rId => rId == roomId, cancellationToken);
    }

    /// <summary>
    /// テナントロール権限をDBから取得
    /// </summary>
    private static async Task<HashSet<string>> GetRolePermissionsFromDbAsync(
        ISet<string> roles,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .Where(p => p.Role != null && p.Role.Name != null && roles.Contains(p.Role.Name))
            .Select(p => p.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
    }
}
