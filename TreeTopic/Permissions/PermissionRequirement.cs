namespace TreeTopic.Permissions;

/// <summary>
/// 権限スコープ
/// </summary>
public enum PermissionScope
{
    /// <summary>Role権限（tenant.*）</summary>
    Role,

    /// <summary>Room権限（room.*）</summary>
    Room,

    /// <summary>Topic権限（topic.*）</summary>
    Topic
}

/// <summary>
/// 権限要件
/// </summary>
public sealed class PermissionRequirement : IEquatable<PermissionRequirement>
{
    public PermissionScope Scope { get; }
    public string Name { get; }

    public PermissionRequirement(PermissionScope scope, string name)
    {
        Scope = scope;
        Name = name;
    }

    public override string ToString() => Name;

    public bool Equals(PermissionRequirement? other) =>
        other is not null && Scope == other.Scope && Name == other.Name;

    public override bool Equals(object? obj) =>
        obj is PermissionRequirement other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Scope, Name);

    public static bool operator ==(PermissionRequirement? left, PermissionRequirement? right) =>
        EqualityComparer<PermissionRequirement>.Default.Equals(left, right);

    public static bool operator !=(PermissionRequirement? left, PermissionRequirement? right) =>
        !(left == right);

    /// <summary>文字列から変換</summary>
    public static PermissionRequirement Parse(string permission)
    {
        if (permission.StartsWith("tenant."))
            return new PermissionRequirement(PermissionScope.Role, permission);
        if (permission.StartsWith("room."))
            return new PermissionRequirement(PermissionScope.Room, permission);
        if (permission.StartsWith("topic."))
            return new PermissionRequirement(PermissionScope.Topic, permission);

        throw new ArgumentException(
            $"Permission must start with 'tenant.', 'room.', or 'topic.'. Got: '{permission}'.");
    }
}

/// <summary>
/// 権限要件ファクトリ
/// </summary>
public static class Perm
{
    /// <summary>Tenant（Role）権限</summary>
    public static PermissionRequirement Tenant(string name) => new PermissionRequirement(PermissionScope.Role, $"tenant.{name}");

    /// <summary>Room権限</summary>
    public static PermissionRequirement Room(string name) => new PermissionRequirement(PermissionScope.Room, $"room.{name}");

    /// <summary>Topic権限</summary>
    public static PermissionRequirement Topic(string name) => new PermissionRequirement(PermissionScope.Topic, $"topic.{name}");
}
