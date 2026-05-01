using Serenity;
using Serenity.Abstractions;

namespace Idevs.Caching;

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
    public static Task<T> GetLocalCachedAsync<T>(
        this ITwoLevelCache cache,
        string key,
        TimeSpan duration,
        string groupKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
        where T : class
    {
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        ct.ThrowIfCancellationRequested();

        T SyncFactory() => factory(ct).GetAwaiter().GetResult();

        return Task.FromResult(cache.GetLocalStoreOnly(key, duration, groupKey, SyncFactory)!);
    }

    /// <summary>
    /// Async wrapper around <see cref="Serenity.TwoLevelCacheExtensions.Get{T}"/>
    /// (the global / two-tier path).
    /// Same semantics as <see cref="GetLocalCachedAsync{T}"/> but also consults the
    /// distributed cache layer before invoking the factory.
    /// </summary>
    public static Task<T> GetGloballyCachedAsync<T>(
        this ITwoLevelCache cache,
        string key,
        TimeSpan duration,
        string groupKey,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default)
        where T : class
    {
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        ct.ThrowIfCancellationRequested();

        T SyncFactory() => factory(ct).GetAwaiter().GetResult();

        return Task.FromResult(cache.Get(key, duration, groupKey, SyncFactory)!);
    }
}
