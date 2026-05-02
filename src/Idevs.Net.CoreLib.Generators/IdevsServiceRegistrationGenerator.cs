using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Idevs.Generators.Abstractions.Emission;
using Idevs.Generators.Abstractions.Scanning;
using Idevs.Generators.Abstractions.Validation;
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
        // Pipeline 1: collect all candidate class info for registration + diagnostics.
        var discoveredTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .SelectMany((pair, _) => CollectTypeInfo(pair.Right, pair.Left));

        var collectedTypes = discoveredTypes.Collect();

        // Pipeline 2: discover IIdevsServiceRegistrar implementations.
        var registrarSyntax = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .SelectMany((pair, _) => DiscoverRegistrars(pair.Right, pair.Left));

        var collectedRegistrars = registrarSyntax.Collect();

        var combined = collectedTypes.Combine(collectedRegistrars);
        context.RegisterSourceOutput(combined, (ctx, both) =>
        {
            var (typeInfos, registrars) = both;

            var writer = new IdevsSourceWriter()
                .WithFileHeader()
                .WithUsings("Idevs.Extensions", "Microsoft.Extensions.DependencyInjection")
                .WithNamespace("Idevs.Generated")
                .OpenClass("IdevsServiceRegistrations", isStatic: true)
                .OpenMethod("public static IServiceCollection AddIdevsServices(this IServiceCollection services)")
                .AppendLine("services.AddIdevsCorelibCore();");

            var registrations = new List<RegistrationRecord>();

            foreach (var info in typeInfos)
            {
                ProcessTypeInfo(ctx, info, registrations);
            }

            // De-dup by (impl, service) pair and sort for deterministic output.
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

    // -------------------------------------------------------------------------
    // CollectTypeInfo: gathers all info needed for conflict detection + emission
    // -------------------------------------------------------------------------

    private static IEnumerable<DiscoveredTypeInfo> CollectTypeInfo(
        Compilation compilation,
        ClassDeclarationSyntax classDecl)
    {
        var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol type) yield break;
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) yield break;

        // Collect all lifetime attributes on the type.
        var lifetimeAttrs = new List<(AttributeData Attr, string Lifetime, bool IsLegacy)>();
        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;
            var (lifetime, isLegacy) = ResolveAttributeLifetimeWithLegacyFlag(attrClass);
            if (lifetime is null) continue;
            lifetimeAttrs.Add((attr, lifetime, isLegacy));
        }

        // Collect marker interface lifetimes.
        var markerLifetimes = CollectMarkerLifetimes(type);

        // Collect generic marker service type.
        INamedTypeSymbol? genericMarkerServiceType = null;
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
                        genericMarkerServiceType = named;
                        break;
                    }
                }
            }
        }

        // Read ServiceType named arg from attribute (e.g. [Scoped(ServiceType = typeof(IFoo))]).
        INamedTypeSymbol? attributeServiceType = null;
        if (lifetimeAttrs.Count == 1)
        {
            var namedArgs = lifetimeAttrs[0].Attr.NamedArguments;
            foreach (var kv in namedArgs)
            {
                if (kv.Key == "ServiceType" && kv.Value.Value is INamedTypeSymbol svc)
                {
                    attributeServiceType = svc;
                    break;
                }
            }
        }

        // Only yield if there's something to process.
        if (lifetimeAttrs.Count == 0 && markerLifetimes.Count == 0) yield break;

        yield return new DiscoveredTypeInfo(
            type: type,
            classDecl: classDecl,
            lifetimeAttrs: lifetimeAttrs,
            markerLifetimes: markerLifetimes,
            genericMarkerServiceType: genericMarkerServiceType,
            attributeServiceType: attributeServiceType);
    }

    private static List<string> CollectMarkerLifetimes(INamedTypeSymbol type)
    {
        var lifetimes = new List<string>();
        foreach (var iface in type.AllInterfaces)
        {
            var fullName = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            switch (fullName)
            {
                case "global::Idevs.Repositories.IScopedService":
                case "global::Idevs.Repositories.IScopedService<TService>":
                    if (!lifetimes.Contains("Scoped")) lifetimes.Add("Scoped");
                    break;
                case "global::Idevs.Repositories.ISingletonService":
                case "global::Idevs.Repositories.ISingletonService<TService>":
                    if (!lifetimes.Contains("Singleton")) lifetimes.Add("Singleton");
                    break;
                case "global::Idevs.Repositories.ITransientService":
                case "global::Idevs.Repositories.ITransientService<TService>":
                    if (!lifetimes.Contains("Transient")) lifetimes.Add("Transient");
                    break;
            }
        }
        return lifetimes;
    }

    // -------------------------------------------------------------------------
    // ProcessTypeInfo: apply conflict rules, emit diagnostics, build registrations
    // -------------------------------------------------------------------------

    private static void ProcessTypeInfo(
        SourceProductionContext ctx,
        DiscoveredTypeInfo info,
        List<RegistrationRecord> registrations)
    {
        var classLocation = info.ClassDecl.Identifier.GetLocation();
        var typeName = info.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var typeFullDisplayName = info.Type.ToDisplayString();

        // IDEVSGEN001: Multiple lifetime attributes.
        if (info.LifetimeAttrs.Count > 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleLifetimeAttributes,
                classLocation,
                typeFullDisplayName));
            return;
        }

        // IDEVSGEN002: Multiple lifetime markers (distinct lifetimes).
        if (info.MarkerLifetimes.Count > 1)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MultipleLifetimeMarkers,
                classLocation,
                typeFullDisplayName));
            return;
        }

        var attrLifetimeStr = info.LifetimeAttrs.Count == 1 ? info.LifetimeAttrs[0].Lifetime : null;
        var markerLifetimeStr = info.MarkerLifetimes.Count == 1 ? info.MarkerLifetimes[0] : null;

        // Convert string lifetimes to enum for LifetimeResolver.
        var attrLifetime = ParseLifetime(attrLifetimeStr);
        var markerLifetime = ParseLifetime(markerLifetimeStr);

        // IDEVSGEN003 / IDEVSGEN004: Attribute + marker conflict resolution.
        string? resolvedLifetimeStr = attrLifetimeStr ?? markerLifetimeStr;
        if (attrLifetime.HasValue && markerLifetime.HasValue)
        {
            var (_, conflict) = LifetimeResolver.Resolve(attrLifetime, markerLifetime);
            if (conflict == LifetimeConflict.Disagreement)
            {
                // IDEVSGEN003: Attribute and marker disagree.
                ctx.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AttributeMarkerDisagreement,
                    classLocation,
                    typeFullDisplayName));
                return;
            }
            if (conflict == LifetimeConflict.Redundant)
            {
                // IDEVSGEN004: Redundant — warn but continue with attribute lifetime.
                ctx.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.RedundantLifetimeAttributeAndMarker,
                    classLocation,
                    typeFullDisplayName));
                // continue to emit using attrLifetimeStr
            }
        }

        // IDEVSGEN010: Legacy attribute usage — warn but continue.
        if (info.LifetimeAttrs.Count == 1 && info.LifetimeAttrs[0].IsLegacy)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.LegacyAttributeUsage,
                classLocation,
                typeFullDisplayName));
            // continue emission
        }

        // IDEVSGEN007: Attribute ServiceType conflicts with generic marker service type.
        if (info.AttributeServiceType is not null && info.GenericMarkerServiceType is not null)
        {
            var attrSvcFull = info.AttributeServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var markerSvcFull = info.GenericMarkerServiceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (attrSvcFull != markerSvcFull)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.AttributeServiceTypeConflictsWithGenericMarker,
                    classLocation,
                    typeFullDisplayName));
                return;
            }
        }

        // Determine final service type.
        INamedTypeSymbol? serviceType = null;

        if (info.AttributeServiceType is not null)
        {
            serviceType = info.AttributeServiceType;
        }
        else if (info.GenericMarkerServiceType is not null)
        {
            serviceType = info.GenericMarkerServiceType;
        }
        else
        {
            // Fall back to I{ClassName} convention.
            ServiceTypeResolver.TryResolveByConvention(info.Type, out serviceType);
        }

        if (serviceType is null)
        {
            // Determine which specific warning to emit.
            if (attrLifetimeStr is null && markerLifetimeStr is not null)
            {
                // Marker-only path: no service type found.
                // Check if it's ambiguous (multiple non-IScopedService interfaces) vs just missing.
                var candidateInterfaces = info.Type.AllInterfaces
                    .Where(i => !IsMarkerInterface(i))
                    .ToList();

                if (candidateInterfaces.Count > 1)
                {
                    // IDEVSGEN005: Ambiguous service type.
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AmbiguousServiceType,
                        classLocation,
                        typeFullDisplayName));
                }
                else
                {
                    // IDEVSGEN006: Cannot register — no service interface.
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.CannotRegister,
                        classLocation,
                        typeFullDisplayName,
                        "no matching service interface found and AllowSelfRegistration is false"));
                }
            }
            else if (attrLifetimeStr is not null)
            {
                // Attribute-only path: convention failed.
                // Could be ambiguous or simply missing.
                var candidateInterfaces = info.Type.AllInterfaces
                    .Where(i => !IsMarkerInterface(i))
                    .ToList();

                if (candidateInterfaces.Count > 1)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.AmbiguousServiceType,
                        classLocation,
                        typeFullDisplayName));
                }
                // else: convention just didn't match — skip silently (no IFoo interface)
            }
            return;
        }

        registrations.Add(new RegistrationRecord(
            ToGlobalQualified(info.Type),
            ToGlobalQualified(serviceType),
            resolvedLifetimeStr!));
    }

    private static bool IsMarkerInterface(INamedTypeSymbol iface)
    {
        var fullName = iface.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName is
            "global::Idevs.Repositories.IScopedService" or
            "global::Idevs.Repositories.IScopedService<TService>" or
            "global::Idevs.Repositories.ISingletonService" or
            "global::Idevs.Repositories.ISingletonService<TService>" or
            "global::Idevs.Repositories.ITransientService" or
            "global::Idevs.Repositories.ITransientService<TService>" or
            "global::Idevs.Repositories.IIdevsServiceRegistrar";
    }

    private static Lifetime? ParseLifetime(string? s) => s switch
    {
        "Scoped" => Lifetime.Scoped,
        "Singleton" => Lifetime.Singleton,
        "Transient" => Lifetime.Transient,
        _ => null
    };

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

    private static (string? Lifetime, bool IsLegacy) ResolveAttributeLifetimeWithLegacyFlag(INamedTypeSymbol attrClass)
    {
        var name = attrClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return name switch
        {
            "global::Idevs.ComponentModels.ScopedAttribute"                 => ("Scoped", false),
            "global::Idevs.ComponentModels.SingletonAttribute"              => ("Singleton", false),
            "global::Idevs.ComponentModels.TransientAttribute"              => ("Transient", false),
            // Legacy attribute names (still supported in 0.7.0):
            "global::Idevs.ComponentModel.ScopedRegistrationAttribute"     => ("Scoped", true),
            "global::Idevs.ComponentModel.SingletonRegiatrationAttribute"   => ("Singleton", true),
            "global::Idevs.ComponentModel.TransientRegistrationAttribute"   => ("Transient", true),
            _ => (null, false)
        };
    }

    private static string ToGlobalQualified(INamedTypeSymbol s) =>
        "global::" + s.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

    // -------------------------------------------------------------------------
    // Data types
    // -------------------------------------------------------------------------

    private sealed class DiscoveredTypeInfo
    {
        public INamedTypeSymbol Type { get; }
        public ClassDeclarationSyntax ClassDecl { get; }
        public List<(AttributeData Attr, string Lifetime, bool IsLegacy)> LifetimeAttrs { get; }
        public List<string> MarkerLifetimes { get; }
        public INamedTypeSymbol? GenericMarkerServiceType { get; }
        public INamedTypeSymbol? AttributeServiceType { get; }

        public DiscoveredTypeInfo(
            INamedTypeSymbol type,
            ClassDeclarationSyntax classDecl,
            List<(AttributeData Attr, string Lifetime, bool IsLegacy)> lifetimeAttrs,
            List<string> markerLifetimes,
            INamedTypeSymbol? genericMarkerServiceType,
            INamedTypeSymbol? attributeServiceType)
        {
            Type = type;
            ClassDecl = classDecl;
            LifetimeAttrs = lifetimeAttrs;
            MarkerLifetimes = markerLifetimes;
            GenericMarkerServiceType = genericMarkerServiceType;
            AttributeServiceType = attributeServiceType;
        }
    }

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
