using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic
{
    public class TenantCatalogDbContext : DbContext
    {
        public DbSet<ApplicationTenantInfo> Tenants => Set<ApplicationTenantInfo>();
        public DbSet<SetupToken> SetupTokens => Set<SetupToken>();

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

                // SetupToken リレーション
                b.HasMany<SetupToken>()
                    .WithOne(st => st.Tenant)
                    .HasForeignKey(st => st.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SetupToken>(b =>
            {
                b.HasKey(st => st.Id);

                b.Property(st => st.TenantId)
                    .HasMaxLength(64)
                    .IsRequired();

                b.Property(st => st.TokenHash)
                    .HasMaxLength(256)
                    .IsRequired();

                b.Property(st => st.CreatedAt)
                    .IsRequired();

                b.Property(st => st.ExpiresAt)
                    .IsRequired();

                // TokenHash の一意性インデックス
                b.HasIndex(st => st.TokenHash)
                    .IsUnique();

                // TenantId でのクエリ用インデックス
                b.HasIndex(st => st.TenantId);
            });
        }
    }
}
