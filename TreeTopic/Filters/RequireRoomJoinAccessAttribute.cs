using MaskedUUID.AspNetCore.Services;
using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireRoomJoinAccessAttribute : Attribute, IAsyncActionFilter
{
    public string RoomIdKey { get; set; } = "roomId";
    public bool FallbackToRoute { get; set; } = true;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var maskedUuidService = httpContext.RequestServices.GetRequiredService<IMaskedUUIDService>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();

        var roomId = GetRoomId(context, maskedUuidService);
        if (!roomId.HasValue)
        {
            context.Result = new BadRequestObjectResult(new { message = "RoomId is required" });
            return;
        }

        var room = await dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomId.Value, httpContext.RequestAborted);
        if (room == null)
        {
            context.Result = new NotFoundObjectResult(new { message = "Room not found" });
            return;
        }

        if (room.JoinPolicy == RoomJoinPolicy.Public || room.CreatedUserId == userId)
        {
            await next();
            return;
        }

        var alreadyJoined = await dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomId.Value && ru.ApplicationUserId == userId, httpContext.RequestAborted);
        if (alreadyJoined)
        {
            await next();
            return;
        }

        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var identityRoles = await userManager.GetRolesAsync(appUser);
        var claimRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var roleNames = new HashSet<string>(identityRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var claimRole in claimRoles)
        {
            if (!string.IsNullOrWhiteSpace(claimRole))
            {
                roleNames.Add(claimRole);
            }
        }

        var roleIds = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(httpContext.RequestAborted);

        var allowedByUser = await dbContext.RoomJoinUserPermissions
            .AsNoTracking()
            .AnyAsync(p => p.RoomId == roomId.Value && p.ApplicationUserId == userId, httpContext.RequestAborted);
        if (allowedByUser)
        {
            await next();
            return;
        }

        var allowedByJoinRole = await dbContext.RoomJoinRolePermissions
            .AsNoTracking()
            .AnyAsync(p => p.RoomId == roomId.Value &&
                           roleIds.Contains(p.RoleId),
                httpContext.RequestAborted);
        if (allowedByJoinRole)
        {
            await next();
            return;
        }

        var hasTenantRoomManage = await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p => p.Role != null &&
                           !string.IsNullOrWhiteSpace(p.Role.Name) &&
                           roleNames.Contains(p.Role.Name) &&
                           p.Name == TenantPermissions.RoomManage,
                httpContext.RequestAborted);
        if (hasTenantRoomManage)
        {
            await next();
            return;
        }

        context.Result = new ForbidResult();
    }

    private Guid? GetRoomId(ActionExecutingContext context, IMaskedUUIDService maskedUuidService)
    {
        if (context.ActionArguments.TryGetValue(RoomIdKey, out var roomIdObj))
        {
            if (roomIdObj is Guid guid) return guid;
            if (roomIdObj is MaskedGuid maskedGuid) return maskedGuid;
            if (Guid.TryParse(roomIdObj?.ToString(), out var parsedRoomId)) return parsedRoomId;
            if (roomIdObj?.ToString() is string roomIdStr && !string.IsNullOrWhiteSpace(roomIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(roomIdStr); }
                catch { }
            }
        }

        if (FallbackToRoute &&
            context.RouteData.Values.TryGetValue(RoomIdKey, out var routeRoomIdObj))
        {
            if (routeRoomIdObj is Guid routeGuid) return routeGuid;
            if (Guid.TryParse(routeRoomIdObj?.ToString(), out var routeRoomId)) return routeRoomId;
            if (routeRoomIdObj?.ToString() is string routeRoomIdStr && !string.IsNullOrWhiteSpace(routeRoomIdStr))
            {
                try { return maskedUuidService.DecodeSynchronous(routeRoomIdStr); }
                catch { }
            }
        }

        return null;
    }
}
