using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;
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
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(
        IMultiTenantContextAccessor<ApplicationTenantInfo> accessor,
        IServiceProvider serviceProvider,
        EncryptionService encryptionService,
        ILogger<MigrationService> logger)
    {
        _accessor = accessor;
        _serviceProvider = serviceProvider;
        _encryptionService = encryptionService;
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

        try
        {
            // テナント用の接続文字列を復号化
            var decryptedConnectionString = DecryptTenantConnectionString(tenant);

            // テナントのDB タイプに応じて、マイグレーション用 DbContext を選択
            var tenantDetail = tenant.Detail;
            if (tenantDetail?.DbProvider?.ToLower() == "mysql")
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContextMySQL>()
                    .UseMySql(decryptedConnectionString, ServerVersion.AutoDetect(decryptedConnectionString))
                    .Options;

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
                var options = new DbContextOptionsBuilder<ApplicationDbContextPostgreSQL>()
                    .UseNpgsql(decryptedConnectionString)
                    .Options;

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
    /// 暗号化された接続文字列を復号化してからマイグレーション実行
    /// </summary>
    public async Task MigrateTenantAsync(ApplicationTenantInfo tenant)
    {
        if (tenant == null)
        {
            throw new ArgumentNullException(nameof(tenant));
        }

        // テナント用の接続文字列を復号化
        var decryptedConnectionString = DecryptTenantConnectionString(tenant);

        // テナントのDB タイプに応じて、マイグレーション用 DbContext を選択
            var tenantDetail = tenant.Detail;
            if (tenantDetail?.DbProvider?.ToLower() == "mysql")
            {
            _logger.LogInformation("MySQL マイグレーション実行: テナント '{TenantName}'", tenant.Name);
            var options = new DbContextOptionsBuilder<ApplicationDbContextMySQL>()
                .UseMySql(decryptedConnectionString, ServerVersion.AutoDetect(decryptedConnectionString))
                .Options;

            using (var mysqlDb = new ApplicationDbContextMySQL(_accessor, options))
            {
                await mysqlDb.Database.MigrateAsync();
            }
        }
        else
        {
            _logger.LogInformation("PostgreSQL マイグレーション実行: テナント '{TenantName}'", tenant.Name);
            var options = new DbContextOptionsBuilder<ApplicationDbContextPostgreSQL>()
                .UseNpgsql(decryptedConnectionString)
                .Options;

            using (var pgDb = new ApplicationDbContextPostgreSQL(_accessor, options))
            {
                await pgDb.Database.MigrateAsync();
            }
        }
    }

    /// <summary>
    /// テナントの接続文字列を 2 段階で復号化
    /// </summary>
    private string DecryptTenantConnectionString(ApplicationTenantInfo tenant)
    {
        if (tenant.Detail == null || string.IsNullOrEmpty(tenant.Detail.TenantEncryptionKey))
            throw new InvalidOperationException($"Tenant '{tenant.Identifier}' has no encryption key.");

        if (string.IsNullOrEmpty(tenant.Detail.ConnectionString))
            throw new InvalidOperationException($"Tenant '{tenant.Identifier}' has no connection string.");

        return _encryptionService.DecryptWithTenantKey(
            tenant.Detail.TenantEncryptionKey,
            tenant.Detail.ConnectionString);
    }

    /// <summary>
    /// すべてのテナントをマイグレーション実行
    /// </summary>
    public async Task MigrateAllTenantsAsync(TenantCatalogDbContext tenantCatalogDb)
    {
        var tenants = await tenantCatalogDb.Tenants
            .Include(t => t.Detail)
            .ToListAsync();

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