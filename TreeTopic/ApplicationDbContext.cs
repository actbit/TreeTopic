using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Models;

namespace TreeTopic
{
    public class ApplicationDbContext : MultiTenantIdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions options) : base(multiTenantContextAccessor, options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // DB プロバイダーに応じて Guid 型を最適化
            var provider = Database.ProviderName ?? "postgresql";
            if (provider.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                modelBuilder.ConfigureMySqlGuidColumns();
        }
    }
}
