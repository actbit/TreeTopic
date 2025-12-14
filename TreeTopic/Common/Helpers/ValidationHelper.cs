using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Common.Helpers;

/// <summary>
/// Helper class for consolidating common validation patterns
/// Provides methods to convert IdentityResult to Result, and validate common scenarios
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Converts an ASP.NET Identity IdentityResult to a Result
    /// Used after UserManager or RoleManager operations
    /// </summary>
    /// <param name="identityResult">The IdentityResult to convert</param>
    /// <returns>Result with success if operation succeeded, Failure with error messages if it failed</returns>
    public static Result ToResult(this IdentityResult identityResult)
    {
        if (identityResult.Succeeded)
        {
            return Result.Success();
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result.BadRequest(errorMessages);
    }

    /// <summary>
    /// Converts an ASP.NET Identity IdentityResult to a Result<T>
    /// Used after UserManager or RoleManager operations that should return data
    /// </summary>
    /// <typeparam name="T">The type of data to return on success</typeparam>
    /// <param name="identityResult">The IdentityResult to convert</param>
    /// <param name="data">The data to return if the operation succeeded</param>
    /// <returns>Result<T> with data if operation succeeded, Failure with error messages if it failed</returns>
    public static Result<T> ToResult<T>(this IdentityResult identityResult, T? data)
    {
        if (identityResult.Succeeded)
        {
            return Result<T>.Success(data!);
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result<T>.BadRequest(errorMessages);
    }

    /// <summary>
    /// Converts an ASP.NET Identity IdentityResult to a Result<T> with custom status code
    /// </summary>
    /// <typeparam name="T">The type of data to return on success</typeparam>
    /// <param name="identityResult">The IdentityResult to convert</param>
    /// <param name="data">The data to return if the operation succeeded</param>
    /// <param name="statusCode">The HTTP status code to use (default: 200 for success, 400 for failure)</param>
    /// <returns>Result<T> with data and custom status code if operation succeeded</returns>
    public static Result<T> ToResult<T>(this IdentityResult identityResult, T? data, int successStatusCode)
    {
        if (identityResult.Succeeded)
        {
            return Result<T>.Success(data!, successStatusCode);
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result<T>.BadRequest(errorMessages);
    }

    /// <summary>
    /// Validates that a required string is not null or empty
    /// Returns a Result indicating validation success or failure
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="fieldName">The name of the field for error messages</param>
    /// <returns>Result with success if valid, BadRequest error if invalid</returns>
    public static Result ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.BadRequest($"{fieldName} is required and cannot be empty");
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates that a required value is not null
    /// Returns a Result indicating validation success or failure
    /// </summary>
    /// <typeparam name="T">The type of value to validate</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="fieldName">The name of the field for error messages</param>
    /// <returns>Result with success if valid, BadRequest error if invalid</returns>
    public static Result ValidateRequired<T>(T? value, string fieldName) where T : class
    {
        if (value == null)
        {
            return Result.BadRequest($"{fieldName} is required");
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates that a string meets minimum length requirements
    /// Returns a Result indicating validation success or failure
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="fieldName">The name of the field for error messages</param>
    /// <param name="minLength">The minimum required length</param>
    /// <returns>Result with success if valid, BadRequest error if invalid</returns>
    public static Result ValidateMinLength(string? value, string fieldName, int minLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.BadRequest($"{fieldName} is required");
        }

        if (value.Length < minLength)
        {
            return Result.BadRequest($"{fieldName} must be at least {minLength} characters long");
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates that a string meets maximum length requirements
    /// Returns a Result indicating validation success or failure
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="fieldName">The name of the field for error messages</param>
    /// <param name="maxLength">The maximum allowed length</param>
    /// <returns>Result with success if valid, BadRequest error if invalid</returns>
    public static Result ValidateMaxLength(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success(); // Max length doesn't apply to empty values
        }

        if (value.Length > maxLength)
        {
            return Result.BadRequest($"{fieldName} must not exceed {maxLength} characters");
        }

        return Result.Success();
    }

    /// <summary>
    /// Validates that a string is within length bounds
    /// Returns a Result indicating validation success or failure
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="fieldName">The name of the field for error messages</param>
    /// <param name="minLength">The minimum required length</param>
    /// <param name="maxLength">The maximum allowed length</param>
    /// <returns>Result with success if valid, BadRequest error if invalid</returns>
    public static Result ValidateLength(string? value, string fieldName, int minLength, int maxLength)
    {
        var minResult = ValidateMinLength(value, fieldName, minLength);
        if (minResult.IsFailure)
            return minResult;

        return ValidateMaxLength(value, fieldName, maxLength);
    }

    /// <summary>
    /// Checks if a collection has items
    /// Returns a Result indicating whether the collection contains data
    /// </summary>
    /// <typeparam name="T">The type of items in the collection</typeparam>
    /// <param name="items">The collection to check</param>
    /// <param name="collectionName">The name of the collection for error messages</param>
    /// <returns>Result with success if collection has items, BadRequest error if empty</returns>
    public static Result ValidateCollectionNotEmpty<T>(IEnumerable<T>? items, string collectionName)
    {
        if (items == null || !items.Any())
        {
            return Result.BadRequest($"{collectionName} cannot be empty");
        }

        return Result.Success();
    }
}
