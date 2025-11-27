
using Finbuckle.MultiTenant;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using TreeTopic.Models;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Services;
namespace TreeTopic;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        // Register TenantDbContext for tenant management
        builder.Services.AddDbContext<TenantCatalogDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("TenantDb"))
        );

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseMultiTenantDatabase(sp); 
        });


        // Add services to the container.
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "oidc";

            })
            .AddCookie(options =>
            {
                options.LoginPath = "/__tenant__/Account/Login";
                options.LogoutPath = "/__tenant__/Account/Logout";
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                // Google 認証設定（デフォルト）
                options.Authority = "https://accounts.google.com";
                options.ClientId = builder.Configuration["Google:ClientId"]
                    ?? throw new InvalidOperationException("Google:ClientId is not configured");
                options.ClientSecret = builder.Configuration["Google:ClientSecret"]
                    ?? throw new InvalidOperationException("Google:ClientSecret is not configured");

                options.ResponseType = "code";
                options.SaveTokens = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");

                options.Events = new Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = ctx =>
                    {
                        // テナント情報からOIDC設定を取得
                        var mtc = ctx.HttpContext.GetMultiTenantContext<ApplicationTenantInfo>();
                        var tenantInfo = mtc?.TenantInfo;

                        if (tenantInfo?.OpenIdConnctAuthority != null)
                        {
                            ctx.ProtocolMessage.IssuerAddress = tenantInfo.OpenIdConnctAuthority;
                        }

                        if (tenantInfo?.OpenIdConnecClientId != null)
                        {
                            ctx.ProtocolMessage.ClientId = tenantInfo.OpenIdConnecClientId;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async ctx =>
                    {
                        var mtc = ctx.HttpContext.GetMultiTenantContext<ApplicationTenantInfo>();
                        var tenantInfo = mtc?.TenantInfo
                                ?? throw new Exception("Tenant not resolved.");

                        // ユーザー同期（ロール情報は除外）
                        var userSync = ctx.HttpContext.RequestServices
                            .GetRequiredService<UserSyncService>();
                        await userSync.SyncUserAsync(ctx.Principal);

                        // テナント情報・ロール情報をclaimに追加
                        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                        if (!string.IsNullOrEmpty(tenantInfo.Identifier))
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
                };
            });
         
        builder.Services
            .AddMultiTenant<ApplicationTenantInfo>()
            .WithRouteStrategy("tenant")
            .WithClaimStrategy("tenant")
            .WithConfigurationStore()
            .WithPerTenantAuthentication();
        
        // マイグレーションサービスを登録
        builder.Services.AddScoped<MigrationService>();

        // ユーザー同期サービスを登録
        builder.Services.AddScoped<UserSyncService>();

        // TenantId Obfuscationサービスを登録（外部露出時に使用）
        builder.Services.AddSingleton<TenantIdObfuscationService>();

        builder.Services.AddControllers();

        builder.Services.AddOpenApi();

        var app = builder.Build();

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

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}
