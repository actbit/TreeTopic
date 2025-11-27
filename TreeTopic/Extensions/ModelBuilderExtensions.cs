using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TreeTopic.Extensions;

/// <summary>
/// マルチテナント環境向けの ModelBuilder 拡張メソッド
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Guid 型カラムを DB プロバイダーに応じて最適化する
    /// MySQL: BINARY(16) / PostgreSQL: uuid
    /// </summary>

    public static void ConfigureMultiTenantGuidColumns(
    this ModelBuilder modelBuilder,
    string databaseProvider)
    {
        bool isMySQL = databaseProvider.Contains("mysql", StringComparison.OrdinalIgnoreCase);
        string guidColumnType = isMySQL ? "BINARY(16)" : "uuid";

        // 全エンティティの Guid 型プロパティを設定
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (IsGuidType(property.ClrType))
                {
                    property.SetColumnType(guidColumnType);
                }
            }
        }
    }
    public static void ConfigureMySqlGuidColumns(this ModelBuilder modelBuilder)
    {
        string guidColumnType = "BINARY(16)";

        // 全エンティティの Guid 型プロパティを設定
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (IsGuidType(property.ClrType))
                {
                    property.SetColumnType(guidColumnType);
                }
            }
        }
    }


    /// <summary>
    /// Guid 型か Guid? 型かを判定
    /// </summary>
    private static bool IsGuidType(Type clrType)
    {
        // Guid 型
        if (clrType == typeof(Guid))
            return true;

        // Guid? 型（Nullable<Guid>）
        if (clrType.IsGenericType &&
            clrType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
            clrType.GetGenericArguments()[0] == typeof(Guid))
            return true;

        return false;
    }
}
