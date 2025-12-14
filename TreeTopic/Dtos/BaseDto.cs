namespace TreeTopic.Dtos;

/// <summary>
/// Base class for all Data Transfer Objects
/// Provides common properties for data entity representation
/// </summary>
public class BaseDto
{
    /// <summary>
    /// Unique identifier of the entity
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tenant ID that this entity belongs to
    /// Used in multi-tenant environments
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Timestamp when the entity was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the entity was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Base class for create/post request DTOs
/// Contains common validation attributes for creation operations
/// </summary>
public class BaseCreateRequest
{
    /// <summary>
    /// Optional tenant ID override for the creation
    /// If not provided, the current tenant from context is used
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Base class for update/put request DTOs
/// Contains common properties for update operations
/// </summary>
public class BaseUpdateRequest
{
    /// <summary>
    /// The ID of the entity to update
    /// Usually provided as a route parameter, but can be included in request body
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Optional tenant ID override for the update
    /// If not provided, the current tenant from context is used
    /// </summary>
    public Guid? TenantId { get; set; }
}

/// <summary>
/// Standard response format for API operations
/// Used for success/failure responses with optional data
/// </summary>
public class BaseResponse
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message about the operation result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional data payload returned by the operation
    /// Only populated on successful operations
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Creates a successful response
    /// </summary>
    public static BaseResponse SuccessResponse(string message = "Operation completed successfully", object? data = null)
    {
        return new BaseResponse
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failure response
    /// </summary>
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

/// <summary>
/// Generic response format with typed data payload
/// </summary>
/// <typeparam name="T">The type of data to return</typeparam>
public class BaseResponse<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message about the operation result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Typed data payload returned by the operation
    /// Only populated on successful operations
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Creates a successful response with typed data
    /// </summary>
    public static BaseResponse<T> SuccessResponse(T data, string message = "Operation completed successfully")
    {
        return new BaseResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failure response
    /// </summary>
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
