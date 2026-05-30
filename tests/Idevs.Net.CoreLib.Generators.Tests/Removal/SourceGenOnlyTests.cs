using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Removal;

public class SourceGenOnlyTests
{
    [Fact]
    public void Generator_NeverEmits_LegacyScanCall()
    {
        var source = @"
using Idevs.ComponentModels;
[Scoped] public class Foo : IFoo { }
public interface IFoo { }";
        var compilation = CSharpCompilation.Create(
            "TestAsm",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new IdevsServiceRegistrationGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = output.SyntaxTrees
            .Where(t => t.FilePath.Contains("IdevsServiceRegistrations"))
            .Select(t => t.ToString());

        Assert.DoesNotContain(generated, g => g.Contains("AddIdevsCorelibLegacyScan"));
    }
}
