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
        public DbSet<BrainBoard> BrainBoards => Set<BrainBoard>();
        public DbSet<BrainIdea> BrainIdeas => Set<BrainIdea>();
        public DbSet<BrainIdeaVote> BrainIdeaVotes => Set<BrainIdeaVote>();
        public DbSet<ShareItem> ShareItems => Set<ShareItem>();
        public DbSet<ShareItemFile> ShareItemFiles => Set<ShareItemFile>();

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




