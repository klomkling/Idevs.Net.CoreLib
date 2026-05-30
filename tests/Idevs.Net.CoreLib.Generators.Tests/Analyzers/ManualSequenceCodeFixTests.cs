using System.Threading.Tasks;
using Idevs.Net.CoreLib.CodeFixes;
using Idevs.Net.CoreLib.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

public class ManualSequenceCodeFixTests
{
    [Fact]
    public async Task ReplacesMaxPlusOneWithNextAsync()
    {
        var before = @"
using System.Linq;
public class S {
    public int Next(System.Collections.Generic.IEnumerable<int> rows)
        => {|#0:rows.Max() + 1|};
}";
        var after = @"
using System.Linq;
public class S {
    public int Next(System.Collections.Generic.IEnumerable<int> rows)
        => await sequenceProvider.NextAsync(""TODO-sequence-key"");
}";
        var expected = new DiagnosticResult("IDEVSGEN103", DiagnosticSeverity.Info).WithLocation(0);
        await new VerifyFix<ManualSequenceAnalyzer, ManualSequenceCodeFixProvider>.Test
        {
            TestCode = before,
            FixedCode = after,
            ExpectedDiagnostics = { expected },
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync();
    }
}
