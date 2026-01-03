using System.ComponentModel.DataAnnotations.Schema;

namespace TreeTopic.Models
{
    public class BrainIdeaVote : BaseModel
    {
        [ForeignKey(nameof(BrainIdea))]
        public Guid BrainIdeaId { get; set; }
        public BrainIdea BrainIdea { get; set; }

        [ForeignKey(nameof(ApplicationUser))]
        public Guid? ApplicationUserId { get; set; }
        public ApplicationUser? ApplicationUser { get; set; }

        public string VoteType { get; set; } // "agree", "consider", "priority", "disagree"

        public int Value { get; set; } = 1;
    }
}
