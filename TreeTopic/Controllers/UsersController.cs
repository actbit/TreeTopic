using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreeTopic.Dtos;
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
        var (success, users, errorMessage) = await _userManagementService.GetAllUsersAsync();

        if (!success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = errorMessage });
        }

        return Ok(users);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<UserSummaryDto>> GetById(Guid userId)
    {
        var (success, user, errorMessage) = await _userManagementService.GetUserByIdAsync(userId);

        if (!success)
        {
            return NotFound(new { message = errorMessage });
        }

        return Ok(user);
    }

    [HttpPost("{userId:guid}/roles")]
    public async Task<ActionResult<UserSummaryDto>> AddRole(Guid userId, [FromBody] RoleAssignmentRequest request)
    {
        var (success, user, errorMessage) = await _userManagementService.AddRoleToUserAsync(userId, request);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("required") == true)
            {
                return BadRequest(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return Ok(user);
    }

    [HttpDelete("{userId:guid}/roles")]
    public async Task<ActionResult<UserSummaryDto>> RemoveRole(Guid userId, [FromBody] RoleAssignmentRequest request)
    {
        var (success, user, errorMessage) = await _userManagementService.RemoveRoleFromUserAsync(userId, request);

        if (!success)
        {
            if (errorMessage?.Contains("not found") == true)
            {
                return NotFound(new { message = errorMessage });
            }
            else if (errorMessage?.Contains("required") == true)
            {
                return BadRequest(new { message = errorMessage });
            }
            return BadRequest(new { message = errorMessage });
        }

        return Ok(user);
    }
}
