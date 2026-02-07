using MaskedUUID.AspNetCore.Services;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Filters;

internal static class PermissionFilterHelper
{
    public static Guid? GetId(
        ActionExecutingContext context,
        string key,
        IMaskedUUIDService maskedUuidService,
        bool fallbackToRoute = true)
    {
        if (context.ActionArguments.TryGetValue(key, out var value))
        {
            if (value is Guid guid) return guid;
            if (value is MaskedGuid maskedGuid) return maskedGuid;
            if (Guid.TryParse(value?.ToString(), out var parsed)) return parsed;
            if (value?.ToString() is string text && !string.IsNullOrWhiteSpace(text))
            {
                try { return maskedUuidService.DecodeSynchronous(text); }
                catch { }
            }
        }

        if (fallbackToRoute && context.RouteData.Values.TryGetValue(key, out var routeValue))
        {
            if (routeValue is Guid routeGuid) return routeGuid;
            if (Guid.TryParse(routeValue?.ToString(), out var parsedRoute)) return parsedRoute;
            if (routeValue?.ToString() is string routeText && !string.IsNullOrWhiteSpace(routeText))
            {
                try { return maskedUuidService.DecodeSynchronous(routeText); }
                catch { }
            }
        }

        return null;
    }

    public static bool TryGetCurrentUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out userId);
    }

    public static async Task<HashSet<string>> GetMergedRolesAsync(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        ApplicationUser appUser)
    {
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

        return roles;
    }

    public static async Task<Guid?> ResolveTopicIdAsync(
        Guid? topicId,
        Guid? boardId,
        bool resolveFromBoard,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolved = topicId;

        if (!resolved.HasValue && boardId.HasValue && resolveFromBoard)
        {
            resolved = await dbContext.BrainBoards
                .AsNoTracking()
                .Where(b => b.Id == boardId.Value)
                .Select(b => (Guid?)b.TopicId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return resolved;
    }

    public static async Task<Guid?> ResolveRoomIdAsync(
        Guid? roomId,
        Guid? topicId,
        Guid? roomUserId,
        bool resolveFromTopic,
        bool resolveFromRoomUser,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var resolved = roomId;

        if (!resolved.HasValue && topicId.HasValue && resolveFromTopic)
        {
            resolved = await dbContext.Topics
                .AsNoTracking()
                .Where(t => t.Id == topicId.Value)
                .Select(t => (Guid?)t.RoomId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolved.HasValue && roomUserId.HasValue && resolveFromRoomUser)
        {
            resolved = await dbContext.RoomUsers
                .AsNoTracking()
                .Where(ru => ru.Id == roomUserId.Value)
                .Select(ru => (Guid?)ru.RoomId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return resolved;
    }

    public static async Task<bool> CheckPermissionAsync(
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

                // Room権限で満たせない場合のみ、Topic権限を取得
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

    public static bool CanSatisfyTopicPermissionViaRoomPermission(
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

    public static async Task<bool> HasTopicPermissionInRoomAsync(
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

    public static async Task<HashSet<string>> GetRolePermissionsFromDbAsync(
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
