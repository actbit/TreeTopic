using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TreeTopic.Extensions;
using TreeTopic.Models;
using File = TreeTopic.Models.File;

namespace TreeTopic
{
    public class ApplicationDbContext : MultiTenantIdentityDbContext<ApplicationUser, ApplicationRole, Guid>
    {
        private readonly IMultiTenantContextAccessor _multiTenantContextAccessor;

        public ApplicationDbContext(IMultiTenantContextAccessor multiTenantContextAccessor, DbContextOptions options) : base(multiTenantContextAccessor, options)
        {
            _multiTenantContextAccessor = multiTenantContextAccessor;
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
        public DbSet<RoomRole> RoomRoles => Set<RoomRole>();
        public DbSet<RoomUserRoomRole> RoomUserRoomRoles => Set<RoomUserRoomRole>();
        public DbSet<RoomRolePermission> RoomRolePermissions => Set<RoomRolePermission>();
        public DbSet<TopicRolePermission> TopicRolePermissions => Set<TopicRolePermission>();
        public DbSet<TopicUserPermission> TopicUserPermissions => Set<TopicUserPermission>();
        public DbSet<BrainBoard> BrainBoards => Set<BrainBoard>();
        public DbSet<BrainIdea> BrainIdeas => Set<BrainIdea>();
        public DbSet<BrainIdeaVote> BrainIdeaVotes => Set<BrainIdeaVote>();
        public DbSet<ShareItem> ShareItems => Set<ShareItem>();
        public DbSet<ShareItemFile> ShareItemFiles => Set<ShareItemFile>();
        public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
        public DbSet<UserTopic> UserTopics => Set<UserTopic>();

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

            modelBuilder.Entity<Room>()
                .HasMany<ShareItem>()
                .WithOne(s => s.Room)
                .HasForeignKey(s => s.RoomId)
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

            modelBuilder.Entity<Topic>()
                .HasMany(t => t.BrainBoards)
                .WithOne(bb => bb.Topic)
                .HasForeignKey(bb => bb.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Topic>()
                .HasMany<ShareItem>()
                .WithOne(s => s.Topic)
                .HasForeignKey(s => s.TopicId)
                .OnDelete(DeleteBehavior.SetNull);

            // BrainBoard リレーション
            modelBuilder.Entity<BrainBoard>()
                .HasMany(bb => bb.BrainIdeas)
                .WithOne(bi => bi.BrainBoard)
                .HasForeignKey(bi => bi.BrainBoardId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BrainBoard>()
                .HasMany<ShareItem>()
                .WithOne(s => s.BrainBoard)
                .HasForeignKey(s => s.BrainBoardId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ShareItem>()
                .HasMany<ShareItemFile>()
                .WithOne(sif => sif.ShareItem)
                .HasForeignKey(sif => sif.ShareItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ShareItem>()
                .HasOne(s => s.CreatedByRoomUser)
                .WithMany()
                .HasForeignKey(s => s.CreatedByRoomUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShareItem>()
                .HasOne(s => s.SourceMessage)
                .WithMany()
                .HasForeignKey(s => s.SourceMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ShareItem>()
                .HasOne(s => s.SourceFile)
                .WithMany()
                .HasForeignKey(s => s.SourceFileId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ShareItem>()
                .HasOne(s => s.SourceShareItem)
                .WithMany()
                .HasForeignKey(s => s.SourceShareItemId)
                .OnDelete(DeleteBehavior.SetNull);

            // BrainIdea リレーション
            modelBuilder.Entity<BrainIdea>()
                .HasOne(bi => bi.RoomUser)
                .WithMany()
                .HasForeignKey(bi => bi.RoomUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BrainIdea>()
                .HasMany(bi => bi.Votes)
                .WithOne(v => v.BrainIdea)
                .HasForeignKey(v => v.BrainIdeaId)
                .OnDelete(DeleteBehavior.Cascade);

            // BrainIdeaVote リレーション
            modelBuilder.Entity<BrainIdeaVote>()
                .HasOne(v => v.RoomUser)
                .WithMany()
                .HasForeignKey(v => v.RoomUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Message リレーション
            modelBuilder.Entity<Message>()
                .HasOne(m => m.RoomUser)
                .WithMany()
                .HasForeignKey(m => m.RoomUserId)
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

            // RoomUserRoomRole 多対多リレーション
            modelBuilder.Entity<RoomUserRoomRole>()
                .HasIndex(rur => new { rur.RoomUserId, rur.RoomRoleId })
                .IsUnique();

            modelBuilder.Entity<RoomUserRoomRole>()
                .HasOne(rur => rur.RoomUser)
                .WithMany(ru => ru.RoomUserRoomRoles)
                .HasForeignKey(rur => rur.RoomUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RoomUserRoomRole>()
                .HasOne(rur => rur.RoomRole)
                .WithMany(rr => rr.RoomUserRoomRoles)
                .HasForeignKey(rur => rur.RoomRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // RoomRole リレーション
            modelBuilder.Entity<RoomRole>()
                .HasMany(rr => rr.Permissions)
                .WithOne(rrp => rrp.RoomRole)
                .HasForeignKey(rrp => rrp.RoomRoleId)
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

            // PushSubscription リレーション
            modelBuilder.Entity<PushSubscription>()
                .HasIndex(ps => new { ps.TenantId, ps.UserId, ps.Endpoint })
                .IsUnique();

            modelBuilder.Entity<PushSubscription>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserTopic リレーション
            modelBuilder.Entity<UserTopic>()
                .HasIndex(ut => new { ut.UserId, ut.TopicId })
                .IsUnique();

            modelBuilder.Entity<UserTopic>()
                .HasOne(ut => ut.Topic)
                .WithMany()
                .HasForeignKey(ut => ut.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            // TopicRolePermission リレーション
            modelBuilder.Entity<TopicRolePermission>()
                .HasIndex(trp => new { trp.TopicId, trp.RoomRoleId, trp.Name })
                .IsUnique();

            modelBuilder.Entity<TopicRolePermission>()
                .HasOne(trp => trp.Topic)
                .WithMany(t => t.TopicRolePermissions)
                .HasForeignKey(trp => trp.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicRolePermission>()
                .HasOne(trp => trp.RoomRole)
                .WithMany(rr => rr.TopicRolePermissions)
                .HasForeignKey(trp => trp.RoomRoleId)
                .OnDelete(DeleteBehavior.Cascade);

            // TopicUserPermission リレーション
            modelBuilder.Entity<TopicUserPermission>()
                .HasIndex(tup => new { tup.TopicId, tup.RoomUserId, tup.Name })
                .IsUnique();

            modelBuilder.Entity<TopicUserPermission>()
                .HasOne(tup => tup.Topic)
                .WithMany(t => t.TopicUserPermissions)
                .HasForeignKey(tup => tup.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicUserPermission>()
                .HasOne(tup => tup.RoomUser)
                .WithMany(ru => ru.TopicUserPermissions)
                .HasForeignKey(tup => tup.RoomUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // DB プロバイダーに応じて Guid 型を最適化
            var provider = Database.ProviderName ?? "postgresql";
            if (provider.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                modelBuilder.ConfigureMySqlGuidColumns();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyTenantId();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            ApplyTenantId();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyTenantId()
        {
            var tenantInfo = _multiTenantContextAccessor.MultiTenantContext?.TenantInfo as ApplicationTenantInfo;
            var tenantId = tenantInfo?.Id;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return;
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added)
                {
                    continue;
                }

                var tenantProperty = entry.Properties.FirstOrDefault(p =>
                    p.Metadata.Name == "TenantId" && p.Metadata.ClrType == typeof(string));

                if (tenantProperty != null && tenantProperty.CurrentValue == null)
                {
                    tenantProperty.CurrentValue = tenantId;
                }
            }
        }
    }
}




