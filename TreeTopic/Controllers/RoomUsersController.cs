using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using MaskedUUID.AspNetCore.Types;
using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Repositories;
using TreeTopic.Common;
using TreeTopic.Services;
using TreeTopic.Constants;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class RoomUsersController : ControllerBase
{
    private readonly IRoomUserRepository _roomUserRepository;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly IconService _iconService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoomUserManager _roomUserManager;
    private readonly ApplicationDbContext _dbContext;

    public RoomUsersController(
        IRoomUserRepository roomUserRepository,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        IconService iconService,
        UserManager<ApplicationUser> userManager,
        RoomUserManager roomUserManager,
        ApplicationDbContext dbContext)
    {
        _roomUserRepository = roomUserRepository;
        _tenantAccessor = tenantAccessor;
        _iconService = iconService;
        _userManager = userManager;
        _roomUserManager = roomUserManager;
        _dbContext = dbContext;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;
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

    [HttpGet("room/{roomId}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> ListByRoom([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var entities = await _roomUserRepository.GetByRoomIdAsync((Guid)roomId, cancellationToken);
        var dtos = entities.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("room/{roomId}/me")]
    public async Task<IActionResult> GetMyRoomUser([FromRoute] MaskedGuid roomId, CancellationToken cancellationToken)
    {
        var entity = await _roomUserRepository.GetByRoomAndUserAsync((Guid)roomId, CurrentUserId, cancellationToken);
        if (entity == null)
            return NotFound();

        // Ensure ApplicationUser has an icon if using main icon
        if (entity.UseMainIcon && entity.ApplicationUser != null)
        {
            var iconFileName = await EnsureApplicationUserIconAsync(entity.ApplicationUser, cancellationToken);
            if (iconFileName != null)
            {
                entity.ApplicationUser.IconFileName = iconFileName;
            }
        }

        return Ok(MapToDto(entity));
    }

    [HttpGet("user/{userId}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> ListByUser([FromRoute] MaskedGuid userId, CancellationToken cancellationToken)
    {
        var entities = await _roomUserRepository.GetByUserIdAsync((Guid)userId, cancellationToken);
        var dtos = entities.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpPost("room/{roomId}/join")]
    [RequireRoomJoinAccess]
    public async Task<IActionResult> Join([FromRoute] MaskedGuid roomId, [FromBody] JoinRoomUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var currentApplicationUser = await GetCurrentApplicationUserAsync();
        if (currentApplicationUser == null)
        {
            return RedirectToTenantLogin();
        }

        var existing = await _roomUserRepository.GetByRoomAndUserAsync((Guid)roomId, CurrentUserId, cancellationToken);
        if (existing != null)
        {
            ApplyNameSettings(existing, request.Name, request.UseMainName);
            ApplyIconSettings(existing, request.UseMainIcon);

            if (!existing.UseMainIcon && string.IsNullOrWhiteSpace(existing.IconFileName))
            {
                var seedName = RoomUserNameHelper.ResolveDisplayName(existing);
                existing.IconFileName = await _iconService.EnsureDefaultRoomUserIconAsync(existing, seedName, cancellationToken);
            }
            _roomUserRepository.Update(existing);
            await _roomUserRepository.SaveChangesAsync(cancellationToken);
            await EnsureDefaultJoinPermissionsAsync(existing, cancellationToken);

            // Ensure ApplicationUser has icon if using main icon
            if (existing.UseMainIcon && existing.ApplicationUser != null)
            {
                var iconFileName = await EnsureApplicationUserIconAsync(existing.ApplicationUser, cancellationToken);
                if (iconFileName != null)
                {
                    existing.ApplicationUser.IconFileName = iconFileName;
                }
            }

            return Ok(MapToDto(existing));
        }

        var toCreate = new RoomUser
        {
            ApplicationUserId = CurrentUserId,
            RoomId = (Guid)roomId,
            UseMainIcon = true
        };

        ApplyNameSettings(toCreate, request.Name, request.UseMainName);
        ApplyIconSettings(toCreate, request.UseMainIcon);

        if (!toCreate.UseMainIcon && string.IsNullOrWhiteSpace(toCreate.IconFileName))
        {
            var seedName = !string.IsNullOrWhiteSpace(request.Name) ? request.Name : "User";
            toCreate.IconFileName = await _iconService.EnsureDefaultRoomUserIconAsync(toCreate, seedName, cancellationToken);
        }

        await _roomUserRepository.AddAsync(toCreate, cancellationToken);
        await _roomUserRepository.SaveChangesAsync(cancellationToken);
        await EnsureDefaultJoinPermissionsAsync(toCreate, cancellationToken);

        // Ensure ApplicationUser has icon
        if (toCreate.UseMainIcon)
        {
            var iconFileName = await EnsureApplicationUserIconAsync(currentApplicationUser, cancellationToken);
            if (iconFileName != null)
            {
                toCreate.ApplicationUser = currentApplicationUser;
                toCreate.ApplicationUser.IconFileName = iconFileName;
            }
        }

        return Ok(MapToDto(toCreate));
    }

    private async Task EnsureDefaultJoinPermissionsAsync(RoomUser roomUser, CancellationToken cancellationToken)
    {
        var permissions = new[]
        {
            RoomPermissions.Join,
            RoomPermissions.TopicRead,
            RoomPermissions.TopicWrite,
            RoomPermissions.Write
        };

        foreach (var permission in permissions)
        {
            await _roomUserManager.AddPermissionAsync(roomUser, permission, cancellationToken);
        }
    }

    private async Task<ApplicationUser?> GetCurrentApplicationUserAsync()
    {
        if (CurrentUserId == Guid.Empty)
        {
            return null;
        }

        return await _userManager.FindByIdAsync(CurrentUserId.ToString());
    }

    private IActionResult RedirectToTenantLogin()
    {
        var tenantIdentifier = HttpContext.GetRouteValue("tenant")?.ToString();
        var baseLoginPath = string.IsNullOrEmpty(tenantIdentifier)
            ? AuthenticationConstants.Paths.LoginPath
            : $"/{tenantIdentifier}{AuthenticationConstants.Paths.LoginPath}";

        var returnUrl = $"{Request.Path}{Request.QueryString}";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
            return Redirect($"{baseLoginPath}?returnUrl={encodedReturnUrl}");
        }

        return Redirect(baseLoginPath);
    }

    [HttpPost("room/{roomId}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Create([FromRoute] MaskedGuid roomId, [FromBody] CreateRoomUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var toCreate = new RoomUser
        {
            ApplicationUserId = (Guid)request.ApplicationUserId,
            RoomId = (Guid)roomId,
            UseMainIcon = true
        };

        ApplyNameSettings(toCreate, request.Name, request.UseMainName);

        await _roomUserRepository.AddAsync(toCreate);
        await _roomUserRepository.SaveChangesAsync();

        return Ok(MapToDto(toCreate));
    }

    [HttpGet("{id}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetById([FromRoute] MaskedGuid id, CancellationToken cancellationToken)
    {
        var entity = await _roomUserRepository.Query()
            .Include(ru => ru.ApplicationUser)
            .FirstOrDefaultAsync(ru => ru.Id == (Guid)id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(entity));
    }

    [HttpPost("room/{roomId}/me/icon")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyRoomIcon([FromRoute] MaskedGuid roomId, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var roomUser = await _roomUserRepository.GetByRoomAndUserAsync((Guid)roomId, CurrentUserId, cancellationToken);
        if (roomUser == null)
        {
            return Forbid();
        }

        var fileName = await _iconService.SaveRoomUserIconAsync(roomUser, file, cancellationToken);
        roomUser.IconFileName = fileName;
        roomUser.UseMainIcon = false;
        _roomUserRepository.Update(roomUser);
        await _roomUserRepository.SaveChangesAsync(cancellationToken);

        return Ok(new { iconUrl = _iconService.GetRoomUserIconUrl(roomUser) });
    }

    [HttpPut("room/{roomId}/me")]
    public async Task<IActionResult> UpdateMyRoomUser([FromRoute] MaskedGuid roomId, [FromBody] UpdateRoomUserRequest request, CancellationToken cancellationToken)
    {
        var roomUser = await _roomUserRepository.GetByRoomAndUserAsync((Guid)roomId, CurrentUserId, cancellationToken);
        if (roomUser == null)
            return NotFound();

        // 表示名の更新
        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            if (RoomUserNameHelper.IsSyncName(request.DisplayName))
            {
                roomUser.UseMainName = true;
                roomUser.Name = RoomUserNameHelper.DefaultUserToken;
            }
            else
            {
                roomUser.UseMainName = request.UseMainName ?? false;
                roomUser.Name = request.DisplayName;
            }
        }

        // メイン名/メインアイコンの使用設定を更新
        if (request.UseMainName.HasValue)
            roomUser.UseMainName = request.UseMainName.Value;

        if (request.UseMainIcon.HasValue)
        {
            roomUser.UseMainIcon = request.UseMainIcon.Value;
            if (request.UseMainIcon.Value)
                roomUser.IconFileName = null;
        }

        // デフォルトアイコンの生成が必要な場合
        if (!roomUser.UseMainIcon && string.IsNullOrWhiteSpace(roomUser.IconFileName))
        {
            var seedName = RoomUserNameHelper.ResolveDisplayName(roomUser);
            roomUser.IconFileName = await _iconService.EnsureDefaultRoomUserIconAsync(roomUser, seedName, cancellationToken);
        }

        _roomUserRepository.Update(roomUser);
        await _roomUserRepository.SaveChangesAsync(cancellationToken);

        return Ok(MapToDto(roomUser));
    }

    [HttpPut("room/{roomId}/me/icon/use-main")]
    public async Task<IActionResult> SetUseMainIcon([FromRoute] MaskedGuid roomId, [FromBody] bool useMainIcon, CancellationToken cancellationToken)
    {
        var roomUser = await _roomUserRepository.GetByRoomAndUserAsync((Guid)roomId, CurrentUserId, cancellationToken);
        if (roomUser == null)
            return NotFound();

        roomUser.UseMainIcon = useMainIcon;
        if (useMainIcon)
            roomUser.IconFileName = null;
        else if (string.IsNullOrWhiteSpace(roomUser.IconFileName))
        {
            var seedName = RoomUserNameHelper.ResolveDisplayName(roomUser);
            roomUser.IconFileName = await _iconService.EnsureDefaultRoomUserIconAsync(roomUser, seedName, cancellationToken);
        }

        _roomUserRepository.Update(roomUser);
        await _roomUserRepository.SaveChangesAsync(cancellationToken);

        return Ok(MapToDto(roomUser));
    }

    [HttpDelete("{id}")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid id)
    {
        var entity = await _roomUserRepository.GetByIdAsync(id);
        if (entity == null)
        {
            return NotFound();
        }

        _roomUserRepository.Delete(entity);
        await _roomUserRepository.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> EnsureApplicationUserIconAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // If user already has an icon file, nothing to do
        if (!string.IsNullOrWhiteSpace(user.IconFileName))
            return user.IconFileName;

        // Generate default icon for user if they don't have one
        var displayName = user.DisplayName ?? user.UserName ?? user.Email ?? "User";
        var generatedFileName = await _iconService.EnsureDefaultUserIconAsync(user, cancellationToken);

        // Update the user's icon filename
        if (generatedFileName != null)
        {
            user.IconFileName = generatedFileName;
            await _userManager.UpdateAsync(user);
            return generatedFileName;
        }

        return null;
    }

    private RoomUserDto MapToDto(RoomUser entity)
    {
        return new RoomUserDto
        {
            Id = entity.Id,
            ApplicationUserId = entity.ApplicationUserId,
            RoomId = entity.RoomId,
            // DisplayName and IconUrl are already resolved based on UseMainName/UseMainIcon settings
            DisplayName = RoomUserNameHelper.ResolveDisplayName(entity),
            IconUrl = _iconService.GetRoomUserIconUrl(entity),
            UseMainName = entity.UseMainName,
            UseMainIcon = entity.UseMainIcon
        };
    }

    private static void ApplyNameSettings(RoomUser target, string? name, bool? useMainName)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            target.Name = name.Trim();
        }

        if (RoomUserNameHelper.IsSyncName(target.Name))
        {
            target.UseMainName = true;
            target.Name = RoomUserNameHelper.DefaultUserToken;
            return;
        }

        if (useMainName.HasValue)
        {
            target.UseMainName = useMainName.Value;
            if (useMainName.Value)
            {
                target.Name = RoomUserNameHelper.DefaultUserToken;
            }
        }
    }

    private static void ApplyIconSettings(RoomUser target, bool? useMainIcon)
    {
        if (useMainIcon.HasValue)
        {
            target.UseMainIcon = useMainIcon.Value;
            if (useMainIcon.Value)
                target.IconFileName = null;
        }
    }

    /// <summary>
    /// RoomUserのRoomRoleを設定（既存のロールをすべて置き換え）
    /// </summary>
    [HttpPut("{roomUserId}/role")]
    [RequireAny(RoomPermissions.ManageUsers, TenantPermissions.RoomManage)]
    public async Task<IActionResult> SetUserRole(
        [FromRoute] MaskedGuid roomUserId,
        [FromBody] SetRoomUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var roomUserGuid = (Guid)roomUserId;

        // RoomUserの存在確認
        var roomUser = await _roomUserRepository.Query()
            .Include(ru => ru.ApplicationUser)
            .FirstOrDefaultAsync(ru => ru.Id == roomUserGuid, cancellationToken);
        if (roomUser == null)
        {
            return NotFound(new { message = "RoomUser not found" });
        }

        // RoleIdがnullの場合は、全てのロールを削除
        if (request.RoleId.HasValue)
        {
            var roleId = (Guid)request.RoleId.Value;
            var role = await _dbContext.RoomRoles
                .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
            if (role == null)
            {
                return NotFound(new { message = $"RoomRole '{request.RoleId}' not found" });
            }

            // RoomUserManagerを使ってロールを設定（既存のロールを置き換え）
            await _roomUserManager.SetRolesAsync(roomUser, new List<Guid> { roleId }, cancellationToken);
        }
        else
        {
            // roleIdが空の場合は、全てのロールを削除
            await _roomUserManager.SetRolesAsync(roomUser, new List<Guid>(), cancellationToken);
        }

        return Ok(MapToDto(roomUser));
    }
}




