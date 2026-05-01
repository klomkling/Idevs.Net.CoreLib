using Idevs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Idevs.Net.CoreLib.Tests.Logging;

public sealed class LogManagerTests : IDisposable
{
    public void Dispose()
    {
        LogManager.Reset();
    }

    [Fact]
    public void GetLogger_ThrowsWhenFactoryIsNotConfigured()
    {
        LogManager.Reset();

        var exception = Assert.Throws<InvalidOperationException>(() => LogManager.GetLogger<LogManagerTests>());

        Assert.Equal("LoggerFactory is not set.", exception.Message);
    }

    [Fact]
    public void TryGetLogger_ReturnsNullLoggerWhenFactoryIsNotConfigured()
    {
        LogManager.Reset();

        var logger = LogManager.TryGetLogger<LogManagerTests>();

        Assert.Same(NullLogger<LogManagerTests>.Instance, logger);
    }

    [Fact]
    public void GetLogger_ReturnsLoggerFromConfiguredFactory()
    {
        using var factory = LoggerFactory.Create(_ => { });
        LogManager.SetLoggerFactory(factory);

        var logger = LogManager.GetLogger<LogManagerTests>();

        Assert.IsAssignableFrom<ILogger<LogManagerTests>>(logger);
        Assert.NotSame(NullLogger<LogManagerTests>.Instance, logger);
    }

    [Fact]
    public void Reset_ClearsConfiguredFactory()
    {
        using var factory = LoggerFactory.Create(_ => { });
        LogManager.SetLoggerFactory(factory);
        LogManager.Reset();

        var exception = Assert.Throws<InvalidOperationException>(() => LogManager.GetLogger<LogManagerTests>());

        Assert.Equal("LoggerFactory is not set.", exception.Message);
    }
}
