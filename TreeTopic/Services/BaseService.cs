using TreeTopic.Common;

namespace TreeTopic.Services;

/// <summary>
/// Base class for all application services
/// Provides common functionality for error handling, logging, and operation execution
/// Eliminates code duplication across service implementations
/// </summary>
public abstract class BaseService
{
    /// <summary>
    /// Logger instance for service operations
    /// Can be accessed by derived classes for logging
    /// </summary>
    protected readonly ILogger<BaseService> Logger;

    /// <summary>
    /// Constructor for base service
    /// </summary>
    /// <param name="logger">Logger instance injected by dependency injection</param>
    protected BaseService(ILogger<BaseService> logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Executes an async operation with automatic error handling and logging
    /// Returns a Result<T> with either success data or failure information
    /// </summary>
    /// <typeparam name="T">The type of data to return on success</typeparam>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="operationName">The name of the operation for logging purposes</param>
    /// <returns>Result<T> containing either success data or error information</returns>
    protected async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug("Starting operation: {OperationName}", operationName);
            var result = await operation();

            if (result.IsSuccess)
            {
                Logger.LogDebug("Operation {OperationName} completed successfully", operationName);
            }
            else
            {
                Logger.LogWarning("Operation {OperationName} failed: {Error}", operationName, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during operation: {OperationName}", operationName);
            return Result<T>.InternalError($"An error occurred while performing {operationName}");
        }
    }

    /// <summary>
    /// Executes an async operation without return data with automatic error handling and logging
    /// Returns a Result indicating success or failure
    /// </summary>
    /// <param name="operation">The async operation to execute</param>
    /// <param name="operationName">The name of the operation for logging purposes</param>
    /// <returns>Result indicating success or failure of the operation</returns>
    protected async Task<Result> ExecuteAsync(
        Func<Task<Result>> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug("Starting operation: {OperationName}", operationName);
            var result = await operation();

            if (result.IsSuccess)
            {
                Logger.LogDebug("Operation {OperationName} completed successfully", operationName);
            }
            else
            {
                Logger.LogWarning("Operation {OperationName} failed: {Error}", operationName, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during operation: {OperationName}", operationName);
            return Result.InternalError($"An error occurred while performing {operationName}");
        }
    }

    /// <summary>
    /// Executes a synchronous operation with automatic error handling and logging
    /// Returns a Result<T> with either success data or failure information
    /// </summary>
    /// <typeparam name="T">The type of data to return on success</typeparam>
    /// <param name="operation">The synchronous operation to execute</param>
    /// <param name="operationName">The name of the operation for logging purposes</param>
    /// <returns>Result<T> containing either success data or error information</returns>
    protected Result<T> Execute<T>(
        Func<Result<T>> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug("Starting operation: {OperationName}", operationName);
            var result = operation();

            if (result.IsSuccess)
            {
                Logger.LogDebug("Operation {OperationName} completed successfully", operationName);
            }
            else
            {
                Logger.LogWarning("Operation {OperationName} failed: {Error}", operationName, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during operation: {OperationName}", operationName);
            return Result<T>.InternalError($"An error occurred while performing {operationName}");
        }
    }

    /// <summary>
    /// Executes a synchronous operation without return data with automatic error handling and logging
    /// Returns a Result indicating success or failure
    /// </summary>
    /// <param name="operation">The synchronous operation to execute</param>
    /// <param name="operationName">The name of the operation for logging purposes</param>
    /// <returns>Result indicating success or failure of the operation</returns>
    protected Result Execute(
        Func<Result> operation,
        string operationName)
    {
        try
        {
            Logger.LogDebug("Starting operation: {OperationName}", operationName);
            var result = operation();

            if (result.IsSuccess)
            {
                Logger.LogDebug("Operation {OperationName} completed successfully", operationName);
            }
            else
            {
                Logger.LogWarning("Operation {OperationName} failed: {Error}", operationName, result.Error?.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during operation: {OperationName}", operationName);
            return Result.InternalError($"An error occurred while performing {operationName}");
        }
    }
}
