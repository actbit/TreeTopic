namespace TreeTopic.Permissions;

/// <summary>
/// Topicレベルの権限
/// TopicUserPermission/TopicRolePermissionに関連する権限名
/// </summary>
public static class TopicPermissions
{
    /// <summary>トピックの読み取り</summary>
    public const string Read = "topic.read";
    public static readonly PermissionRequirement ReadReq = new(PermissionScope.Topic, Read);

    /// <summary>トピックへの書き込み（サブトピック作成）</summary>
    public const string Write = "topic.write";
    public static readonly PermissionRequirement WriteReq = new(PermissionScope.Topic, Write);

    /// <summary>トピックの削除</summary>
    public const string Delete = "topic.delete";
    public static readonly PermissionRequirement DeleteReq = new(PermissionScope.Topic, Delete);

    /// <summary>トピックの管理（権限設定など）</summary>
    public const string Manage = "topic.manage";
    public static readonly PermissionRequirement ManageReq = new(PermissionScope.Topic, Manage);

    /// <summary>トピックのメッセージ読み取り</summary>
    public const string ReadMessages = "topic.readMessages";
    public static readonly PermissionRequirement ReadMessagesReq = new(PermissionScope.Topic, ReadMessages);

    /// <summary>トピックへのメッセージ投稿</summary>
    public const string WriteMessages = "topic.writeMessages";
    public static readonly PermissionRequirement WriteMessagesReq = new(PermissionScope.Topic, WriteMessages);
}

