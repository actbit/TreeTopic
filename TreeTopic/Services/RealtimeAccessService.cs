using MaskedUUID.AspNetCore.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Services;

public class RealtimeAccessService : IRealtimeAccessService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMaskedUUIDService _maskedUuidService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RealtimeAccessService(
        ApplicationDbContext dbContext,
        IMaskedUUIDService maskedUuidService,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _maskedUuidService = maskedUuidService;
        _userManager = userManager;
    }

    public async Task<bool> CanJoinTopicAsync(string topicId, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId) || !TryDecodeGuid(topicId, out var topicGuid))
        {
            return false;
        }

        var roomId = await _dbContext.Topics
            .AsNoTracking()
            .Where(t => t.Id == topicGuid)
            .Select(t => (Guid?)t.RoomId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!roomId.HasValue)
        {
            return false;
        }

        var isRoomUser = await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomId.Value && ru.ApplicationUserId == currentUserId, cancellationToken);
        if (isRoomUser)
        {
            return true;
        }

        return await HasAnyTenantPermissionAsync(
            user,
            currentUserId,
            new[]
            {
                TenantPermissions.TopicReadMessages,
                TenantPermissions.TopicManage,
                TenantPermissions.RoomRead,
                TenantPermissions.RoomManage
            },
            cancellationToken);
    }

    public async Task<bool> CanJoinRoomAsync(string roomId, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId) || !TryDecodeGuid(roomId, out var roomGuid))
        {
            return false;
        }

        var roomExists = await _dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(r => r.Id == roomGuid, cancellationToken);
        if (!roomExists)
        {
            return false;
        }

        var isRoomUser = await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomGuid && ru.ApplicationUserId == currentUserId, cancellationToken);
        if (isRoomUser)
        {
            return true;
        }

        return await HasAnyTenantPermissionAsync(
            user,
            currentUserId,
            new[] { TenantPermissions.RoomRead, TenantPermissions.RoomManage },
            cancellationToken);
    }

    public async Task<bool> CanJoinRoomUserGroupAsync(
        string roomId,
        string userId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId) ||
            !TryDecodeGuid(roomId, out var roomGuid) ||
            !TryDecodeGuid(userId, out var requestedUserId))
        {
            return false;
        }

        if (currentUserId != requestedUserId)
        {
            return false;
        }

        return await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomGuid && ru.ApplicationUserId == currentUserId, cancellationToken);
    }

    private bool TryGetCurrentUserId(ClaimsPrincipal? user, out Guid userId)
    {
        var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out userId);
    }

    private bool TryDecodeGuid(string value, out Guid id)
    {
        if (Guid.TryParse(value, out id))
        {
            return true;
        }

        try
        {
            id = _maskedUuidService.DecodeSynchronous(value);
            return true;
        }
        catch
        {
            id = Guid.Empty;
            return false;
        }
    }

    private async Task<bool> HasAnyTenantPermissionAsync(
        ClaimsPrincipal? user,
        Guid userId,
        IReadOnlyCollection<string> permissionNames,
        CancellationToken cancellationToken)
    {
        var roleNames = await GetCurrentRoleNamesAsync(user, userId);
        if (roleNames.Count == 0)
        {
            return false;
        }

        return await _dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p =>
                    p.Role != null &&
                    p.Role.Name != null &&
                    roleNames.Contains(p.Role.Name) &&
                    permissionNames.Contains(p.Name),
                cancellationToken);
    }

    private async Task<HashSet<string>> GetCurrentRoleNamesAsync(ClaimsPrincipal? user, Guid userId)
    {
        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var appUser = await _userManager.FindByIdAsync(userId.ToString());
        if (appUser != null)
        {
            var identityRoles = await _userManager.GetRolesAsync(appUser);
            foreach (var role in identityRoles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    roleNames.Add(role);
                }
            }
        }

        var claimRoles = user?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
        foreach (var role in claimRoles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                roleNames.Add(role);
            }
        }

        return roleNames;
    }
}
