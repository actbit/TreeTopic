using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using System.Security.Claims;
using Finbuckle.MultiTenant;
using TreeTopic.Models;
using TreeTopic.Constants;
using TreeTopic.Services;
using TreeTopic.Permissions;

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
    private readonly IconService _iconService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        UserManager<ApplicationUser> userManager,
        IconService iconService,
        ILogger<AuthController> logger)
    {
        _tenantAccessor = tenantAccessor;
        _userManager = userManager;
        _iconService = iconService;
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
            var normalizedTenantPath = $"/{currentTenant}";
            if (returnUrl.StartsWith(normalizedTenantPath, StringComparison.OrdinalIgnoreCase))
            {
                if (returnUrl.Length == normalizedTenantPath.Length)
                {
                    return true;
                }

                var nextChar = returnUrl[normalizedTenantPath.Length];
                return nextChar == '/' || nextChar == '?' || nextChar == '#';
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// ログアウト（GET用レガシーエンドポイント - 非推奨）
    /// </summary>
    [HttpGet("logout")]
    [Authorize]
    public IActionResult LogoutGet([FromQuery] string? returnUrl)
    {
        return PerformLogout(returnUrl);
    }

    /// <summary>
    /// ログアウト（POST推奨）
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult LogoutPost([FromBody] LogoutRequest? request)
    {
        return PerformLogout(request?.returnUrl);
    }

    private IActionResult PerformLogout(string? returnUrl)
    {
        _logger.LogInformation("Logout initiated");

        // returnUrl をバリデーション
        var currentTenant = HttpContext.GetRouteValue("tenant")?.ToString();
        if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl, currentTenant))
        {
            _logger.LogWarning("Invalid returnUrl detected on logout: {ReturnUrl}", returnUrl);
            returnUrl = null;
        }

        // リダイレクト先
        var redirectUri = returnUrl ?? "/";
        _logger.LogInformation("Logout redirecting to: {RedirectUri}", redirectUri);

        // Only logout from application session (Cookies)
        // Don't logout from Keycloak to preserve session for other applications
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = redirectUri
            },
            "Cookies"
        );
    }
    /// <summary>
    /// 現在のユーザー情報を取得
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var cs = User.Claims;
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        MaskedGuid? maskedUserId = null;
        Guid userGuid = Guid.Empty;
        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out userGuid))
        {
            maskedUserId = userGuid;
        }
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var tenant = User.FindFirst("tenant")?.Value;

        // DB からユーザー情報を取得して DisplayName を取得
        string? userName = null;
        string? displayName = null;
        string? iconUrl = null;
        if (!string.IsNullOrEmpty(userId) && userGuid != Guid.Empty)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                userName = user.UserName;
                displayName = user.DisplayName ?? user.UserName;
                iconUrl = _iconService.GetUserIconUrl(user);
            }
        }

        return Ok(new
        {
            userId = maskedUserId,
            userName,
            displayName,
            iconUrl,
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
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckAuth()
    {
        return Ok(new { isAuthenticated = User.Identity?.IsAuthenticated ?? false });
    }

    /// <summary>
    /// 現在のユーザーの権限をチェック
    /// </summary>
    [HttpGet("me/permissions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckUserPermissions()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized();
        }

        // DBコンテキストを取得
        var dbContext = HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();

        // ApplicationUserを取得
        var appUser = await userManager.FindByIdAsync(userGuid.ToString());
        if (appUser == null)
        {
            return Unauthorized();
        }

        // ユーザーのロールを取得（Identity管理ロール + claimsロールをマージ）
        var identityRoles = await userManager.GetRolesAsync(appUser);
        var claimRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value);
        var roles = new HashSet<string>(identityRoles, StringComparer.OrdinalIgnoreCase);
        foreach (var claimRole in claimRoles)
        {
            if (!string.IsNullOrWhiteSpace(claimRole))
            {
                roles.Add(claimRole);
            }
        }

        // ユーザーの全ての権限を取得
        var userPermissions = await dbContext.Permissions
            .AsNoTracking()
            .Include(p => p.Role)
            .Where(p => p.Role != null && p.Role.Name != null && roles.Contains(p.Role.Name))
            .Select(p => p.Name)
            .Distinct()
            .ToListAsync(HttpContext.RequestAborted);

        // RoleManage権限をチェック
        var hasRoleManagePermission = userPermissions.Contains(TenantPermissions.RoleManage);

        return Ok(new {
            hasRoleManagePermission,
            permissions = userPermissions
        });
    }
}

/// <summary>
/// ログアウトリクエスト
/// </summary>
public class LogoutRequest
{
    public string? returnUrl { get; set; }
}
