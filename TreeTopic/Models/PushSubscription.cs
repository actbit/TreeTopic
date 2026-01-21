using Finbuckle.MultiTenant;

namespace TreeTopic.Models;

/// <summary>
/// プッシュ通知の購読情報（テナントDBに保存）
/// </summary>
[MultiTenant]
public class PushSubscription : BaseModel
{
    /// <summary>
    /// ユーザーID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// プッシュサービスのエンドポイントURL
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// P256DH公開鍵（Base64 URLエンコード）
    /// </summary>
    public string P256dhKey { get; set; } = string.Empty;

    /// <summary>
    /// 認証キー（Base64 URLエンコード）
    /// </summary>
    public string AuthKey { get; set; } = string.Empty;

    /// <summary>
    /// 作成日時
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最終使用日時
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
}
