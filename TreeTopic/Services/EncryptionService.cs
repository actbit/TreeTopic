using System.Security.Cryptography;
using System.Text;

namespace TreeTopic.Services;

/// <summary>
/// AES-256-GCMを使用して機密データを暗号化・復号化するサービス
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;
    private readonly ILogger _logger;

    // テナント用キー暗号化・復号用（キーを直接指定）
    public EncryptionService(string keyString, ILogger logger)
    {
        _logger = logger;

        try
        {
            _key = Convert.FromBase64String(keyString);

            if (_key.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits). Current length: {_key.Length} bytes.");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption key must be base64-encoded.", ex);
        }
    }

    public EncryptionService(IConfiguration configuration, ILogger<EncryptionService> logger, IWebHostEnvironment env)
    {
        _logger = logger;

        // 環境変数から暗号化キーを取得、なければappsettingsから
        var keyString = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
            ?? configuration["Encryption:Key"];

        if (string.IsNullOrEmpty(keyString))
        {
            if (env.IsDevelopment())
            {
                // 開発環境では便宜上キーを自動生成
                var generatedKey = GenerateNewKey();
                _logger.LogWarning(
                    "╔════════════════════════════════════════════════════════════════╗\n" +
                    "║ No encryption key configured. Generated temporary key:        ║\n" +
                    "║ {Key}\n" +
                    "║                                                                ║\n" +
                    "║ For persistent use, set one of:                               ║\n" +
                    "║ 1. Environment variable: ENCRYPTION_KEY                       ║\n" +
                    "║ 2. appsettings.json: \"Encryption\": {{ \"Key\": \"...\" }}      ║\n" +
                    "╚════════════════════════════════════════════════════════════════╝",
                    generatedKey);

                keyString = generatedKey;
            }
            else
            {
                throw new InvalidOperationException(
                    "Encryption key not configured in production. " +
                    "Set ENCRYPTION_KEY environment variable or Encryption:Key in appsettings.json. " +
                    "Generate a key with: dotnet run --project . -- --generate-key");
            }
        }

        try
        {
            _key = Convert.FromBase64String(keyString);

            // AES-256は32バイト必要
            if (_key.Length != 32)
            {
                throw new InvalidOperationException(
                    $"Encryption key must be 32 bytes (256 bits). Current length: {_key.Length} bytes. " +
                    $"Generate a key with: EncryptionService.GenerateNewKey()");
            }
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption key must be base64-encoded. Generate with: " +
                "EncryptionService.GenerateNewKey()", ex);
        }
    }

    /// <summary>
    /// AES-256-GCMで平文を暗号化
    /// 戻り値: base64エンコードされた "nonce:ciphertext:tag"
    /// </summary>
    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        try
        {
            const int tagSizeInBytes = 16; // 128-bit authentication tag
            using (var aes = new AesGcm(_key, tagSizeInBytes))
            {
                // 96ビットのランダムnonceを生成（GCM推奨の12バイト）
                var nonce = new byte[12];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[tagSizeInBytes];

                // 暗号化と認証
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                // 保存用にnonce:ciphertext:tagをbase64形式で結合
                var nonceBase64 = Convert.ToBase64String(nonce);
                var ciphertextBase64 = Convert.ToBase64String(ciphertext);
                var tagBase64 = Convert.ToBase64String(tag);

                return $"{nonceBase64}:{ciphertextBase64}:{tagBase64}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting data");
            throw;
        }
    }

    /// <summary>
    /// マスターキーで暗号化されたテナントキーを使用してデータを復号化
    /// </summary>
    public string DecryptWithTenantKey(string encryptedTenantKey, string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedTenantKey))
            throw new ArgumentException("Encrypted tenant key cannot be null or empty", nameof(encryptedTenantKey));

        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        var decryptedTenantKey = Decrypt(encryptedTenantKey);
        return DecryptWithPlainTenantKey(decryptedTenantKey, encryptedData);
    }

    /// <summary>
    /// 既に復号化されたテナントキー（base64エンコード32バイトキー）を使用してデータを復号化
    /// </summary>
    public string DecryptWithPlainTenantKey(string tenantKeyString, string encryptedData)
    {
        if (string.IsNullOrEmpty(tenantKeyString))
            throw new ArgumentException("Tenant key cannot be null or empty", nameof(tenantKeyString));

        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        var tenantEncryption = new EncryptionService(tenantKeyString, _logger);
        return tenantEncryption.Decrypt(encryptedData);
    }

    /// <summary>
    /// Encryptメソッドで暗号化されたデータを復号化
    /// 期待フォーマット: base64エンコードされた "nonce:ciphertext:tag"
    /// </summary>
    public string Decrypt(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        try
        {
            // 暗号化データをパース: nonce:ciphertext:tag
            var parts = encryptedData.Split(':');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException(
                    "Invalid encrypted data format. Expected 'nonce:ciphertext:tag'");
            }

            var nonce = Convert.FromBase64String(parts[0]);
            var ciphertext = Convert.FromBase64String(parts[1]);
            var tag = Convert.FromBase64String(parts[2]);

            const int tagSizeInBytes = 16; // 128-bit authentication tag
            using (var aes = new AesGcm(_key, tagSizeInBytes))
            {
                var plaintext = new byte[ciphertext.Length];

                // 復号化と認証タグ検証
                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                return Encoding.UTF8.GetString(plaintext);
            }
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Decryption failed - authentication tag verification failed or invalid key");
            throw new InvalidOperationException("Decryption failed - data may be corrupted or encrypted with different key", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting data");
            throw;
        }
    }

    /// <summary>
    /// 新しい暗号化キーを生成（参照・セットアップ用）
    /// 戻り値: base64エンコードされた256ビットキー
    /// </summary>
    public static string GenerateNewKey()
    {
        var key = new byte[32]; // 256ビット
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return Convert.ToBase64String(key);
    }
}
