using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace TreeTopic.Services;

/// <summary>
/// テナントの定期クリーンアップを行うバックグラウンドサービス
/// </summary>
public class TenantCleanupBackgroundService : BackgroundService
{
    private readonly ILogger<TenantCleanupBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TenantCleanupBackgroundService(
        ILogger<TenantCleanupBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tenant cleanup background service started");

        try
        {
            // 起動直後即時実行
            await CleanupExpiredTenantsAsync();

            // その後1時間ごとに実行
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                await CleanupExpiredTenantsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tenant cleanup service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in tenant cleanup background service");
        }
    }

    private async Task CleanupExpiredTenantsAsync()
    {
        try
        {
            _logger.LogInformation("Starting cleanup of expired empty tenants");

            // スコープを作成してscopedサービスを解決
            using (var scope = _serviceProvider.CreateScope())
            {
                var setupTokenValidator = scope.ServiceProvider.GetRequiredService<SetupTokenValidationService>();
                var cleanedCount = await setupTokenValidator.CleanupExpiredEmptyTenantsAsync();

                if (cleanedCount > 0)
                {
                    _logger.LogInformation(
                        "Completed cleanup: removed {Count} expired empty tenant(s)",
                        cleanedCount);
                }
                else
                {
                    _logger.LogDebug("No expired empty tenants found for cleanup");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tenant cleanup");
        }
    }
}