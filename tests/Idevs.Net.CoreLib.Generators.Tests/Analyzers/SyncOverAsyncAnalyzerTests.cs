using System.Threading.Tasks;
using Idevs.Net.CoreLib.Generators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

public class SyncOverAsyncAnalyzerTests
{
    [Fact]
    public async Task FromResultOfCall_Flagged()
    {
        var src = @"
using System.Threading.Tasks;
public class S {
    private int Compute() => 1;
    public Task<int> {|#0:GetAsync|}() => Task.FromResult(Compute());
}";
        var expected = new DiagnosticResult("IDEVSGEN102", DiagnosticSeverity.Warning).WithLocation(0).WithArguments("GetAsync");
        await new Verify<SyncOverAsyncAnalyzer>.Test { TestCode = src, ExpectedDiagnostics = { expected } }.RunAsync();
    }

    [Fact]
    public async Task FromResultOfValue_NotFlagged()
    {
        var src = @"
using System.Threading.Tasks;
public class S {
    public Task<int> GetAsync(int x) => Task.FromResult(x);
}";
        await new Verify<SyncOverAsyncAnalyzer>.Test { TestCode = src }.RunAsync();
    }

    [Fact]
    public async Task GenuinelyAsync_NotFlagged()
    {
        var src = @"
using System.Threading.Tasks;
public class S {
    public async Task<int> GetAsync() { await Task.Delay(1); return 1; }
}";
        await new Verify<SyncOverAsyncAnalyzer>.Test { TestCode = src }.RunAsync();
    }
}
