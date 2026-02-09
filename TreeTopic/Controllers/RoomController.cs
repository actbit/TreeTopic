using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class RoomController : ControllerBase
{
    private readonly IRoomManagementService _roomManagementService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoomController(
        IRoomManagementService roomManagementService,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _roomManagementService = roomManagementService;
        _db = db;
        _userManager = userManager;
    }

    private Guid CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or has invalid user ID.");
            }
            return userId;
        }
    }

    /// <summary>
    /// SQL LIKE の特殊文字をエスケープする（%, _, [, ], \）
    /// </summary>
    private static string EscapeLikePattern(string pattern)
    {
        return pattern
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[")
            .Replace("]", "\\]");
    }

    [HttpGet]
    [RequireAny(PermissionScope.Role, TenantPermissions.RoomRead)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var appUser = await _userManager.FindByIdAsync(CurrentUserId.ToString());
        if (appUser == null)
            return Unauthorized();

        var identityRoles = await _userManager.GetRolesAsync(appUser);
        var claimRolesFromClaims = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var roleNames = new HashSet<string>(identityRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var claimRole in claimRolesFromClaims)
        {
            if (!string.IsNullOrWhiteSpace(claimRole))
                roleNames.Add(claimRole);
        }

        var result = await _roomManagementService.GetAllRoomsAsync(CurrentUserId, roleNames, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{roomId}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Read, TenantPermissions.RoomRead)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.GetRoomByIdAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// 現在のユーザーのルーム権限一覧を取得
    /// </summary>
    [HttpGet("{roomId}/my/permissions")]
    [Authorize]
    public async Task<IActionResult> GetMyPermissions(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        var roomGuid = (Guid)roomId;
        var userId = CurrentUserId;

        // RoomUserを取得
        var roomUser = await _db.RoomUsers
            .Include(ru => ru.RoomUserRoomRoles)
                .ThenInclude(rur => rur.RoomRole)
                    .ThenInclude(rr => rr!.Permissions)
            .Include(ru => ru.RoomPermission)
            .FirstOrDefaultAsync(ru => ru.RoomId == roomGuid && ru.ApplicationUserId == userId, cancellationToken);

        if (roomUser == null)
        {
            return Ok(new RoomPermissionsResponse
            {
                Permissions = new List<string>()
            });
        }

        var permissions = new List<string>();

        // RoomRoleの権限を収集
        foreach (var userRole in roomUser.RoomUserRoomRoles)
        {
            if (userRole.RoomRole?.Permissions != null)
            {
                foreach (var permission in userRole.RoomRole.Permissions)
                {
                    permissions.Add(permission.PermissionName);
                }
            }
        }

        // 直接付与された権限を追加
        if (roomUser.RoomPermission != null && roomUser.RoomPermission.Count > 0)
        {
            foreach (var perm in roomUser.RoomPermission)
            {
                permissions.Add(perm.Name);
            }
        }

        return Ok(new RoomPermissionsResponse
        {
            Permissions = permissions.Distinct().ToList()
        });
    }

    [HttpPost]
    [RequireAny(PermissionScope.Room, RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.CreateRoomAsync(request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{roomId}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Update([FromRoute] MaskedGuid roomId, [FromBody] UpdateRoomRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _roomManagementService.UpdateRoomAsync((Guid)roomId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{roomId}")]
    [RequireAny(PermissionScope.Room, RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var result = await _roomManagementService.DeleteRoomAsync((Guid)roomId, cancellationToken);
        return result.ToApiResult();
    }

    /// <summary>
    /// Roomに追加可能なユーザー候補を取得（Roomメンバー以外のユーザー）
    /// </summary>
    [HttpGet("{roomId}/users/candidates")]
    [RequireAny(PermissionScope.Room, RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetUserCandidates(
        [FromRoute] MaskedGuid roomId,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var roomGuid = (Guid)roomId;

        // LEFT JOINを使用してRoomメンバー以外のユーザーを取得（パフォーマンス改善）
        var query = from u in _db.Users
                    join ru in _db.RoomUsers
                        on u.Id equals ru.ApplicationUserId into roomUsers
                    from existing in roomUsers.Where(ru => ru.RoomId == roomGuid).DefaultIfEmpty()
                    where existing == null // Roomに参加していないユーザーのみ
                    select u;

        // 検索フィルタ（データベース側で大文字小文字を区別しない検索）
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escapedSearch = EscapeLikePattern(search);
            query = query.Where(u =>
                (u.DisplayName != null && EF.Functions.Like(u.DisplayName, $"%{escapedSearch}%")) ||
                (u.UserName != null && EF.Functions.Like(u.UserName, $"%{escapedSearch}%")) ||
                (u.Email != null && EF.Functions.Like(u.Email, $"%{escapedSearch}%")));
        }

        // 結果を取得（ID、UserName、DisplayNameのみ）
        var candidates = await query
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.UserName)
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.DisplayName,
                u.Email
            })
            .Take(50) // 最大50件
            .ToListAsync(cancellationToken);

        return Ok(candidates);
    }
}
