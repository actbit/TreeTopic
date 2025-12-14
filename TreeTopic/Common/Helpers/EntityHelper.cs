using Microsoft.AspNetCore.Identity;
using TreeTopic.Models;
using TreeTopic.Repositories;

namespace TreeTopic.Common.Helpers;

/// <summary>
/// Helper class for consolidating common entity lookup and validation patterns
/// Reduces code duplication across services by providing reusable entity retrieval methods
/// </summary>
public static class EntityHelper
{

    /// <summary>
    /// Finds a user by ID using UserManager
    /// Returns NotFound result if user doesn't exist
    /// </summary>
    /// <param name="userManager">The UserManager instance</param>
    /// <param name="userId">The user ID to find</param>
    /// <returns>Result<ApplicationUser> with the found user or NotFound error</returns>
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

    /// <summary>
    /// Finds a user by username
    /// Returns NotFound result if user doesn't exist
    /// </summary>
    /// <param name="userManager">The UserManager instance</param>
    /// <param name="userName">The username to find</param>
    /// <returns>Result<ApplicationUser> with the found user or NotFound error</returns>
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

    /// <summary>
    /// Finds a role by ID using RoleManager
    /// Returns NotFound result if role doesn't exist
    /// </summary>
    /// <param name="roleManager">The RoleManager instance</param>
    /// <param name="roleId">The role ID to find</param>
    /// <returns>Result<ApplicationRole> with the found role or NotFound error</returns>
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

    /// <summary>
    /// Finds a role by name using RoleManager
    /// Returns NotFound result if role doesn't exist
    /// </summary>
    /// <param name="roleManager">The RoleManager instance</param>
    /// <param name="roleName">The role name to find</param>
    /// <returns>Result<ApplicationRole> with the found role or NotFound error</returns>
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

    /// <summary>
    /// Validates that a role exists without returning it
    /// Useful for permission checks or references
    /// </summary>
    /// <param name="roleManager">The RoleManager instance</param>
    /// <param name="roleId">The role ID to validate</param>
    /// <returns>Result with success if role exists, NotFound error if it doesn't</returns>
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

    /// <summary>
    /// Validates that a user exists without returning it
    /// Useful for permission checks or references
    /// </summary>
    /// <param name="userManager">The UserManager instance</param>
    /// <param name="userId">The user ID to validate</param>
    /// <returns>Result with success if user exists, NotFound error if it doesn't</returns>
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
