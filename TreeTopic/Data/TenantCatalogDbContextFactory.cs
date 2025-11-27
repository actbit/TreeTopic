using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TreeTopic.Data;

/// <summary>
/// TenantCatalogDbContext マイグレーション作成時用ファクトリ
/// </summary>
public class TenantCatalogDbContextFactory : IDesignTimeDbContextFactory<TenantCatalogDbContext>
{
    public TenantCatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TenantCatalogDbContext>();
        optionsBuilder.UseNpgsql("Host=dummy;Database=dummy;");

        return new TenantCatalogDbContext(optionsBuilder.Options);
    }
}
