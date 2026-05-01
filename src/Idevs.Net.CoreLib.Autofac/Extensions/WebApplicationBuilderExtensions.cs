using Autofac;
using Autofac.Extensions.DependencyInjection;
using Idevs.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Idevs.Extensions;

/// <summary>
/// Extension methods for WebApplicationBuilder to integrate Idevs.Net.CoreLib with Autofac
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the WebApplication to use Autofac and registers Idevs services
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <returns>The updated web application builder</returns>
    public static WebApplicationBuilder UseIdevsAutofac(this WebApplicationBuilder builder)
    {
        // Configure Autofac as the service provider factory
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        
        // Configure the container
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterModule<IdevsModule>();
        });

        // StaticServiceLocator will be initialized via UseIdevsStaticServiceLocator()
        
        return builder;
    }

    /// <summary>
    /// Registers additional Autofac modules along with the Idevs module
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <param name="modules">Additional Autofac modules to register</param>
    /// <returns>The updated web application builder</returns>
    public static WebApplicationBuilder UseIdevsAutofac(this WebApplicationBuilder builder, params Autofac.Module[] modules)
    {
        // Configure Autofac as the service provider factory
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        
        // Configure the container
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterModule<IdevsModule>();
            
            foreach (var module in modules)
            {
                containerBuilder.RegisterModule(module);
            }
        });
        
        return builder;
    }

    /// <summary>
    /// Configures the WebApplication to use Autofac with custom container configuration
    /// </summary>
    /// <param name="builder">The web application builder</param>
    /// <param name="containerConfiguration">Custom container configuration action</param>
    /// <returns>The updated web application builder</returns>
    public static WebApplicationBuilder UseIdevsAutofac(this WebApplicationBuilder builder, Action<ContainerBuilder> containerConfiguration)
    {
        // Configure Autofac as the service provider factory
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        
        // Configure the container
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterModule<IdevsModule>();
            containerConfiguration(containerBuilder);
        });
        
        return builder;
    }
}
