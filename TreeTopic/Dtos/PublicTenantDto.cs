namespace TreeTopic.Dtos;

/// <summary>
/// パブリックテナント情報（機密情報を含まない）
/// </summary>
public class PublicTenantDto
{
    /// <summary>
    /// テナント識別子
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// テナント表示名
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// テナント登録レスポンス
/// </summary>
public class RegisterTenantResponse
{
    /// <summary>
    /// テナント識別子
    /// </summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>
    /// テナント表示名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// セットアップトークン
    /// </summary>
    public string? SetupToken { get; set; }
}
