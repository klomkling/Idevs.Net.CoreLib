using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serenity;
using Serenity.Data;

namespace Idevs;

/// <summary>
/// Legacy v0.3.3 repository base. The <typeparamref name="T"/> generic parameter
/// is the <see cref="ILogger{T}"/> category type — NOT a Serenity row.
/// </summary>
/// <remarks>
/// Restored verbatim from v0.3.3 to keep downstream projects (notably PowerACC)
/// compiling while they migrate to the async-first
/// <see cref="Repositories.RowRepositoryBase{TRow}"/> /
/// <see cref="Repositories.RowRepositoryBase{TRow, TKey}"/> surface in
/// <c>Idevs.Repositories</c>. New code should not derive from this type.
/// </remarks>
/// <typeparam name="T">Type used for logging context.</typeparam>
[Obsolete("Use Idevs.Repositories.RowRepositoryBase<TRow> or RowRepositoryBase<TRow, TKey>. " +
    "This v0.3.3 shape is kept for downstream migration and will be removed in v1.0.")]
public class RepositoryBase<T>
{
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
        var scoped = serviceProvider.CreateScope();
        SqlConnections = scoped.ServiceProvider.GetRequiredService<ISqlConnections>();
        ExceptionLog = logger;
        Localizer = scoped.ServiceProvider.GetRequiredService<ITextLocalizer>();
    }

    protected IDbConnection Connection => SqlConnections.NewByKey("Default");
    protected ISqlDialect Dialect => Connection.GetDialect();
}
