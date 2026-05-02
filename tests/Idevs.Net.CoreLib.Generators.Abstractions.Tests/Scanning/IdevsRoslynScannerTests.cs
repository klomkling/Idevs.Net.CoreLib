using Idevs.Generators.Abstractions.Scanning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Abstractions.Tests.Scanning;

public class IdevsRoslynScannerTests
{
    private static Compilation Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
        return CSharpCompilation.Create("Test", new[] { tree }, refs);
    }

    [Fact]
    public void FindByAttribute_ReturnsAttributedClasses()
    {
        var c = Compile(@"
            public class TaggedAttribute : System.Attribute {}
            [Tagged] public class A {}
            public class B {}
            [Tagged] public class C {}
        ");
        var found = IdevsRoslynScanner.FindByAttribute(c, "TaggedAttribute").Select(t => t.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "C" }, found);
    }

    [Fact]
    public void FindByInterface_ReturnsImplementingClasses()
    {
        var c = Compile(@"
            public interface IMarker {}
            public class A : IMarker {}
            public class B {}
            public class C : A {}  // transitive implementer
        ");
        var found = IdevsRoslynScanner.FindByInterface(c, "IMarker").Select(t => t.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "C" }, found);
    }

    [Fact]
    public void FindByInterface_AcceptsMultipleNames()
    {
        var c = Compile(@"
            public interface IA {} public interface IB {}
            public class HasA : IA {}
            public class HasB : IB {}
            public class HasNeither {}
        ");
        var found = IdevsRoslynScanner.FindByInterface(c, "IA", "IB").Select(t => t.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "HasA", "HasB" }, found);
    }

    [Fact]
    public void FindByAttributeImplementingInterface_FindsAttributesByMarkerInterface()
    {
        var c = Compile(@"
            public interface IMarkerAttribute {}
            public class XAttribute : System.Attribute, IMarkerAttribute {}
            public class YAttribute : System.Attribute {}
            [X] public class A {}
            [Y] public class B {}
            [X] public class C {}
        ");
        var found = IdevsRoslynScanner.FindByAttributeImplementingInterface(c, "IMarkerAttribute")
            .Select(t => t.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "A", "C" }, found);
    }

    [Fact]
    public void FindByAttribute_SkipsAbstractClasses()
    {
        var c = Compile(@"
            public class TaggedAttribute : System.Attribute {}
            [Tagged] public abstract class A {}
            [Tagged] public class B {}
        ");
        var found = IdevsRoslynScanner.FindByAttribute(c, "TaggedAttribute").Select(t => t.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "B" }, found);
    }
}
