using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TreeTopic.Common;
using TreeTopic.Common.Helpers;
using TreeTopic.Dtos;
using TreeTopic.Models;

namespace TreeTopic.Services;

public class UserManagementService : BaseService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UserManagementService> logger) : base(logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<Result<List<(ApplicationUser user, IList<string> roles)>>> GetAllUsersAsync()
    {
        return await ExecuteAsync(async () =>
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var userWithRoles = await Task.WhenAll(users.Select(async user =>
            {
                var roles = await _userManager.GetRolesAsync(user);
                return (user, roles);
            }));

            return Result<List<(ApplicationUser user, IList<string> roles)>>.Success(userWithRoles.ToList());
        }, nameof(GetAllUsersAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> GetUserByIdAsync(Guid userId)
    {
        return await ExecuteAsync(async () =>
        {
            var userResult = await EntityHelper.FindUserByIdOrNotFoundAsync(_userManager, userId);
            if (userResult.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound(userResult.Error!.Message);
            }

            var user = userResult.Data!;
            var roles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, roles));
        }, nameof(GetUserByIdAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> AddRoleToUserAsync(
        Guid userId, RoleAssignmentRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var userResult = await EntityHelper.FindUserByIdOrNotFoundAsync(_userManager, userId);
            if (userResult.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound(userResult.Error!.Message);
            }

            var user = userResult.Data!;

            // Validate role name is not empty
            var roleNameValidation = ValidationHelper.ValidateRequired(request.RoleName, "RoleName");
            if (roleNameValidation.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.BadRequest(roleNameValidation.Error!.Message);
            }

            var roleName = request.RoleName.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound($"Role '{roleName}' does not exist");
            }

            // Check if user already has this role
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains(roleName))
            {
                // Already has role, return success with current roles
                return Result<(ApplicationUser, IList<string>)>.Success((user, currentRoles));
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            var identityResult = result.ToResult<(ApplicationUser, IList<string>)>((user, currentRoles));
            if (identityResult.IsFailure)
            {
                return identityResult;
            }

            // Get updated roles
            var updatedRoles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, updatedRoles));
        }, nameof(AddRoleToUserAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> RemoveRoleFromUserAsync(
        Guid userId, RoleAssignmentRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var userResult = await EntityHelper.FindUserByIdOrNotFoundAsync(_userManager, userId);
            if (userResult.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound(userResult.Error!.Message);
            }

            var user = userResult.Data!;

            // Validate role name is not empty
            var roleNameValidation = ValidationHelper.ValidateRequired(request.RoleName, "RoleName");
            if (roleNameValidation.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.BadRequest(roleNameValidation.Error!.Message);
            }

            var roleName = request.RoleName.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound($"Role '{roleName}' does not exist");
            }

            // Check if user has this role
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (!currentRoles.Contains(roleName))
            {
                // Does not have role, return success with current roles
                return Result<(ApplicationUser, IList<string>)>.Success((user, currentRoles));
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            var identityResult = result.ToResult<(ApplicationUser, IList<string>)>((user, currentRoles));
            if (identityResult.IsFailure)
            {
                return identityResult;
            }

            // Get updated roles
            var updatedRoles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, updatedRoles));
        }, nameof(RemoveRoleFromUserAsync));
    }
}
