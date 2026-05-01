using Autofac;
using Microsoft.AspNetCore.Builder;

namespace Idevs.Extensions;

public static class AutofacWebApplicationExtensions
{
    public static WebApplication UseIdevsStaticServiceLocator(this WebApplication app, ILifetimeScope container)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(container);

        StaticServiceLocator.Initialize(new AutofacServiceProvider(container));
        return app;
    }

    private sealed class AutofacServiceProvider : IServiceProvider
    {
        private readonly ILifetimeScope scope;

        public AutofacServiceProvider(ILifetimeScope scope)
        {
            this.scope = scope;
        }

        public object? GetService(Type serviceType)
        {
            return scope.ResolveOptional(serviceType);
        }
    }
}
