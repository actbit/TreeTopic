using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TreeTopic.Models;
using TreeTopic.Services;

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

            var encryption = serviceProvider
                .GetRequiredService<EncryptionService>();

            var tenant = accessor.MultiTenantContext?.TenantInfo;
            var tenantDetail = tenant?.Detail;

            // 1) どのプロバイダを使うか
            var provider = (tenantDetail?.DbProvider
                            ?? configuration["ConnectionStrings:SharedAppProvider"]
                            ?? "postgres")
                           .ToLowerInvariant();

            // 2) どの接続文字列を使うか
            var encryptedConn = tenantDetail?.ConnectionString
                       ?? configuration.GetConnectionString("SharedApp")
                       ?? throw new InvalidOperationException("No connection string for ApplicationDbContext.");

            // テナント用の接続文字列は 2 段階で復号
            var conn = tenantDetail?.ConnectionString != null
                ? DecryptTenantConnectionString(tenant, tenantDetail, encryption)
                : encryptedConn;

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

        /// <summary>
        /// テナントの接続文字列を 2 段階で復号
        /// 1. マスターキーでテナントキーを復号
        /// 2. テナントキーで接続文字列を復号
        /// </summary>
        private static string DecryptTenantConnectionString(
            ApplicationTenantInfo tenant,
            ApplicationTenantDetail tenantDetail,
            EncryptionService masterEncryption)
        {
            if (string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey))
                throw new InvalidOperationException($"Tenant '{tenant.Identifier}' has no encryption key.");

            if (string.IsNullOrEmpty(tenantDetail.ConnectionString))
                throw new InvalidOperationException($"Tenant '{tenant.Identifier}' has no connection string.");

            return masterEncryption.DecryptWithTenantKey(
                tenantDetail.TenantEncryptionKey,
                tenantDetail.ConnectionString);
        }
    }
}




