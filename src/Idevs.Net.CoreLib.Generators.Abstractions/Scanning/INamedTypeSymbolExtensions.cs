using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Idevs.Generators.Abstractions.Scanning;

/// <summary>
/// Extension methods on <see cref="INamedTypeSymbol"/> commonly needed when
/// building Idevs-flavoured source generators.
/// </summary>
public static class INamedTypeSymbolExtensions
{
    /// <summary>
    /// True when the type (transitively) implements an interface whose
    /// fully-qualified name (without <c>global::</c>) matches <paramref name="fullName"/>.
    /// </summary>
    public static bool ImplementsInterface(this INamedTypeSymbol symbol, string fullName)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));
        if (string.IsNullOrEmpty(fullName)) throw new ArgumentException("fullName cannot be empty", nameof(fullName));

        return symbol.AllInterfaces.Any(iface => MatchesFullName(iface, fullName));
    }

    /// <summary>
    /// True when the type carries an attribute whose fully-qualified name
    /// matches <paramref name="fullName"/>. Inherited attributes are included.
    /// </summary>
    public static bool HasAttributeWithFullName(this INamedTypeSymbol symbol, string fullName)
    {
        if (symbol is null) throw new ArgumentNullException(nameof(symbol));
        if (string.IsNullOrEmpty(fullName)) throw new ArgumentException("fullName cannot be empty", nameof(fullName));

        return symbol.GetAttributes().Any(a =>
            a.AttributeClass is not null && MatchesFullName(a.AttributeClass, fullName));
    }

    private static bool MatchesFullName(INamedTypeSymbol symbol, string fullName)
    {
        var symbolName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        return symbolName == fullName || symbol.Name == fullName;
    }
}
