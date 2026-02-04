using Microsoft.AspNetCore.Mvc;
using TreeTopic.Helpers;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/setup/[controller]")]
[RequireSetupToken]
public class SetupController : ControllerBase
{
    private readonly SetupTokenValidationService _tokenValidator;

    public SetupController(SetupTokenValidationService tokenValidator)
    {
        _tokenValidator = tokenValidator;
    }

    /// <summary>
    /// SetupToken を無効化（使用済みとしてマーク）
    /// </summary>
    [HttpPost("token/invalidate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> InvalidateToken()
    {
        var tenant = HttpContext.Request.RouteValues["tenant"]?.ToString();

        if (string.IsNullOrWhiteSpace(tenant))
        {
            return BadRequest(new { message = "Tenant is required" });
        }

        // AuthorizationヘッダーからSetupトークンを取得
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return BadRequest(new { message = "Bearer token required" });
        }

        var setupToken = authHeader.Substring("Bearer ".Length).Trim();

        var success = await _tokenValidator.InvalidateSetupTokenAsync(
            tenant,
            setupToken);

        if (!success)
            return BadRequest(new { message = "Failed to invalidate token" });

        return Ok();
    }
}