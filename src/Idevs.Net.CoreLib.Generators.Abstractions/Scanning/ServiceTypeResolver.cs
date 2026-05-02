using System.Linq;
using Microsoft.CodeAnalysis;

namespace Idevs.Generators.Abstractions.Scanning;

/// <summary>
/// Resolves the registration target (service type) for a concrete class
/// using the Idevs convention rules: explicit type wins → I{ClassName}
/// convention → AllowSelfRegistration → none.
/// </summary>
public static class ServiceTypeResolver
{
    /// <summary>
    /// Apply only the I{ClassName} convention. Returns true and the matched
    /// interface when exactly one of the type's direct interfaces is named
    /// <c>I&lt;TypeName&gt;</c>.
    /// </summary>
    public static bool TryResolveByConvention(INamedTypeSymbol implementationType, out INamedTypeSymbol? serviceType)
    {
        serviceType = null;
        if (implementationType is null) return false;

        var conventionName = "I" + implementationType.Name;
        var match = implementationType.Interfaces.FirstOrDefault(i => i.Name == conventionName);
        if (match is null) return false;

        serviceType = match;
        return true;
    }

    /// <summary>
    /// Apply the convention and, on miss, return self-registration when allowed.
    /// </summary>
    public static bool TryResolveWithSelfRegistration(
        INamedTypeSymbol implementationType,
        bool allowSelfRegistration,
        out INamedTypeSymbol? serviceType)
    {
        if (TryResolveByConvention(implementationType, out serviceType)) return true;

        if (allowSelfRegistration)
        {
            serviceType = implementationType;
            return true;
        }

        serviceType = null;
        return false;
    }
}
