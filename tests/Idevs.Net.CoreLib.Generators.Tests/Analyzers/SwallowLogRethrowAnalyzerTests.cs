using System.Threading.Tasks;
using Idevs.Net.CoreLib.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

public class SwallowLogRethrowAnalyzerTests
{
    private const string Stubs = @"
public interface ILogger { void LogError(System.Exception e, string m); void LogCritical(System.Exception e, string m); }
";

    [Fact]
    public async Task LogAndRethrow_Flagged()
    {
        var src = Stubs + @"
public class S {
    private ILogger _log = null!;
    public void M() {
        try { } {|#0:catch|} (System.Exception ex) { _log.LogError(ex, ""x""); throw; }
    }
}";
        var expected = new DiagnosticResult("IDEVSGEN101", DiagnosticSeverity.Warning).WithLocation(0);
        await new Verify<SwallowLogRethrowAnalyzer>.Test { TestCode = src, ExpectedDiagnostics = { expected } }.RunAsync();
    }

    [Fact]
    public async Task LogAndTranslate_NotFlagged()
    {
        var src = Stubs + @"
public class S {
    private ILogger _log = null!;
    public void M() {
        try { } catch (System.Exception ex) { _log.LogError(ex, ""x""); throw new System.InvalidOperationException(""wrapped"", ex); }
    }
}";
        await new Verify<SwallowLogRethrowAnalyzer>.Test { TestCode = src }.RunAsync();
    }

    [Fact]
    public async Task RethrowWithoutLog_NotFlagged()
    {
        var src = @"
public class S {
    public void M() { try { } catch { throw; } }
}";
        await new Verify<SwallowLogRethrowAnalyzer>.Test { TestCode = src }.RunAsync();
    }
}
