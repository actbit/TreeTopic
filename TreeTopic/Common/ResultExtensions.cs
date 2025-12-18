using Microsoft.AspNetCore.Mvc;

namespace TreeTopic.Common;

public static class ResultExtensions
{
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
