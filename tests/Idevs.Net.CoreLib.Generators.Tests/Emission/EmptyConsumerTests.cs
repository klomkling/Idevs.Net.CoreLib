using System.Collections.Generic;
using Idevs.Net.CoreLib.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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
    // Explicitly capture the Idevs.Net.CoreLib assembly location so it is always
    // included in test compilations even if not yet JIT-loaded via AppDomain.
    private static readonly MetadataReference IdevsCoreLibRef =
        MetadataReference.CreateFromFile(typeof(Idevs.ComponentModels.ScopedAttribute).Assembly.Location);

    public static Task Verify<TGenerator>(string source) where TGenerator : IIncrementalGenerator, new()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Append(IdevsCoreLibRef);
        var compilation = CSharpCompilation.Create("ConsumerAssembly", new[] { syntaxTree }, refs);

        var generator = new TGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        return Verifier.Verify(driver).UseDirectory("../Snapshots");
    }

    public static Task VerifyWithOptions<TGenerator>(string source, AnalyzerConfigOptionsProvider optionsProvider)
        where TGenerator : IIncrementalGenerator, new()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Append(IdevsCoreLibRef);
        var compilation = CSharpCompilation.Create("ConsumerAssembly", new[] { syntaxTree }, refs);

        var driver = CSharpGeneratorDriver.Create(
            new ISourceGenerator[] { new TGenerator().AsSourceGenerator() },
            Array.Empty<AdditionalText>(),
            parseOptions: (CSharpParseOptions)syntaxTree.Options,
            optionsProvider: optionsProvider);

        GeneratorDriver runDriver = driver.RunGenerators(compilation);
        return Verifier.Verify(runDriver).UseDirectory("../Snapshots");
    }
}

public class TestOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly Dictionary<string, string> _global;
    public TestOptionsProvider(Dictionary<string, string> global) => _global = global;
    public override AnalyzerConfigOptions GlobalOptions => new TestOptions(_global);
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestOptions(_global);
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new TestOptions(_global);

    private class TestOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;
        public TestOptions(Dictionary<string, string> values) => _values = values;
        public override bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
    }
}
