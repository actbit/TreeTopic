using System.Security.Cryptography;
using System.Text.Json;

namespace TreeTopic.Services;

public interface IVapidService
{
    /// <summary>
    /// グローバルなVAPIDキーを取得または生成（全テナント共通）
    /// </summary>
    Task<(string PublicKey, string PrivateKey)> GetOrCreateKeysAsync();
}

public class VapidService : IVapidService
{
    private const string VapidKeysFilePath = "vapid-keys.json";
    private readonly ILogger<VapidService> _logger;
    private readonly EncryptionService _encryption;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public VapidService(ILogger<VapidService> logger, EncryptionService encryption)
    {
        _logger = logger;
        _encryption = encryption;
    }

    public async Task<(string PublicKey, string PrivateKey)> GetOrCreateKeysAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            // ファイルから読み込み
            if (File.Exists(VapidKeysFilePath))
            {
                var json = await File.ReadAllTextAsync(VapidKeysFilePath);
                var keys = JsonSerializer.Deserialize<VapidKeysData>(json);
                if (keys?.EncryptedPublicKey != null && keys?.EncryptedPrivateKey != null)
                {
                    var pubKey = _encryption.Decrypt(keys.EncryptedPublicKey);
                    var privKey = _encryption.Decrypt(keys.EncryptedPrivateKey);
                    _logger.LogInformation("VAPID keys loaded from file: {FilePath}", VapidKeysFilePath);
                    return (pubKey, privKey);
                }
            }

            // ファイルが存在しない場合は生成
            _logger.LogInformation("VAPID keys not found, generating new keys...");
            var (publicKey, privateKey) = GenerateKeys();

            var encryptedPublicKey = _encryption.Encrypt(publicKey);
            var encryptedPrivateKey = _encryption.Encrypt(privateKey);

            var keysData = new VapidKeysData { EncryptedPublicKey = encryptedPublicKey, EncryptedPrivateKey = encryptedPrivateKey };
            var jsonToWrite = JsonSerializer.Serialize(keysData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(VapidKeysFilePath, jsonToWrite);

            _logger.LogInformation("VAPID keys generated and saved to: {FilePath}", VapidKeysFilePath);

            return (publicKey, privateKey);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(true);

        var x = parameters.Q.X!;
        var y = parameters.Q.Y!;
        var xBytes = ConvertToFixedByteArray(x, 32);
        var yBytes = ConvertToFixedByteArray(y, 32);

        var publicKeyBytes = new byte[65];
        publicKeyBytes[0] = 0x04;
        Buffer.BlockCopy(xBytes, 0, publicKeyBytes, 1, 32);
        Buffer.BlockCopy(yBytes, 0, publicKeyBytes, 33, 32);

        var publicKey = Base64UrlEncode(publicKeyBytes);

        var dBytes = ConvertToFixedByteArray(parameters.D!, 32);
        var privateKey = Base64UrlEncode(dBytes);

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

    private class VapidKeysData
    {
        public string EncryptedPublicKey { get; set; } = string.Empty;
        public string EncryptedPrivateKey { get; set; } = string.Empty;
    }
}
