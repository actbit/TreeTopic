using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Helpers;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/setup/[controller]")]
[RequireSetupToken]
public class RoleSetupController : ControllerBase
{
    private readonly RoleManagementService _roleManagementService;

    public RoleSetupController(RoleManagementService roleManagementService)
    {
        _roleManagementService = roleManagementService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetRoles(string tenant)
    {
        var result = await _roleManagementService.GetAllRolesAsync(tenant);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => r.Select(MapRoleToDto));
        }

        var roles = result.Data!;
        return Ok(roles.Select(MapRoleToDto));
    }

    private static RoleDto MapRoleToDto(ApplicationRole role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Permissions = role.Authorities?.Select(a => a.Name).ToList() ?? new List<string>()
        };
    }

    [HttpPost("create")]
    public async Task<ActionResult<RoleDto>> CreateRole(string tenant, [FromBody] SetupRoleCreationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _roleManagementService.CreateRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => MapRoleToDto(r));
        }

        var role = result.Data!;
        return Ok(MapRoleToDto(role));
    }

    [HttpDelete("{roleName}")]
    public async Task<IActionResult> DeleteRole(string tenant, string roleName)
    {
        // 属性で検証済みのSetupTokenを取得
        var setupToken = HttpContext.Items["ValidatedSetupToken"]?.ToString();

        var deletionRequest = new SetupRoleDeletionRequest
        {
            SetupToken = setupToken!,
            RoleName = roleName
        };

        var result = await _roleManagementService.DeleteRoleAsync(tenant, deletionRequest);

        if (result.IsFailure)
        {
            return result.ToApiResult();
        }

        return NoContent();
    }

    [HttpPost("permissions/add")]
    public async Task<ActionResult<PermissionDto>> AddPermission(string tenant)
    {
        // 属性で検証済みのSetupTokenを取得
        var setupToken = HttpContext.Items["ValidatedSetupToken"]?.ToString();

        // ロール名とパーミッション名をボディから取得
        using var reader = new StreamReader(HttpContext.Request.Body);
        var body = await reader.ReadToEndAsync();
        var json = System.Text.Json.JsonDocument.Parse(body);

        var roleName = json.RootElement.GetProperty("roleName").GetString();
        var permissionName = json.RootElement.GetProperty("permissionName").GetString();

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(permissionName))
        {
            return BadRequest(new { message = "roleName and permissionName are required" });
        }

        // SetupToken を使ってリクエストを作成
        var request = new SetupPermissionRequest
        {
            SetupToken = setupToken!,
            RoleName = roleName,
            PermissionName = permissionName
        };

        var result = await _roleManagementService.AddPermissionToRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(p => new PermissionDto
            {
                Id = p.Id,
                Name = p.Name,
                RoleId = p.RoleId,
                RoleName = p.Role?.Name
            });
        }

        var permission = result.Data!;
        var dto = new PermissionDto
        {
            Id = permission.Id,
            Name = permission.Name,
            RoleId = permission.RoleId,
            RoleName = permission.Role?.Name
        };
        return Ok(dto);
    }

    [HttpPost("permissions/delete")]
    public async Task<IActionResult> DeletePermission(string tenant, [FromBody] dynamic requestData)
    {
        // 属性で検証済みのSetupTokenを取得
        var setupToken = HttpContext.Items["ValidatedSetupToken"]?.ToString();

        // ロール名とパーミッション名をボディから取得
        var roleName = (string?)requestData.roleName;
        var permissionName = (string?)requestData.permissionName;

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(permissionName))
        {
            return BadRequest(new { message = "roleName and permissionName are required" });
        }

        // SetupToken を使ってリクエストを作成
        var request = new SetupPermissionDeletionRequest
        {
            SetupToken = setupToken!,
            RoleName = roleName,
            PermissionName = permissionName
        };

        var result = await _roleManagementService.DeletePermissionFromRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToApiResult();
        }

        return NoContent();
    }

    [HttpPost("default")]
    public async Task<ActionResult<RoleSetupCompletionResponse>> SetDefaultRole(string tenant, [FromBody] dynamic requestData)
    {
        // 属性で検証済みのSetupTokenを取得
        var setupToken = HttpContext.Items["ValidatedSetupToken"]?.ToString();

        // データをボディから取得
        var defaultRoleName = (string?)requestData.defaultRoleName;
        var description = (string?)requestData.description;
        var defaultPermissions = (List<string>?)requestData.defaultPermissions;

        if (string.IsNullOrEmpty(defaultRoleName))
        {
            return BadRequest(new { message = "defaultRoleName is required" });
        }

        // SetupToken を使ってリクエストを作成
        var request = new SetupDefaultRoleRequest
        {
            SetupToken = setupToken!,
            DefaultRoleName = defaultRoleName,
            Description = description,
            DefaultPermissions = defaultPermissions ?? new List<string>()
        };

        var result = await _roleManagementService.SetupDefaultRoleAsync(tenant, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(r => r);
        }

        return Ok(result.Data!);
    }
}
