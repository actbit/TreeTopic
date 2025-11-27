using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Data;

/// <summary>
/// マイグレーション作成時用 - PostgreSQL
/// DI に登録しない。DbContextFactory でのみ使用
/// </summary>
public class ApplicationDbContextPostgreSQL : ApplicationDbContext
{
    public ApplicationDbContextPostgreSQL(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions<ApplicationDbContext> options)
        : base(multiTenantContextAccessor, options)
    {
    }
}
