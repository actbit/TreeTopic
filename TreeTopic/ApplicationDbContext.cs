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
        public DbSet<BrainBoard> BrainBoards => Set<BrainBoard>();
        public DbSet<BrainIdea> BrainIdeas => Set<BrainIdea>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ApplicationUser - テナント + Sub の組み合わせに対するユニークインデックスを設定
            modelBuilder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.TenantId, u.Sub })
                .IsUnique();

            // Room リレーション
            modelBuilder.Entity<Room>()
                .HasOne(r => r.CreatedUser)
                .WithMany(u => u.Rooms)
                .HasForeignKey(r => r.CreatedUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Room>()
                .HasMany(r => r.Topics)
                .WithOne(t => t.Room)
                .HasForeignKey(t => t.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Room>()
                .HasMany(r => r.RoomUsers)
                .WithOne(ru => ru.Room)
                .HasForeignKey(ru => ru.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            // Topic リレーション
            modelBuilder.Entity<Topic>()
                .HasMany(t => t.ChildTopics)
                .WithOne(t => t.Parent)
                .HasForeignKey(t => t.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Topic>()
                .HasMany(t => t.Messages)
                .WithOne(m => m.Topic)
                .HasForeignKey(m => m.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Topic>()
                .HasMany(t => t.BrainIdeas)
                .WithOne(bi => bi.Topic)
                .HasForeignKey(bi => bi.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            // BrainBoard リレーション
            modelBuilder.Entity<BrainBoard>()
                .HasMany(bb => bb.BrainIdeas)
                .WithOne(bi => bi.BrainBoard)
                .HasForeignKey(bi => bi.BrainBoardId)
                .OnDelete(DeleteBehavior.Cascade);

            // BrainIdea リレーション
            modelBuilder.Entity<BrainIdea>()
                .HasOne(bi => bi.ApplicationUser)
                .WithMany(u => u.BrainIdeas)
                .HasForeignKey(bi => bi.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Message リレーション
            modelBuilder.Entity<Message>()
                .HasOne(m => m.ApplicationUser)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Message>()
                .HasMany(m => m.Replies)
                .WithOne(m => m.Reply)
                .HasForeignKey(m => m.ReplyId)
                .OnDelete(DeleteBehavior.SetNull);

            // RoomUser リレーション
            modelBuilder.Entity<RoomUser>()
                .HasOne(ru => ru.ApplicationUser)
                .WithMany(u => u.RoomUsers)
                .HasForeignKey(ru => ru.ApplicationUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoomUser>()
                .HasMany(ru => ru.RoomPermission)
                .WithOne(rp => rp.RoomUser)
                .HasForeignKey(rp => rp.RoomUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Permission リレーション
            modelBuilder.Entity<Permission>()
                .HasOne(p => p.Role)
                .WithMany(r => r.Authorities)
                .HasForeignKey(p => p.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // File リレーション
            modelBuilder.Entity<File>()
                .HasMany(f => f.VersionedFiles)
                .WithOne(f => f.SourceFile)
                .HasForeignKey(f => f.SourceFileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<File>()
                .HasOne(f => f.Message)
                .WithMany(m => m.Files)
                .HasForeignKey(f => f.MessageId)
                .OnDelete(DeleteBehavior.SetNull);

            // DB プロバイダーに応じて Guid 型を最適化
            var provider = Database.ProviderName ?? "postgresql";
            if (provider.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                modelBuilder.ConfigureMySqlGuidColumns();
        }
    }
}
