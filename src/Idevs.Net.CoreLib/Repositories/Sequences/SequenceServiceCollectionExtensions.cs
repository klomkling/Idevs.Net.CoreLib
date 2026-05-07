using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Repositories.Sequences;

/// <summary>
/// DI helpers for <see cref="ISequenceProvider"/>.
/// </summary>
/// <remarks>
/// Source-generator-driven DI consumers don't need this — the
/// <c>[Scoped]</c> attribute on <see cref="SqlSequenceProvider"/> is
/// already picked up. This extension exists for manual setups (test
/// hosts, console apps) where the source generator isn't running.
/// </remarks>
public static class SequenceServiceCollectionExtensions
{
    /// <summary>
    /// Register the SQL-backed default <see cref="ISequenceProvider"/>
    /// as scoped. Call <see cref="ISequenceProvider.EnsureSequenceAsync"/>
    /// (or seed via your migration pipeline) before allocating values.
    /// </summary>
    public static IServiceCollection AddIdevsSequenceProvider(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<ISequenceProvider, SqlSequenceProvider>();
        return services;
    }
}
