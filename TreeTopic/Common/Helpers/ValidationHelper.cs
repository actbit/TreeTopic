using Microsoft.AspNetCore.Identity;

namespace TreeTopic.Common.Helpers;

public static class ValidationHelper
{
    public static Result ToResult(this IdentityResult identityResult)
    {
        if (identityResult.Succeeded)
        {
            return Result.Success();
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result.BadRequest(errorMessages);
    }

    public static Result<T> ToResult<T>(this IdentityResult identityResult, T? data)
    {
        if (identityResult.Succeeded)
        {
            return Result<T>.Success(data!);
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result<T>.BadRequest(errorMessages);
    }

    public static Result<T> ToResult<T>(this IdentityResult identityResult, T? data, int successStatusCode)
    {
        if (identityResult.Succeeded)
        {
            return Result<T>.Success(data!, successStatusCode);
        }

        var errorMessages = string.Join("; ", identityResult.Errors.Select(e => e.Description));
        return Result<T>.BadRequest(errorMessages);
    }

    public static Result ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.BadRequest($"{fieldName} is required and cannot be empty");
        }

        return Result.Success();
    }

    public static Result ValidateRequired<T>(T? value, string fieldName) where T : class
    {
        if (value == null)
        {
            return Result.BadRequest($"{fieldName} is required");
        }

        return Result.Success();
    }

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

    public static Result ValidateLength(string? value, string fieldName, int minLength, int maxLength)
    {
        var minResult = ValidateMinLength(value, fieldName, minLength);
        if (minResult.IsFailure)
            return minResult;

        return ValidateMaxLength(value, fieldName, maxLength);
    }

    public static Result ValidateCollectionNotEmpty<T>(IEnumerable<T>? items, string collectionName)
    {
        if (items == null || !items.Any())
        {
            return Result.BadRequest($"{collectionName} cannot be empty");
        }

        return Result.Success();
    }
}
