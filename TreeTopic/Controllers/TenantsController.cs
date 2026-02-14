using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ixnas.AltchaNet;
using TreeTopic.Dtos;
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
    private readonly AltchaService _altchaService;

    public TenantsController(
        TenantManagementService tenantManagementService,
        AltchaService altchaService)
    {
        _tenantManagementService = tenantManagementService;
        _altchaService = altchaService;
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
    /// CAPTCHA情報を取得
    /// </summary>
    [HttpGet("captcha")]
    [AllowAnonymous]
    public IActionResult GetCaptcha()
    {
        var challenge = _altchaService.Generate();
        return Ok(challenge);
    }

    /// <summary>
    /// テナント登録
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterTenant([FromForm] RegisterTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Altcha))
        {
            return BadRequest(new { error = "Security verification is required." });
        }

        var altchaValidation = await _altchaService.Validate(request.Altcha, cancellationToken);
        if (!altchaValidation.IsValid)
        {
            return BadRequest(new { error = "Invalid security code." });
        }

        var result = await _tenantManagementService.RegisterTenantAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return StatusCode(result.StatusCode, new { error = result.Error?.Message ?? "Failed to register tenant" });
        }
        return StatusCode(result.StatusCode, result.Data);
    }
}
