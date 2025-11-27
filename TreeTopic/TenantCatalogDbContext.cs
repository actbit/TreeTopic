using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic
{
    public class TenantCatalogDbContext : DbContext
    {
        public DbSet<ApplicationTenantInfo> Tenants => Set<ApplicationTenantInfo>();

        public TenantCatalogDbContext(DbContextOptions<TenantCatalogDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ApplicationTenantInfo>(b =>
            {
                b.HasKey(t => t.Id);

                b.Property(t => t.Id)
                    .HasMaxLength(64)
                    .IsRequired();
                b.Property(t => t.Name)
                    .HasMaxLength(512)
                    .IsRequired();
                b.HasIndex(t => t.Name)
                    .IsUnique();
                b.Property(t => t.Identifier)
                    .HasMaxLength(256)
                    .IsRequired();
                b.Property(t => t.RoleClaimName)
                    .HasMaxLength(256);

            });
        }
    }
}
