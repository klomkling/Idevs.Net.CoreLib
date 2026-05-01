using Idevs.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;

namespace Idevs.Extensions;

public static class SerilogLogManagerExtensions
{
    public static WebApplication UseIdevsSerilogLogManager(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var loggerFactory = app.Services.GetService<ILoggerFactory>();
        if (loggerFactory is not null)
        {
            LogManager.SetLoggerFactory(loggerFactory);
            return app;
        }

        LogManager.SetLoggerFactory(new SerilogLoggerFactory());
        return app;
    }

    public static IServiceCollection AddIdevsSerilogLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ILoggerFactory>(_ => new SerilogLoggerFactory());
        return services;
    }
}
