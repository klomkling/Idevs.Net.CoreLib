using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Idevs.Logging;

public static class LogManager
{
    private static ILoggerFactory? _loggerFactory;

    public static void SetLoggerFactory(ILoggerFactory factory)
    {
        _loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static ILogger<T> GetLogger<T>()
    {
        var factory = _loggerFactory;
        if (factory is null)
            throw new InvalidOperationException("LoggerFactory is not set.");

        return factory.CreateLogger<T>();
    }

    public static ILogger<T> TryGetLogger<T>()
    {
        return _loggerFactory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
    }

    public static void Reset()
    {
        _loggerFactory = null;
    }
}
