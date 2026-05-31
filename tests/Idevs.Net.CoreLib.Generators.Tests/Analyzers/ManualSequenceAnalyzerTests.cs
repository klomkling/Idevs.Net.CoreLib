using System.Threading.Tasks;
using Idevs.Net.CoreLib.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

public class ManualSequenceAnalyzerTests
{
    [Fact]
    public async Task MaxPlusOne_Flagged()
    {
        var src = @"
using System.Linq;
public class S {
    public int Next(System.Collections.Generic.IEnumerable<int> rows)
        => {|#0:rows.Max() + 1|};
}";
        var expected = new DiagnosticResult("IDEVSGEN103", DiagnosticSeverity.Info).WithLocation(0);
        await new Verify<ManualSequenceAnalyzer>.Test { TestCode = src, ExpectedDiagnostics = { expected } }.RunAsync();
    }

    // Stub with all four recognized method names returning int, so `q.NAME() + 1`
    // compiles and the syntactic name match is exercised for each.
    private const string Stubs = @"
public class Q { public int Max() => 0; public int MaxAsync() => 0; public int Count() => 0; public int CountAsync() => 0; }
";

    [Theory]
    [InlineData("Max")]
    [InlineData("MaxAsync")]
    [InlineData("Count")]
    [InlineData("CountAsync")]
    public async Task RecognizedMethodPlusOne_Flagged(string method)
    {
        var src = Stubs + @"
public class S { public int Next(Q q) => {|#0:q.NAME() + 1|}; }".Replace("NAME", method);
        var expected = new DiagnosticResult("IDEVSGEN103", DiagnosticSeverity.Info).WithLocation(0);
        await new Verify<ManualSequenceAnalyzer>.Test { TestCode = src, ExpectedDiagnostics = { expected } }.RunAsync();
    }

    [Fact]
    public async Task PlainAddition_NotFlagged()
    {
        var src = @"
public class S { public int Next(int x) => x + 1; }";
        await new Verify<ManualSequenceAnalyzer>.Test { TestCode = src }.RunAsync();
    }

    [Fact]
    public async Task UnrecognizedMethodPlusOne_NotFlagged()
    {
        // Only Max/MaxAsync/Count/CountAsync are recognized; a generic call is not.
        var src = @"
public class S { private int Get() => 0; public int Next() => Get() + 1; }";
        await new Verify<ManualSequenceAnalyzer>.Test { TestCode = src }.RunAsync();
    }
}
