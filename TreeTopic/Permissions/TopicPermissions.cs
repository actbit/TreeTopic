namespace TreeTopic.Permissions;

/// <summary>
/// Topicレベルの権限
/// TopicUserPermission/TopicRolePermissionに関連する権限名
/// </summary>
public static class TopicPermissions
{
    /// <summary>トピックの読み取り</summary>
    public static readonly PermissionRequirement Read = new(PermissionScope.Topic, "topic.read");

    /// <summary>トピックへの書き込み（サブトピック作成）</summary>
    public static readonly PermissionRequirement Write = new(PermissionScope.Topic, "topic.write");

    /// <summary>トピックの削除</summary>
    public static readonly PermissionRequirement Delete = new(PermissionScope.Topic, "topic.delete");

    /// <summary>トピックの管理（権限設定など）</summary>
    public static readonly PermissionRequirement Manage = new(PermissionScope.Topic, "topic.manage");

    /// <summary>トピックのメッセージ読み取り</summary>
    public static readonly PermissionRequirement ReadMessages = new(PermissionScope.Topic, "topic.readMessages");

    /// <summary>トピックへのメッセージ投稿</summary>
    public static readonly PermissionRequirement WriteMessages = new(PermissionScope.Topic, "topic.writeMessages");
}

