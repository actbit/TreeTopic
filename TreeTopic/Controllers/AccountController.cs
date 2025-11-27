using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TreeTopic.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class AccountController : Controller
{
    /// <summary>
    /// OIDCログインを開始
    /// </summary>
    [HttpGet("Account/Login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // 既にログイン済みの場合
        if (User?.Identity?.IsAuthenticated == true)
        {
            return Redirect(returnUrl ?? "/");
        }

        // OIDCチャレンジを返す（Google認証画面へリダイレクト）
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl ?? "/" }, "oidc");
    }

    /// <summary>
    /// ログアウト
    /// </summary>
    [HttpPost("Account/Logout")]
    [Authorize]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        // Cookieをクリア
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // OIDCからもサインアウト
        await HttpContext.SignOutAsync("oidc", new AuthenticationProperties
        {
            RedirectUri = returnUrl ?? "/"
        });

        return Redirect(returnUrl ?? "/");
    }
}
