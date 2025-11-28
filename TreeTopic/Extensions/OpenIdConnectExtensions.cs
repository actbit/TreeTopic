using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Services;
using Finbuckle.MultiTenant;

namespace TreeTopic.Extensions;

public static class OpenIdConnectExtensions
{
    /// <summary>
    /// OpenID Connect マルチテナント設定を追加
    /// </summary>
    public static AuthenticationBuilder AddOpenIdConnectConfiguration(
        this AuthenticationBuilder builder,
        IConfiguration configuration)
    {
        return builder.AddOpenIdConnect("oidc", options =>
        {
            options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;

            // Callback paths for SPA
            options.CallbackPath = "/auth/signin-oidc";
            options.SignedOutCallbackPath = "/auth/signout-oidc";

            // Google 認証設定（デフォルト）
            options.Authority = "https://accounts.google.com";
            options.ClientId = configuration["Google:ClientId"]
                ?? throw new InvalidOperationException("Google:ClientId is not configured");
            options.ClientSecret = configuration["Google:ClientSecret"]
                ?? throw new InvalidOperationException("Google:ClientSecret is not configured");

            options.ResponseType = "code";
            options.SaveTokens = true;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = OnRedirectToIdentityProvider,
                OnAuthorizationCodeReceived = OnAuthorizationCodeReceived,
                OnTokenValidated = OnTokenValidated
            };
        });
    }

    /// <summary>
    /// テナント固有の Authority と ClientId を設定
    /// </summary>
    private static Task OnRedirectToIdentityProvider(RedirectContext ctx)
    {
        var mtc = ctx.HttpContext.GetMultiTenantContext<ApplicationTenantInfo>();
        var tenantInfo = mtc?.TenantInfo;

        if (tenantInfo != null)
        {
            if (!string.IsNullOrEmpty(tenantInfo.OpenIdConnctAuthority))
            {
                ctx.ProtocolMessage.IssuerAddress = tenantInfo.OpenIdConnctAuthority;
            }

            if (!string.IsNullOrEmpty(tenantInfo.OpenIdConnecClientId))
            {
                ctx.ProtocolMessage.ClientId = tenantInfo.OpenIdConnecClientId;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// テナント固有の ClientSecret を設定（2段階復号）
    /// </summary>
    private static Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext ctx)
    {
        var mtc = ctx.HttpContext.GetMultiTenantContext<ApplicationTenantInfo>();
        var tenantInfo = mtc?.TenantInfo;

        if (tenantInfo != null && !string.IsNullOrEmpty(tenantInfo.OpenIdConnecClientSecret))
        {
            var masterEncryption = ctx.HttpContext.RequestServices.GetRequiredService<EncryptionService>();
            var decryptedSecret = DecryptTenantSecret(tenantInfo, masterEncryption, ctx.HttpContext.RequestServices);
            ctx.ProtocolMessage.ClientSecret = decryptedSecret;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// トークン検証後の処理
    /// テナント検証、ユーザー同期、クレーム追加
    /// </summary>
    private static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        // URL から抽出したテナント（絶対的な信頼源）
        var urlTenant = ctx.HttpContext.GetRouteValue("tenant")?.ToString();

        // Cookie から復元された claim のテナント
        var claimTenant = ctx.Principal?.FindFirst("tenant")?.Value;

        // ログイン済み（claim がある）場合のみ検証
        if (!string.IsNullOrEmpty(claimTenant))
        {
            if (claimTenant != urlTenant)
            {
                ctx.Fail("テナント情報が一致しません");
                return;
            }
        }

        // 以降の処理用にテナント情報を取得
        var mtc = ctx.HttpContext.GetMultiTenantContext<ApplicationTenantInfo>();
        var tenantInfo = mtc?.TenantInfo
                ?? throw new Exception("Tenant not resolved.");

        // ユーザー同期（ロール情報は除外）
        var userSync = ctx.HttpContext.RequestServices
            .GetRequiredService<UserSyncService>();
        await userSync.SyncUserAsync(ctx.Principal);

        // テナント情報をclaimに追加（ログイン時）
        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
        if (!string.IsNullOrEmpty(tenantInfo.Identifier) && string.IsNullOrEmpty(claimTenant))
        {
            identity.AddClaim(new Claim("tenant", tenantInfo.Identifier));
        }

        // OIDC からのロール情報を claim に追加（DBには保存しない）
        if (!string.IsNullOrEmpty(tenantInfo.RoleClaimName))
        {
            try
            {
                var roleClaims = ctx.Principal?.FindAll(tenantInfo.RoleClaimName);
                if (roleClaims != null)
                {
                    foreach (var roleClaim in roleClaims)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleClaim.Value));
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
                logger.LogWarning(ex, "Failed to add role claims from OIDC");
            }
        }
    }

    /// <summary>
    /// テナントのシークレット（ClientSecret）を 2 段階で復号
    /// </summary>
    private static string DecryptTenantSecret(
        ApplicationTenantInfo tenantInfo,
        EncryptionService masterEncryption,
        IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(tenantInfo.TenantEncryptionKey))
            throw new InvalidOperationException($"Tenant '{tenantInfo.Identifier}' has no encryption key.");

        // 1. マスターキーで テナント用キーを復号
        var decryptedTenantKey = masterEncryption.Decrypt(tenantInfo.TenantEncryptionKey);

        // 2. テナント用キーでシークレットを復号
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        var tenantEncryption = new EncryptionService(decryptedTenantKey, logger);
        return tenantEncryption.Decrypt(tenantInfo.OpenIdConnecClientSecret);
    }
}
