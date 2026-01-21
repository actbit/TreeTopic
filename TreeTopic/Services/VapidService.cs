using System.Security.Cryptography;
using Finbuckle.MultiTenant;
using TreeTopic.Models;

namespace TreeTopic.Services;

public interface IVapidService
{
    /// <summary>
    /// 指定したテナントのVAPIDキーを取得または生成
    /// </summary>
    Task<(string PublicKey, string PrivateKey)> GetOrCreateKeysAsync(string tenantId);

    /// <summary>
    /// VAPIDキーペアを生成
    /// </summary>
    (string PublicKey, string PrivateKey) GenerateKeys();
}

public class VapidService : IVapidService
{
    private readonly TenantCatalogDbContext _tenantCatalogDb;
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<VapidService> _logger;

    public VapidService(
        TenantCatalogDbContext tenantCatalogDb,
        EncryptionService encryptionService,
        ILogger<VapidService> logger)
    {
        _tenantCatalogDb = tenantCatalogDb;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<(string PublicKey, string PrivateKey)> GetOrCreateKeysAsync(string tenantId)
    {
        // データベースからキーを取得
        var vapidKey = await _tenantCatalogDb.VapidKeys.FindAsync(tenantId);

        if (vapidKey != null)
        {
            // テナント詳細から暗号化キーを取得して復号
            var tenantDetail = await _tenantCatalogDb.TenantDetails.FindAsync(tenantId);
            if (tenantDetail == null || string.IsNullOrEmpty(tenantDetail.TenantEncryptionKey))
            {
                throw new InvalidOperationException($"Tenant {tenantId} has no encryption key configured");
            }

            // テナントキーを使用して復号（ConnectionStringと同じ方法）
            var publicKey = _encryptionService.DecryptWithTenantKey(tenantDetail.TenantEncryptionKey, vapidKey.EncryptedPublicKey);
            var privateKey = _encryptionService.DecryptWithTenantKey(tenantDetail.TenantEncryptionKey, vapidKey.EncryptedPrivateKey);

            _logger.LogInformation("VAPID keys loaded from database for tenant: {TenantId}", tenantId);
            return (publicKey, privateKey);
        }

        _logger.LogInformation("VAPID keys not found for tenant: {TenantId}, generating new keys...", tenantId);

        // キーが存在しない場合は生成して保存
        var (newPublicKey, newPrivateKey) = GenerateKeys();

        // テナント詳細から暗号化キーを取得
        var tenantDetailForEncryption = await _tenantCatalogDb.TenantDetails.FindAsync(tenantId);
        if (tenantDetailForEncryption == null || string.IsNullOrEmpty(tenantDetailForEncryption.TenantEncryptionKey))
        {
            throw new InvalidOperationException($"Tenant {tenantId} has no encryption key configured");
        }

        // テナント固有の暗号化サービスを作成
        var tenantEncryption = new EncryptionService(
            _encryptionService.Decrypt(tenantDetailForEncryption.TenantEncryptionKey),
            _logger);

        // テナントキーで暗号化して保存（ConnectionStringと同じ方法）
        var encryptedPublicKey = tenantEncryption.Encrypt(newPublicKey);
        var encryptedPrivateKey = tenantEncryption.Encrypt(newPrivateKey);

        var newVapidKey = new VapidKey
        {
            TenantId = tenantId,
            EncryptedPublicKey = encryptedPublicKey,
            EncryptedPrivateKey = encryptedPrivateKey,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _tenantCatalogDb.VapidKeys.Add(newVapidKey);
        await _tenantCatalogDb.SaveChangesAsync();

        _logger.LogInformation("VAPID keys generated and saved to database for tenant: {TenantId}", tenantId);

        return (newPublicKey, newPrivateKey);
    }

    public (string PublicKey, string PrivateKey) GenerateKeys()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(true);

        // 公開鍵をエンコード (X, Y座標)
        var x = parameters.Q.X!;
        var y = parameters.Q.Y!;
        var xBytes = ConvertToFixedByteArray(x, 32);
        var yBytes = ConvertToFixedByteArray(y, 32);

        // 65バイトの形式: 0x04 (1バイト) + X (32バイト) + Y (32バイト)
        var publicKeyBytes = new byte[65];
        publicKeyBytes[0] = 0x04;
        Buffer.BlockCopy(xBytes, 0, publicKeyBytes, 1, 32);
        Buffer.BlockCopy(yBytes, 0, publicKeyBytes, 33, 32);

        var publicKey = Base64UrlEncode(publicKeyBytes);

        // 秘密鍵をBase64 URLエンコード
        var dBytes = ConvertToFixedByteArray(parameters.D!, 32);
        var privateKey = Base64UrlEncode(dBytes);

        _logger.LogDebug("Generated new VAPID key pair");

        return (publicKey, privateKey);
    }

    private static byte[] ConvertToFixedByteArray(byte[] bytes, int length)
    {
        var result = new byte[length];
        var offset = Math.Max(0, bytes.Length - length);
        var copyLength = Math.Min(bytes.Length - offset, length);
        Buffer.BlockCopy(bytes, offset, result, length - copyLength, copyLength);
        return result;
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
