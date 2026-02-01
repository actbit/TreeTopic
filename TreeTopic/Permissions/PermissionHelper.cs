using System.Collections.Concurrent;
using System.Reflection;

namespace TreeTopic.Permissions;

/// <summary>
/// Permissionsクラスから権限定数をReflectionで取得するヘルパークラス
/// </summary>
public static class PermissionHelper
{
    private static readonly ConcurrentDictionary<Type, string[]> _permissionCache = new();

    /// <summary>
    /// 指定したPermissionsクラスのすべての権限名（定数値）を取得
    /// </summary>
    /// <param name="type">Permissionsクラスの型</param>
    /// <returns>権限名の配列</returns>
    public static string[] GetAllPermissions(Type type)
    {
        return _permissionCache.GetOrAdd(type, t =>
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => f.GetValue(null)?.ToString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        });
    }

    /// <summary>
    /// IdentityPermissionsのすべての権限名を取得
    /// </summary>
    public static string[] GetIdentityPermissions() => GetAllPermissions(typeof(IdentityPermissions));

    /// <summary>
    /// RoomPermissionsのすべての権限名を取得
    /// </summary>
    public static string[] GetRoomPermissions() => GetAllPermissions(typeof(RoomPermissions));

    /// <summary>
    /// TopicPermissionsのすべての権限名を取得
    /// </summary>
    public static string[] GetTopicPermissions() => GetAllPermissions(typeof(TopicPermissions));

    /// <summary>
    /// すべての権限名を取得（Identity + Room + Topic）
    /// </summary>
    public static string[] GetAllPermissions()
    {
        return
        [
            .. GetIdentityPermissions(),
            .. GetRoomPermissions(),
            .. GetTopicPermissions()
        ];
    }

    /// <summary>
    /// 指定したPermissionsクラスの権限定数名と値のディクショナリを取得
    /// </summary>
    /// <param name="type">Permissionsクラスの型</param>
    /// <returns>権限定数名と権限値のディクショナリ</returns>
    public static Dictionary<string, string> GetPermissionNameValuePairs(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(
                f => f.Name,
                f => (f.GetValue(null)?.ToString() ?? string.Empty)
            );
    }

    /// <summary>
    /// IdentityPermissionsの権限定数名と値のディクショナリを取得
    /// </summary>
    public static Dictionary<string, string> GetIdentityPermissionNameValuePairs() =>
        GetPermissionNameValuePairs(typeof(IdentityPermissions));

    /// <summary>
    /// RoomPermissionsの権限定数名と値のディクショナリを取得
    /// </summary>
    public static Dictionary<string, string> GetRoomPermissionNameValuePairs() =>
        GetPermissionNameValuePairs(typeof(RoomPermissions));

    /// <summary>
    /// TopicPermissionsの権限定数名と値のディクショナリを取得
    /// </summary>
    public static Dictionary<string, string> GetTopicPermissionNameValuePairs() =>
        GetPermissionNameValuePairs(typeof(TopicPermissions));
}

