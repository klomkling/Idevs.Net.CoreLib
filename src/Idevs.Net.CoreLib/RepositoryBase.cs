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
/// <para>
/// Restored from v0.3.3 to keep downstream projects (notably PowerACC)
/// compiling while they migrate to the async-first
/// <see cref="Repositories.RowRepositoryBase{TRow}"/> /
/// <see cref="Repositories.RowRepositoryBase{TRow, TKey}"/> surface in
/// <c>Idevs.Repositories</c>. New code should not derive from this type.
/// </para>
/// <para>
/// Differences from the v0.3.3 verbatim body: implements
/// <see cref="IDisposable"/> so the inner <see cref="IServiceScope"/> created
/// in the constructor is released (v0.3.3 leaked it for the lifetime of the
/// repository), and <see cref="Dialect"/> is lazily cached so reading it does
/// not allocate a throwaway <see cref="IDbConnection"/> on every access.
/// Behaviour from the caller's perspective is unchanged.
/// </para>
/// </remarks>
/// <typeparam name="T">Type used for logging context.</typeparam>
[Obsolete("Use Idevs.Repositories.RowRepositoryBase<TRow> or RowRepositoryBase<TRow, TKey>. " +
    "This v0.3.3 shape is kept for downstream migration and will be removed in v1.0.")]
public class RepositoryBase<T> : IDisposable
{
    private readonly IServiceScope _scope;
    private ISqlDialect? _cachedDialect;
    private bool _disposed;

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
        _scope = serviceProvider.CreateScope();
        SqlConnections = _scope.ServiceProvider.GetRequiredService<ISqlConnections>();
        ExceptionLog = logger;
        Localizer = _scope.ServiceProvider.GetRequiredService<ITextLocalizer>();
    }

    /// <summary>
    /// Returns a NEW <see cref="IDbConnection"/> on every read. The caller owns
    /// disposal — typically <c>using var conn = Connection;</c>. Reading this
    /// property without disposing the returned instance leaks the connection.
    /// </summary>
    protected IDbConnection Connection => SqlConnections.NewByKey("Default");

    /// <summary>
    /// Active SQL dialect for the <c>"Default"</c> connection key, resolved
    /// once and cached. The throwaway <see cref="IDbConnection"/> used for
    /// the first resolution is disposed inline.
    /// </summary>
    protected ISqlDialect Dialect
    {
        get
        {
            if (_cachedDialect is not null) return _cachedDialect;
            using var c = SqlConnections.NewByKey("Default");
            return _cachedDialect = c.GetDialect();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _scope.Dispose();
        _disposed = true;
    }
}
