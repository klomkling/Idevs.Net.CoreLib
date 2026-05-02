using Idevs.Generators.Abstractions.Emission;
using Microsoft.CodeAnalysis;

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
        // For now, register a single source output that always emits the
        // minimal wrapper. Discovery + diagnostics are added in subsequent tasks.
        context.RegisterPostInitializationOutput(ctx =>
        {
            var writer = new IdevsSourceWriter()
                .WithFileHeader()
                .WithUsings(
                    "Idevs.Extensions",
                    "Microsoft.Extensions.DependencyInjection")
                .WithNamespace("Idevs.Generated")
                .OpenClass("IdevsServiceRegistrations", isStatic: true)
                .OpenMethod("public static IServiceCollection AddIdevsServices(this IServiceCollection services)")
                .AppendLine("services.AddIdevsCorelibCore();")
                .AppendLine("return services;")
                .CloseMethod()
                .CloseClass();

            ctx.AddSource("IdevsServiceRegistrations.g.cs", writer.ToSourceText());
        });
    }
}
