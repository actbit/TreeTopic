
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using TreeTopic.Models;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Services;
using TreeTopic.Repositories;
using TreeTopic.Middleware;
using TreeTopic.Authentication;
namespace TreeTopic;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

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

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "oidc";
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/auth/login";
                options.LogoutPath = "/auth/logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                // SameSite=None is required for the cross-site OIDC redirect round-trip
                options.Cookie.SameSite = SameSiteMode.None;
                // Keep Secure in production; allow HTTP during local dev to avoid cookie drop
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                // Set cookie path per tenant to allow multiple tenant logins
                options.Events = new CookieAuthenticationEvents
                {
                    OnSigningIn = async context =>
                    {
                        var tenantId = context.HttpContext.GetRouteValue("tenant")?.ToString();
                        if (!string.IsNullOrEmpty(tenantId))
                        {
                            context.Properties.IsPersistent = true;
                            // Set cookie to root path so multiple tenant cookies can coexist
                            // Tenant-specific cookie name is handled by TenantAwareCookieManager
                            var cookieOptions = new CookieOptions
                            {
                                Path = "/",
                                HttpOnly = true,
                                SameSite = SameSiteMode.None,
                                Secure = true,
                                Expires = DateTimeOffset.UtcNow.AddHours(8)
                            };
                            context.CookieOptions = cookieOptions;
                        }
                        await Task.CompletedTask;
                    }
                };
            })
            .AddOpenIdConnectConfiguration(builder.Configuration, builder.Environment);

        // Set TenantAwareCookieManager after authentication configuration
        builder.Services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            var httpContextAccessor = builder.Services.BuildServiceProvider().GetRequiredService<IHttpContextAccessor>();
            var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<TenantAwareCookieManager>>();
            options.CookieManager = new TenantAwareCookieManager(httpContextAccessor, logger);
        });
         
        builder.Services
            .AddMultiTenant<ApplicationTenantInfo>()
            .WithStrategy<CustomClaimStrategy>(ServiceLifetime.Singleton, "tenant")
            .WithStore<EFCoreMultiTenantStore>(ServiceLifetime.Scoped)  // EF Core Store を使用
            .WithPerTenantAuthentication();
        
        // カスタム Claim Strategy を登録
        builder.Services.AddSingleton(sp =>
            new CustomClaimStrategy("tenant", sp.GetRequiredService<ILogger<CustomClaimStrategy>>()));

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

        builder.Services.AddControllers();
        builder.Services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        builder.Services.AddScoped<IRoomRepository, RoomRepository>();
        builder.Services.AddScoped<ITopicRepository, TopicRepository>();
        builder.Services.AddScoped<IMessageRepository, MessageRepository>();
        builder.Services.AddScoped<IFileRepository, FileRepository>();
        builder.Services.AddScoped<IRoomUserRepository, RoomUserRepository>();
        builder.Services.AddScoped<IRoomPermissionRepository, RoomPermissionRepository>();
        builder.Services.AddScoped<IBrainBoardRepository, BrainBoardRepository>();
        builder.Services.AddScoped<IBrainIdeaRepository, BrainIdeaRepository>();

        builder.Services.AddOpenApi();

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
                    logger.LogInformation("Applying {Count} pending migrations to TenantCatalog database", pendingMigrations.Count());
                    await tenantDbContext.Database.MigrateAsync();
                    logger.LogInformation("TenantCatalog database migration completed successfully");
                }
                else
                {
                    logger.LogInformation("TenantCatalog database is up to date");
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

        app.UseAuthentication();

        app.UseAuthorization();

        // Map API controllers first (priority over static files)
        app.MapControllers();

        // Serve static files (SPA) after API routes
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.Run();
    }
}
