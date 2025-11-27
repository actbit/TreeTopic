using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using TreeTopic.Models;

namespace TreeTopic.Data;

/// <summary>
/// MySQL マイグレーション作成時用ファクトリ
/// </summary>
public class ApplicationDbContextMySQLFactory : IDesignTimeDbContextFactory<ApplicationDbContextMySQL>
{
    public ApplicationDbContextMySQL CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql("Server=dummy;Database=dummy;",
            new MySqlServerVersion(new Version(8, 0)));

        var tenantInfo = new ApplicationTenantInfo
        {
            Id = "migration-dummy",
            Name = "migration-dummy",
            DbProvider = "mysql"
        };

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);
        return new ApplicationDbContextMySQL(accessor, optionsBuilder.Options);
    }
}
