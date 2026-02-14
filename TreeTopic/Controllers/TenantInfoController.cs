using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
namespace TreeTopic.Controllers;

/// <summary>
/// テナント情報コントローラ（テナントスコープ）
/// </summary>
[ApiController]
[Route("{tenant}/api/tenant")]
[Authorize]
public class TenantInfoController : ControllerBase
{
    private readonly TenantCatalogDbContext _tenantDb;

    public TenantInfoController(TenantCatalogDbContext tenantDb)
    {
        _tenantDb = tenantDb;
    }

    /// <summary>
    /// テナント詳細情報を取得
    /// </summary>
    [HttpGet("detail")]
    public async Task<IActionResult> GetTenantDetail(CancellationToken cancellationToken)
    {
        var tenantIdentifier = HttpContext.GetRouteValue("tenant")?.ToString();
        if (string.IsNullOrEmpty(tenantIdentifier))
        {
            return BadRequest(new { message = "Tenant identifier is required" });
        }

        var tenantInfo = await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantIdentifier, cancellationToken);

        if (tenantInfo?.Detail == null)
        {
            return NotFound(new { message = "Tenant not found" });
        }

        var detail = tenantInfo.Detail;

        return Ok(new
        {
            canCreateUsers = detail.CanCreateUsers(),
            canAssignRolesToUsers = detail.CanAssignRolesToUsers(),
            roleClaimName = detail.RoleClaimName,
            hasOidcSettings = detail.HasOidcSettings(),
            hasOidcRoleSync = detail.HasOidcRoleSync()
        });
    }
}
