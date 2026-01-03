using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Repositories;
using TreeTopic.Common;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;

namespace TreeTopic.Services;

public class BrainstormManagementService : BaseService, IBrainstormManagementService
{
    private readonly IBrainBoardRepository _boardRepository;
    private readonly IBrainIdeaRepository _ideaRepository;
    private readonly IBrainIdeaVoteRepository _voteRepository;
    private readonly ITopicRepository _topicRepository;
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _tenantAccessor;

    public BrainstormManagementService(
        IBrainBoardRepository boardRepository,
        IBrainIdeaRepository ideaRepository,
        IBrainIdeaVoteRepository voteRepository,
        ITopicRepository topicRepository,
        IMultiTenantContextAccessor<ApplicationTenantInfo> tenantAccessor,
        ILogger<BrainstormManagementService> logger) : base(logger)
    {
        _boardRepository = boardRepository;
        _ideaRepository = ideaRepository;
        _voteRepository = voteRepository;
        _topicRepository = topicRepository;
        _tenantAccessor = tenantAccessor;
    }

    private string? CurrentTenantId => _tenantAccessor.MultiTenantContext?.TenantInfo?.Id;

    // Board operations
    public async Task<Result<List<BrainstormBoardDto>>> GetAllBoardsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var boards = await _boardRepository.Query()
                .Include(b => b.Topic)
                .Include(b => b.BrainIdeas)
                .ToListAsync(cancellationToken);

            var dtos = boards.Select(MapBoardToDto).ToList();
            return Result<List<BrainstormBoardDto>>.Success(dtos);
        }, nameof(GetAllBoardsAsync));
    }

    public async Task<Result<List<BrainstormBoardDto>>> GetBoardsByTopicAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var boards = await _boardRepository.Query()
                .Where(b => b.TopicId == topicId)
                .Include(b => b.Topic)
                .Include(b => b.BrainIdeas)
                .ToListAsync(cancellationToken);

            var dtos = boards.Select(MapBoardToDto).ToList();
            return Result<List<BrainstormBoardDto>>.Success(dtos);
        }, nameof(GetBoardsByTopicAsync));
    }

    public async Task<Result<BrainstormBoardDto>> GetBoardByIdAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var board = await _boardRepository.Query()
                .Where(b => b.Id == boardId)
                .Include(b => b.Topic)
                .Include(b => b.BrainIdeas)
                .ThenInclude(i => i.Votes)
                .ThenInclude(v => v.ApplicationUser)
                .FirstOrDefaultAsync(cancellationToken);

            if (board == null)
                return Result<BrainstormBoardDto>.NotFound("Board not found");

            var dto = MapBoardToDto(board);
            return Result<BrainstormBoardDto>.Success(dto);
        }, nameof(GetBoardByIdAsync));
    }

    public async Task<Result<BrainstormBoardDto>> CreateBoardAsync(CreateBrainstormBoardRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var topic = await _topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
            if (topic == null)
                return Result<BrainstormBoardDto>.NotFound("Topic not found");

            var board = new BrainBoard
            {
                TopicId = request.TopicId,
                Name = request.Title,
                IsSign = false
            };

            await _boardRepository.AddAsync(board, cancellationToken);

            var dto = MapBoardToDto(board);
            return Result<BrainstormBoardDto>.Success(dto, 201);
        }, nameof(CreateBoardAsync));
    }

    public async Task<Result<BrainstormBoardDto>> UpdateBoardAsync(Guid boardId, UpdateBrainstormBoardRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var board = await _boardRepository.GetByIdAsync(boardId, cancellationToken);
            if (board == null)
                return Result<BrainstormBoardDto>.NotFound("Board not found");

            if (!string.IsNullOrEmpty(request.Title))
                board.Name = request.Title;

            await _boardRepository.UpdateAsync(board, cancellationToken);

            var dto = MapBoardToDto(board);
            return Result<BrainstormBoardDto>.Success(dto);
        }, nameof(UpdateBoardAsync));
    }

    public async Task<Result> DeleteBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var board = await _boardRepository.GetByIdAsync(boardId, cancellationToken);
            if (board == null)
                return Result.NotFound("Board not found");

            await _boardRepository.DeleteAsync(board, cancellationToken);
            return Result.Success(statusCode: 204);
        }, nameof(DeleteBoardAsync));
    }

    // Idea operations
    public async Task<Result<List<BrainIdeaDto>>> GetIdeasByBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var ideas = await _ideaRepository.Query()
                .Where(i => i.BrainBoardId == boardId)
                .Include(i => i.ApplicationUser)
                .Include(i => i.Votes)
                .ThenInclude(v => v.ApplicationUser)
                .ToListAsync(cancellationToken);

            var dtos = ideas.Select(MapIdeaToDto).ToList();
            return Result<List<BrainIdeaDto>>.Success(dtos);
        }, nameof(GetIdeasByBoardAsync));
    }

    public async Task<Result<BrainIdeaDto>> GetIdeaByIdAsync(Guid ideaId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var idea = await _ideaRepository.Query()
                .Where(i => i.Id == ideaId)
                .Include(i => i.ApplicationUser)
                .Include(i => i.Votes)
                .ThenInclude(v => v.ApplicationUser)
                .FirstOrDefaultAsync(cancellationToken);

            if (idea == null)
                return Result<BrainIdeaDto>.NotFound("Idea not found");

            var dto = MapIdeaToDto(idea);
            return Result<BrainIdeaDto>.Success(dto);
        }, nameof(GetIdeaByIdAsync));
    }

    public async Task<Result<BrainIdeaDto>> UpdateIdeaPositionAsync(Guid ideaId, UpdateBrainIdeaPositionRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var idea = await _ideaRepository.GetByIdAsync(ideaId, cancellationToken);
            if (idea == null)
                return Result<BrainIdeaDto>.NotFound("Idea not found");

            idea.PositionTop = request.PositionTop;
            idea.PositionLeft = request.PositionLeft;

            await _ideaRepository.UpdateAsync(idea, cancellationToken);

            var dto = MapIdeaToDto(idea);
            return Result<BrainIdeaDto>.Success(dto);
        }, nameof(UpdateIdeaPositionAsync));
    }

    public async Task<Result> DeleteIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var idea = await _ideaRepository.GetByIdAsync(ideaId, cancellationToken);
            if (idea == null)
                return Result.NotFound("Idea not found");

            await _ideaRepository.DeleteAsync(idea, cancellationToken);
            return Result.Success(statusCode: 204);
        }, nameof(DeleteIdeaAsync));
    }

    // Vote operations
    public async Task<Result<BrainIdeaVoteDto>> AddVoteAsync(Guid boardId, Guid ideaId, CreateBrainIdeaVoteRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var idea = await _ideaRepository.GetByIdAsync(ideaId, cancellationToken);
            if (idea == null)
                return Result<BrainIdeaVoteDto>.NotFound("Idea not found");

            // Check if user already voted with this vote type
            var existingVote = await _voteRepository.GetVoteAsync(ideaId, userId, request.VoteType, cancellationToken);
            if (existingVote != null)
                return Result<BrainIdeaVoteDto>.BadRequest("Already voted with this type");

            // Remove previous votes of other types from this user for this idea
            var previousVotes = await _voteRepository.Query()
                .Where(v => v.BrainIdeaId == ideaId && v.ApplicationUserId == userId)
                .ToListAsync(cancellationToken);

            foreach (var vote in previousVotes)
            {
                await _voteRepository.DeleteAsync(vote, cancellationToken);
            }

            var newVote = new BrainIdeaVote
            {
                BrainIdeaId = ideaId,
                ApplicationUserId = userId,
                VoteType = request.VoteType,
                Value = request.Value
            };

            await _voteRepository.AddAsync(newVote, cancellationToken);

            var dto = MapVoteToDto(newVote);
            return Result<BrainIdeaVoteDto>.Success(dto, 201);
        }, nameof(AddVoteAsync));
    }

    public async Task<Result> RemoveVoteAsync(Guid boardId, Guid ideaId, Guid voteId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var vote = await _voteRepository.GetByIdAsync(voteId, cancellationToken);
            if (vote == null)
                return Result.NotFound("Vote not found");

            // Verify ownership
            if (vote.ApplicationUserId != userId)
                return Result.Unauthorized("You can only remove your own votes");

            await _voteRepository.DeleteAsync(vote, cancellationToken);
            return Result.Success(statusCode: 204);
        }, nameof(RemoveVoteAsync));
    }

    public async Task<Result<List<BrainIdeaVoteDto>>> GetVotesByIdeaAsync(Guid ideaId, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var votes = await _voteRepository.GetVotesByIdeaAsync(ideaId, cancellationToken);
            var dtos = votes.Select(MapVoteToDto).ToList();
            return Result<List<BrainIdeaVoteDto>>.Success(dtos);
        }, nameof(GetVotesByIdeaAsync));
    }

    // Mapping methods
    private BrainstormBoardDto MapBoardToDto(BrainBoard board)
    {
        return new BrainstormBoardDto
        {
            Id = board.Id,
            TopicId = board.TopicId,
            Name = board.Name,
            IsSign = board.IsSign,
            IdeaCount = board.BrainIdeas?.Count ?? 0,
            Ideas = board.BrainIdeas?.Select(MapIdeaToDto).ToList()
        };
    }

    private BrainIdeaDto MapIdeaToDto(BrainIdea idea)
    {
        return new BrainIdeaDto
        {
            Id = idea.Id,
            BrainBoardId = idea.BrainBoardId,
            TopicId = idea.TopicId,
            ApplicationUserId = idea.ApplicationUserId,
            UserName = idea.ApplicationUser?.UserName,
            Idea = idea.Idea,
            PositionTop = idea.PositionTop,
            PositionLeft = idea.PositionLeft,
            Votes = idea.Votes?.Select(MapVoteToDto).ToList()
        };
    }

    private BrainIdeaVoteDto MapVoteToDto(BrainIdeaVote vote)
    {
        return new BrainIdeaVoteDto
        {
            Id = vote.Id,
            BrainIdeaId = vote.BrainIdeaId,
            ApplicationUserId = vote.ApplicationUserId,
            UserName = vote.ApplicationUser?.UserName,
            VoteType = vote.VoteType,
            Value = vote.Value
        };
    }
}
