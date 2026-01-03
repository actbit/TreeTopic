using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserManagementService _userManagementService;

    public UsersController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _userManagementService.GetAllUsersAsync();

        if (result.IsFailure)
        {
            return result.ToActionResult(userList => userList.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList());
        }

        var userDtos = result.Data!.Select(tuple => UserToDto(tuple.user, tuple.roles)).ToList();
        return Ok(userDtos);
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<UserSummaryDto>> GetById([FromRoute] MaskedGuid userId)
    {
        var result = await _userManagementService.GetUserByIdAsync((Guid)userId);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    [HttpPost("{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> AddRole([FromRoute] MaskedGuid userId, [FromBody] RoleAssignmentRequest request)
    {
        var result = await _userManagementService.AddRoleToUserAsync((Guid)userId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    [HttpDelete("{userId}/roles")]
    public async Task<ActionResult<UserSummaryDto>> RemoveRole([FromRoute] MaskedGuid userId, [FromBody] RoleAssignmentRequest request)
    {
        var result = await _userManagementService.RemoveRoleFromUserAsync(userId, request);

        if (result.IsFailure)
        {
            return result.ToActionResult(tuple => UserToDto(tuple.user, tuple.roles));
        }

        var (user, roles) = result.Data!;
        var dto = UserToDto(user, roles);
        return Ok(dto);
    }

    private static UserSummaryDto UserToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Roles = roles
        };
    }
}
