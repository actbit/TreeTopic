using System.Reflection;
using TreeTopic.Permissions;

namespace TreeTopic.Services;

/// <summary>
/// 権限スキャンサービス - アプリケーション内で使用されている権限を収集
/// </summary>
public class PermissionScanService
{
    public HashSet<PermissionRequirement> Permissions { get; private set; } = new HashSet<PermissionRequirement>();
    private readonly Assembly _assembly;

    public PermissionScanService()
    {
        _assembly = Assembly.GetExecutingAssembly();
        Scan();
    }

    public void Scan()
    {
        var types = _assembly.GetTypes();

        foreach (var type in types)
        {
            ScanType(type);
        }
    }

    private void ScanType(Type type)
    {
        // クラスレベルのRequireAny/RequireAll属性をスキャン
        var classAttributes = type.GetCustomAttributes()
            .Where(a => a.GetType().Name is "RequireAnyAttribute" or "RequireAllAttribute");

        foreach (var attr in classAttributes)
        {
            var requirements = ExtractRequirements(attr);
            foreach (var req in requirements)
            {
                Permissions.Add(req);
            }
        }

        // メソッドレベルのRequireAny/RequireAll属性をスキャン
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var method in methods)
        {
            var methodAttributes = method.GetCustomAttributes()
                .Where(a => a.GetType().Name is "RequireAnyAttribute" or "RequireAllAttribute");

            foreach (var attr in methodAttributes)
            {
                var requirements = ExtractRequirements(attr);
                foreach (var req in requirements)
                {
                    Permissions.Add(req);
                }
            }
        }
    }

    private IEnumerable<PermissionRequirement> ExtractRequirements(Attribute attribute)
    {
        var attrType = attribute.GetType();

        // _requirements フィールドをリフレクションで取得
        var field = attrType.GetField("_requirements", BindingFlags.NonPublic | BindingFlags.Instance);

        if (field != null)
        {
            var value = field.GetValue(attribute);
            if (value is PermissionRequirement[] requirements)
            {
                return requirements;
            }
        }

        return Enumerable.Empty<PermissionRequirement>();
    }

    /// <summary>
    /// スコープ別に権限を取得
    /// </summary>
    public Dictionary<PermissionScope, List<PermissionRequirement>> GetPermissionsByScope()
    {
        return Permissions
            .GroupBy(p => p.Scope)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// 指定されたスコープの権限のみ取得
    /// </summary>
    public IEnumerable<PermissionRequirement> GetPermissionsByScope(PermissionScope scope)
    {
        return Permissions.Where(p => p.Scope == scope);
    }

    /// <summary>
    /// 使用されている権限の数を取得
    /// </summary>
    public int GetPermissionCount()
    {
        return Permissions.Count;
    }
}
