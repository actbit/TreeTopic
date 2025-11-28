using System.Security.Cryptography;
using System.Text;

namespace TreeTopic.Services;

/// <summary>
/// Service for encrypting and decrypting sensitive data using AES-256-GCM
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

        // Try to get encryption key from environment variable first, then appsettings
        var keyString = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
            ?? configuration["Encryption:Key"];

        if (string.IsNullOrEmpty(keyString))
        {
            if (env.IsDevelopment())
            {
                // Auto-generate key in development for convenience
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

            // AES-256 requires 32 bytes
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
    /// Encrypts plaintext using AES-256-GCM
    /// Returns: base64-encoded "nonce:ciphertext:tag"
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
                // Generate random 96-bit nonce (12 bytes recommended for GCM)
                var nonce = new byte[12];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }

                var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                var ciphertext = new byte[plaintextBytes.Length];
                var tag = new byte[tagSizeInBytes];

                // Encrypt and authenticate
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

                // Combine nonce:ciphertext:tag as base64 for storage
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
    /// Decrypts ciphertext encrypted with Encrypt method
    /// Expected format: base64-encoded "nonce:ciphertext:tag"
    /// </summary>
    public string Decrypt(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        try
        {
            // Parse the encrypted data: nonce:ciphertext:tag
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

                // Decrypt and verify authentication tag
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
    /// Generates a new encryption key (for reference/setup purposes)
    /// Returns base64-encoded 256-bit key
    /// </summary>
    public static string GenerateNewKey()
    {
        var key = new byte[32]; // 256 bits
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return Convert.ToBase64String(key);
    }
}
