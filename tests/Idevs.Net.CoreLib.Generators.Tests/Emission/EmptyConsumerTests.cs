using Idevs.Net.CoreLib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class EmptyConsumerTests
{
    static EmptyConsumerTests() => VerifySourceGenerators.Initialize();

    [Fact]
    public Task EmptyConsumer_EmitsWrapperCallingCoreOnly()
    {
        var source = "// empty consumer";
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }
}

internal static class TestHelpers
{
    public static Task Verify<TGenerator>(string source) where TGenerator : IIncrementalGenerator, new()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        var compilation = CSharpCompilation.Create("ConsumerAssembly", new[] { syntaxTree }, refs);

        var generator = new TGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        return Verifier.Verify(driver).UseDirectory("../Snapshots");
    }
}
