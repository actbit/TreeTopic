using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Topic : BaseModel
    {
        [ForeignKey(nameof(Room))]
        public Guid RoomId { get; set; }
        public Room Room { get; set; }

        [ForeignKey(nameof(Parent))]
        public Guid? ParentId { get; set; }
        public Topic? Parent { get; set; }

        [ForeignKey(nameof(SourceMessage))]
        public Guid? SourceMessageId { get; set; }
        public Message? SourceMessage { get; set; }

        public List<BrainBoard> BrainBoards { get; set; } = new List<BrainBoard>();
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public List<Message> Messages { get; set; } = new List<Message>();
        public List<Topic> ChildTopics { get; set; } = new List<Topic>();
        public List<BrainIdea> BrainIdeas { get; set; } = new List<BrainIdea>();

        /// <summary>
        /// このトピックのロール権限設定
        /// </summary>
        public List<TopicRolePermission> TopicRolePermissions { get; set; } = new();

        /// <summary>
        /// このトピックのユーザー権限設定
        /// </summary>
        public List<TopicUserPermission> TopicUserPermissions { get; set; } = new();
    }
}
