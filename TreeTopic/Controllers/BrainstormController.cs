using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using System.Security.Claims;
using MaskedUUID.AspNetCore.Types;
using MaskedUUID.AspNetCore.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class BrainstormController : ControllerBase
{
    private readonly IBrainstormManagementService _brainstormManagementService;
    private readonly IMaskedUUIDService _maskedUuidService;

    public BrainstormController(
        IBrainstormManagementService brainstormManagementService,
        IMaskedUUIDService maskedUuidService)
    {
        _brainstormManagementService = brainstormManagementService;
        _maskedUuidService = maskedUuidService;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    // Board endpoints
    [HttpGet]
    public async Task<IActionResult> GetAllBoards(CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetAllBoardsAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}")]
    public async Task<IActionResult> GetBoardsByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardsByTopicAsync((Guid)topicId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{boardId}")]
    public async Task<IActionResult> GetBoardById([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardByIdAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("encode/{rawId:guid}")]
    public IActionResult EncodeBoardId([FromRoute] Guid rawId)
    {
        var masked = _maskedUuidService.EncodeSynchronous(rawId);
        return Ok(new { maskedId = masked });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.CreateBoardAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{boardId}")]
    public async Task<IActionResult> UpdateBoard([FromRoute] MaskedGuid boardId, [FromBody] UpdateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateBoardAsync((Guid)boardId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}")]
    public async Task<IActionResult> DeleteBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteBoardAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    // Idea endpoints
    [HttpGet("{boardId}/ideas")]
    public async Task<IActionResult> GetIdeasByBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeasByBoardAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("ideas/{ideaId}")]
    public async Task<IActionResult> GetIdeaById([FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeaByIdAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("{boardId}/ideas")]
    public async Task<IActionResult> CreateIdea([FromRoute] MaskedGuid boardId, [FromBody] CreateBrainIdeaRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.CreateIdeaAsync((Guid)boardId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPatch("{boardId}/ideas/{ideaId}")]
    public async Task<IActionResult> UpdateIdeaPosition([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] UpdateBrainIdeaPositionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateIdeaPositionAsync((Guid)ideaId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}/ideas/{ideaId}")]
    public async Task<IActionResult> DeleteIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteIdeaAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

    // Vote endpoints
    [HttpPost("{boardId}/ideas/{ideaId}/votes")]
    public async Task<IActionResult> AddVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] CreateBrainIdeaVoteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.AddVoteAsync((Guid)boardId, (Guid)ideaId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}/ideas/{ideaId}/votes/{voteId}")]
    public async Task<IActionResult> RemoveVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromRoute] MaskedGuid voteId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.RemoveVoteAsync((Guid)boardId, (Guid)ideaId, (Guid)voteId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{boardId}/ideas/{ideaId}/votes")]
    public async Task<IActionResult> GetVotesByIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetVotesByIdeaAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

}
