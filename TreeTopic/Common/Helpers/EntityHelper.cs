using Microsoft.AspNetCore.Identity;
using TreeTopic.Models;
using TreeTopic.Repositories;

namespace TreeTopic.Common.Helpers;

public static class EntityHelper
{
    public static async Task<Result<ApplicationUser>> FindUserByIdOrNotFoundAsync(
        UserManager<ApplicationUser> userManager,
        Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Result<ApplicationUser>.BadRequest("User ID cannot be empty");
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result<ApplicationUser>.NotFound($"User with ID '{userId}' not found");
        }

        return Result<ApplicationUser>.Success(user);
    }

    public static async Task<Result<ApplicationUser>> FindUserByNameOrNotFoundAsync(
        UserManager<ApplicationUser> userManager,
        string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result<ApplicationUser>.BadRequest("Username cannot be empty");
        }

        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            return Result<ApplicationUser>.NotFound($"User '{userName}' not found");
        }

        return Result<ApplicationUser>.Success(user);
    }

    public static async Task<Result<ApplicationRole>> FindRoleByIdOrNotFoundAsync(
        RoleManager<ApplicationRole> roleManager,
        Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            return Result<ApplicationRole>.BadRequest("Role ID cannot be empty");
        }

        var role = await roleManager.FindByIdAsync(roleId.ToString());
        if (role == null)
        {
            return Result<ApplicationRole>.NotFound($"Role with ID '{roleId}' not found");
        }

        return Result<ApplicationRole>.Success(role);
    }

    public static async Task<Result<ApplicationRole>> FindRoleByNameOrNotFoundAsync(
        RoleManager<ApplicationRole> roleManager,
        string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return Result<ApplicationRole>.BadRequest("Role name cannot be empty");
        }

        var role = await roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            return Result<ApplicationRole>.NotFound($"Role '{roleName}' not found");
        }

        return Result<ApplicationRole>.Success(role);
    }

    public static async Task<Result> ValidateRoleExistsAsync(
        RoleManager<ApplicationRole> roleManager,
        Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            return Result.BadRequest("Role ID cannot be empty");
        }

        var roleExists = await roleManager.FindByIdAsync(roleId.ToString()) != null;
        if (!roleExists)
        {
            return Result.NotFound($"Role with ID '{roleId}' not found");
        }

        return Result.Success();
    }

    public static async Task<Result> ValidateUserExistsAsync(
        UserManager<ApplicationUser> userManager,
        Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return Result.BadRequest("User ID cannot be empty");
        }

        var userExists = await userManager.FindByIdAsync(userId.ToString()) != null;
        if (!userExists)
        {
            return Result.NotFound($"User with ID '{userId}' not found");
        }

        return Result.Success();
    }
}
