using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class ShareItem : BaseModel
    {
        [ForeignKey(nameof(Room))]
        public Guid RoomId { get; set; }
        public Room Room { get; set; }

        [ForeignKey(nameof(Topic))]
        public Guid? TopicId { get; set; }
        public Topic? Topic { get; set; }

        [ForeignKey(nameof(BrainBoard))]
        public Guid? BrainBoardId { get; set; }
        public BrainBoard? BrainBoard { get; set; }

        public string Kind { get; set; } = "document";
        public string Title { get; set; } = string.Empty;

        public Guid CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = "Unknown";

        public Guid? SourceMessageId { get; set; }
        public Guid? SourceFileId { get; set; }
        public Guid? SourceShareItemId { get; set; }
    }
}
