using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    [MultiTenant]
    public class Message : BaseModel
    {
        [ForeignKey(nameof(Topic))]
        public Guid TopicId { get; set; }
        public Topic Topic { get; set; }
        [ForeignKey(nameof(ApplicationUser))]
        public Guid ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
        public string Header { get; set; }
        public string Body { get; set; }
        
        [ForeignKey(nameof(Reply))]
        public Guid? ReplyId { get; set; }
        public Message Reply { get; set; }

        public List<Message> Replies { get; set; } = new List<Message>();
        public List<File> Files { get; set; } = new List<File>();
    }
}




