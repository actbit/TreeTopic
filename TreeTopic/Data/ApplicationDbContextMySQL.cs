using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;

namespace TreeTopic.Data;

/// <summary>
/// マイグレーション作成時用 - MySQL
/// DI に登録しない。DbContextFactory でのみ使用
/// </summary>
public class ApplicationDbContextMySQL : ApplicationDbContext
{
    public ApplicationDbContextMySQL(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions<ApplicationDbContextMySQL> options)
        : base(multiTenantContextAccessor, options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureMySqlGuidColumns();
    }
}