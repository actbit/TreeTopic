using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    public class BrainIdeaVote : BaseModel
    {
        [ForeignKey(nameof(BrainIdea))]
        public Guid BrainIdeaId { get; set; }
        public BrainIdea BrainIdea { get; set; }

        [ForeignKey(nameof(RoomUser))]
        public Guid? RoomUserId { get; set; }
        public RoomUser? RoomUser { get; set; }

        public string VoteType { get; set; } // "agree", "consider", "priority", "disagree"

        public int Value { get; set; } = 1;
    }
}
