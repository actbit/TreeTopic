namespace TreeTopic.Permissions;

/// <summary>
/// 権限スコープ
/// </summary>
public enum PermissionScope
{
    /// <summary>全ての体系（後方互換）</summary>
    All,

    /// <summary>Role権限</summary>
    Role,

    /// <summary>Room権限</summary>
    Room,

    /// <summary>Topic権限</summary>
    Topic
}

/// <summary>
/// 権限要件
/// </summary>
public readonly record struct PermissionRequirement(PermissionScope Scope, string Name)
{
    public override string ToString() => Name;

    /// <summary>文字列から変換</summary>
    public static PermissionRequirement Parse(string permission)
    {
        if (permission.StartsWith("identity."))
            return new PermissionRequirement(PermissionScope.Role, permission);
        if (permission.StartsWith("room."))
            return new PermissionRequirement(PermissionScope.Room, permission);
        if (permission.StartsWith("topic."))
            return new PermissionRequirement(PermissionScope.Topic, permission);
        return new PermissionRequirement(PermissionScope.All, permission);
    }
}

/// <summary>
/// 権限要件ファクトリ
/// </summary>
public static class Perm
{
    /// <summary>Identity（Role）権限</summary>
    public static PermissionRequirement Identity(string name) => new PermissionRequirement(PermissionScope.Role, $"identity.{name}");

    /// <summary>Room権限</summary>
    public static PermissionRequirement Room(string name) => new PermissionRequirement(PermissionScope.Room, $"room.{name}");

    /// <summary>Topic権限</summary>
    public static PermissionRequirement Topic(string name) => new PermissionRequirement(PermissionScope.Topic, $"topic.{name}");
}
