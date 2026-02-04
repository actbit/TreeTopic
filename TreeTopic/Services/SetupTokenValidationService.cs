using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;
using TreeTopic.Data;

namespace TreeTopic.Services;

/// <summary>
/// SetupToken の検証サービス
/// テナント初期化時の一時認可トークンを管理
/// </summary>
public class SetupTokenValidationService
{
    private readonly TenantCatalogDbContext _tenantDb;
    private readonly ApplicationDbContext _applicationDbContext;
    private readonly ILogger<SetupTokenValidationService> _logger;

    public SetupTokenValidationService(
        TenantCatalogDbContext tenantDb,
        ApplicationDbContext applicationDbContext,
        ILogger<SetupTokenValidationService> logger)
    {
        _tenantDb = tenantDb;
        _applicationDbContext = applicationDbContext;
        _logger = logger;
    }

    /// <summary>
    /// SetupToken を検証
    /// トークンが有効でかつ指定のテナントに属していることを確認
    /// </summary>
    /// <param name="tenantId">テナントID</param>
    /// <param name="token">SetupToken（平文）</param>
    /// <returns>トークンが有効な場合 true、無効な場合 false</returns>
    public async Task<bool> ValidateSetupTokenAsync(string tenantId, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("SetupToken validation failed: token is empty for tenant {TenantId}", tenantId);
            return false;
        }

        // トークンをハッシング
        var tokenHash = SetupToken.HashToken(token);

        try
        {
            var utcNow = DateTime.UtcNow;

            // ハッシュ値が一致し、テナントに属し、有効期限内のトークンを検索
            // IsValid プロパティは計算プロパティなので、直接 ExpiresAt を比較
            var setupToken = await _tenantDb.SetupTokens
                .FirstOrDefaultAsync(st =>
                    st.TenantId == tenantId &&
                    st.TokenHash == tokenHash &&
                    st.ExpiresAt > utcNow);

            if (setupToken == null)
            {
                // トークンが存在しない場合、有効期限切れかつユーザーのいないテナントをクリーンアップ
                await CleanupExpiredEmptyTenantAsync(tenantId);

                _logger.LogWarning(
                    "SetupToken validation failed: token not found or expired for tenant {TenantId}",
                    tenantId);
                return false;
            }

            _logger.LogInformation("SetupToken validated successfully for tenant {TenantId}", tenantId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SetupToken for tenant {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// SetupToken を無効化（使用済みとしてマーク）
    /// </summary>
    /// <param name="tenantId">テナントID</param>
    /// <param name="token">SetupToken（平文）</param>
    /// <returns>無効化に成功した場合 true</returns>
    public async Task<bool> InvalidateSetupTokenAsync(string tenantId, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var tokenHash = SetupToken.HashToken(token);

        try
        {
            var setupToken = await _tenantDb.SetupTokens
                .FirstOrDefaultAsync(st =>
                    st.TenantId == tenantId &&
                    st.TokenHash == tokenHash);

            if (setupToken != null)
            {
                // 有効期限を過去に設定して無効化
                setupToken.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
                await _tenantDb.SaveChangesAsync();
                _logger.LogInformation("SetupToken invalidated for tenant {TenantId}", tenantId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating SetupToken for tenant {TenantId}", tenantId);
            return false;
        }
    }

    /// <summary>
    /// ユーザーがゼロでSetupTokenの期限が1日以上2日未満前のテナントを削除
    /// </summary>
    /// <returns>削除されたテナントの数</returns>
    public async Task<int> CleanupExpiredEmptyTenantsAsync()
    {
        try
        {
            var expiredTenantIds = new List<string>();

            // ユーザーがゼロでSetupTokenの期限が1日以上2日未満前のテナントを検索
            var expiredTenants = await _tenantDb.SetupTokens
                .Where(st =>
                    // 有効期限が1日以上2日未満前（つまり1〜2日前に切れた）
                    st.ExpiresAt < DateTime.UtcNow &&
                    st.ExpiresAt >= DateTime.UtcNow.AddDays(-2) &&
                    st.ExpiresAt < DateTime.UtcNow.AddDays(-1) &&
                    // まだ削除処理されていない（テナントが存在する）
                    st.Tenant != null)
                .Select(st => new { st.TenantId, st.ExpiresAt })
                .ToListAsync();

            foreach (var expiredTenant in expiredTenants)
            {
                // テナントにユーザーが存在するか確認
                var hasUsers = await _applicationDbContext.Users
                    .AnyAsync(u => u.TenantId == expiredTenant.TenantId);

                if (!hasUsers)
                {
                    expiredTenantIds.Add(expiredTenant.TenantId);
                }
            }

            if (expiredTenantIds.Count > 0)
            {
                // SetupTokenを削除
                await _tenantDb.SetupTokens
                    .Where(st => expiredTenantIds.Contains(st.TenantId))
                    .ExecuteDeleteAsync();

                // テナント情報を削除
                await _tenantDb.Tenants
                    .Where(t => expiredTenantIds.Contains(t.Id))
                    .ExecuteDeleteAsync();

                _logger.LogInformation("Cleaned up {Count} tenants with expired setup tokens (1-2 days old)", expiredTenantIds.Count);

                // 対象テナントのDBファイル削除はバックグラウンドタスクで実行される想定
            }

            return expiredTenantIds.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired empty tenants");
            return 0;
        }
    }

    /// <summary>
    /// 特定の有効期限切れのテナントを削除（ユーザーがいない場合）
    /// </summary>
    private async Task CleanupExpiredEmptyTenantAsync(string tenantId)
    {
        try
        {
            // ユーザーがゼロでSetupTokenの期限が1日以上2日未満前の指定テナントを検索
            var expiredToken = await _tenantDb.SetupTokens
                .FirstOrDefaultAsync(st =>
                    st.TenantId == tenantId &&
                    // 有効期限が1日以上2日未満前
                    st.ExpiresAt < DateTime.UtcNow &&
                    st.ExpiresAt >= DateTime.UtcNow.AddDays(-2) &&
                    st.ExpiresAt < DateTime.UtcNow.AddDays(-1) &&
                    // まだ削除処理されていない（テナントが存在する）
                    st.Tenant != null);

            if (expiredToken != null)
            {
                // テナントにユーザーが存在するか確認
                var hasUsers = await _applicationDbContext.Users
                    .AnyAsync(u => u.TenantId == expiredToken.TenantId.ToString());

                if (!hasUsers)
                {
                    // SetupTokenを削除
                    await _tenantDb.SetupTokens
                        .Where(st => st.Id == expiredToken.Id)
                        .ExecuteDeleteAsync();

                    // ナビゲーションプロパティを含めてテナントを取得
                    var tenant = await _tenantDb.Tenants
                        .Include(t => t.Detail)
                        .FirstOrDefaultAsync(t => t.Id == expiredToken.TenantId);

                    if (tenant != null)
                    {
                        // テナント情報を削除
                        _tenantDb.Tenants.Remove(tenant);
                        await _tenantDb.SaveChangesAsync();

                        _logger.LogInformation(
                            "Removed expired empty tenant {TenantIdentifier} (ID: {TenantId})",
                            tenant.Identifier,
                            tenantId);

                        // 対象テナントのDBファイル削除はバックグラウンドタスクで実行される想定
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired empty tenant {TenantId}", tenantId);
        }
    }
}
