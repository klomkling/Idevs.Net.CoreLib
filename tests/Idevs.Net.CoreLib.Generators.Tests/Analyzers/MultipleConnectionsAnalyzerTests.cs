using System.Threading.Tasks;
using Idevs.Net.CoreLib.Generators.Analyzers;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

public class MultipleConnectionsAnalyzerTests
{
    // Inline stubs so the test compiles without Serenity references; the analyzer
    // matches by simple type/method name, so these stand in for the real types.
    private const string Stubs = @"
public interface ISqlConnections { System.Data.IDbConnection NewByKey(string key); }
public sealed class UnitOfWorkScope : System.IDisposable { public void Dispose() {} }
";

    [Fact]
    public async Task TwoOpens_NoUnitOfWork_Flagged()
    {
        var src = Stubs + @"
public class Repo {
    private ISqlConnections _c = null!;
    public void {|#0:Work|}() {
        var a = _c.NewByKey(""Default"");
        var b = _c.NewByKey(""Default"");
    }
}";
        var expected = new DiagnosticResult("IDEVSGEN100", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(0).WithArguments("Work", "2");
        await new Verify<MultipleConnectionsAnalyzer>.Test { TestCode = src, ExpectedDiagnostics = { expected } }
            .RunAsync();
    }

    [Fact]
    public async Task TwoOpens_InsideUnitOfWork_NotFlagged()
    {
        var src = Stubs + @"
public class Repo {
    private ISqlConnections _c = null!;
    public void Work() {
        using var uow = new UnitOfWorkScope();
        var a = _c.NewByKey(""Default"");
        var b = _c.NewByKey(""Default"");
    }
}";
        await new Verify<MultipleConnectionsAnalyzer>.Test { TestCode = src }.RunAsync();
    }

    [Fact]
    public async Task SingleOpen_NotFlagged()
    {
        var src = Stubs + @"
public class Repo {
    private ISqlConnections _c = null!;
    public void Work() { var a = _c.NewByKey(""Default""); }
}";
        await new Verify<MultipleConnectionsAnalyzer>.Test { TestCode = src }.RunAsync();
    }
}
