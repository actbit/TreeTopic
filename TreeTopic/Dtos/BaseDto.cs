namespace TreeTopic.Dtos;

public class BaseDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class BaseCreateRequest
{
    public Guid? TenantId { get; set; }
}

public class BaseUpdateRequest
{
    public Guid? Id { get; set; }

    public Guid? TenantId { get; set; }
}

public class BaseResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public object? Data { get; set; }

    public static BaseResponse SuccessResponse(string message = "Operation completed successfully", object? data = null)
    {
        return new BaseResponse
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static BaseResponse FailureResponse(string message)
    {
        return new BaseResponse
        {
            Success = false,
            Message = message,
            Data = null
        };
    }
}

public class BaseResponse<T>
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public T? Data { get; set; }

    public static BaseResponse<T> SuccessResponse(T data, string message = "Operation completed successfully")
    {
        return new BaseResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static BaseResponse<T> FailureResponse(string message)
    {
        return new BaseResponse<T>
        {
            Success = false,
            Message = message,
            Data = default
        };
    }
}
