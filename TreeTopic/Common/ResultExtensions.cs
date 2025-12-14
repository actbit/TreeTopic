using Microsoft.AspNetCore.Mvc;

namespace TreeTopic.Common;

/// <summary>
/// Extension methods for Result<T> to convert to ASP.NET Core ActionResult
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts Result<T> to ActionResult<T> with automatic HTTP status code and error response formatting
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return new ObjectResult(result.Data) { StatusCode = result.StatusCode };
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }

    /// <summary>
    /// Converts Result to ActionResult with automatic HTTP status code and error response formatting
    /// </summary>
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new StatusCodeResult(result.StatusCode);
        }

        var errorResponse = new ErrorResponse(result.Error!);
        return new ObjectResult(errorResponse) { StatusCode = result.StatusCode };
    }

    /// <summary>
    /// Maps the data in a successful Result<T> to a new type and converts to ActionResult
    /// Useful for transforming entity results to DTOs before returning to client
    /// </summary>
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
}

/// <summary>
/// Standard error response format returned when an operation fails
/// Includes error type, message, and optional validation errors
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// The type of error that occurred (e.g., Validation, NotFound, Conflict)
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional validation errors organized by field name
    /// Only populated for Validation error types
    /// </summary>
    public Dictionary<string, string[]>? ValidationErrors { get; }

    /// <summary>
    /// Creates an ErrorResponse from an Error object
    /// </summary>
    public ErrorResponse(Error error)
    {
        Type = error.Type.ToString();
        Message = error.Message;
        ValidationErrors = error.ValidationErrors;
    }

    /// <summary>
    /// Creates an ErrorResponse with a custom message
    /// </summary>
    public ErrorResponse(string message)
    {
        Type = "Error";
        Message = message;
        ValidationErrors = null;
    }
}
