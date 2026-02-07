using MaskedUUID.AspNetCore.Services;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Models;

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
}
