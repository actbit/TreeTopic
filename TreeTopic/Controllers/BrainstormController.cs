using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
using TreeTopic.Services;
using System.Security.Claims;
using MaskedUUID.AspNetCore.Types;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class BrainstormController : ControllerBase
{
    private readonly IBrainstormManagementService _brainstormManagementService;

    public BrainstormController(
        IBrainstormManagementService brainstormManagementService)
    {
        _brainstormManagementService = brainstormManagementService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    // Board endpoints
    [HttpGet]
    public async Task<IActionResult> GetAllBoards(CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetAllBoardsAsync(cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("topic/{topicId}")]
    public async Task<IActionResult> GetBoardsByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardsByTopicAsync((Guid)topicId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{boardId}")]
    public async Task<IActionResult> GetBoardById([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardByIdAsync((Guid)boardId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.CreateBoardAsync(request, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("{boardId}")]
    public async Task<IActionResult> UpdateBoard([FromRoute] MaskedGuid boardId, [FromBody] UpdateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateBoardAsync((Guid)boardId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{boardId}")]
    public async Task<IActionResult> DeleteBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteBoardAsync((Guid)boardId, cancellationToken);
        return HandleResult(result);
    }

    // Idea endpoints
    [HttpGet("{boardId}/ideas")]
    public async Task<IActionResult> GetIdeasByBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeasByBoardAsync((Guid)boardId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("ideas/{ideaId}")]
    public async Task<IActionResult> GetIdeaById([FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeaByIdAsync((Guid)ideaId, cancellationToken);
        return HandleResult(result);
    }

    [HttpPatch("{boardId}/ideas/{ideaId}")]
    public async Task<IActionResult> UpdateIdeaPosition([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] UpdateBrainIdeaPositionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateIdeaPositionAsync((Guid)ideaId, request, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{boardId}/ideas/{ideaId}")]
    public async Task<IActionResult> DeleteIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteIdeaAsync((Guid)ideaId, cancellationToken);
        return HandleResult(result);
    }

    // Vote endpoints
    [HttpPost("{boardId}/ideas/{ideaId}/votes")]
    public async Task<IActionResult> AddVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] CreateBrainIdeaVoteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.AddVoteAsync((Guid)boardId, (Guid)ideaId, request, CurrentUserId, cancellationToken);
        return HandleResult(result);
    }

    [HttpDelete("{boardId}/ideas/{ideaId}/votes/{voteId}")]
    public async Task<IActionResult> RemoveVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromRoute] MaskedGuid voteId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.RemoveVoteAsync((Guid)boardId, (Guid)ideaId, (Guid)voteId, CurrentUserId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{boardId}/ideas/{ideaId}/votes")]
    public async Task<IActionResult> GetVotesByIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetVotesByIdeaAsync((Guid)ideaId, cancellationToken);
        return HandleResult(result);
    }

    private IActionResult HandleResult<T>(Common.Result<T> result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode, result.Data);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }

    private IActionResult HandleResult(Common.Result result)
    {
        if (result.IsSuccess)
            return StatusCode(result.StatusCode);

        return StatusCode(result.StatusCode, new { error = result.Error?.Message });
    }
}
