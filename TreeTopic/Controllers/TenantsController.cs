using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Permissions;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

/// <summary>
/// テナント管理コントローラ
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly TenantManagementService _tenantManagementService;

    public TenantsController(TenantManagementService tenantManagementService)
    {
        _tenantManagementService = tenantManagementService;
    }

    /// <summary>
    /// パブリックテナント一覧を取得
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicTenants(CancellationToken cancellationToken)
    {
        var tenants = await _tenantManagementService.GetPublicTenantsAsync(cancellationToken);
        return Ok(tenants);
    }

    /// <summary>
    /// パブリックテナントを取得
    /// </summary>
    [HttpGet("public/{identifier}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicTenant(string identifier, CancellationToken cancellationToken)
    {
        var tenant = await _tenantManagementService.GetPublicTenantAsync(identifier, cancellationToken);
        if (tenant == null)
        {
            return NotFound(new { error = "Tenant not found" });
        }
        return Ok(tenant);
    }

    /// <summary>
    /// テナント登録
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _tenantManagementService.RegisterTenantAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return StatusCode(result.StatusCode, new { error = result.Error?.Message ?? "Failed to register tenant" });
        }
        return StatusCode(result.StatusCode, result.Data);
    }
}
