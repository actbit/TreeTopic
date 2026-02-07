using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Filters;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Constants;
using TreeTopic.Common;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// Topic権限管理
/// </summary>
[ApiController]
[Route("{tenant}/api/topics/{topicId}/permissions")]
[Authorize]
public class TopicPermissionsController : BaseController
{
    private readonly ITopicPermissionsService _service;

    public TopicPermissionsController(ITopicPermissionsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Topic権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionCatalogService permissionCatalogService)
    {
        return Ok(permissionCatalogService.GetTopicPermissions());
    }

    /// <summary>
    /// トピックに割り当てられているユーザー権限一覧を取得
    /// </summary>
    [HttpGet("users")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> GetTopicUserPermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var result = await _service.GetTopicUserPermissionsAsync(topicGuid, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// 特定ユーザーのトピック権限を取得
    /// </summary>
    [HttpGet("users/{roomUserId}")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> GetUserTopicPermissions(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var roomUserGuid = (Guid)roomUserId;

        var result = await _service.GetUserTopicPermissionsAsync(topicGuid, roomUserGuid, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(new { topicId = topicGuid, roomUserId = roomUserGuid, permissions = result.Data });
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// ユーザーにトピック権限を割り当て
    /// </summary>
    [HttpPost("users")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> AddPermissionToUser(
        [FromRoute] MaskedGuid topicId,
        [FromBody] AddTopicPermissionToUserRequest request,
        CancellationToken cancellationToken)
    {
        // Validate permission name
        var validTopicPermissions = Permissions.PermissionHelper.GetTopicPermissions();
        if (!validTopicPermissions.Contains(request.PermissionName))
        {
            return BadRequest(new { message = $"Invalid permission name: {request.PermissionName}" });
        }

        var topicGuid = (Guid)topicId;
        var result = await _service.AddPermissionToUserAsync(
            topicGuid,
            request.RoomUserId,
            request.PermissionName,
            request.ApplyToDescendants,
            cancellationToken);
        if (result.IsSuccess)
        {
            var permission = result.Data;
            return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.Name });
        }

        return result.Error?.Type == ErrorType.Conflict
            ? Conflict(new { message = "Permission already assigned" })
            : result.ToActionResult();
    }

    /// <summary>
    /// ユーザーからトピック権限を削除
    /// </summary>
    [HttpDelete("users/{roomUserId}/{permissionName}")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> RemovePermissionFromUser(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roomUserId,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken,
        [FromQuery] bool applyToDescendants = false)
    {
        var topicGuid = (Guid)topicId;
        var roomUserGuid = (Guid)roomUserId;
        var result = await _service.RemovePermissionFromUserAsync(
            topicGuid,
            roomUserGuid,
            permissionName,
            applyToDescendants,
            cancellationToken);
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
    /// トピックに割り当てられているTopicRolePermission（RoomRole権限）一覧を取得
    /// </summary>
    [HttpGet("role-permissions")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> GetTopicRolePermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var result = await _service.GetTopicRolePermissionsAsync(topicGuid, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// トピックにRoomRole権限を割り当て（TopicRolePermissionとして追加）
    /// </summary>
    [HttpPost("role-permissions")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> AddTopicRolePermission(
        [FromRoute] MaskedGuid topicId,
        [FromBody] AddTopicRolePermissionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate permission name
        var validTopicPermissions = Permissions.PermissionHelper.GetTopicPermissions();
        if (!validTopicPermissions.Contains(request.PermissionName))
        {
            return BadRequest(new { message = $"Invalid permission name: {request.PermissionName}" });
        }

        var topicGuid = (Guid)topicId;
        var result = await _service.AddTopicRolePermissionAsync(
            topicGuid,
            request.RoleName,
            request.PermissionName,
            request.ApplyToDescendants,
            cancellationToken);
        if (result.IsSuccess)
        {
            var permission = result.Data;
            return Ok(new { permissionId = new MaskedGuid(permission.Id), name = permission.Name });
        }

        return result.Error?.Type == ErrorType.Conflict
            ? StatusCode(409, new { message = "Permission already assigned to RoomRole" })
            : StatusCode(result.Error?.Type switch
            {
                ErrorType.NotFound => 404,
                _ => 500
            }, new { message = result.Error?.Message });
    }

    /// <summary>
    /// トピックからRoomRole権限を削除
    /// </summary>
    [HttpDelete("role-permissions/{roleName}/{permissionName}")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> RemoveTopicRolePermission(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] string roleName,
        [FromRoute] string permissionName,
        CancellationToken cancellationToken,
        [FromQuery] bool applyToDescendants = false)
    {
        var topicGuid = (Guid)topicId;
        var result = await _service.RemoveTopicRolePermissionAsync(
            topicGuid,
            roleName,
            permissionName,
            applyToDescendants,
            cancellationToken);
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
    /// ユーザーのTopic権限をクリア
    /// </summary>
    [HttpDelete("users/{roomUserId}")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> ClearUserPermissions(
        [FromRoute] MaskedGuid topicId,
        [FromRoute] MaskedGuid roomUserId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var roomUserGuid = (Guid)roomUserId;
        var result = await _service.ClearUserPermissionsAsync(topicGuid, roomUserGuid, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return NotFound(new { message = result.Error?.Message });
    }

    /// <summary>
    /// Topicの全RoomRole権限をクリア
    /// </summary>
    [HttpDelete("role-permissions")]
    [RequireAny(TopicPermissions.Manage, TenantPermissions.TopicManage)]
    public async Task<IActionResult> ClearRolePermissions(
        [FromRoute] MaskedGuid topicId,
        CancellationToken cancellationToken)
    {
        var topicGuid = (Guid)topicId;
        var result = await _service.ClearRolePermissionsAsync(topicGuid, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }
        return NotFound(new { message = result.Error?.Message });
    }
}
