using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Common;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Services;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace TreeTopic.Controllers;

[ApiController]
[Route("{tenant}/api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserManagementService _userManagementService;
    private readonly IconService _iconService;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(
        UserManagementService userManagementService,
        IconService iconService,
        UserManager<ApplicationUser> userManager)
    {
        _userManagementService = userManagementService;
        _iconService = iconService;
        _userManager = userManager;
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

    [HttpPost("me/icon")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyIcon([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "File is required." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var guid))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(guid.ToString());
        if (user == null)
            return NotFound();

        var fileName = await _iconService.SaveUserIconAsync(user, file, cancellationToken);
        user.IconFileName = fileName;
        await _userManager.UpdateAsync(user);

        return Ok(new { iconUrl = _iconService.GetUserIconUrl(user), iconFileName = fileName });
    }

    private UserSummaryDto UserToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            IconUrl = _iconService.GetUserIconUrl(user),
            Roles = roles
        };
    }
}
