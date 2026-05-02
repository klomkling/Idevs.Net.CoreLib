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
        // Pipeline: discover registrations, then emit one file.
        var attributedTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is ClassDeclarationSyntax c
                                        && c.AttributeLists.Count > 0,
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Combine(context.CompilationProvider)
            .SelectMany((pair, _) => DiscoverFromAttributes(pair.Right, pair.Left));

        var collected = attributedTypes.Collect();

        context.RegisterSourceOutput(collected, (ctx, registrations) =>
        {
            var writer = new IdevsSourceWriter()
                .WithFileHeader()
                .WithUsings("Idevs.Extensions", "Microsoft.Extensions.DependencyInjection")
                .WithNamespace("Idevs.Generated")
                .OpenClass("IdevsServiceRegistrations", isStatic: true)
                .OpenMethod("public static IServiceCollection AddIdevsServices(this IServiceCollection services)")
                .AppendLine("services.AddIdevsCorelibCore();");

            var sorted = registrations
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

            writer
                .AppendLine()
                .AppendLine("return services;")
                .CloseMethod()
                .CloseClass();

            ctx.AddSource("IdevsServiceRegistrations.g.cs", writer.ToSourceText());
        });
    }

    private static IEnumerable<RegistrationRecord> DiscoverFromAttributes(
        Compilation compilation,
        ClassDeclarationSyntax classDecl)
    {
        var model = compilation.GetSemanticModel(classDecl.SyntaxTree);
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol type) yield break;
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) yield break;

        foreach (var attr in type.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass is null) continue;

            var lifetime = ResolveAttributeLifetime(attrClass);
            if (lifetime is null) continue;

            if (!ServiceTypeResolver.TryResolveByConvention(type, out var serviceType)) continue;

            var implFullName = "global::" + type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
            var serviceFullName = "global::" + serviceType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");

            yield return new RegistrationRecord(implFullName, serviceFullName, lifetime);
        }
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
