namespace TreeTopic.Dtos;

/// <summary>
/// ログアウトリクエスト
/// </summary>
public record LogoutRequest(string? returnUrl = null);
