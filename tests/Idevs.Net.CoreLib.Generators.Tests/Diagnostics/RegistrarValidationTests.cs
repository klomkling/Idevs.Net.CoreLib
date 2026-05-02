using System.Linq;
using Idevs.Net.CoreLib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Diagnostics;

/// <summary>
/// Tests that the generator emits IDEVSGEN008 and IDEVSGEN009 diagnostics
/// for invalid IIdevsServiceRegistrar implementations.
/// Uses the same manual driver approach as ConflictDiagnosticTests.
/// </summary>
public class RegistrarValidationTests
{
    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var idevsCoreLibRef = MetadataReference.CreateFromFile(
            typeof(Idevs.ComponentModels.ScopedAttribute).Assembly.Location);

        var refs = System.AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .Append(idevsCoreLibRef);

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new IdevsServiceRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static void AssertSingleDiagnostic(
        GeneratorDriverRunResult result,
        string expectedId,
        DiagnosticSeverity expectedSeverity)
    {
        var diags = result.Diagnostics;
        var matching = diags.Where(d => d.Id == expectedId).ToList();

        Assert.True(matching.Count > 0,
            $"Expected diagnostic {expectedId} but found none. " +
            $"Actual: [{string.Join(", ", diags.Select(d => d.Id))}]");

        Assert.Equal(expectedSeverity, matching[0].Severity);
    }

    [Fact]
    public void IDEVSGEN008_RegistrarMissingPublicCtor()
    {
        const string source = """
            using Idevs.Repositories;
            using Microsoft.Extensions.DependencyInjection;
            namespace Demo;
            public class MyReg : IIdevsServiceRegistrar
            {
                public MyReg(int x) { }
                public void Register(IServiceCollection services) { }
            }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN008", DiagnosticSeverity.Error);
    }

    [Fact]
    public void IDEVSGEN009_RegistrarIsInternal()
    {
        const string source = """
            using Idevs.Repositories;
            using Microsoft.Extensions.DependencyInjection;
            namespace Demo;
            internal class MyReg : IIdevsServiceRegistrar
            {
                public void Register(IServiceCollection services) { }
            }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN009", DiagnosticSeverity.Warning);
    }
}
