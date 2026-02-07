using Microsoft.AspNetCore.Http;

namespace TreeTopic.Extensions;

/// <summary>
/// HttpContext.Items を使用したリクエスト内キャッシュ拡張
/// </summary>
public static class HttpContextExtensions
{
    private const string CacheKeyPrefix = "__ReqCache_";

    /// <summary>
    /// キャッシュから取得。なければfactoryを実行してキャッシュに保存
    /// </summary>
    public static async Task<T> GetOrCreateAsync<T>(
        this HttpContext httpContext,
        string key,
        Func<Task<T>> factory)
    {
        var fullKey = CacheKeyPrefix + key;

        if (httpContext.Items.TryGetValue(fullKey, out var cached) && cached is T value)
        {
            return value;
        }

        var result = await factory();
        httpContext.Items[fullKey] = result;
        return result;
    }

    /// <summary>
    /// キャッシュから取得（非同期版）
    /// </summary>
    public static bool TryGetCache<T>(this HttpContext httpContext, string key, out T? value)
    {
        var fullKey = CacheKeyPrefix + key;
        if (httpContext.Items.TryGetValue(fullKey, out var cached) && cached is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// キャッシュに設定
    /// </summary>
    public static void SetCache<T>(this HttpContext httpContext, string key, T value)
    {
        httpContext.Items[CacheKeyPrefix + key] = value;
    }

    /// <summary>
    /// 権限関連のキャッシュをクリア
    /// </summary>
    public static void InvalidatePermissionCache(this HttpContext httpContext, string pattern)
    {
        var keysToRemove = httpContext.Items.Keys
            .OfType<string>()
            .Where(k => k.StartsWith(CacheKeyPrefix + pattern))
            .ToList();

        foreach (var key in keysToRemove)
        {
            httpContext.Items.Remove(key);
        }
    }
}
