using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace TreeTopic.Data;

/// <summary>
/// マイグレーション作成時用 - MySQL
/// DI に登録しない。DbContextFactory でのみ使用
/// </summary>
public class ApplicationDbContextMySQL : ApplicationDbContext
{
    public ApplicationDbContextMySQL(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions<ApplicationDbContext> options)
        : base(multiTenantContextAccessor, options)
    {
    }
}
