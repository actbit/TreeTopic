using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using TreeTopic.Models;
using TreeTopic.Services;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Extensions.Hosting;

namespace TreeTopic.Extensions;

public static class OpenIdConnectExtensions
{
    /// <summary>
    /// OpenID Connect マルチテナント設定を追加
    /// </summary>
    public static AuthenticationBuilder AddOpenIdConnectConfiguration(
        this AuthenticationBuilder builder,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return builder.AddOpenIdConnect("oidc", options =>
        {
            options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            // Ensure OIDC correlation/nonce cookies survive the cross-site round-trip
            options.CorrelationCookie.SameSite = SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
            options.NonceCookie.SameSite = SameSiteMode.None;
            options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

            // Callback paths for SPA
            // テナント情報は query parameter で渡す
            options.CallbackPath = "/auth/signin-oidc";
            options.SignedOutCallbackPath = "/auth/signout-oidc";

            // メタデータ自動発見を防ぐため、Configuration を手動で設定
            // Google のデフォルトエンドポイントを使用
            options.Configuration = new OpenIdConnectConfiguration
            {
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                JwksUri = "https://www.googleapis.com/oauth2/v3/certs"
            };

            // ClientId と ClientSecret は Google 設定をデフォルトとして設定
            // OnRedirectToIdentityProvider でテナント固有設定に上書きされる
            options.ClientId = configuration["Google:ClientId"]
                ?? throw new InvalidOperationException("Google:ClientId is not configured");
            options.ClientSecret = configuration["Google:ClientSecret"]
                ?? throw new InvalidOperationException("Google:ClientSecret is not configured");

            options.ResponseType = "code";
            options.SaveTokens = true;

            // Pushed Authorization Request (PAR) を無効化
            // Keycloak などの一部のプロバイダーは PAR をサポートしていない
            options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("email");

            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = async ctx => await OnRedirectToIdentityProvider(ctx),
                OnRedirectToIdentityProviderForSignOut = OnRedirectToIdentityProviderForSignOut,
                OnAuthorizationCodeReceived = async ctx => await OnAuthorizationCodeReceived(ctx),
                OnTokenValidated = OnTokenValidated
            };
        });
    }

    /// <summary>
    /// テナント固有の OIDC 設定を動的に設定
    /// テナント設定がない場合、Google デフォルトを使用
    /// redirect_uri にテナント識別子を含める
    /// </summary>
    private static async Task OnRedirectToIdentityProvider(RedirectContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        var tenantId = ctx.HttpContext.GetRouteValue("tenant")?.ToString();

        ApplicationTenantInfo? tenantInfo = null;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Tenant not found on redirect request");
            // Stop the OIDC redirect to avoid anonymous tenant-less flow
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Tenant is required.");
            return;
        }

        // redirect_uri を設定（query に tenant を含める）
        var scheme = ctx.HttpContext.Request.Scheme;
        var host = ctx.HttpContext.Request.Host;
        ctx.ProtocolMessage.RedirectUri = $"{scheme}://{host}/auth/signin-oidc?tenant={Uri.EscapeDataString(tenantId)}";

        // state は ASP.NET Core に任せる（上書きしない）
        // tenant 情報は query パラメータで渡す

        ctx.Properties.Items["tenant"] = tenantId;

        // IMultiTenantStore から tenant を取得
        var store = ctx.HttpContext.RequestServices
            .GetRequiredService<IMultiTenantStore<ApplicationTenantInfo>>();
        tenantInfo = await store.TryGetAsync(tenantId);

        // テナント固有の OIDC 設定があるか確認
        bool hasTenantOidcConfig = tenantInfo != null &&
            !string.IsNullOrEmpty(tenantInfo.OpenIdConnectAuthority) &&
            !string.IsNullOrEmpty(tenantInfo.OpenIdConnectAuthorizationEndpoint) &&
            !string.IsNullOrEmpty(tenantInfo.OpenIdConnecClientId);

        if (hasTenantOidcConfig)
        {
            // テナント固有の OIDC 設定を使用
            // ctx.Options.Configuration を動的に変更
            ctx.Options.Configuration = new OpenIdConnectConfiguration
            {
                AuthorizationEndpoint = tenantInfo!.OpenIdConnectAuthorizationEndpoint,
                TokenEndpoint = tenantInfo.OpenIdConnectTokenEndpoint,
                JwksUri = tenantInfo.OpenIdConnectJwksUri,
                EndSessionEndpoint = tenantInfo.OpenIdConnectEndSessionEndpoint,
                Issuer = tenantInfo.OpenIdConnectAuthority
            };

            // Authority を設定（TokenValidationParameters.ValidIssuer の自動設定に必要）
            ctx.Options.Authority = tenantInfo.OpenIdConnectAuthority;

            // ConfigurationManager を設定して JwksUri から公開鍵を取得できるようにする
            if (!string.IsNullOrEmpty(tenantInfo.OpenIdConnectMetadataAddress))
            {
                var env = ctx.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                var httpDocumentRetriever = new HttpDocumentRetriever
                {
                    RequireHttps = !env.IsDevelopment()
                };

                ctx.Options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    tenantInfo.OpenIdConnectMetadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    httpDocumentRetriever);
            }

            // TokenValidationParameters を直接設定（これが重要！）
            ctx.Options.TokenValidationParameters.ValidIssuer = tenantInfo.OpenIdConnectAuthority;
            ctx.Options.TokenValidationParameters.ValidateIssuer = true;
            ctx.Options.TokenValidationParameters.ValidAudience = tenantInfo.OpenIdConnecClientId;
            ctx.Options.TokenValidationParameters.ValidateAudience = true;

            // ClientId も変更
            ctx.Options.ClientId = tenantInfo.OpenIdConnecClientId;
            ctx.ProtocolMessage.ClientId = tenantInfo.OpenIdConnecClientId;

            // ProtocolMessage を直接設定（既に構築済みのため、これが重要！）
            ctx.ProtocolMessage.IssuerAddress = tenantInfo.OpenIdConnectAuthorizationEndpoint;

            // ClientSecret は Authorization エンドポイントに送信されないため、ここでは設定しない
            // OnAuthorizationCodeReceived で Token エンドポイント用に設定する
        }
        else
        {
            // Google デフォルト設定を使用
            // options.Configuration で既に設定済みのため、追加の設定は不要
        }
    }

    /// <summary>
    /// テナント固有の ClientSecret を設定（2段階復号）
    /// Google設定の場合は何もしない（options.ClientSecretで既に設定済み）
    /// </summary>
    private static async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // query パラメータから tenant を取得
        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            ctx.Fail("Tenant not found");
            return;
        }

        // Store からテナント情報を取得
        var store = ctx.HttpContext.RequestServices.GetRequiredService<IMultiTenantStore<ApplicationTenantInfo>>();
        var tenantInfo = await store.TryGetAsync(tenantId);

        if (tenantInfo != null && !string.IsNullOrEmpty(tenantInfo.OpenIdConnecClientSecret))
        {
            try
            {
                var masterEncryption = ctx.HttpContext.RequestServices.GetRequiredService<EncryptionService>();
                var decryptedSecret = DecryptTenantSecret(tenantInfo, masterEncryption, ctx.HttpContext.RequestServices);

                // Options と TokenEndpointRequest の両方を設定（これが重要！）
                ctx.Options.ClientId = tenantInfo.OpenIdConnecClientId;
                ctx.Options.ClientSecret = decryptedSecret;
                ctx.ProtocolMessage.ClientId = tenantInfo.OpenIdConnecClientId;
                ctx.ProtocolMessage.ClientSecret = decryptedSecret;

                // TokenEndpointRequest にも設定（TokenEndpoint が最重要！）
                ctx.TokenEndpointRequest.TokenEndpoint = tenantInfo.OpenIdConnectTokenEndpoint;
                ctx.TokenEndpointRequest.ClientId = tenantInfo.OpenIdConnecClientId;
                ctx.TokenEndpointRequest.ClientSecret = decryptedSecret;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decrypt ClientSecret for tenant: {Tenant}", tenantInfo.Identifier);
            }
        }
        else if (tenantInfo != null)
        {
            logger.LogDebug("TenantInfo found but ClientSecret is empty for tenant: {TenantId}", tenantId);
        }
    }

    /// <summary>
    /// トークン検証後の処理
    /// テナント検証、ユーザー同期、クレーム追加
    /// </summary>
    /// <summary>
    /// サインアウト後のコールバックURIにテナント識別子を含める
    /// </summary>
    private static Task OnRedirectToIdentityProviderForSignOut(RedirectContext ctx)
    {
        var tenantId = ctx.HttpContext.GetRouteValue("tenant")?.ToString();

        if (!string.IsNullOrEmpty(tenantId))
        {
            var scheme = ctx.HttpContext.Request.Scheme;
            var host = ctx.HttpContext.Request.Host;
            ctx.ProtocolMessage.PostLogoutRedirectUri = $"{scheme}://{host}/auth/signout-oidc?tenant={Uri.EscapeDataString(tenantId)}";
        }

        return Task.CompletedTask;
    }

    private static Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // query パラメータから tenant を取得
        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning("Tenant not found in query parameter");
            ctx.Fail("Tenant not found");
            return Task.CompletedTask;
        }

        // claim に tenant を追加（claim 戦略で自動的にテナント解決される）
        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
        if (identity.FindFirst("tenant") == null)
        {
            identity.AddClaim(new Claim("tenant", tenantId));
            logger.LogInformation("Tenant claim added: {TenantId}", tenantId);
        }

        return Task.CompletedTask;
    }

    private static string? ResolveTenantId(AuthenticationProperties? properties, HttpContext httpContext)
    {
        if (properties != null &&
            properties.Items.TryGetValue("tenant", out var tenantId) &&
            !string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        var tenantFromQuery = httpContext.Request.Query["tenant"].ToString();
        return string.IsNullOrWhiteSpace(tenantFromQuery) ? null : tenantFromQuery;
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
