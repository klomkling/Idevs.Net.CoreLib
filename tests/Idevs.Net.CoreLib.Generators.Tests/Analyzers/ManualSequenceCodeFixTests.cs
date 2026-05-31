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
            // The fix is a deliberately-incomplete scaffold: it references an
            // injected `sequenceProvider`, a placeholder key, and (here) uses await
            // in a sync method — so the fixed code does not compile by design.
            // CompilerDiagnostics.None tolerates that; CodeActionEquivalenceKey pins
            // the action's title so the "manual follow-up" contract can't silently change.
            CompilerDiagnostics = CompilerDiagnostics.None,
            CodeActionEquivalenceKey = "Scaffold ISequenceProvider.NextAsync (inject provider, name key, make async)",
        }.RunAsync();
    }
}
