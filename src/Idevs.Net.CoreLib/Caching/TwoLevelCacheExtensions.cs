using Serenity;
using Serenity.Abstractions;

namespace Idevs.Net.CoreLib.Caching;

/// <summary>
/// Async wrappers over Serenity's <see cref="ITwoLevelCache"/>. The underlying
/// cache surface is sync (<c>Func&lt;T&gt;</c> factory), so the async wrapper
/// resolves the factory before handing it to the cache. Acceptable for typical
/// repository read-through scenarios; do not use these wrappers when the
/// factory must run truly asynchronously inside the cache lock.
/// </summary>
public static class TwoLevelCacheExtensions
{
    /// <summary>
    /// Async wrapper around <see cref="Serenity.TwoLevelCacheExtensions.GetLocalStoreOnly{T}"/>.
    /// On cache hit returns the cached value without invoking <paramref name="factory"/>.
    /// On miss runs the factory, populates the local memory cache, and returns the fresh value.
    /// </summary>
    public static Task<T> GetLocalCachedAsync<T>(this ITwoLevelCache cache,
        string key,
        TimeSpan duration,
        string groupKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(factory);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(cache.GetLocalStoreOnly(key, duration, groupKey, SyncFactory)!);

        T SyncFactory() => factory(ct).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async wrapper around Serenity's two-tier <c>ITwoLevelCache.Get</c> path.
    /// Same semantics as <see cref="GetLocalCachedAsync{T}"/> but also consults the
    /// distributed cache layer before invoking the factory.
    /// </summary>
    public static Task<T> GetGloballyCachedAsync<T>(this ITwoLevelCache cache,
        string key,
        TimeSpan duration,
        string groupKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(factory);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(cache.Get(key, duration, groupKey, SyncFactory)!);

        T SyncFactory() => factory(ct).GetAwaiter().GetResult();
    }
}
