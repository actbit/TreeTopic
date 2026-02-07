using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// Room権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/rooms/{roomId}/roomroles/{roleName}/permissions")]
[Authorize]
public class RoomPermissionsController : BaseController
{
    private readonly IRoomPermissionsService _service;

    public RoomPermissionsController(IRoomPermissionsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Room権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionCatalogService permissionCatalogService)
    {
        return Ok(permissionCatalogService.GetRoomPermissions());
    }

    /// <summary>
    /// RoomRoleに割り当てられている権限一覧を取得
    /// </summary>
    [HttpGet]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> GetRolePermissions(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetRolePermissionsAsync(roleName, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new { roleName, permissions = result.Data });
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// RoomRoleに権限を割り当て
    /// </summary>
    [HttpPost]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> AddPermissionToRole(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        [FromBody] AddRoomPermissionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate permission name
        var validRoomPermissions = Permissions.PermissionHelper.GetRoomPermissions();
        if (!validRoomPermissions.Contains(request.PermissionName))
        {
            return BadRequest(new { message = $"Invalid permission name: {request.PermissionName}. Valid permissions: {string.Join(", ", validRoomPermissions)}" });
        }

        var result = await _service.AddPermissionToRoleAsync(roleName, request.PermissionName, cancellationToken);
        if (result.IsSuccess)
        {
            var permission = result.Data;
            return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.PermissionName });
        }

        return result.Error?.Type == ErrorType.Conflict
            ? Conflict(new { message = "Permission already assigned" })
            : result.ToActionResult();
    }

    /// <summary>
    /// RoomRoleから権限を削除
    /// </summary>
    [HttpDelete("{permissionName}")]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> RemovePermissionFromRole(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken)
    {
        var result = await _service.RemovePermissionFromRoleAsync(roleName, permissionName, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.Error?.Type switch
        {
            ErrorType.NotFound => NotFound(new { message = result.Error.Message }),
            _ => StatusCode(500, new { message = result.Error?.Message })
        };
    }

    /// <summary>
    /// RoomRoleの全権限をクリア
    /// </summary>
    [HttpDelete("clear")]
    [RequireAny(RoomPermissions.ManageRoles, TenantPermissions.RoomManage)]
    public async Task<IActionResult> ClearRolePermissions(
        [FromRoute] MaskedGuid roomId,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var result = await _service.ClearRolePermissionsAsync(roleName, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return NotFound(new { message = result.Error?.Message });
    }
}
