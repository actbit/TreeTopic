
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

        var keysDir = Path.Combine(builder.Environment.ContentRootPath, ".keys");
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
            .SetApplicationName("TreeTopic");

        var tenantConnectionString = builder.Configuration.GetConnectionString("treetopic-tenants")
            ?? builder.Configuration.GetConnectionString("TenantDb")
            ?? throw new InvalidOperationException("TenantDb connection string not configured");

        builder.Services.AddDbContext<TenantCatalogDbContext>(options =>
            options.UseNpgsql(tenantConnectionString)
        );

        var appConnectionString = builder.Configuration.GetConnectionString("SharedApp")
            ?? throw new InvalidOperationException("ApplicationDb connection string (SharedApp) not configured");

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMultiTenantDatabase(sp);
        });

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
                        var isApi = ctx.Request.IsApiRequest();
                        var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogDebug("[OnRedirectToLogin] Path: {Path}, IsApi: {IsApi}", ctx.Request.Path, isApi);

                        if (isApi)
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

                        // returnUrlを保持 - 現在のパスをreturnUrlとして設定
                        var currentPath = ctx.Request.Path.Value ?? string.Empty;
                        var currentQuery = ctx.Request.QueryString.Value ?? string.Empty;
                        var fullReturnUrl = currentPath + currentQuery;

                        if (!string.IsNullOrEmpty(fullReturnUrl) && fullReturnUrl != "/" && !fullReturnUrl.Contains("/auth/login"))
                        {
                            loginPath += $"?returnUrl={Uri.EscapeDataString(fullReturnUrl)}";
                        }

                        ctx.Response.Redirect(loginPath);
                        return Task.CompletedTask;
                    },
                    OnRedirectToAccessDenied = ctx =>
                    {
                        if (ctx.Request.IsApiRequest())
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
                            context.HttpContext.Items[AuthenticationConstants.Cookie.TenantForCookieKey] = tenantId;
                            context.Properties.Items[AuthenticationConstants.TenantClaimType] = tenantId;

                            context.Properties.IsPersistent = true;
                            var isSecure = !builder.Environment.IsDevelopment() || context.HttpContext.Request.IsHttps;
                            var cookieOptions = new CookieOptions
                            {
                                HttpOnly = true,
                                SameSite = SameSiteMode.None,
                                Secure = isSecure,
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
            .WithStore<EFCoreMultiTenantStore>(ServiceLifetime.Scoped);

        if (usePerTenantAuthentication)
        {
            multiTenantBuilder.WithPerTenantAuthentication();
        }

        var masterEncryptionKey = builder.Configuration["Encryption:Key"]
            ?? Environment.GetEnvironmentVariable("ENCRYPTION_KEY");

        if (string.IsNullOrWhiteSpace(masterEncryptionKey))
        {
            throw new InvalidOperationException(
                "Encryption key is not configured. " +
                "Please set the 'Encryption:Key' configuration value or the 'ENCRYPTION_KEY' environment variable. " +
                "This key is required for decrypting tenant-specific OIDC ClientSecrets.");
        }

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
                var tenantDetail = tenantInfo.Detail;
                if (tenantDetail != null &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectAuthority) &&
                    !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientId))
                {
                    if (!Uri.TryCreate(tenantDetail.OpenIdConnectAuthority, UriKind.Absolute, out var authorityUri) ||
                        (authorityUri.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                    {
                    }

                    options.Authority = tenantDetail.OpenIdConnectAuthority;
                    options.ClientId = tenantDetail.OpenIdConnectClientId;

                    if (string.IsNullOrEmpty(tenantDetail.OpenIdConnectAuthorizationEndpoint) ||
                        string.IsNullOrEmpty(tenantDetail.OpenIdConnectTokenEndpoint))
                    {
                    }
                    else
                    {
                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectAuthorizationEndpoint, UriKind.Absolute, out var authEndpoint) ||
                            (authEndpoint.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
                        }

                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectTokenEndpoint, UriKind.Absolute, out var tokenEndpoint) ||
                            (tokenEndpoint.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
                        }
                    }

                    if (!string.IsNullOrEmpty(tenantDetail.OpenIdConnectJwksUri))
                    {
                        if (!Uri.TryCreate(tenantDetail.OpenIdConnectJwksUri, UriKind.Absolute, out var jwksUri) ||
                            (jwksUri.Scheme != Uri.UriSchemeHttps && !builder.Environment.IsDevelopment()))
                        {
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

                    options.TokenValidationParameters.ValidIssuer = tenantDetail.OpenIdConnectAuthority;
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidAudience = tenantDetail.OpenIdConnectClientId;
                    options.TokenValidationParameters.ValidateAudience = true;

                    if (!string.IsNullOrEmpty(tenantDetail.OpenIdConnectMetadataAddress))
                    {
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
                        }
                    }

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
                        }
                    }
                    else if (!string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey) || !string.IsNullOrEmpty(tenantDetail.OpenIdConnectClientSecret))
                    {
                    }
                }
                else
                {
                    var googleConfig = builder.Configuration.GetSection("OpenIdConnect:Providers:Google");
                    var googleAuthority = googleConfig["Authority"] ?? "https://accounts.google.com";
                    var googleClientId = builder.Configuration["Google:ClientId"];
                    var googleClientSecret = builder.Configuration["Google:ClientSecret"];

                    var googleAuthorizationEndpoint = googleConfig["AuthorizationEndpoint"] ?? "https://accounts.google.com/o/oauth2/v2/auth";
                    var googleTokenEndpoint = googleConfig["TokenEndpoint"] ?? "https://oauth2.googleapis.com/token";
                    var googleJwksUri = googleConfig["JwksUri"] ?? "https://www.googleapis.com/oauth2/v3/certs";

                    if (!string.IsNullOrEmpty(googleClientId))
                    {
                        options.Authority = googleAuthority;
                        options.ClientId = googleClientId;

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

                        options.TokenValidationParameters.ValidIssuer = googleAuthority;
                        options.TokenValidationParameters.ValidateIssuer = true;
                        options.TokenValidationParameters.ValidAudience = googleClientId;
                        options.TokenValidationParameters.ValidateAudience = true;
                    }
                    else
                    {
                        options.Authority = string.Empty;
                        options.ClientId = string.Empty;
                        options.ClientSecret = string.Empty;
                    }
                }
            });

        builder.Services.AddScoped<MigrationService>();

        builder.Services.AddScoped<UserSyncService>();

        builder.Services.AddScoped<TenantManagementService>();

        builder.Services.AddScoped<SetupTokenValidationService>();

        // Register background service for tenant cleanup
        builder.Services.AddHostedService<TenantCleanupBackgroundService>();

        builder.Services.AddSingleton<TenantIdObfuscationService>();

        builder.Services.AddSingleton<EncryptionService>();

        builder.Services.AddScoped<RoleManagementService>();

        builder.Services.AddScoped<UserManagementService>();

        builder.Services.AddScoped<RoomRoleManager>();
        builder.Services.AddScoped<RoomUserManager>();
        builder.Services.AddScoped<RoomRoleManagementService>();
        builder.Services.AddScoped<IRoomPermissionsService, RoomPermissionsService>();

        builder.Services.AddScoped<TopicPermissionManager>();

        builder.Services.AddScoped<ITopicPermissionsService, TopicPermissionsService>();

        builder.Services.AddScoped<IRoomManagementService, RoomManagementService>();

        builder.Services.AddScoped<ITopicManagementService, TopicManagementService>();

        builder.Services.AddScoped<IMessageManagementService, MessageManagementService>();

        
        builder.Services.AddScoped<IFileManagementService, FileManagementService>();

        builder.Services.AddScoped<IBrainstormManagementService, BrainstormManagementService>();

        builder.Services.AddSingleton<PermissionScanService>();

        builder.Services.AddHttpClient();

        builder.Services.AddScoped<IVapidService, VapidService>();
        builder.Services.AddScoped<IPushService, PushService>();
        builder.Services.AddScoped<IPushSubscriptionService, PushSubscriptionService>();

        builder.Services.AddMemoryCache();

        builder.Services.AddSingleton<IMaskedUUIDKeyProvider, TreeTopicMaskedUUIDKeyProvider>();
        builder.Services.AddSingleton<IMaskedUUIDService, MaskedUUIDService>();

        var mvcBuilder = builder.Services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
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

        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;

            options.JsonSerializerOptions.MaxDepth = 128;
        });

        builder.Services.AddMaskedUUID();
        mvcBuilder.AddMaskedUUIDModelBinder();

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
                    .AllowCredentials()
                    .SetIsOriginAllowed(origin => true);
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
                        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH")
                        .WithHeaders("Content-Type", "Authorization")
                        .AllowCredentials()
                        .SetIsOriginAllowedToAllowWildcardSubdomains();
                }
                else
                {
                    policy
                        .WithOrigins()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            });
        });

        var maxFileSize = builder.Configuration.GetValue<long>("FileUpload:MaxFileSize", 31457280);
        builder.Services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = (int)Math.Min(maxFileSize / 10, int.MaxValue);
            options.MultipartBodyLengthLimit = maxFileSize;
            options.KeyLengthLimit = 2048;
        });

        builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<ITopicRepository, TopicRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<IFileRepository, FileRepository>();
        builder.Services.AddScoped<IRoomUserRepository, RoomUserRepository>();
        builder.Services.AddScoped<IRoomPermissionRepository, RoomPermissionRepository>();
        builder.Services.AddScoped<IRoomRoleRepository, RoomRoleRepository>();
        builder.Services.AddScoped<IRoomUserRoleRepository, RoomUserRoleRepository>();
        builder.Services.AddScoped<IBrainBoardRepository, BrainBoardRepository>();
        builder.Services.AddScoped<IBrainIdeaRepository, BrainIdeaRepository>();
        builder.Services.AddScoped<IBrainIdeaVoteRepository, BrainIdeaVoteRepository>();
        builder.Services.AddScoped<IconService>();

        builder.Services.AddOpenApi(options => options.AddMaskedGuidSchemaTransformer());

        var app = builder.Build();

        app.UseMiddleware<SelectiveRateLimitMiddleware>();

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

        app.UseMiddleware<InvalidCookieCleanupMiddleware>();

        app.UseMultiTenant();

        var corsPolicy = app.Environment.IsDevelopment() ? "development" : "production";
        app.UseCors(corsPolicy);

        app.UseAuthentication();

        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<MessageHub>("/{tenant}/hubs/messages").RequireAuthorization();
        app.MapHub<RoomTopicHub>("/{tenant}/hubs/rooms").RequireAuthorization();
        app.MapHub<RoomUserSyncHub>("/{tenant}/hubs/roomusersync").RequireAuthorization();

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

        // Tenant cleanup background task is now started as HostedService
        app.Run();
    }
}

