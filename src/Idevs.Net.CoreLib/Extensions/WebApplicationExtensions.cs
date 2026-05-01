using Microsoft.AspNetCore.Builder;

namespace Idevs.Extensions;

/// <summary>
/// Extension methods for WebApplication to initialize StaticServiceLocator.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Initializes the StaticServiceLocator with the application's service provider.
    /// </summary>
    public static WebApplication UseIdevsStaticServiceLocator(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        StaticServiceLocator.Initialize(app.Services);
        return app;
    }

    /// <summary>
    /// Initializes the StaticServiceLocator with a specific service provider.
    /// </summary>
    public static WebApplication UseIdevsStaticServiceLocator(this WebApplication app, IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(app);

        StaticServiceLocator.Initialize(provider);
        return app;
    }
}
