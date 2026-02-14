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

            var userWithRoles = new List<(ApplicationUser user, IList<string> roles)>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userWithRoles.Add((user, roles));
            }

            return Result<List<(ApplicationUser user, IList<string> roles)>>.Success(userWithRoles);
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

            var roleName = request.RoleName!.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound($"Role '{roleName}' does not exist");
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains(roleName))
            {
                return Result<(ApplicationUser, IList<string>)>.Success((user, currentRoles));
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            var identityResult = result.ToResult<(ApplicationUser, IList<string>)>((user, currentRoles));
            if (identityResult.IsFailure)
            {
                return identityResult;
            }

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

            var roleName = request.RoleName!.Trim();
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

            var updatedRoles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, updatedRoles));
        }, nameof(RemoveRoleFromUserAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> CreateUserAsync(CreateUserRequest request)
    {
        return await ExecuteAsync(async () =>
        {
            var email = request.Email!.Trim().ToLowerInvariant();

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return Result<(ApplicationUser, IList<string>)>.Conflict($"User with email '{email}' already exists");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return Result<(ApplicationUser, IList<string>)>.BadRequest(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, roles), 201);
        }, nameof(CreateUserAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> BanUserAsync(
        Guid userId, BanUserRequest request, string bannedBy)
    {
        return await ExecuteAsync(async () =>
        {
            var userResult = await EntityHelper.FindUserByIdOrNotFoundAsync(_userManager, userId);
            if (userResult.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound(userResult.Error!.Message);
            }

            var user = userResult.Data!;

            user.IsBanned = true;
            user.BannedAt = DateTime.UtcNow;
            user.BannedBy = bannedBy;
            user.BanReason = request.Reason!.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Result<(ApplicationUser, IList<string>)>.BadRequest(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, roles));
        }, nameof(BanUserAsync));
    }

    public async Task<Result<(ApplicationUser user, IList<string> roles)>> UnbanUserAsync(Guid userId)
    {
        return await ExecuteAsync(async () =>
        {
            var userResult = await EntityHelper.FindUserByIdOrNotFoundAsync(_userManager, userId);
            if (userResult.IsFailure)
            {
                return Result<(ApplicationUser, IList<string>)>.NotFound(userResult.Error!.Message);
            }

            var user = userResult.Data!;

            user.IsBanned = false;
            user.BannedAt = null;
            user.BannedBy = null;
            user.BanReason = null;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Result<(ApplicationUser, IList<string>)>.BadRequest(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            var roles = await _userManager.GetRolesAsync(user);
            return Result<(ApplicationUser, IList<string>)>.Success((user, roles));
        }, nameof(UnbanUserAsync));
    }
}
