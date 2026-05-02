using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Idevs.Generators.Abstractions.Emission;
using Idevs.Generators.Abstractions.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Idevs.Net.CoreLib.Generators;


/// <summary>
/// Idevs DI source generator. Emits one file:
/// <c>Idevs.Generated.IdevsServiceRegistrations.AddIdevsServices(IServiceCollection)</c>
/// in the consumer's assembly. The method calls <c>AddIdevsCorelibCore()</c>
/// followed by every discovered registration (attributes + markers + registrars)
/// or, when the MSBuild flag is off, by <c>AddIdevsCorelibLegacyScan()</c>.
/// </summary>
[Generator]
public sealed class IdevsServiceRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline 1: discover registrations (attributes + marker interfaces).
        var attributedTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .SelectMany((pair, _) => DiscoverRegistrations(pair.Right, pair.Left));

        var collectedRegs = attributedTypes.Collect();

        // Pipeline 2: discover IIdevsServiceRegistrar implementations.
        var registrarSyntax = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .SelectMany((pair, _) => DiscoverRegistrars(pair.Right, pair.Left));

        var collectedRegistrars = registrarSyntax.Collect();

        var combined = collectedRegs.Combine(collectedRegistrars);
        context.RegisterSourceOutput(combined, (ctx, both) =>
        {
            var (registrations, registrars) = both;

            var writer = new IdevsSourceWriter()
                .WithFileHeader()
                .WithUsings("Idevs.Extensions", "Microsoft.Extensions.DependencyInjection")
                .WithNamespace("Idevs.Generated")
                .OpenClass("IdevsServiceRegistrations", isStatic: true)
                .OpenMethod("public static IServiceCollection AddIdevsServices(this IServiceCollection services)")
                .AppendLine("services.AddIdevsCorelibCore();");

            // De-dup by (impl, service) pair — prevents double registration when a class
            // uses both an attribute and a marker interface. Task 18 will emit IDEVSGEN004
            // for that case; for now we simply take first.
            var sorted = registrations
                .GroupBy(r => (r.ImplementationFullName, r.ServiceFullName))
                .Select(g => g.First())
                .OrderBy(r => r.ImplementationFullName, System.StringComparer.Ordinal)
                .ToImmutableArray();

            if (sorted.Length > 0)
            {
                writer.AppendLine();
                foreach (var reg in sorted)
                {
                    writer.AppendLine($"services.Add{reg.Lifetime}<{reg.ServiceFullName}, {reg.ImplementationFullName}>();");
                }
            }

            var sortedRegistrars = registrars
                .OrderBy(r => r, System.StringComparer.Ordinal)
                .ToImmutableArray();

            if (sortedRegistrars.Length > 0)
            {
                writer.AppendLine();
                foreach (var registrar in sortedRegistrars)
                {
                    writer.AppendLine($"new {registrar}().Register(services);");
                }
            }

            writer
                .AppendLine()
                .AppendLine("return services;")
                .CloseMethod()
                .CloseClass();

            ctx.AddSource("IdevsServiceRegistrations.g.cs", writer.ToSourceText());
        });
    }

    private static IEnumerable<RegistrationRecord> DiscoverRegistrations(
        Compilation compilation,
        ClassDeclarationSyntax classDecl)
    {
        var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol type) yield break;
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) yield break;

        // ----- Path 1: attribute-based discovery -----
        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            var lifetime = ResolveAttributeLifetime(attrClass);
            if (lifetime is null) continue;

            if (!ServiceTypeResolver.TryResolveByConvention(type, out var serviceType)) continue;

            yield return new RegistrationRecord(ToGlobalQualified(type), ToGlobalQualified(serviceType!), lifetime);
        }

        // ----- Path 2: marker-interface discovery -----
        var markerLifetime = ResolveMarkerLifetime(type);
        if (markerLifetime is not null)
        {
            // Check for generic marker first (pins ServiceType explicitly).
            INamedTypeSymbol? serviceTypeFromMarker = null;
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.IsGenericType && iface.TypeArguments.Length == 1)
                {
                    var origDef = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (origDef == "global::Idevs.Repositories.IScopedService<TService>"
                        || origDef == "global::Idevs.Repositories.ISingletonService<TService>"
                        || origDef == "global::Idevs.Repositories.ITransientService<TService>")
                    {
                        if (iface.TypeArguments[0] is INamedTypeSymbol named)
                        {
                            serviceTypeFromMarker = named;
                            break;
                        }
                    }
                }
            }

            if (serviceTypeFromMarker is null)
            {
                // No generic marker — fall back to I{ClassName} convention.
                if (!ServiceTypeResolver.TryResolveByConvention(type, out serviceTypeFromMarker)) yield break;
            }

            yield return new RegistrationRecord(ToGlobalQualified(type), ToGlobalQualified(serviceTypeFromMarker!), markerLifetime);
        }
    }

    private static IEnumerable<string> DiscoverRegistrars(
        Compilation compilation,
        ClassDeclarationSyntax classDecl)
    {
        var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol type) yield break;
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) yield break;

        if (!type.AllInterfaces.Any(i =>
                i.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::Idevs.Repositories.IIdevsServiceRegistrar"))
            yield break;

        var hasParameterlessCtor = type.Constructors.Any(c =>
            c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public);
        if (!hasParameterlessCtor) yield break;

        yield return ToGlobalQualified(type);
    }

    private static string? ResolveAttributeLifetime(INamedTypeSymbol attrClass)
    {
        var name = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return name switch
        {
            "global::Idevs.ComponentModels.ScopedAttribute"                 => "Scoped",
            "global::Idevs.ComponentModels.SingletonAttribute"              => "Singleton",
            "global::Idevs.ComponentModels.TransientAttribute"              => "Transient",
            // Legacy attribute names (still supported in 0.7.0):
            "global::Idevs.ComponentModel.ScopedRegistrationAttribute"     => "Scoped",
            "global::Idevs.ComponentModel.SingletonRegiatrationAttribute"   => "Singleton",
            "global::Idevs.ComponentModel.TransientRegistrationAttribute"   => "Transient",
            _ => null
        };
    }

    private static string? ResolveMarkerLifetime(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            var fullName = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            switch (fullName)
            {
                case "global::Idevs.Repositories.IScopedService":
                case "global::Idevs.Repositories.IScopedService<TService>":
                    return "Scoped";
                case "global::Idevs.Repositories.ISingletonService":
                case "global::Idevs.Repositories.ISingletonService<TService>":
                    return "Singleton";
                case "global::Idevs.Repositories.ITransientService":
                case "global::Idevs.Repositories.ITransientService<TService>":
                    return "Transient";
            }
        }
        return null;
    }

    private static string ToGlobalQualified(INamedTypeSymbol s) =>
        "global::" + s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    private sealed class RegistrationRecord
    {
        public string ImplementationFullName { get; }
        public string ServiceFullName { get; }
        public string Lifetime { get; }

        public RegistrationRecord(string implementationFullName, string serviceFullName, string lifetime)
        {
            ImplementationFullName = implementationFullName;
            ServiceFullName = serviceFullName;
            Lifetime = lifetime;
        }
    }
}
