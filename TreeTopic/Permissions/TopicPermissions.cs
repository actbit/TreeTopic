namespace TreeTopic.Permissions;

/// <summary>
/// Topicレベルの権限
/// TopicUserPermission/TopicRolePermissionに関連する権限名
/// </summary>
public static class TopicPermissions
{
    /// <summary>トピックの読み取り</summary>
    public const string Read = "topic.read";

    /// <summary>トピックへの書き込み（サブトピック作成）</summary>
    public const string Write = "topic.write";

    /// <summary>トピックの削除</summary>
    public const string Delete = "topic.delete";

    /// <summary>トピックの管理（権限設定など）</summary>
    public const string Manage = "topic.manage";

    /// <summary>トピックのメッセージ読み取り</summary>
    public const string ReadMessages = "topic.readMessages";

    /// <summary>トピックへのメッセージ投稿</summary>
    public const string WriteMessages = "topic.writeMessages";
}
