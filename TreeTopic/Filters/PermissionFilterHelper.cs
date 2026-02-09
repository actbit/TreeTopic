using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;
using Finbuckle.MultiTenant;

namespace TreeTopic.Filters;

internal static class PermissionFilterHelper
{
    /// <summary>
    /// 権限チェックのためのコンテキスト情報
    /// </summary>
    public sealed class PermissionContext
    {
        public Guid UserId { get; init; }
        public HashSet<string> RolePermissions { get; init; } = null!;
        public HashSet<string> Roles { get; init; } = null!;
        public Guid? RoomId { get; set; }
        public Guid? TopicId { get; set; }
        public RoomUser? RoomUser { get; set; }
        public HashSet<string>? RoomPermissions { get; set; }
    }

    /// <summary>
    /// 認証・ユーザー情報・ロール権限を取得してPermissionContextを構築する共通メソッド
    /// </summary>
    /// <summary>
    /// TopicIdを各種IDソースから解決する
    /// </summary>
    public static async Task<Guid?> ResolveTopicIdFromContextAsync(
        ActionExecutingContext context,
        string topicIdKey,
        string boardIdKey,
        string messageIdKey,
        string fileIdKey,
        bool fallbackToRoute,
        bool resolveFromBoard,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        var topicId = GetId(context, topicIdKey, fallbackToRoute);
        var boardId = GetId(context, boardIdKey, fallbackToRoute);
        var messageId = GetId(context, messageIdKey, fallbackToRoute);
        var fileId = GetId(context, fileIdKey, fallbackToRoute);

        return await ResolveTopicIdAsync(
            topicId, boardId, messageId, fileId,
            resolveFromBoard, dbContext, cancellationToken);
    }

    /// <summary>
    /// RoomIdを各種IDソースから解決する
    /// </summary>
    public static async Task<Guid?> ResolveRoomIdFromContextAsync(
        ActionExecutingContext context,
        string roomIdKey,
        string roomUserIdKey,
        Guid? resolvedTopicId,
        bool fallbackToRoute,
        bool resolveFromTopic,
        bool resolveFromRoomUser,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

        var roomId = GetId(context, roomIdKey, fallbackToRoute);
        var roomUserId = GetId(context, roomUserIdKey, fallbackToRoute);

        return await ResolveRoomIdAsync(
            roomId, resolvedTopicId, roomUserId,
            resolveFromTopic, resolveFromRoomUser,
            dbContext, cancellationToken);
    }

    public static async Task<(PermissionContext? context, ActionResult? errorResult)> InitializePermissionContextAsync(
        ActionExecutingContext context,
        Guid? resolvedTopicId,
        Guid? resolvedRoomId,
        RoomUserManager roomUserManager,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var userManager = httpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var memoryCache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();

        var user = httpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
            return (null, new UnauthorizedResult());

        if (!TryGetCurrentUserId(user, out var userId))
            return (null, new UnauthorizedResult());

        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
            return (null, new UnauthorizedResult());

        var roles = await GetMergedRolesAsync(user, userManager, appUser);

        // テナントIDを取得
        var tenantInfo = httpContext.GetMultiTenantContext<ApplicationTenantInfo>()?.TenantInfo;
        var tenantId = tenantInfo?.Id ?? string.Empty;

        // IMemoryCacheでロール権限を取得（テナント分離済み）
        var rolePermissions = await GetRolePermissionsFromDbAsync(
            roles, dbContext, memoryCache, tenantId, cancellationToken);

        var permissionContext = new PermissionContext
        {
            UserId = userId,
            RolePermissions = rolePermissions,
            Roles = roles,
            TopicId = resolvedTopicId,
            RoomId = resolvedRoomId
        };

        // RoomUserとRoom権限を取得
        if (resolvedRoomId.HasValue)
        {
            permissionContext.RoomUser = await roomUserManager.FindByRoomAndUserAsync(
                resolvedRoomId.Value, userId, cancellationToken);

            if (permissionContext.RoomUser != null)
            {
                permissionContext.RoomPermissions = await roomUserManager.GetPermissionsAsync(
                    permissionContext.RoomUser, cancellationToken);
            }
        }

        return (permissionContext, null);
    }
    public static Guid? GetId(
        ActionExecutingContext context,
        string key,
        bool fallbackToRoute = true)
    {
        // 1. ActionArguments から直接取得（Route, Query, Body, Form — モデルバインド済み）
        if (context.ActionArguments.TryGetValue(key, out var value))
        {
            var result = TryExtractGuid(value);
            if (result.HasValue)
                return result;
        }

        // 2. Route から取得（フォールバック）
        if (fallbackToRoute && context.RouteData.Values.TryGetValue(key, out var routeValue))
        {
            var result = TryExtractGuid(routeValue);
            if (result.HasValue)
                return result;
        }

        // 3. ActionArguments 内のオブジェクトのプロパティを探索（Body, Form のネストされたプロパティ）
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg == null) continue;
            var type = arg.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(Guid) || type == typeof(MaskedGuid)) continue;

            var prop = type.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var result = TryExtractGuid(prop.GetValue(arg));
                if (result.HasValue)
                    return result;
            }
        }

        return null;
    }

    private static Guid? TryExtractGuid(object? value)
    {
        if (value == null) return null;
        if (value is Guid guid) return guid;
        if (value is MaskedGuid maskedGuid) return (Guid)maskedGuid;
        if (Guid.TryParse(value.ToString(), out var parsed)) return parsed;
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
        Guid? messageId,
        Guid? fileId,
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

        if (!resolved.HasValue && messageId.HasValue)
        {
            resolved = await dbContext.Messages
                .AsNoTracking()
                .Where(m => m.Id == messageId.Value)
                .Select(m => (Guid?)m.TopicId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!resolved.HasValue && fileId.HasValue)
        {
            resolved = await dbContext.Files
                .AsNoTracking()
                .Where(f => f.Id == fileId.Value)
                .Join(dbContext.Messages.AsNoTracking(),
                    f => f.MessageId,
                    m => m.Id,
                    (f, m) => (Guid?)m.TopicId)
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

    public static bool CheckPermission(
        PermissionRequirement requirement,
        HashSet<string> rolePermissions,
        RoomUser? roomUser,
        HashSet<string>? roomPermissions,
        HashSet<string>? topicPermissions)
    {
        switch (requirement.Scope)
        {
            case PermissionScope.Role:
                return rolePermissions.Contains(requirement.Name);

            case PermissionScope.Room:
                return roomPermissions?.Contains(requirement.Name) ?? false;

            case PermissionScope.Topic:
                if (topicPermissions != null)
                    return topicPermissions.Contains(requirement.Name);
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Room/Topicスコープの権限を要求する場合、room.read 相当の権限があるかチェック
    /// </summary>
    internal static bool CheckRoomAccessIfNeeded(
        PermissionScope methodScope,
        PermissionContext permissionContext,
        ILogger logger,
        string attributeName)
    {
        if (methodScope != PermissionScope.Room && methodScope != PermissionScope.Topic)
            return true;

        var hasRoomAccess = HasRoomAccess(
            permissionContext.RoomUser,
            permissionContext.RoomPermissions,
            permissionContext.RolePermissions);

        if (!hasRoomAccess)
        {
            logger.LogWarning(
                "[{AttributeName}] Room access denied: UserId={UserId}, MethodScope={MethodScope}",
                attributeName, permissionContext.UserId, methodScope);
        }

        return hasRoomAccess;
    }

    /// <summary>
    /// Room配下のリソースへのアクセス権があるか判定
    /// </summary>
    internal static bool HasRoomAccess(
        RoomUser? roomUser,
        HashSet<string>? roomPermissions,
        HashSet<string> rolePermissions)
    {
        if (roomUser == null || roomPermissions == null)
            return false;
        return roomPermissions.Contains(RoomPermissions.Read)
            || rolePermissions.Contains(TenantPermissions.RoomRead)
            || rolePermissions.Contains(TenantPermissions.RoomManage);
    }

    /// <summary>
    /// Topic scope の requirement がある場合にTopic権限を一括取得する
    /// </summary>
    public static async Task<HashSet<string>?> ResolveTopicPermissionsAsync(
        PermissionRequirement[] requirements,
        RoomUser? roomUser,
        Guid? topicId,
        TopicPermissionManager topicPermissionManager,
        CancellationToken cancellationToken)
    {
        if (!requirements.Any(r => r.Scope == PermissionScope.Topic))
            return null;

        if (roomUser == null || !topicId.HasValue)
            return null;

        return await topicPermissionManager.GetPermissionsAsync(
            roomUser, topicId.Value, cancellationToken);
    }

    public static async Task<HashSet<string>> GetRolePermissionsFromDbAsync(
        ISet<string> roles,
        ApplicationDbContext dbContext,
        IMemoryCache cache,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            // テナントIDを含めたキャッシュキー（テナント分離）
            var cacheKey = $"tenant_{tenantId}_role_perms_{role}";

            if (cache.TryGetValue(cacheKey, out HashSet<string>? rolePerms))
            {
                permissions.UnionWith(rolePerms);
                continue;
            }

            // DBから取得
            var perms = await dbContext.Permissions
                .AsNoTracking()
                .Include(p => p.Role)
                .Where(p => p.Role != null && p.Role.Name == role)
                .Select(p => p.Name)
                .Distinct()
                .ToListAsync(cancellationToken);

            rolePerms = new HashSet<string>(perms.Where(p => p != null), StringComparer.OrdinalIgnoreCase);

            // キャッシュに保存（30分）
            cache.Set(cacheKey, rolePerms, TimeSpan.FromMinutes(30));

            permissions.UnionWith(rolePerms);
        }

        return permissions;
    }
}
