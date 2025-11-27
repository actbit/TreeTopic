using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
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
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql("Host=dummy;Database=dummy;");

        var tenantInfo = new ApplicationTenantInfo
        {
            Id = "migration-dummy",
            Name = "migration-dummy",
            DbProvider = "postgresql"
        };

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);
        return new ApplicationDbContextPostgreSQL(accessor, optionsBuilder.Options);
    }
}
