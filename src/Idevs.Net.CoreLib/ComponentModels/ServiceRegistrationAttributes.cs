using Microsoft.Extensions.DependencyInjection;

namespace Idevs.ComponentModels;

/// <summary>
/// Marks a class for scoped (per-request) service registration
/// </summary>
/// <remarks>
/// This attribute follows standard .NET DI patterns. Optional integration packages
/// can use the same metadata for container-specific features.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class ScopedAttribute : Attribute, IServiceRegistrationAttribute
{
    /// <summary>
    /// Gets or sets the service type to register as (optional)
    /// </summary>
    /// <remarks>
    /// If not specified, will attempt to find interface following I{ClassName} pattern
    /// </remarks>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// Gets or sets the service key for container-specific named registrations (optional)
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// Gets or sets whether to try self-registration if no interface is found
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = false;

    /// <summary>
    /// Gets the service lifetime
    /// </summary>
    public ServiceLifetime Lifetime => ServiceLifetime.Scoped;
}

/// <summary>
/// Marks a class for singleton service registration
/// </summary>
/// <remarks>
/// This attribute follows standard .NET DI patterns. Optional integration packages
/// can use the same metadata for container-specific features.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : Attribute, IServiceRegistrationAttribute
{
    /// <summary>
    /// Gets or sets the service type to register as (optional)
    /// </summary>
    /// <remarks>
    /// If not specified, will attempt to find interface following I{ClassName} pattern
    /// </remarks>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// Gets or sets the service key for container-specific named registrations (optional)
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// Gets or sets whether to try self-registration if no interface is found
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = false;

    /// <summary>
    /// Gets the service lifetime
    /// </summary>
    public ServiceLifetime Lifetime => ServiceLifetime.Singleton;
}

/// <summary>
/// Marks a class for transient service registration
/// </summary>
/// <remarks>
/// This attribute follows standard .NET DI patterns. Optional integration packages
/// can use the same metadata for container-specific features.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : Attribute, IServiceRegistrationAttribute
{
    /// <summary>
    /// Gets or sets the service type to register as (optional)
    /// </summary>
    /// <remarks>
    /// If not specified, will attempt to find interface following I{ClassName} pattern
    /// </remarks>
    public Type? ServiceType { get; set; }

    /// <summary>
    /// Gets or sets the service key for container-specific named registrations (optional)
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// Gets or sets whether to try self-registration if no interface is found
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = false;

    /// <summary>
    /// Gets the service lifetime
    /// </summary>
    public ServiceLifetime Lifetime => ServiceLifetime.Transient;
}

/// <summary>
/// Base interface for service registration attributes
/// </summary>
public interface IServiceRegistrationAttribute
{
    /// <summary>
    /// Gets the service type to register as
    /// </summary>
    Type? ServiceType { get; }

    /// <summary>
    /// Gets the service key for container-specific named registrations
    /// </summary>
    string? ServiceKey { get; }

    /// <summary>
    /// Gets whether to allow self-registration
    /// </summary>
    bool AllowSelfRegistration { get; }

    /// <summary>
    /// Gets the service lifetime
    /// </summary>
    ServiceLifetime Lifetime { get; }
}
