using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Extensions;
using TreeTopic.Models;
using File = TreeTopic.Models.File;

namespace TreeTopic
{
    public class ApplicationDbContext : MultiTenantIdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        public ApplicationDbContext(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions options) : base(multiTenantContextAccessor, options)
        {
        }
        public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
        public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Topic> Topics => Set<Topic>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<File> Files => Set<File>();
        public DbSet<RoomUser> RoomUsers => Set<RoomUser>();
        public DbSet<RoomPermission> RoomPermissions => Set<RoomPermission>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ApplicationUser - テナント + Sub の組み合わせに対するユニークインデックスを設定
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.TenantId, u.Sub })
                .IsUnique();

            // DB プロバイダーに応じて Guid 型を最適化
            var provider = Database.ProviderName ?? "postgresql";
            if (provider.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                modelBuilder.ConfigureMySqlGuidColumns();
        }
    }
}
