using Idevs.Generators.Abstractions.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Abstractions.Tests.Scanning;

public class ServiceTypeResolverTests
{
    private static INamedTypeSymbol GetType(string source, string typeName)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, refs);
        var symbol = compilation.GetTypeByMetadataName(typeName);
        Assert.NotNull(symbol);
        return symbol!;
    }

    [Fact]
    public void TryResolve_UsesIClassNameConvention_WhenInterfaceMatches()
    {
        var t = GetType("public interface IFoo {} public class Foo : IFoo {}", "Foo");
        var ok = ServiceTypeResolver.TryResolveByConvention(t, out var serviceType);
        Assert.True(ok);
        Assert.Equal("IFoo", serviceType!.Name);
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenNoConventionMatch_AndAllowSelfFalse()
    {
        var t = GetType("public class Foo {}", "Foo");
        var ok = ServiceTypeResolver.TryResolveByConvention(t, out var _);
        Assert.False(ok);
    }

    [Fact]
    public void TryResolve_ReturnsTrue_WithSelfType_WhenAllowSelfTrue()
    {
        var t = GetType("public class Foo {}", "Foo");
        var ok = ServiceTypeResolver.TryResolveWithSelfRegistration(t, allowSelfRegistration: true, out var serviceType);
        Assert.True(ok);
        Assert.Equal("Foo", serviceType!.Name);
    }

    [Fact]
    public void TryResolve_DoesNotPickAmbiguousInterface_WhenNoIClassNameMatch()
    {
        var t = GetType(@"
            public interface IBar {}
            public interface IBaz {}
            public class Foo : IBar, IBaz {}
        ", "Foo");
        var ok = ServiceTypeResolver.TryResolveByConvention(t, out var _);
        Assert.False(ok); // ambiguous; no I{ClassName} match
    }
}
