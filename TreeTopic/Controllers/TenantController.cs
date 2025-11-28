using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TreeTopic.Dtos;
using TreeTopic.Services;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

/// <summary>
/// テナント管理 API
/// </summary>
[ApiController]
[Route("{tenant}/api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly TenantManagementService _tenantService;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly ILogger<TenantController> _logger;

    public TenantController(
        TenantManagementService tenantService,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<TenantController> logger)
    {
        _tenantService = tenantService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 新しいテナントを登録（認可不要）
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TenantResponse>> RegisterTenant([FromBody] CreateTenantRequest request)
    {
        try
        {
            var tenant = await _tenantService.CreateTenantAsync(request);
            return CreatedAtAction(nameof(GetTenant), new { identifier = tenant.Identifier },
                new TenantResponse
                {
                    Id = tenant.Id,
                    Identifier = tenant.Identifier,
                    Name = tenant.Name,
                    DbProvider = tenant.DbProvider,
                    CreatedAt = DateTime.UtcNow
                });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid tenant registration request");
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Tenant already exists");
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering tenant");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Failed to register tenant" });
        }
    }

    /// <summary>
    /// テナント情報を取得
    /// </summary>
    [HttpGet("{identifier}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantResponse>> GetTenant(string identifier)
    {
        try
        {
            var tenant = await _tenantService.GetTenantByIdentifierAsync(identifier);
            if (tenant == null)
            {
                return NotFound(new { message = $"Tenant '{identifier}' not found" });
            }

            return Ok(new TenantResponse
            {
                Id = tenant.Id,
                Identifier = tenant.Identifier,
                Name = tenant.Name,
                DbProvider = tenant.DbProvider,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant: {Identifier}", identifier);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// すべてのテナントを取得
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TenantResponse>>> GetAllTenants()
    {
        try
        {
            var tenants = await _tenantService.GetAllTenantsAsync();
            var responses = tenants.Select(t => new TenantResponse
            {
                Id = t.Id,
                Identifier = t.Identifier,
                Name = t.Name,
                DbProvider = t.DbProvider,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenants");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// テナントを削除
    /// </summary>
    [HttpDelete("{tenantId}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteTenant(string tenantId)
    {
        try
        {
            await _tenantService.DeleteTenantAsync(tenantId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Tenant not found: {TenantId}", tenantId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant: {TenantId}", tenantId);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// テナント情報レスポンス
/// </summary>
public class TenantResponse
{
    public string? Id { get; set; }
    public string? Identifier { get; set; }
    public string? Name { get; set; }
    public string? DbProvider { get; set; }
    public DateTime CreatedAt { get; set; }
}
