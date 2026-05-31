using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Extensions;

/// <summary>
/// Bootstrapper extension methods for Idevs CoreLib services.
/// </summary>
public static class CoreLibBootstrapper
{
    /// <summary>
    /// Registers the three hand-coded CoreLib services:
    /// <see cref="IViewPageRenderer"/>, <see cref="IIdevsExcelExporter"/>, and <see cref="IIdevsPdfExporter"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIdevsCorelibCore(this IServiceCollection services)
    {
        services.AddScoped<IViewPageRenderer, ViewPageRenderer>();
        services.AddScoped<IIdevsExcelExporter, IdevsExcelExporter>();
        services.AddSingleton<IIdevsPdfExporter>(_ => new IdevsPdfExporter());
        return services;
    }
}
