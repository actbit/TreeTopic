using System.ComponentModel.DataAnnotations;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Dtos;

public class BrainstormBoardDto : BaseDto
{
    public MaskedGuid TopicId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSign { get; set; }

    public int IdeaCount { get; set; }

    public List<BrainIdeaDto>? Ideas { get; set; }
}

public class CreateBrainstormBoardRequest
{
    [Required]
    public MaskedGuid TopicId { get; set; }

    [Required]
    [StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? BackgroundImage { get; set; }
}

public class UpdateBrainstormBoardRequest
{
    [StringLength(255)]
    public string? Title { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? BackgroundImage { get; set; }

    public bool? IsArchived { get; set; }
}

public class BrainIdeaDto : BaseDto
{
    public MaskedGuid BrainBoardId { get; set; }

    public MaskedGuid TopicId { get; set; }

    public MaskedGuid? ApplicationUserId { get; set; }

    public string? UserName { get; set; }

    public string Idea { get; set; } = string.Empty;

    public double PositionTop { get; set; }

    public double PositionLeft { get; set; }

    public List<BrainIdeaVoteDto>? Votes { get; set; }
}

public class CreateBrainIdeaRequest
{
    [Required]
    [StringLength(1000)]
    public string Idea { get; set; } = string.Empty;

    public double? PositionTop { get; set; }

    public double? PositionLeft { get; set; }
}

public class UpdateBrainIdeaPositionRequest
{
    [Required]
    public double PositionTop { get; set; }

    [Required]
    public double PositionLeft { get; set; }
}

public class BrainIdeaVoteDto : BaseDto
{
    public MaskedGuid BrainIdeaId { get; set; }

    public MaskedGuid? ApplicationUserId { get; set; }

    public string? UserName { get; set; }

    [Required]
    [StringLength(50)]
    public string VoteType { get; set; } = string.Empty;

    public int Value { get; set; } = 1;
}

public class CreateBrainIdeaVoteRequest
{
    [Required]
    [StringLength(50)]
    public string VoteType { get; set; } = string.Empty;

    public int Value { get; set; } = 1;
}
