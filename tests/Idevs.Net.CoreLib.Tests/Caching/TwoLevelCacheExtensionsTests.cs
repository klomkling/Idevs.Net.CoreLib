using Idevs.Net.CoreLib.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serenity;
using Serenity.Abstractions;

namespace Idevs.Net.CoreLib.Tests.Caching;

/// <summary>
/// Tests for <see cref="TwoLevelCacheExtensions"/>.
///
/// GetLocalStoreOnly and Get are extension methods on Serenity.TwoLevelCacheExtensions,
/// not interface members, so they cannot be stubbed with NSubstitute. Tests use a real
/// TwoLevelCache backed by in-process MemoryCache + DistributedMemoryCache so behavior
/// is verified through the actual Serenity cache path.
/// </summary>
public class TwoLevelCacheExtensionsTests
{
    private static ITwoLevelCache CreateCache() =>
        new TwoLevelCache(
            new MemoryCache(Options.Create(new MemoryCacheOptions())),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    [Fact]
    public async Task GetLocalCachedAsync_CacheHit_ReturnsCachedValue_WithoutInvokingFactory()
    {
        var cache = CreateCache();

        // Populate the cache by running once.
        await cache.GetLocalCachedAsync("key", TimeSpan.FromMinutes(1), "group",
            ct => Task.FromResult("first"));

        var factoryInvoked = false;
        var result = await cache.GetLocalCachedAsync(
            "key",
            TimeSpan.FromMinutes(1),
            "group",
            ct => { factoryInvoked = true; return Task.FromResult("second"); });

        Assert.Equal("first", result);
        Assert.False(factoryInvoked);
    }

    [Fact]
    public async Task GetLocalCachedAsync_CacheMiss_InvokesFactory()
    {
        var cache = CreateCache();

        var result = await cache.GetLocalCachedAsync(
            "miss-key",
            TimeSpan.FromMinutes(1),
            "group",
            ct => Task.FromResult("fresh"));

        Assert.Equal("fresh", result);
    }

    [Fact]
    public async Task GetLocalCachedAsync_CancelledToken_ThrowsBeforeInvokingCache()
    {
        var cache = CreateCache();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetLocalCachedAsync(
                "key",
                TimeSpan.FromMinutes(1),
                "group",
                ct => Task.FromResult("value"),
                cts.Token));
    }

    [Fact]
    public async Task GetLocalCachedAsync_NullCache_Throws()
    {
        ITwoLevelCache? cache = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            cache!.GetLocalCachedAsync("key", TimeSpan.FromMinutes(1), "group",
                                       ct => Task.FromResult("value")));
    }
}
