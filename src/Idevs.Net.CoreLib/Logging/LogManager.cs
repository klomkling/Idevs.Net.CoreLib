using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Idevs.Logging;

public static class LogManager
{
    private static ILoggerFactory? loggerFactory;

    public static void SetLoggerFactory(ILoggerFactory factory)
    {
        loggerFactory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public static ILogger<T> GetLogger<T>()
    {
        var factory = loggerFactory;
        if (factory is null)
            throw new InvalidOperationException("LoggerFactory is not set.");

        return factory.CreateLogger<T>();
    }

    public static ILogger<T> TryGetLogger<T>()
    {
        return loggerFactory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
    }

    public static void Reset()
    {
        loggerFactory = null;
    }
}
