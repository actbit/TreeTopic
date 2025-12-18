namespace TreeTopic.Common;

public class Result<T>
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Data { get; }

    public Error? Error { get; }

    public int StatusCode { get; private set; }

    private Result(bool isSuccess, T? data, Error? error, int statusCode = 200)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T data, int statusCode = 200)
        => new(true, data, null, statusCode);

    public static Result<T> Created(T data)
        => new(true, data, null, 201);

    public static Result<T> NoContent()
        => new(false, default, null, 204);

    public static Result<T> Failure(Error error, int statusCode = 400)
        => new(false, default, error, statusCode);

    public static Result<T> NotFound(string message = "Resource not found")
        => new(false, default, new Error(ErrorType.NotFound, message), 404);

    public static Result<T> BadRequest(string message)
        => new(false, default, new Error(ErrorType.Validation, message), 400);

    public static Result<T> Conflict(string message)
        => new(false, default, new Error(ErrorType.Conflict, message), 409);

    public static Result<T> InternalError(string message = "An error occurred")
        => new(false, default, new Error(ErrorType.Internal, message), 500);

    public static Result<T> Unauthorized(string message = "Unauthorized")
        => new(false, default, new Error(ErrorType.Unauthorized, message), 401);

    public static Result<T> Forbidden(string message = "Forbidden")
        => new(false, default, new Error(ErrorType.Forbidden, message), 403);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error!, StatusCode);

        return Result<TNew>.Success(mapper(Data!), StatusCode);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (IsFailure)
            return Result<TNew>.Failure(Error!, StatusCode);

        var mapped = await mapper(Data!);
        return Result<TNew>.Success(mapped, StatusCode);
    }
}

public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public int StatusCode { get; private set; }

    private Result(bool isSuccess, Error? error, int statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    public static Result Success(int statusCode = 200)
        => new(true, null, statusCode);

    public static Result NoContent()
        => new(true, null, 204);

    public static Result Failure(Error error, int statusCode = 400)
        => new(false, error, statusCode);

    public static Result NotFound(string message = "Resource not found")
        => new(false, new Error(ErrorType.NotFound, message), 404);

    public static Result BadRequest(string message)
        => new(false, new Error(ErrorType.Validation, message), 400);

    public static Result Conflict(string message)
        => new(false, new Error(ErrorType.Conflict, message), 409);

    public static Result InternalError(string message = "An error occurred")
        => new(false, new Error(ErrorType.Internal, message), 500);

    public static Result Unauthorized(string message = "Unauthorized")
        => new(false, new Error(ErrorType.Unauthorized, message), 401);

    public static Result Forbidden(string message = "Forbidden")
        => new(false, new Error(ErrorType.Forbidden, message), 403);
}

public record Error(
    ErrorType Type,
    string Message,
    Dictionary<string, string[]>? ValidationErrors = null);

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Internal
}
