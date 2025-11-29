namespace TreeTopic.Models;

/// <summary>
/// テナント初期設定用の一時トークン
/// 一回限りの使用で、有効期限付き（1時間）
/// トークンは SHA-256 ハッシュ化して保存
/// </summary>
public class SetupToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// テナントID
    /// </summary>
    public string TenantId { get; set; } = null!;

    /// <summary>
    /// トークンの SHA-256 ハッシュ値（Base64エンコード）
    /// </summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>
    /// トークン作成時刻
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// トークン有効期限（デフォルト1時間）
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// リレーション：所属するテナント
    /// </summary>
    public virtual ApplicationTenantInfo? Tenant { get; set; }

    /// <summary>
    /// トークンが有効かどうかをチェック
    /// </summary>
    public bool IsValid => DateTime.UtcNow <= ExpiresAt;

    /// <summary>
    /// トークンのハッシュ値を生成（SHA-256）
    /// </summary>
    public static string HashToken(string token)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    /// <summary>
    /// トークンを生成（64バイト = Base64で約88文字）
    /// </summary>
    public static string GenerateToken()
    {
        var tokenBytes = new byte[64];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes);
    }
}
