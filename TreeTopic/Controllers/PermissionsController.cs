using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// 利用可能な権限一覧取得API
/// </summary>
[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    /// <summary>
    /// 利用可能なすべての権限一覧を取得（PermissionScanServiceで動的取得）
    /// </summary>
    [HttpGet("available")]
    [RequireAny(TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionScanService permissionScanService)
    {
        if (permissionScanService == null)
        {
            return StatusCode(500, new { error = "Permission scan service is not available" });
        }

        var permissionsByCategory = permissionScanService.GetPermissionsByCategory() ?? new Dictionary<string, List<PermissionRequirement>>();

        var permissions = new
        {
            tenant = permissionsByCategory.TryGetValue("tenant", out var tenantPerms)
                ? (object)tenantPerms.Select(p => new
                {
                    name = p.Name,
                    scope = p.Scope.ToString()
                })
                : Array.Empty<object>(),
            topic = permissionsByCategory.TryGetValue("topic", out var topicPerms)
                ? (object)topicPerms.Select(p => new
                {
                    name = p.Name,
                    scope = p.Scope.ToString()
                })
                : Array.Empty<object>(),
            room = permissionsByCategory.TryGetValue("room", out var roomPerms)
                ? (object)roomPerms.Select(p => new
                {
                    name = p.Name,
                    scope = p.Scope.ToString()
                })
                : Array.Empty<object>()
        };

        return Ok(permissions);
    }
}
