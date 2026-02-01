using MaskedUUID.AspNetCore.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly PermissionManagementService _permissionManagementService;

    public PermissionsController(PermissionManagementService permissionManagementService)
    {
        _permissionManagementService = permissionManagementService;
    }

    [HttpGet]
    [RequirePermission(IdentityPermissions.PermissionRead)]
    public async Task<ActionResult<List<PermissionDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _permissionManagementService.ListPermissionsAsync();

        if (result.IsFailure)
        {
            return result.ToActionResult(p => p.Select(PermissionToDto).ToList());
        }

        var permissionDtos = result.Data!.Select(PermissionToDto).ToList();
        return Ok(permissionDtos);
    }

    [HttpGet("{permissionId}")]
    [RequirePermission(IdentityPermissions.PermissionRead)]
    public async Task<ActionResult<PermissionDto>> Get([FromRoute] MaskedGuid permissionId, CancellationToken cancellationToken)
    {
        var result = await _permissionManagementService.GetPermissionByIdAsync((Guid)permissionId);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return Ok(dto);
    }

    [HttpPost]
    [RequirePermission(IdentityPermissions.PermissionManage)]
    public async Task<ActionResult<PermissionDto>> Create([FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _permissionManagementService.CreatePermissionAsync(request);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return CreatedAtAction(nameof(Get), new { permissionId = dto.Id }, dto);
    }

    [HttpPut("{permissionId}")]
    [RequirePermission(IdentityPermissions.PermissionManage)]
    public async Task<ActionResult<PermissionDto>> Update([FromRoute] MaskedGuid permissionId, [FromBody] PermissionModificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _permissionManagementService.UpdatePermissionAsync((Guid)permissionId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(PermissionToDto);
        }

        var dto = PermissionToDto(result.Data!);
        return Ok(dto);
    }

    [HttpDelete("{permissionId}")]
    [RequirePermission(IdentityPermissions.PermissionManage)]
    public async Task<IActionResult> Delete([FromRoute] MaskedGuid permissionId)
    {
        var result = await _permissionManagementService.DeletePermissionAsync(permissionId);

        if (result.IsFailure)
        {
            return result.ToApiResult();
        }

        return NoContent();
    }

    private static PermissionDto PermissionToDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
    }

    /// <summary>
    /// 利用可能なすべての権限一覧を取得（定義済み権限定数）
    /// </summary>
    [HttpGet("available")]
    [RequirePermission(IdentityPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions()
    {
        var permissions = new
        {
            identity = new
            {
                users = new[]
                {
                    new { name = IdentityPermissions.UserRead, label = "ユーザー閲覧", description = "ユーザー情報を閲覧できます" },
                    new { name = IdentityPermissions.UserManage, label = "ユーザー管理", description = "ユーザー情報を変更できます" }
                },
                roles = new[]
                {
                    new { name = IdentityPermissions.RoleRead, label = "ロール閲覧", description = "ロール情報を閲覧できます" },
                    new { name = IdentityPermissions.RoleManage, label = "ロール管理", description = "ロールを管理できます" }
                },
                permissions = new[]
                {
                    new { name = IdentityPermissions.PermissionRead, label = "権限閲覧", description = "権限設定を閲覧できます" },
                    new { name = IdentityPermissions.PermissionManage, label = "権限管理", description = "権限を管理できます" }
                },
                tenants = new[]
                {
                    new { name = IdentityPermissions.TenantRead, label = "テナント閲覧", description = "テナント情報を閲覧できます" },
                    new { name = IdentityPermissions.TenantManage, label = "テナント管理", description = "テナントを管理できます" }
                }
            },
            room = new[]
            {
                new { name = RoomPermissions.Join, label = "ルーム参加", description = "ルームに参加できます" },
                new { name = RoomPermissions.Read, label = "ルーム閲覧", description = "ルーム情報を閲覧できます" },
                new { name = RoomPermissions.Write, label = "ルーム書き込み", description = "トピック作成、ファイルアップロード等ができます" },
                new { name = RoomPermissions.Delete, label = "ルーム削除", description = "シェア、ファイル等を削除できます" },
                new { name = RoomPermissions.Manage, label = "ルーム管理", description = "ルーム設定を変更できます" },
                new { name = RoomPermissions.ManageUsers, label = "メンバー管理", description = "ルームメンバーを管理できます" },
                new { name = RoomPermissions.ManageRoles, label = "ロール管理", description = "ルームロールを管理できます" }
            },
            topic = new[]
            {
                new { name = TopicPermissions.Read, label = "トピック閲覧", description = "トピックを閲覧できます" },
                new { name = TopicPermissions.Write, label = "トピック編集", description = "トピックを作成・編集できます" },
                new { name = TopicPermissions.Delete, label = "トピック削除", description = "トピックを削除できます" },
                new { name = TopicPermissions.Manage, label = "トピック管理", description = "トピック権限を管理できます" },
                new { name = TopicPermissions.ReadMessages, label = "メッセージ閲覧", description = "メッセージを閲覧できます" },
                new { name = TopicPermissions.WriteMessages, label = "メッセージ投稿", description = "メッセージを投稿・編集できます" }
            }
        };

        return Ok(permissions);
    }
}
