using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic
{
    public class TenantCatalogDbContext : DbContext
    {
        public DbSet<ApplicationTenantInfo> Tenants => Set<ApplicationTenantInfo>();
        public DbSet<ApplicationTenantDetail> TenantDetails => Set<ApplicationTenantDetail>();
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

                b.HasOne(t => t.Detail)
                    .WithOne(d => d.Tenant)
                    .HasForeignKey<ApplicationTenantDetail>(d => d.TenantId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                // SetupToken リレーション
                b.HasMany<SetupToken>()
                    .WithOne(st => st.Tenant)
                    .HasForeignKey(st => st.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ApplicationTenantDetail>(b =>
            {
                b.HasKey(d => d.TenantId);

                b.Property(d => d.TenantId)
                    .HasMaxLength(64)
                    .IsRequired();

                b.Property(d => d.DbProvider)
                    .HasMaxLength(20);

                b.Property(d => d.TenantEncryptionKey)
                    .HasMaxLength(200);

                b.Property(d => d.ConnectionString)
                    .HasMaxLength(5000);

                b.Property(d => d.OpenIdConnectMetadataAddress)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectAuthority)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectAuthorizationEndpoint)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectTokenEndpoint)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectJwksUri)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectEndSessionEndpoint)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectClientId)
                    .HasMaxLength(500);
                b.Property(d => d.OpenIdConnectClientSecret)
                    .HasMaxLength(1000);

                b.Property(d => d.RoleClaimName)
                    .HasMaxLength(256);
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
