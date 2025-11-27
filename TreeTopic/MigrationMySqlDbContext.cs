using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;

namespace TreeTopic
{
    public class MigrationMySqlDbContext : ApplicationDbContext
    {
        public MigrationMySqlDbContext(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions options) : base(multiTenantContextAccessor, options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureMySqlGuidColumns();
        }
    }
}
