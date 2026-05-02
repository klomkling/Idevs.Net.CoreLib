using Idevs.Generators.Abstractions.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Abstractions.Tests.Scanning;

public class INamedTypeSymbolExtensionsTests
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
    public void ImplementsInterface_ReturnsTrue_WhenTypeImplementsTheInterface()
    {
        var t = GetType("public interface IFoo {} public class Foo : IFoo {}", "Foo");
        Assert.True(t.ImplementsInterface("IFoo"));
    }

    [Fact]
    public void ImplementsInterface_ReturnsTrue_ForTransitiveImplementation()
    {
        var t = GetType(
            "public interface IBase {} public interface IDerived : IBase {} public class Foo : IDerived {}",
            "Foo");
        Assert.True(t.ImplementsInterface("IBase"));
    }

    [Fact]
    public void ImplementsInterface_ReturnsFalse_WhenTypeDoesNotImplement()
    {
        var t = GetType("public class Foo {}", "Foo");
        Assert.False(t.ImplementsInterface("IFoo"));
    }

    [Fact]
    public void HasAttributeWithFullName_ReturnsTrue_WhenAttributePresent()
    {
        var t = GetType(
            "public class MarkAttribute : System.Attribute {} [Mark] public class Foo {}",
            "Foo");
        Assert.True(t.HasAttributeWithFullName("MarkAttribute"));
    }

    [Fact]
    public void HasAttributeWithFullName_ReturnsFalse_WhenAttributeAbsent()
    {
        var t = GetType("public class Foo {}", "Foo");
        Assert.False(t.HasAttributeWithFullName("MarkAttribute"));
    }

    [Fact]
    public void HasAttributeWithFullName_DoesNotMatchHomonymousAttributeInDifferentNamespace()
    {
        // Regression: a consumer-defined attribute with the same short name as a
        // well-known one must not produce a false positive when looked up by FQN.
        var t = GetType(
            "namespace Acme { public class ScopedAttribute : System.Attribute {} [Scoped] public class Foo {} }",
            "Acme.Foo");
        Assert.False(t.HasAttributeWithFullName("Idevs.ComponentModels.ScopedAttribute"));
        Assert.True(t.HasAttributeWithFullName("Acme.ScopedAttribute"));
    }

    [Fact]
    public void ImplementsInterface_DoesNotMatchHomonymousInterfaceInDifferentNamespace()
    {
        // Regression: same short-name interface in another namespace must not match.
        var t = GetType(
            "namespace Acme { public interface IFoo {} public class Foo : IFoo {} }",
            "Acme.Foo");
        Assert.False(t.ImplementsInterface("Other.IFoo"));
        Assert.True(t.ImplementsInterface("Acme.IFoo"));
    }
}
