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
        var permissionsByCategory = permissionScanService.GetPermissionsByCategory();

        var permissions = new
        {
            tenant = permissionsByCategory["tenant"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            }),
            topic = permissionsByCategory["topic"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            }),
            room = permissionsByCategory["room"].Select(p => new
            {
                name = p.Name,
                scope = p.Scope.ToString()
            })
        };

        return Ok(permissions);
    }
}
