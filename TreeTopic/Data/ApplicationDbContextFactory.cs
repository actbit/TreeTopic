using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TreeTopic.Models;

namespace TreeTopic.Data;

/// <summary>
/// マイグレーション作成時用の DbContext ファクトリ
/// 環境変数 EF_PROVIDER で PostgreSQL / MySQL を指定
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var efProvider = Environment.GetEnvironmentVariable("EF_PROVIDER")?.ToLower() ?? "postgresql";
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (efProvider == "mysql")
        {
            // ダミー接続文字列（実際には接続しない）
            optionsBuilder.UseMySql("Server=dummy;Database=dummy;",
                new MySqlServerVersion(new Version(8, 0)));
        }
        else
        {
            // ダミー接続文字列（実際には接続しない）
            optionsBuilder.UseNpgsql("Host=dummy;Database=dummy;");
        }

        var tenantInfo = new ApplicationTenantInfo
        {
            Id = "migration-dummy",
            Name = "migration-dummy",
            DbProvider = efProvider
        };

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);

        // マイグレーション専用 DbContext を返す
        if (efProvider == "mysql")
        {
            return new ApplicationDbContextMySQL(accessor, optionsBuilder.Options);
        }
        else
        {
            return new ApplicationDbContextPostgreSQL(accessor, optionsBuilder.Options);
        }
    }
}

/// <summary>
/// マイグレーション作成時用のダミー IMultiTenantContextAccessor 実装
/// </summary>
internal class DesignTimeMultiTenantContextAccessor : IMultiTenantContextAccessor
{
    public IMultiTenantContext MultiTenantContext { get; set; }

    public DesignTimeMultiTenantContextAccessor(ApplicationTenantInfo tenantInfo)
    {
        MultiTenantContext = new MultiTenantContext<ApplicationTenantInfo>
        {
            TenantInfo = tenantInfo
        };
    }

    public void SetTenantContext(IMultiTenantContext context)
    {
        MultiTenantContext = context;
    }
}
