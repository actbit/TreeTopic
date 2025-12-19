using TreeTopic.Common;

namespace TreeTopic.Services;

public abstract class BaseService
{
    protected readonly ILogger<BaseService> Logger;

    protected BaseService(ILogger<BaseService> logger)
    {
        Logger = logger;
    }

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