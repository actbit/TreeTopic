using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Services;

/// <summary>
/// SignalR Hub のリアルタイムアクセス制御サービス
/// テナント分離は Finbuckle.MultiTenant のクエリフィルタにより自動適用される
/// Hubメソッド引数のMaskedGuidは、MaskedGuidConverterによって自動的にデコードされる
/// </summary>
public class RealtimeAccessService : IRealtimeAccessService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RealtimeAccessService> _logger;

    public RealtimeAccessService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<RealtimeAccessService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<bool> CanJoinTopicAsync(MaskedGuid topicId, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId))
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinTopic denied: no user context");
            return false;
        }

        // トピックのルームIDを取得（Finbuckleのクエリフィルタがテナント分離を自動適用）
        var roomId = await _dbContext.Topics
            .AsNoTracking()
            .Where(t => t.Id == topicId)
            .Select(t => (Guid?)t.RoomId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!roomId.HasValue)
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinTopic denied: topic not found {TopicId}", topicId);
            return false;
        }

        // RoomUserとして参加しているか確認
        var isRoomUser = await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomId.Value && ru.ApplicationUserId == currentUserId, cancellationToken);
        if (isRoomUser)
        {
            return true;
        }

        // テナントレベルの権限チェック
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

    public async Task<bool> CanJoinRoomAsync(MaskedGuid roomId, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId))
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinRoom denied: no user context");
            return false;
        }

        // ルームの存在確認（Finbuckleのクエリフィルタがテナント分離を自動適用）
        var roomExists = await _dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(r => r.Id == roomId, cancellationToken);

        if (!roomExists)
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinRoom denied: room not found {RoomId}", roomId);
            return false;
        }

        // RoomUserとして参加しているか確認
        var isRoomUser = await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomId && ru.ApplicationUserId == currentUserId, cancellationToken);
        if (isRoomUser)
        {
            return true;
        }

        // テナントレベルの権限チェック
        return await HasAnyTenantPermissionAsync(
            user,
            currentUserId,
            new[] { TenantPermissions.RoomRead, TenantPermissions.RoomManage },
            cancellationToken);
    }

    public async Task<bool> CanJoinRoomUserGroupAsync(
        MaskedGuid roomId,
        MaskedGuid userId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(user, out var currentUserId))
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinRoomUserGroup denied: no user context");
            return false;
        }

        // ユーザーID照合: 自分のグループにのみ参加可能
        if (currentUserId != userId)
        {
            _logger.LogWarning("[RealtimeAccessService] CanJoinRoomUserGroup denied: userId mismatch. CurrentUserId={CurrentUserId}, RequestedUserId={RequestedUserId}",
                currentUserId, userId);
            return false;
        }

        // RoomUserとして参加しているか確認（Finbuckleのクエリフィルタがテナント分離を自動適用）
        return await _dbContext.RoomUsers
            .AsNoTracking()
            .AnyAsync(ru => ru.RoomId == roomId && ru.ApplicationUserId == currentUserId, cancellationToken);
    }

    private bool TryGetCurrentUserId(ClaimsPrincipal? user, out Guid userId)
    {
        var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out userId);
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
            _logger.LogWarning("[RealtimeAccessService] HasAnyTenantPermission denied: no roles for user {UserId}", userId);
            return false;
        }

        var hasPermission = await _dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .AnyAsync(p =>
                    p.Role != null &&
                    p.Role.Name != null &&
                    roleNames.Contains(p.Role.Name) &&
                    permissionNames.Contains(p.Name),
                cancellationToken);

        if (!hasPermission)
        {
            _logger.LogWarning("[RealtimeAccessService] HasAnyTenantPermission denied: user {UserId} with roles [{Roles}] does not have any of [{Permissions}]",
                userId, string.Join(", ", roleNames), string.Join(", ", permissionNames));
        }

        return hasPermission;
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

        var claimRoles = user?.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
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
