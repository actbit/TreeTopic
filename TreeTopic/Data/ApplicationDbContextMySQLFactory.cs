using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TreeTopic.Models;

namespace TreeTopic.Data;

/// <summary>
/// MySQL マイグレーション作成時用ファクトリ
/// </summary>
public class ApplicationDbContextMySQLFactory : IDesignTimeDbContextFactory<ApplicationDbContextMySQL>
{
    public ApplicationDbContextMySQL CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContextMySQL>();
        optionsBuilder.UseMySql("Server=dummy;Database=dummy;",
            new MySqlServerVersion(new Version(8, 0)));

        var tenantInfo = new ApplicationTenantInfo("migration-dummy", "migration-dummy")
        {
            Detail = new ApplicationTenantDetail
            {
                TenantId = string.Empty,
                DbProvider = "mysql"
            }
        };

        tenantInfo.Detail!.TenantId = tenantInfo.Id!;

        var accessor = new DesignTimeMultiTenantContextAccessor(tenantInfo);
        return new ApplicationDbContextMySQL(accessor, optionsBuilder.Options);
    }
}