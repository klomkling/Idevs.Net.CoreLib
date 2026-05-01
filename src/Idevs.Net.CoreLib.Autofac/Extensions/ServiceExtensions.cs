using Autofac;
using Autofac.Extensions.DependencyInjection;
using Idevs.Modules;
using Microsoft.Extensions.Hosting;

namespace Idevs.Extensions;

public static class AutofacServiceExtensions
{
    public static IHostBuilder UseIdevsAutofac(this IHostBuilder hostBuilder)
    {
        return hostBuilder
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterModule<IdevsModule>();
            });
    }

    public static ContainerBuilder RegisterIdevsModule(this ContainerBuilder builder)
    {
        builder.RegisterModule<IdevsModule>();
        return builder;
    }
}
