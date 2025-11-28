
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using TreeTopic.Models;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Services;
using TreeTopic.Middleware;
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
        // Connection string from AppHost: "ConnectionStrings:treetopic-default"
        var appConnectionString = builder.Configuration.GetConnectionString("treetopic-default")
            ?? throw new InvalidOperationException("ApplicationDb connection string not configured");

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
                options.Cookie.Name = "auth_token";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            })
            .AddOpenIdConnectConfiguration(builder.Configuration);
         
        builder.Services
            .AddMultiTenant<ApplicationTenantInfo>()
            .WithRouteStrategy("tenant")  // {tenant} というルートパラメータでテナント抽出（OpenAPI対応）
            .WithClaimStrategy("tenant")
            .WithConfigurationStore()
            .WithPerTenantAuthentication();
        
        // マイグレーションサービスを登録
        builder.Services.AddScoped<MigrationService>();

        // ユーザー同期サービスを登録
        builder.Services.AddScoped<UserSyncService>();

        // テナント管理サービスを登録
        builder.Services.AddScoped<TenantManagementService>();

        // TenantId Obfuscationサービスを登録（外部露出時に使用）
        builder.Services.AddSingleton<TenantIdObfuscationService>();

        // 暗号化サービスを登録（Connection String暗号化用）
        builder.Services.AddSingleton<EncryptionService>();

        builder.Services.AddControllers();

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
