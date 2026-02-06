using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/rooms/{roomId}/join-permissions")]
[Authorize]
public class RoomJoinPermissionsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<RoomJoinPermissionsController> _logger;

    public RoomJoinPermissionsController(
        ApplicationDbContext dbContext,
        ILogger<RoomJoinPermissionsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomJoinPermissionsResponse>> GetJoinPermissions(
        [FromRoute] MaskedGuid roomId,
        CancellationToken cancellationToken)
    {
        var roomGuid = (Guid)roomId;
        var room = await _dbContext.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roomGuid, cancellationToken);
        if (room == null)
        {
            return NotFound(new { message = "Room not found" });
        }

        var users = await _dbContext.RoomJoinUserPermissions
            .AsNoTracking()
            .Where(p => p.RoomId == roomGuid)
            .Include(p => p.ApplicationUser)
            .Select(p => new RoomJoinAllowedUserDto
            {
                UserId = new MaskedGuid(p.ApplicationUserId),
                UserName = p.ApplicationUser.UserName,
                DisplayName = p.ApplicationUser.DisplayName,
                Email = p.ApplicationUser.Email
            })
            .ToListAsync(cancellationToken);

        var roles = await _dbContext.RoomJoinRolePermissions
            .AsNoTracking()
            .Where(p => p.RoomId == roomGuid)
            .Include(p => p.Role)
            .Select(p => new RoomJoinAllowedRoleDto
            {
                RoleId = new MaskedGuid(p.RoleId),
                RoleName = p.Role.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(new RoomJoinPermissionsResponse
        {
            JoinPolicy = room.JoinPolicy,
            Users = users,
            Roles = roles
        });
    }

    [HttpPut("policy")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> UpdateJoinPolicy(
        [FromRoute] MaskedGuid roomId,
        [FromBody] UpdateRoomJoinPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var room = await _dbContext.Rooms
            .FirstOrDefaultAsync(r => r.Id == (Guid)roomId, cancellationToken);
        if (room == null)
        {
            return NotFound(new { message = "Room not found" });
        }

        room.JoinPolicy = request.JoinPolicy;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Room join policy updated: RoomId={RoomId}, JoinPolicy={JoinPolicy}, UserId={UserId}",
            room.Id,
            room.JoinPolicy,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return NoContent();
    }

    [HttpGet("available-users")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomJoinAvailableUsersResponse>> GetAvailableUsers(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email)
            .Select(u => new RoomJoinAllowedUserDto
            {
                UserId = new MaskedGuid(u.Id),
                UserName = u.UserName,
                DisplayName = u.DisplayName,
                Email = u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(new RoomJoinAvailableUsersResponse { Users = users });
    }

    [HttpGet("available-roles")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<ActionResult<RoomJoinAvailableRolesResponse>> GetAvailableRoles(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoomJoinAllowedRoleDto
            {
                RoleId = new MaskedGuid(r.Id),
                RoleName = r.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(new RoomJoinAvailableRolesResponse { Roles = roles });
    }

    [HttpPost("users")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddAllowedUser(
        [FromRoute] MaskedGuid roomId,
        [FromBody] AddRoomJoinAllowedUserRequest request,
        CancellationToken cancellationToken)
    {
        var roomGuid = (Guid)roomId;
        var allowedUserId = (Guid)request.UserId;
        var roomExists = await _dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(r => r.Id == roomGuid, cancellationToken);
        if (!roomExists)
        {
            return NotFound(new { message = "Room not found" });
        }

        var userExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == allowedUserId, cancellationToken);
        if (!userExists)
        {
            return NotFound(new { message = "User not found" });
        }

        var exists = await _dbContext.RoomJoinUserPermissions
            .AsNoTracking()
            .AnyAsync(p => p.RoomId == roomGuid && p.ApplicationUserId == allowedUserId, cancellationToken);
        if (exists)
        {
            return Ok(new { message = "User already allowed" });
        }

        _dbContext.RoomJoinUserPermissions.Add(new RoomJoinUserPermission
        {
            RoomId = roomGuid,
            ApplicationUserId = allowedUserId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Room join allowed user added: RoomId={RoomId}, AllowedUserId={AllowedUserId}, ActorUserId={ActorUserId}",
            roomGuid,
            allowedUserId,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return Ok();
    }

    [HttpDelete("users/{userId}")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemoveAllowedUser(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid userId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RoomJoinUserPermissions
            .FirstOrDefaultAsync(p => p.RoomId == (Guid)roomId && p.ApplicationUserId == (Guid)userId, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _dbContext.RoomJoinUserPermissions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Room join allowed user removed: RoomId={RoomId}, AllowedUserId={AllowedUserId}, ActorUserId={ActorUserId}",
            (Guid)roomId,
            (Guid)userId,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return NoContent();
    }

    [HttpPost("roles")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddAllowedRole(
        [FromRoute] MaskedGuid roomId,
        [FromBody] AddRoomJoinAllowedRoleRequest request,
        CancellationToken cancellationToken)
    {
        var roomGuid = (Guid)roomId;
        var allowedRoleId = (Guid)request.RoleId;
        var roomExists = await _dbContext.Rooms
            .AsNoTracking()
            .AnyAsync(r => r.Id == roomGuid, cancellationToken);
        if (!roomExists)
        {
            return NotFound(new { message = "Room not found" });
        }

        var roleExists = await _dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == allowedRoleId, cancellationToken);
        if (!roleExists)
        {
            return NotFound(new { message = "Role not found" });
        }

        var exists = await _dbContext.RoomJoinRolePermissions
            .AsNoTracking()
            .AnyAsync(p => p.RoomId == roomGuid && p.RoleId == allowedRoleId, cancellationToken);
        if (exists)
        {
            return Ok(new { message = "Role already allowed" });
        }

        _dbContext.RoomJoinRolePermissions.Add(new RoomJoinRolePermission
        {
            RoomId = roomGuid,
            RoleId = allowedRoleId
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Room join allowed role added: RoomId={RoomId}, AllowedRoleId={AllowedRoleId}, ActorUserId={ActorUserId}",
            roomGuid,
            allowedRoleId,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return Ok();
    }

    [HttpDelete("roles/{roleId}")]
    [RequireAny(RoomPermissions.Manage, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemoveAllowedRole(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] MaskedGuid roleId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RoomJoinRolePermissions
            .FirstOrDefaultAsync(p => p.RoomId == (Guid)roomId && p.RoleId == (Guid)roleId, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        _dbContext.RoomJoinRolePermissions.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Room join allowed role removed: RoomId={RoomId}, AllowedRoleId={AllowedRoleId}, ActorUserId={ActorUserId}",
            (Guid)roomId,
            (Guid)roleId,
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return NoContent();
    }
}
