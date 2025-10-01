using System.ComponentModel;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Idevs.ComponentModel;
using Idevs.ComponentModels.Standard;
using Idevs.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Idevs.Extensions;

/// <summary>
/// Extension methods for configuring Idevs.Net.CoreLib services with Autofac
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Configures the host to use Autofac as the service provider factory and registers Idevs services
    /// </summary>
    /// <param name="hostBuilder">The host builder</param>
    /// <returns>The updated host builder</returns>
    public static IHostBuilder UseIdevsAutofac(this IHostBuilder hostBuilder)
    {
        return hostBuilder
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>(builder =>
            {
                builder.RegisterModule<IdevsModule>();
            });
    }

    /// <summary>
    /// Adds Idevs CoreLib services to the service collection (fallback for non-Autofac scenarios)
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The updated service collection</returns>
    public static IServiceCollection AddIdevsCorelibServices(this IServiceCollection services)
    {
        // Register core services directly for non-Autofac scenarios
        services.AddScoped<IViewPageRenderer, ViewPageRenderer>();
        services.AddScoped<IIdevsExcelExporter, IdevsExcelExporter>();
        services.AddSingleton<IIdevsPdfExporter>(_ => new IdevsPdfExporter());

        // Register attribute-based services
        RegisterAttributeBasedServices(services);
        
        return services;
    }

    /// <summary>
    /// Registers the Idevs Autofac module to an existing ContainerBuilder
    /// </summary>
    /// <param name="builder">The container builder</param>
    /// <returns>The updated container builder</returns>
    public static ContainerBuilder RegisterIdevsModule(this ContainerBuilder builder)
    {
        builder.RegisterModule<IdevsModule>();
        return builder;
    }

    /// <summary>
    /// Registers services decorated with registration attributes (fallback for non-Autofac scenarios)
    /// This method is maintained for backward compatibility
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The updated service collection</returns>
    [Obsolete("Use AddIdevsCorelibServices() instead, which includes attribute-based registration")]
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        return AddIdevsCorelibServices(services);
    }

    /// <summary>
    /// Registers services decorated with registration attributes using reflection (for non-Autofac scenarios)
    /// Supports both legacy attributes and standard attributes
    /// </summary>
    /// <param name="services">The service collection</param>
    private static void RegisterAttributeBasedServices(IServiceCollection services)
    {
        // Legacy attribute types
        var legacyScopedRegistration = typeof(ScopedRegistrationAttribute);
        var legacySingletonRegistration = typeof(SingletonRegiatrationAttribute);
        var legacyTransientRegistration = typeof(TransientRegistrationAttribute);

        // Standard attribute types
        var scopedAttribute = typeof(ScopedAttribute);
        var singletonAttribute = typeof(SingletonAttribute);
        var transientAttribute = typeof(TransientAttribute);

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !(assembly.FullName ?? string.Empty).StartsWith("System.Data.SqlClient"))
            .Where(assembly => !(assembly.FullName ?? string.Empty).StartsWith("MySql.Data"))
            .Where(assembly => !(assembly.FullName ?? string.Empty).StartsWith("Npgsql"))
            .Where(assembly => !(assembly.FullName ?? string.Empty).StartsWith("System."))
            .Where(assembly => !(assembly.FullName ?? string.Empty).StartsWith("Microsoft."))
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null);
                }
            })
            .Where(type => !type.IsInterface && !type.IsAbstract)
            .Where(type =>
                // Legacy attributes
                type.IsDefined(legacyScopedRegistration, false) ||
                type.IsDefined(legacySingletonRegistration, false) ||
                type.IsDefined(legacyTransientRegistration, false) ||
                // Standard attributes
                type.IsDefined(scopedAttribute, false) ||
                type.IsDefined(singletonAttribute, false) ||
                type.IsDefined(transientAttribute, false))
            .ToList();

        foreach (var implementationType in types)
        {
            // Handle legacy attributes first (for backward compatibility)
            if (HandleLegacyAttributesForServiceCollection(services, implementationType, legacyScopedRegistration, legacySingletonRegistration, legacyTransientRegistration))
            {
                continue;
            }

            // Handle standard attributes
            HandleStandardAttributesForServiceCollection(services, implementationType);
        }
    }

    /// <summary>
    /// Handles legacy registration attributes for service collection
    /// </summary>
    private static bool HandleLegacyAttributesForServiceCollection(IServiceCollection services, Type implementationType,
        Type legacyScoped, Type legacySingleton, Type legacyTransient)
    {
        var interfaceType = implementationType.GetInterface($"I{implementationType.Name}");
        if (interfaceType == null) return false;

        if (implementationType.IsDefined(legacyScoped, false))
        {
            services.AddScoped(interfaceType, implementationType);
            return true;
        }

        if (implementationType.IsDefined(legacyTransient, false))
        {
            services.AddTransient(interfaceType, implementationType);
            return true;
        }

        if (implementationType.IsDefined(legacySingleton, false))
        {
            services.AddSingleton(interfaceType, implementationType);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles standard registration attributes for service collection
    /// </summary>
    private static void HandleStandardAttributesForServiceCollection(IServiceCollection services, Type implementationType)
    {
        // Check for Scoped attribute
        var scopedAttr = implementationType.GetCustomAttribute<ScopedAttribute>();
        if (scopedAttr != null)
        {
            RegisterWithStandardAttributeForServiceCollection(services, implementationType, scopedAttr.ServiceType, scopedAttr.ServiceKey, scopedAttr.AllowSelfRegistration, ServiceLifetime.Scoped);
            return;
        }

        // Check for Transient attribute
        var transientAttr = implementationType.GetCustomAttribute<TransientAttribute>();
        if (transientAttr != null)
        {
            RegisterWithStandardAttributeForServiceCollection(services, implementationType, transientAttr.ServiceType, transientAttr.ServiceKey, transientAttr.AllowSelfRegistration, ServiceLifetime.Transient);
            return;
        }

        // Check for Singleton attribute
        var singletonAttr = implementationType.GetCustomAttribute<SingletonAttribute>();
        if (singletonAttr != null)
        {
            RegisterWithStandardAttributeForServiceCollection(services, implementationType, singletonAttr.ServiceType, singletonAttr.ServiceKey, singletonAttr.AllowSelfRegistration, ServiceLifetime.Singleton);
        }
    }

    /// <summary>
    /// Registers a service with standard attribute configuration for service collection
    /// </summary>
    private static void RegisterWithStandardAttributeForServiceCollection(IServiceCollection services, Type implementationType,
        Type? serviceType, string? serviceKey, bool allowSelfRegistration, ServiceLifetime lifetime)
    {
        // Determine the service type
        var targetServiceType = serviceType ?? implementationType.GetInterface($"I{implementationType.Name}");
        
        // If no interface found and self-registration is not allowed, try to find any interface
        if (targetServiceType == null && !allowSelfRegistration)
        {
            var interfaces = implementationType.GetInterfaces();
            targetServiceType = interfaces.FirstOrDefault();
        }

        // If still no service type and self-registration is allowed, use the implementation type
        if (targetServiceType == null && allowSelfRegistration)
        {
            targetServiceType = implementationType;
        }

        // Skip registration if no valid service type found
        if (targetServiceType == null) return;

        // Register with appropriate lifetime (note: service keys are not supported in basic service collection)
        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                services.AddScoped(targetServiceType, implementationType);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(targetServiceType, implementationType);
                break;
            case ServiceLifetime.Singleton:
                services.AddSingleton(targetServiceType, implementationType);
                break;
        }
    }
}
