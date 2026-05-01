using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Idevs;

/// <summary>
/// Provides static service resolution for legacy integration points where constructor injection is not feasible.
/// </summary>
/// <remarks>
/// This type is not obsolete, but it should be treated as a last-resort compatibility bridge.
/// Prefer constructor dependency injection, method injection, or explicit factory services for new code.
/// Avoid using this type from normal application services because static resolution hides dependencies
/// and can make service lifetime issues harder to diagnose.
/// </remarks>
public static class StaticServiceLocator
{
    private static readonly object Lock = new();
    private static readonly ConcurrentDictionary<Type, object> SingletonCache = new();
    private static IServiceProvider? serviceProvider;
    private static bool isInitialized;

    /// <summary>
    /// Gets a value indicating whether the service locator has been initialized.
    /// </summary>
    public static bool IsInitialized => isInitialized;

    /// <summary>
    /// Initializes the service locator with a service provider.
    /// </summary>
    /// <remarks>
    /// Call this once during application startup only when legacy or static code paths require
    /// service resolution. New code should receive dependencies through constructors instead.
    /// </remarks>
    public static void Initialize(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (Lock)
        {
            serviceProvider = provider;
            isInitialized = true;
            SingletonCache.Clear();
        }
    }

    /// <summary>
    /// Resolves a required service of the specified type.
    /// </summary>
    /// <remarks>
    /// Prefer constructor injection. Use this method only from legacy or static code paths
    /// that cannot receive dependencies through DI.
    /// </remarks>
    public static T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    /// <summary>
    /// Resolves a required service of the specified type.
    /// </summary>
    /// <remarks>
    /// Prefer constructor injection. Use this method only from legacy or static code paths
    /// that cannot receive dependencies through DI.
    /// </remarks>
    public static object Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        EnsureInitialized();

        try
        {
            return serviceProvider!.GetRequiredService(serviceType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resolve service of type {serviceType.Name}.", ex);
        }
    }

    /// <summary>
    /// Tries to resolve a service of the specified type.
    /// </summary>
    /// <remarks>
    /// Prefer constructor injection. Use this method only from legacy or static code paths
    /// that cannot receive dependencies through DI.
    /// </remarks>
    public static T? TryResolve<T>() where T : class
    {
        return TryResolve(typeof(T)) as T;
    }

    /// <summary>
    /// Tries to resolve a service of the specified type.
    /// </summary>
    /// <remarks>
    /// Prefer constructor injection. Use this method only from legacy or static code paths
    /// that cannot receive dependencies through DI.
    /// </remarks>
    public static object? TryResolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (!isInitialized)
            return null;

        try
        {
            return serviceProvider!.GetService(serviceType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a singleton service and caches it for subsequent calls.
    /// </summary>
    /// <remarks>
    /// Use only for services that are registered as singletons. Caching scoped or transient
    /// services can create lifetime bugs. Prefer constructor injection for new code.
    /// </remarks>
    public static T ResolveSingleton<T>() where T : class
    {
        var serviceType = typeof(T);

        if (SingletonCache.TryGetValue(serviceType, out var cachedService))
            return (T)cachedService;

        var service = Resolve<T>();
        SingletonCache.TryAdd(serviceType, service);
        return service;
    }

    /// <summary>
    /// Creates a new service scope for scoped service resolution.
    /// </summary>
    /// <remarks>
    /// Prefer normal scoped dependency injection. This method exists for legacy/static code
    /// that must create a scope explicitly.
    /// </remarks>
    public static IServiceScope CreateScope()
    {
        EnsureInitialized();
        return serviceProvider!.CreateScope();
    }

    /// <summary>
    /// Resets the service locator, clearing cached services and initialization.
    /// </summary>
    public static void Reset()
    {
        lock (Lock)
        {
            serviceProvider = null;
            isInitialized = false;
            SingletonCache.Clear();
        }
    }

    private static void EnsureInitialized()
    {
        if (!isInitialized)
        {
            throw new InvalidOperationException(
                "StaticServiceLocator is not initialized. Call Initialize() with an IServiceProvider before using.");
        }
    }
}
