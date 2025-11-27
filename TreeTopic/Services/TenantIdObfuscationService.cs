using UUIDv47Sharp;

namespace TreeTopic.Services;

/// <summary>
/// TenantIdを外部公開時に変換（obfuscate）するサービス
/// UUIDv47を使用してTenantIdを隠蔽し、タイムスタンプ情報を保護
/// </summary>
public class TenantIdObfuscationService
{
    private readonly ILogger<TenantIdObfuscationService> _logger;

    public TenantIdObfuscationService(ILogger<TenantIdObfuscationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// TenantIdをUUIDv47でencode（外部公開用）
    /// </summary>
    public string ObfuscateTenantId(string tenantId, ulong keyK0, ulong keyK1)
    {
        try
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return tenantId;
            }

            if (!Guid.TryParse(tenantId, out var guid))
            {
                _logger.LogWarning("Invalid GUID format for tenantId: {TenantId}", tenantId);
                return tenantId;
            }

            var key = new Key(keyK0, keyK1);
            var uuid = guid.ToUuid();
            var obfuscated = Uuid47Codec.Encode(uuid, key);
            return obfuscated.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obfuscating tenantId: {TenantId}", tenantId);
            return tenantId;
        }
    }

    /// <summary>
    /// ObfuscateされたTenantIdを元に戻す（内部処理用）
    /// </summary>
    public string DeobfuscateTenantId(string obfuscatedId, ulong keyK0, ulong keyK1)
    {
        try
        {
            if (string.IsNullOrEmpty(obfuscatedId))
            {
                return obfuscatedId;
            }

            var key = new Key(keyK0, keyK1);
            var facade = Uuid.Parse(obfuscatedId);
            var original = Uuid47Codec.Decode(facade, key);
            return original.ToGuid().ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deobfuscating tenantId: {ObfuscatedId}", obfuscatedId);
            throw;
        }
    }

    /// <summary>
    /// テナント用の新しい暗号化キーを生成
    /// </summary>
    public (ulong k0, ulong k1) GenerateNewKey()
    {
        var key = Key.NewRandom();
        return (key.K0, key.K1);
    }
}
