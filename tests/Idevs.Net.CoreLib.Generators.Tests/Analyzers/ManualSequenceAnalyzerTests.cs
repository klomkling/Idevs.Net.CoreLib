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

    [Fact]
    public async Task PlainAddition_NotFlagged()
    {
        var src = @"
public class S { public int Next(int x) => x + 1; }";
        await new Verify<ManualSequenceAnalyzer>.Test { TestCode = src }.RunAsync();
    }
}
