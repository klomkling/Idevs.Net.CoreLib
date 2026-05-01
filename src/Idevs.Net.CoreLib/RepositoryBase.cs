using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serenity;
using Serenity.Data;

namespace Idevs;

/// <summary>
/// Base class for repository pattern implementation with Serenity Framework integration
/// </summary>
/// <typeparam name="T">Type used for logging context</typeparam>
public class RepositoryBase<T>
{
    private const string DefaultConnectionKey = "Default";

    private ISqlDialect? _dialect;

    protected IServiceProvider ServiceProvider { get; }
    protected ILogger<T> ExceptionLog { get; }
    protected ISqlConnections SqlConnections { get; }
    protected ITextLocalizer Localizer { get; }

    protected SqlQuery SqlQuery => new SqlQuery();
    protected SqlInsert SqlInsert(string tableName) => new SqlInsert(tableName);
    protected SqlUpdate SqlUpdate(string tableName) => new SqlUpdate(tableName);
    protected SqlDelete SqlDelete(string tableName) => new SqlDelete(tableName);

    public RepositoryBase(IServiceProvider serviceProvider, ILogger<T> logger)
    {
        ServiceProvider = serviceProvider;
        SqlConnections = serviceProvider.GetRequiredService<ISqlConnections>();
        ExceptionLog = logger;
        Localizer = serviceProvider.GetRequiredService<ITextLocalizer>();
    }

    protected IDbConnection Connection => SqlConnections.NewByKey(DefaultConnectionKey);

    protected ISqlDialect Dialect
    {
        get
        {
            if (_dialect != null) return _dialect;

            using var connection = SqlConnections.NewByKey(DefaultConnectionKey);
            _dialect = connection.GetDialect();
            return _dialect;
        }
    }
}
