using System;
using Idevs.Generators.Abstractions.Validation;

namespace Idevs.Generators.Abstractions.Scanning;

/// <summary>
/// Represents a resolved service registration pairing an implementation type
/// with the service type it fulfils and the lifetime it is registered under.
/// </summary>
public sealed class ResolvedServiceType
{
    public string ImplementationType { get; }
    public string ServiceType { get; }
    public Lifetime Lifetime { get; }

    public ResolvedServiceType(string implementationType, string serviceType, Lifetime lifetime)
    {
        ImplementationType = implementationType ?? throw new ArgumentNullException(nameof(implementationType));
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        Lifetime = lifetime;
    }
}
