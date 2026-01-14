using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    public class BrainIdea : BaseModel
    {
        [ForeignKey(nameof(BrainBoard))]
        public Guid BrainBoardId { get; set; }
        public BrainBoard BrainBoard { get; set; }
        [ForeignKey(nameof(Topic))]
        public Guid TopicId { get; set; }
        public Topic Topic { get; set; }
        [ForeignKey(nameof(RoomUser))]
        public Guid? RoomUserId { get; set; }
        public RoomUser? RoomUser { get; set; }
        public string Idea { get; set; }

        public double PositionTop { get; set; }
        public double PositionLeft { get; set; }

        public List<BrainIdeaVote> Votes { get; set; } = new List<BrainIdeaVote>();
    }
}
