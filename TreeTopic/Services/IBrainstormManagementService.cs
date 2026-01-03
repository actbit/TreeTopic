using TreeTopic.Dtos;
using TreeTopic.Common;

namespace TreeTopic.Services;

public interface IBrainstormManagementService
{
    // Board operations
    Task<Result<List<BrainstormBoardDto>>> GetAllBoardsAsync(CancellationToken cancellationToken = default);
    Task<Result<List<BrainstormBoardDto>>> GetBoardsByTopicAsync(Guid topicId, CancellationToken cancellationToken = default);
    Task<Result<BrainstormBoardDto>> GetBoardByIdAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BrainstormBoardDto>> CreateBoardAsync(CreateBrainstormBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrainstormBoardDto>> UpdateBoardAsync(Guid boardId, UpdateBrainstormBoardRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBoardAsync(Guid boardId, CancellationToken cancellationToken = default);

    // Idea operations
    Task<Result<List<BrainIdeaDto>>> GetIdeasByBoardAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task<Result<BrainIdeaDto>> GetIdeaByIdAsync(Guid ideaId, CancellationToken cancellationToken = default);
    Task<Result<BrainIdeaDto>> UpdateIdeaPositionAsync(Guid ideaId, UpdateBrainIdeaPositionRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default);

    // Vote operations
    Task<Result<BrainIdeaVoteDto>> AddVoteAsync(Guid boardId, Guid ideaId, CreateBrainIdeaVoteRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> RemoveVoteAsync(Guid boardId, Guid ideaId, Guid voteId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<List<BrainIdeaVoteDto>>> GetVotesByIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default);
}
