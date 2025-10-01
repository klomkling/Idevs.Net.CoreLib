using System.Reflection;
using Autofac;
using Idevs.ComponentModel;
using Idevs.ComponentModels.Standard;
using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Modules;

/// <summary>
/// Autofac module for registering Idevs.Net.CoreLib services
/// </summary>
public class IdevsModule : Autofac.Module
{
    /// <summary>
    /// Loads the module and registers all Idevs services
    /// </summary>
    /// <param name="builder">The container builder</param>
    protected override void Load(ContainerBuilder builder)
    {
        RegisterCoreServices(builder);
        RegisterAttributeBasedServices(builder);
    }

    /// <summary>
    /// Registers the core Idevs services
    /// </summary>
    /// <param name="builder">The container builder</param>
    private static void RegisterCoreServices(ContainerBuilder builder)
    {
        // Register core Idevs services
        builder.RegisterType<ViewPageRenderer>()
            .As<IViewPageRenderer>()
            .InstancePerLifetimeScope();

        builder.RegisterType<IdevsPdfExporter>()
            .As<IIdevsPdfExporter>()
            .SingleInstance();

        builder.RegisterType<IdevsExcelExporter>()
            .As<IIdevsExcelExporter>()
            .InstancePerLifetimeScope();
    }

    /// <summary>
    /// Registers services decorated with registration attributes using reflection
    /// Supports both legacy attributes (ScopedRegistrationAttribute, etc.) and standard attributes (ScopedAttribute, etc.)
    /// </summary>
    /// <param name="builder">The container builder</param>
    private static void RegisterAttributeBasedServices(ContainerBuilder builder)
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
            .Where(type => type is { IsInterface: false, IsAbstract: false })
            .Where(type =>
                // Legacy attributes
                type != null && (
                    type.IsDefined(legacyScopedRegistration, false) ||
                    type.IsDefined(legacySingletonRegistration, false) ||
                    type.IsDefined(legacyTransientRegistration, false) ||
                    // Standard attributes
                    type.IsDefined(scopedAttribute, false) ||
                    type.IsDefined(singletonAttribute, false) ||
                    type.IsDefined(transientAttribute, false)
                )
            )
            .ToList();

        foreach (var implementationType in types.Where(implementationType => !HandleLegacyAttributes(builder,
                     implementationType, legacyScopedRegistration, legacySingletonRegistration,
                     legacyTransientRegistration)))
        {
            // Handle standard attributes
            if (implementationType != null)
                HandleStandardAttributes(builder, implementationType, scopedAttribute, singletonAttribute,
                    transientAttribute);
        }
    }

    /// <summary>
    /// Handles legacy registration attributes
    /// </summary>
    private static bool HandleLegacyAttributes(ContainerBuilder builder, Type? implementationType,
        Type legacyScoped, Type legacySingleton, Type legacyTransient)
    {
        var interfaceType = implementationType?.GetInterface($"I{implementationType.Name}");
        if (interfaceType == null) return false;

        if (implementationType != null && implementationType.IsDefined(legacyScoped, false))
        {
            builder.RegisterType(implementationType)
                .As(interfaceType)
                .InstancePerLifetimeScope();
            return true;
        }

        if (implementationType != null && implementationType.IsDefined(legacyTransient, false))
        {
            builder.RegisterType(implementationType)
                .As(interfaceType)
                .InstancePerDependency();
            return true;
        }

        if (implementationType == null || !implementationType.IsDefined(legacySingleton, false)) return false;

        builder.RegisterType(implementationType)
            .As(interfaceType)
            .SingleInstance();
        return true;

    }

    /// <summary>
    /// Handles standard registration attributes with enhanced features
    /// </summary>
    private static void HandleStandardAttributes(ContainerBuilder builder, Type implementationType,
        Type scopedAttribute, Type singletonAttribute, Type transientAttribute)
    {
        // Check for Scoped attribute
        var scopedAttr = implementationType.GetCustomAttribute<ScopedAttribute>();
        if (scopedAttr != null)
        {
            RegisterWithStandardAttribute(builder, implementationType, scopedAttr.ServiceType, scopedAttr.ServiceKey, scopedAttr.AllowSelfRegistration, ServiceLifetime.Scoped);
            return;
        }

        // Check for Transient attribute
        var transientAttr = implementationType.GetCustomAttribute<TransientAttribute>();
        if (transientAttr != null)
        {
            RegisterWithStandardAttribute(builder, implementationType, transientAttr.ServiceType, transientAttr.ServiceKey, transientAttr.AllowSelfRegistration, ServiceLifetime.Transient);
            return;
        }

        // Check for Singleton attribute
        var singletonAttr = implementationType.GetCustomAttribute<SingletonAttribute>();
        if (singletonAttr != null)
        {
            RegisterWithStandardAttribute(builder, implementationType, singletonAttr.ServiceType, singletonAttr.ServiceKey, singletonAttr.AllowSelfRegistration, ServiceLifetime.Singleton);
        }
    }

    /// <summary>
    /// Registers a service with standard attribute configuration
    /// </summary>
    private static void RegisterWithStandardAttribute(ContainerBuilder builder, Type implementationType, 
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

        // Create the registration
        var registration = builder.RegisterType(implementationType).As(targetServiceType);

        // Apply lifetime scope
        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                registration.InstancePerLifetimeScope();
                break;
            case ServiceLifetime.Transient:
                registration.InstancePerDependency();
                break;
            case ServiceLifetime.Singleton:
                registration.SingleInstance();
                break;
        }

        // Apply service key if specified
        if (!string.IsNullOrEmpty(serviceKey))
        {
            registration.Keyed(serviceKey, targetServiceType);
        }
    }
}
