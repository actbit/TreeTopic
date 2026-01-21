namespace TreeTopic.Models;

/// <summary>
/// テナントごとのVAPIDキー
/// </summary>
public class VapidKey
{
    /// <summary>
    /// テナントID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// 暗号化された公開鍵
    /// </summary>
    public required string EncryptedPublicKey { get; set; }

    /// <summary>
    /// 暗号化された秘密鍵
    /// </summary>
    public required string EncryptedPrivateKey { get; set; }

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新日時
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
