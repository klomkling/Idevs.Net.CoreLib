using Microsoft.AspNetCore.Builder;

namespace Idevs.Extensions;

/// <summary>
/// Extension methods for WebApplication to initialize StaticServiceLocator.
/// </summary>
public static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Initializes the StaticServiceLocator with the application's service provider.
        /// </summary>
        public WebApplication UseIdevsStaticServiceLocator()
        {
            ArgumentNullException.ThrowIfNull(app);

            StaticServiceLocator.Initialize(app.Services);
            return app;
        }

        /// <summary>
        /// Initializes the StaticServiceLocator with a specific service provider.
        /// </summary>
        public WebApplication UseIdevsStaticServiceLocator(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(app);

            StaticServiceLocator.Initialize(provider);
            return app;
        }
    }
}
