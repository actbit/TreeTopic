namespace TreeTopic.Dtos;

/// <summary>
/// ルーム権限レスポンス
/// </summary>
public class RoomPermissionsResponse
{
    public List<string> Permissions { get; set; } = new();
}

/// <summary>
/// トピック権限レスポンス
/// </summary>
public class TopicPermissionsResponse
{
    public List<string> Permissions { get; set; } = new();
}
