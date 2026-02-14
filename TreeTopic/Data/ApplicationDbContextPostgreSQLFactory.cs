using Finbuckle.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TreeTopic.Models;

namespace TreeTopic.Data;

/// <summary>
/// PostgreSQL マイグレーション作成時用ファクトリ
/// </summary>
public class ApplicationDbContextPostgreSQLFactory : IDesignTimeDbContextFactory<ApplicationDbContextPostgreSQL>
{
    public ApplicationDbContextPostgreSQL CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextPostgreSQL>();
        optionsBuilder.UseNpgsql("Host=dummy;Database=dummy;");

        var tenantInfo = new ApplicationTenantInfo("migration-dummy", "migration-dummy")
        {
            Detail = new ApplicationTenantDetail
            {
                TenantId = string.Empty,
                DbProvider = "postgresql"
            }
        };

        tenantInfo.Detail!.TenantId = tenantInfo.Id!;

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);
        return new ApplicationDbContextPostgreSQL(accessor, optionsBuilder.Options);
    }
}
