
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using TreeTopic.Models;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Services;
using TreeTopic.Repositories;
using TreeTopic.Middleware;
using TreeTopic.Authentication;
using TreeTopic.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MaskedUUID.AspNetCore.Extensions;
using MaskedUUID.AspNetCore.KeyProviders;
using MaskedUUID.AspNetCore.Services;
using TreeTopic.KeyProviders;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using TreeTopic.Hubs;
namespace TreeTopic;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // helper local function
        static bool IsApiRequest(HttpRequest request)
        {
            var path = request.Path.Value ?? string.Empty;
            if (path.EndsWith("/auth/me", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/auth/check", StringComparison.OrdinalIgnoreCase))
                return true;

            if (AuthenticationConstants.Paths.IsApiPath(path))
                return true;

            var accept = request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(accept) && accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
                return true;

            var xRequestedWith = request.Headers["X-Requested-With"].ToString();
            if (string.Equals(xRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        // DataProtection キーを永続化（認証クッキー復号の揺らぎ防止）
        var keysDir = Path.Combine(builder.Environment.ContentRootPath, ".keys");
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
            .SetApplicationName("TreeTopic");

        // Register TenantDbContext for tenant management
        // Connection string from AppHost: "ConnectionStrings:treetopic-tenants" or fallback to config
        var tenantConnectionString = builder.Configuration.GetConnectionString("treetopic-tenants")
            ?? builder.Configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("TenantDb connection string not configured");

        builder.Services.AddDbContext<TenantCatalogDbContext>(options =>
            options.UseNpgsql(tenantConnectionString)
        );

        // Register ApplicationDbContext for multi-tenant app data
        // Connection string from AppHost: "ConnectionStrings:SharedApp"
        var appConnectionString = builder.Configuration.GetConnectionString("SharedApp")
            ?? throw new InvalidOperationException("ApplicationDb connection string (SharedApp) not configured");

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMultiTenantDatabase(sp);
        });

        // Add ASP.NET Core Identity
        builder.Services
            .AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // Add services to the container.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<IConfigureOptions<Microsoft.AspNetCore.SignalR.JsonHubProtocolOptions>, SignalRJsonOptionsConfiguration>();

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = builder.Configuration["Authentication:CookieName"] ?? "TreeTopic.Cookie";
                options.LoginPath = AuthenticationConstants.Paths.LoginPath;
                options.LogoutPath = AuthenticationConstants.Paths.LogoutPath;
                options.ExpireTimeSpan = TimeSpan.FromHours(AuthenticationConstants.Cookie.ExpirationHours);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                // SameSite=None is required for the cross-site OIDC redirect round-trip
                options.Cookie.SameSite = SameSiteMode.None;
                // Always use Secure policy (Secure=true for HTTPS, can be relaxed in dev if HTTPS is not available)
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest  // 開発環境：HTTP許可（SameSite=Noneの警告を受け入れ）
                    : CookieSecurePolicy.Always;         // 本番環境：HTTPS必須

                // Set cookie path per tenant to allow multiple tenant logins
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        if (IsApiRequest(ctx.Request))
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        }
                        var tenantId = ctx.Request.RouteValues[AuthenticationConstants.TenantClaimType]?.ToString();
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            var pathValue = ctx.Request.Path.Value;
                            if (!string.IsNullOrEmpty(pathValue))
                            {
                                var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 0)
                                {
                                    var firstSegment = segments[0];
                                    if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                        firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                                    {
                                        tenantId = firstSegment;
                                    }
                                }
                            }
                        }

                        var loginPath = string.IsNullOrEmpty(tenantId)
                            ? AuthenticationConstants.Paths.LoginPath
                            : $"/{tenantId}{AuthenticationConstants.Paths.LoginPath}";

                        ctx.Response.Redirect(loginPath);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        if (IsApiRequest(ctx.Request))
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                        ctx.Response.Redirect(ctx.RedirectUri);
                        return Task.CompletedTask;
                    },
                    OnValidatePrincipal = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogDebug("[Cookies] Principal validated for path {Path}", context.HttpContext.Request.Path);

                        if (!context.Properties.Items.ContainsKey(AuthenticationConstants.TenantClaimType))
                        {
                            var tenantId = context.HttpContext.GetRouteValue("tenant")?.ToString();
                            if (string.IsNullOrEmpty(tenantId))
                            {
                                tenantId = context.HttpContext.Request.Query[AuthenticationConstants.TenantClaimType].ToString();
                            }
                            if (string.IsNullOrEmpty(tenantId))
                            {
                                var pathValue = context.HttpContext.Request?.Path.Value;
                                if (!string.IsNullOrEmpty(pathValue))
                                {
                                    var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                                    if (segments.Length > 0)
                                    {
                                        var firstSegment = segments[0];
                                        if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                            firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                                        {
                                            tenantId = firstSegment;
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(tenantId))
                            {
                                context.Properties.Items[AuthenticationConstants.TenantClaimType] = tenantId;
                                context.HttpContext.Items[AuthenticationConstants.Cookie.TenantForCookieKey] = tenantId;
                                context.ShouldRenew = true;
                                logger.LogDebug("[Cookies] Tenant injected into auth properties: {Tenant}", tenantId);
                            }
                        }

                        return Task.CompletedTask;
                    },
                    OnSigningIn = async context =>
                    {
                        // tenant を route → query → path の順で解決
                        var tenantId = context.HttpContext.GetRouteValue("tenant")?.ToString();
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            tenantId = context.HttpContext.Request.Query[AuthenticationConstants.TenantClaimType].ToString();
                        }
                        if (string.IsNullOrEmpty(tenantId))
                        {
                            var pathValue = context.HttpContext.Request?.Path.Value;
                            if (!string.IsNullOrEmpty(pathValue))
                            {
                                var segments = pathValue.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                                if (segments.Length > 0)
                                {
                                    var firstSegment = segments[0];
                                    if (!string.IsNullOrWhiteSpace(firstSegment) &&
                                        firstSegment.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
                                    {
                                        tenantId = firstSegment;
                                    }
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(tenantId))
                        {
                            // TenantAwareCookieManager が拾えるように Items に格納
                            context.HttpContext.Items[AuthenticationConstants.Cookie.TenantForCookieKey] = tenantId;
                            context.Properties.Items[AuthenticationConstants.TenantClaimType] = tenantId;

                            context.Properties.IsPersistent = true;
                            // Set cookie to root path so multiple tenant cookies can coexist
                            // Tenant-specific cookie name is handled by TenantAwareCookieManager
                            var isSecure = !builder.Environment.IsDevelopment() || context.HttpContext.Request.IsHttps;
                            var cookieOptions = new CookieOptions
                            {
                                HttpOnly = true,
                                SameSite = SameSiteMode.None,
                                Secure = isSecure,  // 本番環境またはHTTPS接続時のみSecure=true
                                Path = AuthenticationConstants.Cookie.CookiePath,
                                Expires = DateTimeOffset.UtcNow.AddHours(AuthenticationConstants.Cookie.ExpirationHours)
                            };
                            context.CookieOptions = cookieOptions;
                        }
                    }
                };
            })
            .AddOpenIdConnectConfiguration(builder.Configuration, builder.Environment);

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(
                CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, CookieAuthenticationConfiguration>();

        var usePerTenantAuthentication = builder.Configuration.GetValue<bool>("Authentication:UsePerTenantAuthentication");

        var multiTenantBuilder = builder.Services
            .AddMultiTenant<ApplicationTenantInfo>()
            .WithStrategy<CustomClaimStrategy>(ServiceLifetime.Singleton, "tenant")
            .WithStore<EFCoreMultiTenantStore>(ServiceLifetime.Scoped);  // EF Core Store を使用

        if (usePerTenantAuthentication)
        {
            multiTenantBuilder.WithPerTenantAuthentication();
        }

        // ConfigurePerTenant を使用してテナントごとの OIDC 設定を適用
        // マスターキーを取得して EncryptionService を直接作成
        var masterEncryptionKey = builder.Configuration["Encryption:Key"]
            ?? Environment.GetEnvironmentVariable("ENCRYPTION_KEY");

        if (string.IsNullOrWhiteSpace(masterEncryptionKey))
        {
            throw new InvalidOperationException(
                "Encryption key is not configured. " +
                "Please set the 'Encryption:Key' configuration value or the 'ENCRYPTION_KEY' environment variable. " +
                "This key is required for decrypting tenant-specific OIDC ClientSecrets.");
        }

        // マスターキーが最小限の長さであることを検証（AES-256 では 32 バイト必要）
        if (masterEncryptionKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Encryption key is too short. " +
                "The encryption key must be at least 32 characters long for AES-256 encryption.");
        }

        var masterEncryptionForOidc = new EncryptionService(
            masterEncryptionKey,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        builder.Services.ConfigurePerTenant<OpenIdConnectOptions, ApplicationTenantInfo>(
            AuthenticationConstants.OidcSchemeName,
            (options, tenantInfo) =>
            {
                // テナント固有の OIDC 設定がある場合のみ適用
                var tenantDetail = tenantInfo.Detail;
                if (tenantDetail != null &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectAuthority) &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientId))
                {
                    // Authority が有効な URI かを検証
                    if (!Uri.TryCreate(tenantDetail.OpenIdConnectAuthority, UriKind.Absolute, out var authorityUri) ||
                        (authorityUri.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                    {
                        if (builder.Environment.IsDevelopment())
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Invalid Authority for tenant {tenantInfo.Id}");
                        }
                    }

                    // テナント固有の OIDC 設定を使用
                    options.Authority = tenantDetail.OpenIdConnectAuthority;
                    options.ClientId = tenantDetail.OpenIdConnectClientId;

                    // 必須のエンドポイント情報を検証
                    if (string.IsNullOrEmpty(tenantDetail.OpenIdConnectAuthorizationEndpoint) ||
                        string.IsNullOrEmpty(tenantDetail.OpenIdConnectTokenEndpoint))
                    {
                        // テナントID以外の機密情報（URL等）は記録しない
                        if (builder.Environment.IsDevelopment())
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Warning: Required OIDC endpoints not configured for tenant {tenantInfo.Id}");
                        }
                    }
                    else
                    {
                        // AuthorizationEndpoint と TokenEndpoint の URI 形式を検証
                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectAuthorizationEndpoint, UriKind.Absolute, out var authEndpoint) ||
                            (authEndpoint.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
                            if (builder.Environment.IsDevelopment())
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Invalid AuthorizationEndpoint for tenant {tenantInfo.Id}");
                            }
                        }

                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectTokenEndpoint, UriKind.Absolute, out var tokenEndpoint) ||
                            (tokenEndpoint.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
                            if (builder.Environment.IsDevelopment())
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Invalid TokenEndpoint for tenant {tenantInfo.Id}");
                            }
                        }
                    }

                    // JwksUri の URI 形式を検証（オプショナル）
                    if (!string.IsNullOrEmpty(tenantDetail.OpenIdConnectJwksUri))
                    {
                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectJwksUri, UriKind.Absolute, out var jwksUri) ||
                            (jwksUri.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
                            if (builder.Environment.IsDevelopment())
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Invalid JwksUri for tenant {tenantInfo.Id}");
                            }
                        }
                    }

                    options.Configuration = new OpenIdConnectConfiguration
                    {
                        AuthorizationEndpoint = tenantDetail.OpenIdConnectAuthorizationEndpoint,
                        TokenEndpoint = tenantDetail.OpenIdConnectTokenEndpoint,
                        JwksUri = tenantDetail.OpenIdConnectJwksUri,
                        EndSessionEndpoint = tenantDetail.OpenIdConnectEndSessionEndpoint,
                        Issuer = tenantDetail.OpenIdConnectAuthority
                    };

                    // TokenValidationParameters を設定
                    options.TokenValidationParameters.ValidIssuer = tenantDetail.OpenIdConnectAuthority;
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidAudience = tenantDetail.OpenIdConnectClientId;
                    options.TokenValidationParameters.ValidateAudience = true;

                    // ConfigurationManager を設定（JWKS 取得用）
                    if (!string.IsNullOrEmpty(tenantDetail.OpenIdConnectMetadataAddress))
                    {
                        // MetadataAddress が有効な URI かを検証
                        if (Uri.TryCreate(tenantDetail.OpenIdConnectMetadataAddress, UriKind.Absolute, out var metadataUri) &&
                            (metadataUri.Scheme == Uri.UriSchemeHttp || metadataUri.Scheme == Uri.UriSchemeHttps))
                        {
                            var httpDocumentRetriever = new HttpDocumentRetriever
                            {
                                RequireHttps = !builder.Environment.IsDevelopment()
                            };

                            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                                tenantDetail.OpenIdConnectMetadataAddress,
                                new OpenIdConnectConfigurationRetriever(),
                                httpDocumentRetriever);
                        }
                        else
                        {
                            // MetadataAddress が無効な形式：テナントID以外の機密情報は記録しない
                            if (builder.Environment.IsDevelopment())
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Invalid MetadataAddress for tenant {tenantInfo.Id}");
                            }
                        }
                    }

                    // ClientSecret を復号化して設定
                    if (!string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey) &&
                        !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientSecret))
                    {
                        try
                        {
                            var decryptedSecret = masterEncryptionForOidc.DecryptWithTenantKey(
                                tenantDetail.TenantEncryptionKey,
                                tenantDetail.OpenIdConnectClientSecret);

                            if (!string.IsNullOrEmpty(decryptedSecret))
                            {
                                options.ClientSecret = decryptedSecret;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 復号化失敗時は、ClientSecret を設定しない（デフォルトでOAuth接続時にエラーが発生）
                            if (builder.Environment.IsDevelopment())
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] ClientSecret decryption failed for tenant {tenantInfo.Id}: {ex.GetType().Name}");
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey) || !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientSecret))
                    {
                        // 暗号化キーまたはClientSecretのどちらか一方のみが設定されている（不完全な設定）
                        if (builder.Environment.IsDevelopment())
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Incomplete ClientSecret configuration for tenant {tenantInfo.Id}");
                        }
                    }
                }
                else
                {
                    // 外部 OIDC が指定されていない場合は Google デフォルト設定
                    var googleConfig = builder.Configuration.GetSection("OpenIdConnect:Providers:Google");
                    var googleAuthority = googleConfig["Authority"] ?? "https://accounts.google.com";
                    var googleClientId = builder.Configuration["Google:ClientId"];
                    var googleClientSecret = builder.Configuration["Google:ClientSecret"];

                    // Google エンドポイントを appsettings.json から取得（デフォルト値付き）
                    var googleAuthorizationEndpoint = googleConfig["AuthorizationEndpoint"] ?? "https://accounts.google.com/o/oauth2/v2/auth";
                    var googleTokenEndpoint = googleConfig["TokenEndpoint"] ?? "https://oauth2.googleapis.com/token";
                    var googleJwksUri = googleConfig["JwksUri"] ?? "https://www.googleapis.com/oauth2/v3/certs";

                    if (!string.IsNullOrEmpty(googleClientId))
                    {
                        // Google の OIDC 設定
                        options.Authority = googleAuthority;
                        options.ClientId = googleClientId;

                        // ClientSecret が設定されている場合のみ設定
                        if (!string.IsNullOrEmpty(googleClientSecret))
                        {
                            options.ClientSecret = googleClientSecret;
                        }

                        options.Configuration = new OpenIdConnectConfiguration
                        {
                            AuthorizationEndpoint = googleAuthorizationEndpoint,
                            TokenEndpoint = googleTokenEndpoint,
                            JwksUri = googleJwksUri,
                            Issuer = googleAuthority
                        };

                        // TokenValidationParameters を設定
                        options.TokenValidationParameters.ValidIssuer = googleAuthority;
                        options.TokenValidationParameters.ValidateIssuer = true;
                        options.TokenValidationParameters.ValidAudience = googleClientId;
                        options.TokenValidationParameters.ValidateAudience = true;
                    }
                    else
                    {
                        // テナント OIDC もGoogle設定もない場合
                        if (builder.Environment.IsDevelopment())
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigurePerTenant] Warning: No OIDC provider configured for tenant {tenantInfo.Id}");
                        }

                        // 設定がない場合は空文字列を設定（OnRedirectToIdentityProvider で適切に処理される）
                        options.Authority = string.Empty;
                        options.ClientId = string.Empty;
                        options.ClientSecret = string.Empty;
                    }
                }
            });

        // マイグレーションサービスを登録
        builder.Services.AddScoped<MigrationService>();

        // ユーザー同期サービスを登録
        builder.Services.AddScoped<UserSyncService>();

        // テナント管理サービスを登録
        builder.Services.AddScoped<TenantManagementService>();

        // SetupToken検証サービスを登録
        builder.Services.AddScoped<SetupTokenValidationService>();

        // TenantId Obfuscationサービスを登録（外部露出時に使用）
        builder.Services.AddSingleton<TenantIdObfuscationService>();

        // 暗号化サービスを登録（Connection String暗号化用）
        builder.Services.AddSingleton<EncryptionService>();

        // ロール管理サービスを登録
        builder.Services.AddScoped<RoleManagementService>();

        // ユーザー管理サービスを登録
        builder.Services.AddScoped<UserManagementService>();

        // パーミッション管理サービスを登録
        builder.Services.AddScoped<PermissionManagementService>();

        // Room管理サービスを登録
        builder.Services.AddScoped<IRoomManagementService, RoomManagementService>();

        // Topic管理サービスを登録
        builder.Services.AddScoped<ITopicManagementService, TopicManagementService>();

        // Message管理サービスを登録
        builder.Services.AddScoped<IMessageManagementService, MessageManagementService>();

        // File管理サービスを登録
        builder.Services.AddScoped<IFileManagementService, FileManagementService>();

        // Brainstorm管理サービスを登録
        builder.Services.AddScoped<IBrainstormManagementService, BrainstormManagementService>();

        // HttpClientを登録
        builder.Services.AddHttpClient();

        // Push通知サービスを登録（TenantCatalogDbContextを使用するためScoped）
        builder.Services.AddScoped<IVapidService, VapidService>();
        builder.Services.AddScoped<IPushService, PushService>();

        // メモリキャッシュを登録
        builder.Services.AddMemoryCache();

        // MaskedUUIDサービスを登録
        builder.Services.AddSingleton<IMaskedUUIDKeyProvider, TreeTopicMaskedUUIDKeyProvider>();
        builder.Services.AddSingleton<IMaskedUUIDService, MaskedUUIDService>();

        var mvcBuilder = builder.Services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                // Return a stable, JSON-serializable error payload for model binding / validation errors.
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(kvp => kvp.Value?.Errors != null && kvp.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors
                                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage)
                                .ToArray()
                        );

                    return new BadRequestObjectResult(new
                    {
                        message = "Invalid request",
                        errors
                    });
                };
            });

        // JSONシリアライザ設定 - 循環参照対策
        mvcBuilder.AddJsonOptions(options =>
        {
            // 循環参照を無視する設定
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

            // 最大深度を設定（デフォルトは64）
            options.JsonSerializerOptions.MaxDepth = 128;
        });

        // MaskedUUID サービスを登録
        builder.Services.AddMaskedUUID();
        mvcBuilder.AddMaskedUUIDModelBinder();

        // CORS設定
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("development", policy =>
            {
                var developmentOrigins = builder.Configuration
                    .GetSection("Cors:DevelopmentOrigins")
                    .Get<string[]>() ?? new[] { "http://localhost:5173", "http://localhost:3000", "http://localhost" };

                policy
                    .WithOrigins(developmentOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials() // SameSite=None との組み合わせに注意
                    .SetIsOriginAllowed(origin => true); // 開発環境では許容
            });

            options.AddPolicy("production", policy =>
            {
                var allowedOrigins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(allowedOrigins)
                        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH") // 明示的にメソッドを指定
                        .WithHeaders("Content-Type", "Authorization") // 明示的にヘッダーを指定
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains();
                }
                else
                {
                    // 本番環境で AllowedOrigins が設定されていない場合は警告
                    policy
                        .WithOrigins() // デフォルトでは何も許可しない
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            });
        });

        
        // ファイルアップロードのサイズ制限
        var maxFileSize = builder.Configuration.GetValue<long>("FileUpload:MaxFileSize", 31457280); // 30MB default
        builder.Services.Configure<FormOptions>(options =>
        {
            // ValueLengthLimit：フォーム値（テキストフィールド）の最大長を制限
            // アップロード最大値の1/10程度に制限（爆弾攻撃の防止）
            options.ValueLengthLimit = (int)Math.Min(maxFileSize / 10, int.MaxValue);
            // MultipartBodyLengthLimit：マルチパート本体全体のサイズ制限
            options.MultipartBodyLengthLimit = maxFileSize;
            // KeyLengthLimit：フォームキーの最大長
            options.KeyLengthLimit = 2048;
        });

        builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<ITopicRepository, TopicRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<IFileRepository, FileRepository>();
        builder.Services.AddScoped<IRoomUserRepository, RoomUserRepository>();
        builder.Services.AddScoped<IRoomPermissionRepository, RoomPermissionRepository>();
        builder.Services.AddScoped<IBrainBoardRepository, BrainBoardRepository>();
        builder.Services.AddScoped<IBrainIdeaRepository, BrainIdeaRepository>();
        builder.Services.AddScoped<IBrainIdeaVoteRepository, BrainIdeaVoteRepository>();
        builder.Services.AddScoped<IconService>();

        builder.Services.AddOpenApi(options => options.AddMaskedGuidSchemaTransformer());

        var app = builder.Build();

        // テナント作成エンドポイントのレート制限
        app.UseMiddleware<SelectiveRateLimitMiddleware>();

        // Migrate TenantCatalogDbContext at startup
        using (var scope = app.Services.CreateScope())
        {
            var tenantDbContext = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            try
            {
                var pendingMigrations = await tenantDbContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    await tenantDbContext.Database.MigrateAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during TenantCatalog database migration");
                throw;
            }
        }

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUi(options =>
            {
                options.DocumentPath = "openapi/v1.json";
            });
        }

        app.UseHttpsRedirection();

        app.UseRouting();

        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value?.EndsWith("/auth/me", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var cookieHeader = context.Request.Headers["Cookie"].ToString();
                    var hasAuthCookie = context.Request.Cookies.ContainsKey(
                        builder.Configuration["Authentication:CookieName"] ?? "TreeTopic.Cookie");
                    bool? ticketUnprotectOk = null;
                    string? ticketAuthType = null;
                    int? ticketClaimCount = null;
                    if (hasAuthCookie)
                    {
                        var cookieName = builder.Configuration["Authentication:CookieName"] ?? "TreeTopic.Cookie";
                        var cookieValue = context.Request.Cookies[cookieName];
                        var optionsMonitor = context.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
                        var cookieOptions = optionsMonitor.Get(CookieAuthenticationDefaults.AuthenticationScheme);
                        var ticket = cookieOptions.TicketDataFormat.Unprotect(cookieValue);
                        ticketUnprotectOk = ticket != null;
                        ticketAuthType = ticket?.Principal?.Identity?.AuthenticationType;
                        ticketClaimCount = ticket?.Principal?.Claims?.Count();
                    }
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation(
                        "[AuthMe] CookieHeaderLength={Length}, HasAuthCookie={HasAuthCookie}, TicketUnprotectOk={TicketUnprotectOk}, TicketAuthType={TicketAuthType}, TicketClaimCount={TicketClaimCount}",
                        cookieHeader.Length, hasAuthCookie, ticketUnprotectOk, ticketAuthType, ticketClaimCount);
                }
                await next();
            });
        }

        // Cleanup legacy chunked auth cookies so the new single cookie is used.
        app.Use(async (context, next) =>
        {
            var baseCookieName = builder.Configuration["Authentication:CookieName"] ?? "TreeTopic.Cookie";
            var deleteOptions = new CookieOptions
            {
                Path = AuthenticationConstants.Cookie.CookiePath,
                Secure = true,
                SameSite = SameSiteMode.None
            };

            void DeleteChunkedCookieSet(string cookieKey)
            {
                context.Response.Cookies.Delete(cookieKey, deleteOptions);
                for (var i = 1; i <= 5; i++)
                {
                    var chunkName = $"{cookieKey}C{i}";
                    context.Response.Cookies.Delete(chunkName, deleteOptions);
                }
            }

            var tenantSeparator = AuthenticationConstants.Cookie.TenantCookieNameSeparator;
            var tenantSuffix = AuthenticationConstants.Cookie.TenantCookieSuffix;

            foreach (var cookie in context.Request.Cookies)
            {
                var key = cookie.Key;
                if (!key.StartsWith(baseCookieName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only handle base cookie or tenant-suffixed cookie: ".TreeTopic.Auth" or ".TreeTopic.Auth_{tenant}"
                var baseKey = key;
                if (!string.Equals(key, baseCookieName, StringComparison.OrdinalIgnoreCase))
                {
                    if (key.Length <= baseCookieName.Length + 1 || key[baseCookieName.Length] != tenantSeparator[0])
                        continue;

                    // validate tenant suffix chars
                    if (!key.EndsWith(tenantSuffix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var tenantPart = key.Substring(baseCookieName.Length + 1, key.Length - baseCookieName.Length - 1 - tenantSuffix.Length);
                    var tenantValid = tenantPart.Length > 0;
                    if (tenantValid)
                    {
                        foreach (var ch in tenantPart)
                        {
                            if (!(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'))
                            {
                                tenantValid = false;
                                break;
                            }
                        }
                    }
                    if (!tenantValid)
                        continue;
                }

                if (cookie.Value.StartsWith("chunks-", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteChunkedCookieSet(baseKey);
                    continue;
                }

                // If this is a chunk cookie (ends with C + digits), delete the chunk set for its base.
                var lastIndex = key.LastIndexOf('C');
                if (lastIndex > baseCookieName.Length &&
                    lastIndex < key.Length - 1)
                {
                    var digitOk = true;
                    for (var i = lastIndex + 1; i < key.Length; i++)
                    {
                        if (!char.IsDigit(key[i]))
                        {
                            digitOk = false;
                            break;
                        }
                    }
                    if (!digitOk)
                        continue;

                    baseKey = key.Substring(0, lastIndex);
                    // Ensure baseKey still matches base cookie pattern to avoid accidental deletions.
                    if (string.Equals(baseKey, baseCookieName, StringComparison.OrdinalIgnoreCase) ||
                        (baseKey.Length > baseCookieName.Length + 1 &&
                         baseKey.StartsWith(baseCookieName + tenantSeparator, StringComparison.OrdinalIgnoreCase) &&
                         baseKey.EndsWith(tenantSuffix, StringComparison.OrdinalIgnoreCase)))
                    {
                        DeleteChunkedCookieSet(baseKey);
                    }
                }
            }

            await next();
        });

        // マルチテナントコンテキストを解決（UseAuthenticationより前に実行）
        app.UseMultiTenant();

        // CORS ミドルウェアを使用
        var corsPolicy = app.Environment.IsDevelopment() ? "development" : "production";
        app.UseCors(corsPolicy);

        app.UseAuthentication();

        app.UseAuthorization();

        // Map API controllers first (priority over static files)
        app.MapControllers();
        app.MapHub<MessageHub>("/{tenant}/hubs/messages").RequireAuthorization();
        app.MapHub<RoomTopicHub>("/{tenant}/hubs/rooms").RequireAuthorization();

        // Serve static files (SPA) after API routes
        app.UseDefaultFiles();
        var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsRoot);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsRoot),
            RequestPath = "/uploads"
        });
        app.UseStaticFiles();

        app.MapFallback(async context =>
        {
            if (AuthenticationConstants.Paths.IsApiPath(context.Request.Path.Value))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.WebRootPath, "index.html"));
        });

        app.Run();
    }
}

