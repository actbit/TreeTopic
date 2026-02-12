using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Services;
using TreeTopic.Filters;
using TreeTopic.Permissions;
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

    private Guid CurrentUserId
    {
        get
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated or has invalid user ID.");
            }
            return userId;
        }
    }

    // Board endpoints
    [HttpGet]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetAllBoards(CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetAllBoardsAsync(cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("topic/{topicId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetBoardsByTopic([FromRoute] MaskedGuid topicId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardsByTopicAsync((Guid)topicId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{boardId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetBoardById([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetBoardByIdAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.CreateBoardAsync(request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPut("{boardId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> UpdateBoard([FromRoute] MaskedGuid boardId, [FromBody] UpdateBrainstormBoardRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateBoardAsync((Guid)boardId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> DeleteBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteBoardAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    // Idea endpoints
    [HttpGet("{boardId}/ideas")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetIdeasByBoard([FromRoute] MaskedGuid boardId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeasByBoardAsync((Guid)boardId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("ideas/{ideaId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetIdeaById([FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetIdeaByIdAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPost("{boardId}/ideas")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> CreateIdea([FromRoute] MaskedGuid boardId, [FromBody] CreateBrainIdeaRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.CreateIdeaAsync((Guid)boardId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpPatch("{boardId}/ideas/{ideaId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> UpdateIdeaPosition([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] UpdateBrainIdeaPositionRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.UpdateIdeaPositionAsync((Guid)ideaId, request, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}/ideas/{ideaId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> DeleteIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.DeleteIdeaAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

    // Vote endpoints
    [HttpPost("{boardId}/ideas/{ideaId}/votes")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> AddVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromBody] CreateBrainIdeaVoteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _brainstormManagementService.AddVoteAsync((Guid)boardId, (Guid)ideaId, request, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpDelete("{boardId}/ideas/{ideaId}/votes/{voteId}")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Write, RoomPermissions.TopicWrite, TenantPermissions.TopicManage)]
    public async Task<IActionResult> RemoveVote([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, [FromRoute] MaskedGuid voteId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.RemoveVoteAsync((Guid)boardId, (Guid)ideaId, (Guid)voteId, CurrentUserId, cancellationToken);
        return result.ToApiResult();
    }

    [HttpGet("{boardId}/ideas/{ideaId}/votes")]
    [RequireAny(PermissionScope.Topic, TopicPermissions.Read, RoomPermissions.TopicRead, TenantPermissions.TopicRead)]
    public async Task<IActionResult> GetVotesByIdea([FromRoute] MaskedGuid boardId, [FromRoute] MaskedGuid ideaId, CancellationToken cancellationToken)
    {
        var result = await _brainstormManagementService.GetVotesByIdeaAsync((Guid)ideaId, cancellationToken);
        return result.ToApiResult();
    }

}
