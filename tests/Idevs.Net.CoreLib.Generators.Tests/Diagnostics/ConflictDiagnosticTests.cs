using System.Linq;
using Idevs.Net.CoreLib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Diagnostics;

/// <summary>
/// Tests that the generator emits IDEVSGEN001–007 and IDEVSGEN010 conflict diagnostics.
/// Uses the same manual driver approach as EmissionTests to avoid xunit version conflicts
/// with CSharpSourceGeneratorTest.
/// </summary>
public class ConflictDiagnosticTests
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
    public void IDEVSGEN001_MultipleAttributes()
    {
        const string source = """
            using Idevs.ComponentModels;
            namespace Demo;
            public interface IFoo { }
            [Scoped][Singleton]
            public class Foo : IFoo { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN001", DiagnosticSeverity.Error);
    }

    [Fact]
    public void IDEVSGEN002_MultipleMarkers()
    {
        const string source = """
            using Idevs.Repositories;
            namespace Demo;
            public interface IFoo { }
            public class Foo : IFoo, IScopedService, ISingletonService { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN002", DiagnosticSeverity.Error);
    }

    [Fact]
    public void IDEVSGEN003_AttributeMarkerDisagreement()
    {
        const string source = """
            using Idevs.ComponentModels;
            using Idevs.Repositories;
            namespace Demo;
            public interface IFoo { }
            [Scoped]
            public class Foo : IFoo, ISingletonService { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN003", DiagnosticSeverity.Error);
    }

    [Fact]
    public void IDEVSGEN004_RedundantAttributeAndMarker()
    {
        const string source = """
            using Idevs.ComponentModels;
            using Idevs.Repositories;
            namespace Demo;
            public interface IFoo { }
            [Scoped] public class Foo : IFoo, IScopedService { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN004", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void IDEVSGEN005_AmbiguousServiceType()
    {
        const string source = """
            using Idevs.Repositories;
            namespace Demo;
            public interface IBar { }
            public interface IBaz { }
            public class Foo : IBar, IBaz, IScopedService { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN005", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void IDEVSGEN006_CannotRegister_NoServiceInterfaceNoSelf()
    {
        const string source = """
            using Idevs.Repositories;
            namespace Demo;
            public class Foo : IScopedService { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN006", DiagnosticSeverity.Warning);
    }

    [Fact]
    public void IDEVSGEN007_AttributeServiceTypeVsGenericMarker()
    {
        const string source = """
            using Idevs.ComponentModels;
            using Idevs.Repositories;
            namespace Demo;
            public interface IFoo { }
            public interface IBar { }
            [Scoped(ServiceType = typeof(IFoo))]
            public class Foo : IFoo, IBar, IScopedService<IBar> { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN007", DiagnosticSeverity.Error);
    }

    [Fact]
    public void IDEVSGEN010_LegacyAttributeUsage()
    {
        const string source = """
            using Idevs.ComponentModel;
            namespace Demo;
            public interface IFoo { }
            [ScopedRegistration]
            public class Foo : IFoo { }
            """;

        var result = RunGenerator(source);
        AssertSingleDiagnostic(result, "IDEVSGEN010", DiagnosticSeverity.Warning);
    }
}
