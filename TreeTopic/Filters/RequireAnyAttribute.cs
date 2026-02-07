using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Permissions;
using System.Linq;

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

        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var roles = await PermissionFilterHelper.GetMergedRolesAsync(user, userManager, appUser);

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
        if (roomId.HasValue)
        {
            roomUser = await roomUserManager.FindByRoomAndUserAsync(
                roomId.Value, userId, httpContext.RequestAborted);
        }

        foreach (var requirement in _requirements)
        {
            var hasPermission = await CheckPermissionAsync(
                requirement, roles, roomUser, roomId, topicId,
                dbContext, roomUserManager, topicPermissionManager,
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
                if (requirement.Name.StartsWith("tenant.topic.", StringComparison.OrdinalIgnoreCase) && roomId.HasValue)
                {
                    var hasRoomRead = await HasRoomReadPrerequisiteAsync(roles, roomUser, dbContext, cancellationToken);
                    if (!hasRoomRead)
                    {
                        return false;
                    }
                }
                return await CheckRolePermissionAsync(requirement.Name, roles, dbContext, cancellationToken);

            case PermissionScope.Room:
                if (roomUser == null)
                {
                    return false;
                }
                if (string.Equals(requirement.Name, RoomPermissions.Member, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                var roomPermissions = await roomUserManager.GetPermissionsAsync(roomUser, cancellationToken);
                return roomPermissions.Contains(requirement.Name);

            case PermissionScope.Topic:
                if (roomUser == null)
                {
                    return false;
                }

                var hasReadPrerequisite = await HasRoomReadPrerequisiteAsync(roles, roomUser, dbContext, cancellationToken);
                if (!hasReadPrerequisite)
                {
                    return false;
                }

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

    private static async Task<bool> HasRoomReadPrerequisiteAsync(
        ISet<string> roles,
        RoomUser? roomUser,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (roomUser != null)
        {
            var roomPermissions = await dbContext.RoomUserRoomRoles
                .Where(rur => rur.RoomUserId == roomUser.Id)
                .Join(
                    dbContext.RoomRolePermissions,
                    rur => rur.RoomRoleId,
                    rp => rp.RoomRoleId,
                    (_, rp) => rp.PermissionName)
                .Concat(dbContext.RoomPermissions
                    .Where(rp => rp.RoomUserId == roomUser.Id)
                    .Select(rp => rp.Name))
                .Distinct()
                .ToListAsync(cancellationToken);

            if (roomPermissions.Contains(RoomPermissions.Member, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p => p.Role != null &&
                           p.Role.Name != null &&
                           roles.Contains(p.Role.Name) &&
                           (p.Name == TenantPermissions.RoomRead || p.Name == TenantPermissions.RoomManage),
                cancellationToken);
    }

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

    private static async Task<bool> CheckRolePermissionAsync(
        string permissionName,
        ISet<string> roles,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p => p.Role != null &&
                          p.Role.Name != null &&
                          roles.Contains(p.Role.Name) &&
                          p.Name == permissionName,
                      cancellationToken);
    }
}
