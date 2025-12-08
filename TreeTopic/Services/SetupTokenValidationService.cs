using Microsoft.EntityFrameworkCore;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// SetupToken の検証サービス
/// テナント初期化時の一時認可トークンを管理
/// </summary>
public class SetupTokenValidationService
{
    private readonly TenantCatalogDbContext _tenantDb;
    private readonly ILogger<SetupTokenValidationService> _logger;

    public SetupTokenValidationService(
        TenantCatalogDbContext tenantDb,
        ILogger<SetupTokenValidationService> logger)
    {
        _tenantDb = tenantDb;
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
            // ハッシュ値が一致し、テナントに属し、有効期限内のトークンを検索
            var setupToken = await _tenantDb.SetupTokens
                .FirstOrDefaultAsync(st =>
                    st.TenantId == tenantId &&
                    st.TokenHash == tokenHash &&
                    st.IsValid);

            if (setupToken == null)
            {
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
}
