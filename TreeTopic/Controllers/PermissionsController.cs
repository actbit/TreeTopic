using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Filters;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    [HttpGet("available")]
    [RequireAny(PermissionScope.Role, TenantPermissions.PermissionRead)]
    public IActionResult GetAvailablePermissions([FromServices] PermissionCatalogService permissionCatalogService)
    {
        return Ok(permissionCatalogService.GetAllByCategory());
    }
}
