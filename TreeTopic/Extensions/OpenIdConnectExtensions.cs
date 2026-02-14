using Finbuckle.MultiTenant.Abstractions;
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
using TreeTopic.Data;
using Finbuckle.MultiTenant;
using Microsoft.Extensions.Hosting;
using TreeTopic.Constants;
using Microsoft.Extensions.Configuration;

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
            // OIDC相関/ナンスCookieがクロスサイトラウンドトリップで生存するよう設定
            options.CorrelationCookie.SameSite = SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.NonceCookie.SameSite = SameSiteMode.None;
            options.NonceCookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

            // SPA用コールバックパス
            // テナント情報は query parameter で渡す
            options.CallbackPath = "/auth/signin-oidc";
            // アプリケーションセッション（Cookie）のみ管理し、Keycloakセッションは管理しないためSignedOutCallbackPathは不要

            // メタデータ自動発見を防ぐため、空の Configuration を設定
            // 実際の設定は ConfigurePerTenant で動的に適用される
            options.Configuration = new OpenIdConnectConfiguration();

            // Authority と TokenValidationParameters は ConfigurePerTenant で設定される
            options.Authority = string.Empty;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;

            // ClientId は空だと Options 検証で例外になるためダミー値を設定
            // 実際の値は ConfigurePerTenant で上書きされる
            options.ClientId = "placeholder-client-id";
            options.ClientSecret = string.Empty;

            options.ResponseType = "code";
            // query モードを明示: callbackはGETリダイレクトで行う
            // （認証CookieがSameSite=Laxのため、form_post（POST）ではcorrelation以外のCookieが送信されない）
            options.ResponseMode = "query";
            // 認証Cookieの肥大化を防止（このアプリではCookie内のトークンは不要）
            options.SaveTokens = false;

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
                OnAuthorizationCodeReceived = async ctx => await OnAuthorizationCodeReceived(ctx),
                OnTokenValidated = async ctx => await OnTokenValidated(ctx),
                OnAuthenticationFailed = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ctx.Exception, "[OIDC] Authentication failed");
                    return Task.CompletedTask;
                },
                OnRemoteFailure = ctx =>
                {
                    var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ctx.Failure, "[OIDC] Remote failure: {Error}", ctx.Failure?.Message);
                    return Task.CompletedTask;
                }
            };
        });
    }

    /// <summary>
    /// テナント検証と redirect_uri 設定
    /// OIDC 設定は WithPerTenantOptions で自動的に適用される
    /// </summary>
    private static async Task OnRedirectToIdentityProvider(RedirectContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // ログインエンドポイントからのOIDCリダイレクトのみ許可
        var requestPath = ctx.HttpContext.Request.Path.Value ?? string.Empty;
        var tenantFromRoute = ctx.HttpContext.GetRouteValue("tenant")?.ToString();
        var expectedLoginPath = string.IsNullOrEmpty(tenantFromRoute)
            ? AuthenticationConstants.Paths.LoginPath
            : $"/{tenantFromRoute}{AuthenticationConstants.Paths.LoginPath}";

        if (!requestPath.Equals(expectedLoginPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[OnRedirectToIdentityProvider] Blocked OIDC redirect from non-login path: {Path}", requestPath);
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // ルートパラメータから tenant を取得
        var tenantId = ctx.HttpContext.GetRouteValue("tenant")?.ToString();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            logger.LogWarning("Tenant not found on redirect request");
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Tenant is required.");
            return;
        }

        // redirect_uri を設定
        var configuration = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var tenantDb = ctx.HttpContext.RequestServices.GetRequiredService<TenantCatalogDbContext>();
        var tenantInfo = await tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantId);

        var publicBaseUrl = configuration["Authentication:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(publicBaseUrl) &&
            Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicBaseUri))
        {
            // デフォルトテナント（OIDC未登録）の場合はtenantなし
            if (tenantInfo?.Detail?.HasOidcSettings() ?? false)
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{publicBaseUri.Scheme}://{publicBaseUri.Authority}{AuthenticationConstants.Paths.OidcCallbackPath}?tenant={Uri.EscapeDataString(tenantId)}";
            }
            else
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{publicBaseUri.Scheme}://{publicBaseUri.Authority}{AuthenticationConstants.Paths.OidcCallbackPath}";
            }
        }
        else
        {
            var scheme = ctx.HttpContext.Request.Scheme;
            var host = ctx.HttpContext.Request.Host;
            // デフォルトテナント（OIDC未登録）の場合はtenantなし
            if (tenantInfo?.Detail?.HasOidcSettings() ?? false)
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{scheme}://{host}{AuthenticationConstants.Paths.OidcCallbackPath}?tenant={Uri.EscapeDataString(tenantId)}";
            }
            else
            {
                ctx.ProtocolMessage.RedirectUri =
                    $"{scheme}://{host}{AuthenticationConstants.Paths.OidcCallbackPath}";
            }
        }

        // Properties に tenant を保存
        ctx.Properties.Items["tenant"] = tenantId;

        // ProtocolMessage の ClientId と IssuerAddress を設定
        // （ConfigurePerTenant で設定された値を ProtocolMessage にも反映）
        if (!string.IsNullOrEmpty(ctx.Options.ClientId))
        {
            ctx.ProtocolMessage.ClientId = ctx.Options.ClientId;
        }

        if (ctx.Options.Configuration?.AuthorizationEndpoint != null)
        {
            ctx.ProtocolMessage.IssuerAddress = ctx.Options.Configuration.AuthorizationEndpoint;
        }

        // 両方の設定がない場合はエラーとして記録し、処理を中断
        if (string.IsNullOrEmpty(ctx.Options.ClientId) && ctx.Options.Configuration?.AuthorizationEndpoint == null)
        {
            logger.LogError("[OnRedirectToIdentityProvider] OIDC configuration not found for tenant: {TenantId}. Ensure ConfigurePerTenant has properly initialized the OpenIdConnectOptions.", tenantId);
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "OIDC configuration is not properly configured for this tenant." });
            return;
        }
    }

    /// <summary>
    /// 認証コード受信時の処理
    /// TokenEndpointRequest に ClientSecret などを設定
    /// </summary>
    private static async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // query パラメータから tenant を取得
        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogError("[OnAuthorizationCodeReceived] Tenant not found");
            ctx.Fail("Tenant not found");
            return;
        }

        // TokenEndpointRequest に ClientId と ClientSecret を明示的に設定
        // ConfigurePerTenant で Options に設定されているが、TokenEndpointRequest にも必要
        if (!string.IsNullOrEmpty(ctx.Options.ClientId))
        {
            ctx.TokenEndpointRequest.ClientId = ctx.Options.ClientId;
        }

        if (!string.IsNullOrEmpty(ctx.Options.ClientSecret))
        {
            ctx.TokenEndpointRequest.ClientSecret = ctx.Options.ClientSecret;
        }

        logger.LogInformation("[OnAuthorizationCodeReceived] TokenEndpoint configured for tenant: {TenantId}, ClientId: {ClientId}",
            tenantId, ctx.TokenEndpointRequest.ClientId);
    }

    /// <summary>
    /// トークン検証後の処理
    /// テナント検証、ユーザー同期、クレーム追加
    /// </summary>

    private static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        // query パラメータから tenant を取得
        var tenantId = ResolveTenantId(ctx.Properties, ctx.HttpContext);

        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogWarning("Tenant not found in query parameter");
            ctx.Fail("Tenant not found");
            return;
        }

        // Audience の明示的な検証（追加のセキュリティチェック）
        var audience = ctx.Principal?.FindFirst("aud")?.Value;
        var expectedAudience = ctx.Options.ClientId;

        if (!string.IsNullOrEmpty(expectedAudience))
        {
            if (string.IsNullOrEmpty(audience))
            {
                logger.LogError("[OnTokenValidated] Audience claim not found in token for tenant: {TenantId}", tenantId);
                ctx.Fail("Audience claim missing");
                return;
            }

            if (audience != expectedAudience)
            {
                logger.LogError("[OnTokenValidated] Audience validation failed for tenant: {TenantId}. Expected: {Expected}, Actual: {Actual}",
                    tenantId, expectedAudience, audience);
                ctx.Fail("Invalid audience");
                return;
            }

            logger.LogInformation("[OnTokenValidated] Audience validation succeeded for tenant: {TenantId}, Audience: {Audience}",
                tenantId, audience);
        }

        var userSync = ctx.HttpContext.RequestServices.GetRequiredService<UserSyncService>();
        await userSync.SyncUserAsync(ctx.Principal);

        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;

        // RoleClaimName取得のためテナント情報を取得
        var tenantDb = ctx.HttpContext.RequestServices.GetRequiredService<TenantCatalogDbContext>();
        var tenant = await tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == tenantId);
        var roleClaimName = tenant?.Detail?.RoleClaimName;

        // ユーザーの存在確認とBAN状態チェック
        var subClaim = ctx.Principal?.FindFirst("sub")?.Value
            ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(subClaim))
        {
            var dbContext = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Sub == subClaim);

            if (user != null)
            {
                // BAN状態をチェック
                if (user.IsBanned)
                {
                    logger.LogWarning("[OnTokenValidated] User {UserId} is banned. Reason: {Reason}", user.Id, user.BanReason ?? "No reason provided");
                    ctx.Fail($"This account has been banned. {user.BanReason ?? "Contact administrator for details."}");
                    return;
                }

                // identityUserIdを実際のユーザーIDで更新
                var existingNameId = identity.FindFirst(ClaimTypes.NameIdentifier);
                if (existingNameId != null)
                {
                    identity.RemoveClaim(existingNameId);
                }
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            }
            else
            {
                // ユーザーがDBに存在しない場合
                // OIDCが未設定の場合、ユーザーは事前に作成されている必要がある
                if (!tenant.Detail.HasOidcSettings())
                {
                    // OIDC未設定モード - ユーザーはsetupまたはDefaultUserController経由で事前に作成されている必要がある
                    logger.LogError("[OnTokenValidated] User {Sub} does not exist in database and OIDC is not configured for tenant {TenantId}. User must be created first.", subClaim, tenantId);
                    ctx.Fail("User account not found. Please contact your administrator to create an account.");
                    return;
                }
                // OIDC設定済みの場合、UserSyncServiceにより自動作成される
            }
        }

        // claim に tenant を追加（claim 戦略で自動的にテナント解決される）
        if (identity.FindFirst("tenant") == null)
        {
            identity.AddClaim(new Claim("tenant", tenantId));
            logger.LogInformation("Tenant claim added: {TenantId}", tenantId);
        }

        // Cookieサイズ削減のため必須クレームのみ保持
        var minimalClaims = new List<Claim>();
        var nameId = identity.FindFirst(ClaimTypes.NameIdentifier)
            ?? ctx.Principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? ctx.Principal.FindFirst("sub");
        if (nameId != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.NameIdentifier, nameId.Value));
        }

        var email = ctx.Principal.FindFirst(ClaimTypes.Email)
            ?? ctx.Principal.FindFirst("email");
        if (email != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.Email, email.Value));
        }

        var name = ctx.Principal.FindFirst(ClaimTypes.Name)
            ?? ctx.Principal.FindFirst("name")
            ?? ctx.Principal.FindFirst("preferred_username");
        if (name != null)
        {
            minimalClaims.Add(new Claim(ClaimTypes.Name, name.Value));
        }

        // 上記で取得済みのroleClaimNameを使用
        if (!string.IsNullOrEmpty(roleClaimName))
        {
            // RoleClaimNameが設定されている場合：OIDCプロバイダーのロールクレームを使用
            var roleClaims = ctx.Principal.FindAll(roleClaimName);
            foreach (var claim in roleClaims)
            {
                minimalClaims.Add(new Claim(ClaimTypes.Role, claim.Value));
            }

            ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogInformation("[OnTokenValidated] Extracted {Count} roles from OIDC claim '{ClaimName}' for tenant {TenantId}",
                    roleClaims.Count(), roleClaimName, tenantId);
        }
        else
        {
            // RoleClaimNameが未設定の場合：Identity側のロールを使用（OIDCからのロールは無視）
            ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                .LogInformation("[OnTokenValidated] RoleClaimName not set, using Identity-managed roles for tenant {TenantId}", tenantId);

            // OIDCからのロールクレームを追加しない
            // 代わりに、ユーザーのロールはIdentityのUserManager経由で管理
        }

        var tenantClaim = ctx.Principal.FindFirst("tenant");
        if (tenantClaim != null)
        {
            minimalClaims.Add(new Claim("tenant", tenantClaim.Value));
        }

        var reducedIdentity = new ClaimsIdentity(
            minimalClaims,
            identity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role);
        ctx.Principal = new ClaimsPrincipal(reducedIdentity);

        // IDトークン保存は廃止（OIDCログアウト用）
        // アプリケーションはセッションCookieのみ管理し、Keycloakセッションは管理しない

        return;
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

}
