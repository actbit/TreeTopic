using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    public class BrainBoard : BaseModel
    {
        [ForeignKey(nameof(Topic))]
        public Guid TopicId { get; set; }
        public Topic Topic { get; set; }
        public string Name { get; set; }
        public bool IsSign { get; set; }

        public List<BrainIdea> BrainIdeas { get; set; } = new List<BrainIdea>();
    }
}
