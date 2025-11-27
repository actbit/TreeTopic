using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TreeTopic.Data;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// マルチテナント対応のマイグレーション実行サービス
/// テナント情報から DB タイプを認識して、専用の DbContext でマイグレーション実行
/// </summary>
public class MigrationService
{
    private readonly IMultiTenantContextAccessor<ApplicationTenantInfo> _accessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(
        IMultiTenantContextAccessor<ApplicationTenantInfo> accessor,
        IServiceProvider serviceProvider,
        ILogger<MigrationService> logger)
    {
        _accessor = accessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 現在のテナント情報に基づいて、マイグレーション実行
    /// PostgreSQL / MySQL を自動判定
    /// </summary>
    public async Task MigrateCurrentTenantAsync()
    {
        var tenantInfo = _accessor.MultiTenantContext?.TenantInfo;
        if (tenantInfo == null)
        {
            throw new InvalidOperationException("Tenant information not found in context.");
        }

        await MigrateTenantAsync(tenantInfo);
    }

    /// <summary>
    /// テナントがマイグレーション対象かどうかを判定
    /// 定義されているマイグレーションが実行済みマイグレーションより多い場合、true を返す
    /// </summary>
    public async Task<bool> NeedsMigrationAsync(ApplicationTenantInfo tenant)
    {
        if (tenant == null)
        {
            throw new ArgumentNullException(nameof(tenant));
        }

        // テナント用 DbContext のオプションを取得
        var baseDbContext = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var options = baseDbContext.GetType().GetProperty("ContextOptions")?.GetValue(baseDbContext)
            as DbContextOptions<ApplicationDbContext>
            ?? throw new InvalidOperationException("Cannot retrieve DbContextOptions");

        try
        {
            // テナントのDB タイプに応じて、マイグレーション用 DbContext を選択
            if (tenant.DbProvider?.ToLower() == "mysql")
            {
                using (var mysqlDb = new ApplicationDbContextMySQL(_accessor, options))
                {
                    var allMigrations = mysqlDb.Database.GetMigrations();
                    var appliedMigrations = await mysqlDb.Database.GetAppliedMigrationsAsync();
                    var unapplied = allMigrations.Except(appliedMigrations);
                    return unapplied.Any();
                }
            }
            else
            {
                using (var pgDb = new ApplicationDbContextPostgreSQL(_accessor, options))
                {
                    var allMigrations = pgDb.Database.GetMigrations();
                    var appliedMigrations = await pgDb.Database.GetAppliedMigrationsAsync();
                    var unapplied = allMigrations.Except(appliedMigrations);
                    return unapplied.Any();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "テナント '{TenantName}' のマイグレーション判定エラー", tenant.Name);
            return false;
        }
    }

    /// <summary>
    /// 指定されたテナント情報に基づいて、マイグレーション実行
    /// </summary>
    public async Task MigrateTenantAsync(ApplicationTenantInfo tenant)
    {
        if (tenant == null)
        {
            throw new ArgumentNullException(nameof(tenant));
        }

        // テナント用 DbContext のオプションを取得
        var baseDbContext = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        var options = baseDbContext.GetType().GetProperty("ContextOptions")?.GetValue(baseDbContext)
            as DbContextOptions<ApplicationDbContext>
            ?? throw new InvalidOperationException("Cannot retrieve DbContextOptions");

        // テナントのDB タイプに応じて、マイグレーション用 DbContext を選択
        if (tenant.DbProvider?.ToLower() == "mysql")
        {
            _logger.LogInformation("MySQL マイグレーション実行: テナント '{TenantName}'", tenant.Name);
            using (var mysqlDb = new ApplicationDbContextMySQL(_accessor, options))
            {
                await mysqlDb.Database.MigrateAsync();
            }
        }
        else
        {
            _logger.LogInformation("PostgreSQL マイグレーション実行: テナント '{TenantName}'", tenant.Name);
            using (var pgDb = new ApplicationDbContextPostgreSQL(_accessor, options))
            {
                await pgDb.Database.MigrateAsync();
            }
        }
    }

    /// <summary>
    /// すべてのテナントをマイグレーション実行
    /// </summary>
    public async Task MigrateAllTenantsAsync(TenantCatalogDbContext tenantCatalogDb)
    {
        var tenants = await tenantCatalogDb.Tenants.ToListAsync();

        foreach (var tenant in tenants)
        {
            try
            {
                await MigrateTenantAsync(tenant);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "テナント '{TenantName}' のマイグレーション失敗", tenant.Name);
                throw;
            }
        }

        _logger.LogInformation("✅ {TenantCount} 件のテナントマイグレーション完了", tenants.Count);
    }
}
