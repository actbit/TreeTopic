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

        public BrainBoard? BrainBoard { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public List<Message> Messages { get; set; } = new List<Message>();
        public List<Topic> ChildTopics { get; set; } = new List<Topic>();
        public List<BrainIdea> BrainIdeas { get; set; } = new List<BrainIdea>();

    }
}




