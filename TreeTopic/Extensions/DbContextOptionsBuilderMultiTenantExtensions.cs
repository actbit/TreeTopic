using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TreeTopic.Models;

namespace TreeTopic.Extensions
{
    public static class DbContextOptionsBuilderMultiTenantExtensions
    {
        public static void UseMultiTenantDatabase(
    this DbContextOptionsBuilder options,
    IServiceProvider serviceProvider)
        {
            var accessor = serviceProvider
                .GetRequiredService<IMultiTenantContextAccessor<ApplicationTenantInfo>>();

            var configuration = serviceProvider
                .GetRequiredService<IConfiguration>();

            var tenant = accessor.MultiTenantContext?.TenantInfo;

            // 1) どのプロバイダを使うか
            var provider = (tenant?.DbProvider
                            ?? configuration["ConnectionStrings:SharedAppProvider"]
                            ?? "postgres")
                           .ToLowerInvariant();

            // 2) どの接続文字列を使うか
            var conn = tenant?.ConnectionString
                       ?? configuration.GetConnectionString("SharedApp")
                       ?? throw new InvalidOperationException("No connection string for ApplicationDbContext.");

            switch (provider)
            {
                case "mysql":
                    options.UseMySql(conn, ServerVersion.AutoDetect(conn));
                    break;

                case "postgres":
                case "postgresql":
                case "pgsql":
                default:
                    options.UseNpgsql(conn);
                    break;

            }
        }
    }
}
