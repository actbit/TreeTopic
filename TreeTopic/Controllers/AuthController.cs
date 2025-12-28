using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Finbuckle.MultiTenant;
using TreeTopic.Models;

namespace TreeTopic.Controllers;

/// <summary>
/// 認証エンドポイント
/// </summary>
[ApiController]
[Route("{tenant}/auth")]
public class AuthController : ControllerBase
{
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        UserManager<ApplicationUser> userManager,
        ILogger<AuthController> logger)
    {
        _tenantAccessor = tenantAccessor;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// ログイン（OIDC プロバイダーへリダイレクト）
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        // 現在のテナント
        var currentTenant = HttpContext.GetRouteValue("tenant")?.ToString();

        // returnUrl をバリデーション
        // 相対 URL で、かつ同じテナント内のパスのみを許可
        if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl, currentTenant))
        {
            _logger.LogWarning("Invalid returnUrl detected: {ReturnUrl}", returnUrl);
            returnUrl = null;
        }

        // 有効な returnUrl があればそれを使用、なければテナントのインデックスページ
        var redirectUri = returnUrl ?? $"/{currentTenant}/";
        _logger.LogInformation("Login initiated. RedirectUri: {RedirectUri}", redirectUri);

        return Challenge(
            new AuthenticationProperties
            {
                RedirectUri = redirectUri
            },
            "oidc"
        );
    }

    private bool IsValidReturnUrl(string returnUrl, string? currentTenant)
    {
        // 相対 URL か確認
        if (!Url.IsLocalUrl(returnUrl))
            return false;

        // 同じテナント内の URL か確認
        if (!string.IsNullOrEmpty(currentTenant))
        {
            return returnUrl.StartsWith($"/{currentTenant}/");
        }

        return true;
    }

    /// <summary>
    /// ログアウト
    /// </summary>
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        _logger.LogInformation("Logout initiated");

        // Only logout from application session (Cookies)
        // Don't logout from Keycloak to preserve session for other applications
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            },
            "Cookies"
        );
    }

    /// <summary>
    /// 現在のユーザー情報を取得
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var tenant = User.FindFirst("tenant")?.Value;

        // DB からユーザー情報を取得して DisplayName を取得
        string? userName = null;
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            userName = user?.DisplayName ?? user?.UserName;
        }

        return Ok(new
        {
            userId,
            userName,
            email,
            roles,
            tenant,
            isAuthenticated = true
        });
    }

    /// <summary>
    /// ログイン状態をチェック
    /// </summary>
    [HttpGet("check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckAuth()
    {
        return Ok(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });
    }
}




