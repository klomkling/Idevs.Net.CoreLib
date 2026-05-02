using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Idevs.Generators.Abstractions.Scanning;

/// <summary>
/// Compilation-wide search helpers for finding types by attribute, by
/// interface implementation, or by attribute-implementing-interface.
/// All methods skip abstract classes and interfaces — only concrete types
/// are returned, which is the typical DI-registration target.
/// </summary>
public static class IdevsRoslynScanner
{
    public static IEnumerable<INamedTypeSymbol> FindByAttribute(Compilation compilation, string fullAttributeName)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (string.IsNullOrEmpty(fullAttributeName)) throw new ArgumentException("fullAttributeName cannot be empty", nameof(fullAttributeName));

        return EnumerateConcreteTypes(compilation)
            .Where(t => t.HasAttributeWithFullName(fullAttributeName));
    }

    public static IEnumerable<INamedTypeSymbol> FindByInterface(Compilation compilation, params string[] fullInterfaceNames)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (fullInterfaceNames is null || fullInterfaceNames.Length == 0)
            throw new ArgumentException("At least one interface name is required.", nameof(fullInterfaceNames));

        return EnumerateConcreteTypes(compilation)
            .Where(t => fullInterfaceNames.Any(name => t.ImplementsInterface(name)));
    }

    public static IEnumerable<INamedTypeSymbol> FindByAttributeImplementingInterface(
        Compilation compilation, string fullInterfaceName)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));
        if (string.IsNullOrEmpty(fullInterfaceName)) throw new ArgumentException("fullInterfaceName cannot be empty", nameof(fullInterfaceName));

        return EnumerateConcreteTypes(compilation)
            .Where(t => t.GetAttributes().Any(a =>
                a.AttributeClass is INamedTypeSymbol attrClass &&
                attrClass.ImplementsInterface(fullInterfaceName)));
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateConcreteTypes(Compilation compilation)
    {
        return EnumerateAllTypes(compilation.GlobalNamespace)
            .Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateAllTypes(INamespaceSymbol root)
    {
        foreach (var member in root.GetMembers())
        {
            if (member is INamespaceSymbol ns)
            {
                foreach (var t in EnumerateAllTypes(ns)) yield return t;
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in EnumerateNested(type)) yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateNested(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in EnumerateNested(nested)) yield return deeper;
        }
    }
}
