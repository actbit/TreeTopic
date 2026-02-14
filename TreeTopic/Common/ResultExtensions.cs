using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace TreeTopic.Common;

public static class ResultExtensions
{
    public static Result<T> ToResult<T>(this IdentityResult identityResult, T data)
    {
        if (identityResult.Succeeded)
        {
            return Result<T>.Success(data);
        }

        var errors = identityResult.Errors.Select(e => e.Description).ToArray();
        return Result<T>.BadRequest(string.Join(", ", errors));
    }

    public static Result ToResult(this IdentityResult identityResult)
    {
        if (identityResult.Succeeded)
        {
            return Result.Success();
        }

        var errors = identityResult.Errors.Select(e => e.Description).ToArray();
        return Result.BadRequest(string.Join(", ", errors));
    }

    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result.Data) { StatusCode = result.StatusCode };
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }

    public static ActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(result.StatusCode);
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }

    public static ActionResult<TNew> ToActionResult<T, TNew>(this Result<T> result, Func<T, TNew> mapper)
    {
        if (result.IsFailure)
        {
            var errorResponse = new ErrorResponse(result.Error!);
            return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
        }

        var mappedData = mapper(result.Data!);
        return new ObjectResult(mappedData) { StatusCode = result.StatusCode };
    }

    public static IActionResult ToApiResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result.Data) { StatusCode = result.StatusCode };
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }

    public static IActionResult ToApiResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(result.StatusCode);
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }
}

public class ErrorResponse
{
    public string Type { get; }

    public string Message { get; }

    public Dictionary<string, string[]>? ValidationErrors { get; }

    public ErrorResponse(Error error)
    {
        Type = error.Type.ToString();
        Message = error.Message;
        ValidationErrors = error.ValidationErrors;
    }

    public ErrorResponse(string message)
    {
        Type = "Error";
        Message = message;
        ValidationErrors = null;
    }
}
