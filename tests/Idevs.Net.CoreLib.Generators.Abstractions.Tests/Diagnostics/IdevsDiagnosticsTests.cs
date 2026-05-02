using Idevs.Generators.Abstractions.Diagnostics;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Abstractions.Tests.Diagnostics;

public class IdevsDiagnosticsTests
{
    [Fact]
    public void DiagnosticIdRange_HasReservedRanges()
    {
        Assert.Equal("IDEVSGEN001-IDEVSGEN099", DiagnosticIdRange.CoreLibDIRange);
        Assert.Equal("IDEVSGEN100-IDEVSGEN199", DiagnosticIdRange.CoreLibAnalyzersRange);
        Assert.Equal("IDEVSGEN200+", DiagnosticIdRange.ConsumerRange);
    }

    [Fact]
    public void CreateError_ProducesErrorDescriptor()
    {
        var d = IdevsDiagnostics.CreateError("IDEVSGEN001", "Test title", "Test message");
        Assert.Equal("IDEVSGEN001", d.Id);
        Assert.Equal(DiagnosticSeverity.Error, d.DefaultSeverity);
        Assert.True(d.IsEnabledByDefault);
        Assert.Equal("Idevs.DI", d.Category);
    }

    [Fact]
    public void CreateWarning_ProducesWarningDescriptor()
    {
        var d = IdevsDiagnostics.CreateWarning("IDEVSGEN004", "Title", "Message");
        Assert.Equal(DiagnosticSeverity.Warning, d.DefaultSeverity);
    }

    [Fact]
    public void CreateInfo_ProducesInfoDescriptor()
    {
        var d = IdevsDiagnostics.CreateInfo("IDEVSGEN050", "Title", "Message");
        Assert.Equal(DiagnosticSeverity.Info, d.DefaultSeverity);
    }

    [Theory]
    [InlineData("IDEVSGEN")]
    [InlineData("idevsgen001")]
    [InlineData("IDEVS001")]
    public void CreateError_RejectsInvalidIdFormat(string invalidId)
    {
        Assert.Throws<ArgumentException>(() =>
            IdevsDiagnostics.CreateError(invalidId, "Title", "Message"));
    }
}
