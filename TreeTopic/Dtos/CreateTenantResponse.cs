using TreeTopic.Models;

namespace TreeTopic.Dtos;

/// <summary>
/// テナント作成レスポンス
/// </summary>
public class CreateTenantResponse
{
    /// <summary>
    /// 作成されたテナント情報
    /// </summary>
    public required ApplicationTenantInfo Tenant { get; set; }

    /// <summary>
    /// 初期設定用トークン（1時間有効、一回限り）
    /// </summary>
    public required string SetupToken { get; set; }
}
