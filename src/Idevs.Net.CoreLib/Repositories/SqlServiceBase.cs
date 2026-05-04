using System.Data;
using System.Reflection;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Base for services that need raw SQL access without being a typed-row repository.
/// Provides connection management, dialect lookup, SQL builder factories, and a
/// uniform <see cref="ExecuteAsync{T}"/> template.
/// </summary>
/// <remarks>
/// Does not inject ILogger, ITextLocalizer, or IServiceProvider — consumers inject
/// what they need into their own derived primary constructors.
/// </remarks>
public abstract class SqlServiceBase
{
    private readonly string _connectionKeyFromAttribute;

    protected ISqlConnections SqlConnections { get; }

    /// <summary>
    /// Connection key used when opening connections internally. Override per repo,
    /// or annotate the derived class with [ConnectionKey("MyDb")] for the same effect.
    /// Override of this property wins over the attribute.
    /// </summary>
    protected virtual string ConnectionKey => _connectionKeyFromAttribute;

    private readonly Lazy<ISqlDialect> _dialect;

    /// <summary>
    /// Cached SQL dialect for this base's <see cref="ConnectionKey"/>. Resolved on
    /// first access from the connection's metadata; cached for the lifetime of this
    /// instance. Resolution is thread-safe (single execution, published once).
    /// </summary>
    protected ISqlDialect Dialect => _dialect.Value;

    protected SqlServiceBase(ISqlConnections sqlConnections)
    {
        SqlConnections = sqlConnections ?? throw new ArgumentNullException(nameof(sqlConnections));

        var attr = GetType().GetCustomAttribute<ConnectionKeyAttribute>(inherit: true);
        _connectionKeyFromAttribute = attr?.Key ?? "Default";

        _dialect = new Lazy<ISqlDialect>(() =>
        {
            using var connection = SqlConnections.NewByKey(ConnectionKey);
            return connection.GetDialect();
        });
    }

    /// <summary>
    /// Run <paramref name="work"/> on a managed connection.
    /// </summary>
    /// <param name="work">Delegate that receives the open <see cref="IDbConnection"/>
    /// and the cancellation token.</param>
    /// <param name="uow">Optional UnitOfWork. When provided, <paramref name="work"/> runs
    /// against <c>uow.Connection</c> and the connection is NOT disposed by this method —
    /// the caller owns the lifetime. When null, a connection is opened from
    /// <see cref="ConnectionKey"/> for the duration of the call and disposed on exit.</param>
    /// <param name="ct">Cancellation token. Checked before opening a connection.</param>
    /// <remarks>
    /// Does not catch exceptions. If <paramref name="work"/> throws, the exception
    /// propagates and the connection (if owned) is still disposed via <c>using</c>.
    /// Override in derived classes to add retry, structured logging, or other
    /// cross-cutting concerns.
    /// </remarks>
    protected virtual async Task<T> ExecuteAsync<T>(
        Func<IDbConnection, CancellationToken, Task<T>> work,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();

        if (uow is not null)
            return await work(uow.Connection, ct).ConfigureAwait(false);

        using var connection = SqlConnections.NewByKey(ConnectionKey);
        return await work(connection, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Acquire a unit of work for the duration of a <c>using</c> block.
    /// </summary>
    /// <remarks>
    /// When <paramref name="uow"/> is supplied, the returned scope wraps it
    /// without taking ownership — <see cref="UnitOfWorkScope.Commit"/> and
    /// <see cref="UnitOfWorkScope.Dispose"/> are no-ops, the caller's outer
    /// transaction wins. When <paramref name="uow"/> is null, the scope opens
    /// a connection from <see cref="ConnectionKey"/> and a fresh
    /// <see cref="UnitOfWork"/> on it; the caller MUST call
    /// <see cref="UnitOfWorkScope.Commit"/> before the scope leaves the using
    /// block, otherwise dispose rolls back. Use this form for long methods
    /// with sequential statements, conditional branches, or early returns.
    /// For short blocks, prefer <see cref="CommitOnSuccessAsync{T}"/>.
    /// </remarks>
    protected UnitOfWorkScope BeginUnitOfWork(IUnitOfWork? uow = null)
    {
        if (uow is not null)
            return new UnitOfWorkScope(uow);

        var connection = SqlConnections.NewByKey(ConnectionKey);
        try
        {
            // BeginTransaction (called inside UnitOfWork's ctor) can throw —
            // dispose the just-opened connection before propagating so we
            // don't leak it.
            var newUow = new UnitOfWork(connection);
            return new UnitOfWorkScope(connection, newUow);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Run <paramref name="work"/> inside a unit of work; commit on success,
    /// roll back on exception.
    /// </summary>
    /// <remarks>
    /// If <paramref name="uow"/> is supplied, the caller's transaction is used
    /// and this method does not commit/rollback — the caller wins. If null,
    /// a fresh connection + UoW is opened, the work runs, and Commit() is
    /// called when the work returns normally. If the work throws, Commit() is
    /// skipped and the underlying scope rolls back via dispose.
    /// </remarks>
    protected async Task<T> CommitOnSuccessAsync<T>(
        Func<IUnitOfWork, CancellationToken, Task<T>> work,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();

        using var scope = BeginUnitOfWork(uow);
        var result = await work(scope.Uow, ct).ConfigureAwait(false);
        scope.Commit();
        return result;
    }

    /// <summary>
    /// Non-generic <see cref="CommitOnSuccessAsync{T}"/> overload for void work.
    /// </summary>
    protected async Task CommitOnSuccessAsync(
        Func<IUnitOfWork, CancellationToken, Task> work,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();

        using var scope = BeginUnitOfWork(uow);
        await work(scope.Uow, ct).ConfigureAwait(false);
        scope.Commit();
    }

    /// <summary>
    /// Execute raw SQL that returns a single scalar value.
    /// </summary>
    /// <remarks>
    /// Thin wrapper over <see cref="SqlHelper.ExecuteScalar(System.Data.IDbConnection, string, System.Collections.Generic.IDictionary{string, object}, Microsoft.Extensions.Logging.ILogger)"/>
    /// composed with <see cref="ExecuteAsync{T}"/> for connection lifetime
    /// + <see cref="IUnitOfWork"/> participation. Returns
    /// <c>default(T)</c> when the result is <c>null</c> or
    /// <see cref="DBNull.Value"/>; otherwise converts via
    /// <see cref="Convert.ChangeType(object, Type)"/>.
    /// <para>
    /// MySQL/MariaDB consumers should set <c>Use Affected Rows=false</c> on
    /// the connection string if the underlying SQL is an <c>UPDATE</c>
    /// returning a count via <c>OUTPUT</c>/<c>RETURNING</c>; pure
    /// <c>SELECT</c> scalars are unaffected by the matched-vs-changed-rows
    /// distinction. See the v0.7.4 MIGRATION note.
    /// </para>
    /// </remarks>
    protected Task<T?> ExecuteScalarAsync<T>(
        string sql,
        IDictionary<string, object?>? parameters = null,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return ExecuteAsync((c, _) =>
        {
            var paramDict = parameters as IDictionary<string, object>;
            if (parameters is not null && paramDict is null)
                paramDict = parameters.ToDictionary(p => p.Key, p => p.Value!);

            var result = SqlHelper.ExecuteScalar(c, sql, paramDict, logger: null);
            if (result is null || result == DBNull.Value)
                return Task.FromResult<T?>(default);
            return Task.FromResult<T?>((T)Convert.ChangeType(result, typeof(T))!);
        }, uow, ct);
    }

    /// <summary>
    /// Execute raw SQL that returns an affected-row count
    /// (<c>UPDATE</c> / <c>DELETE</c> / <c>INSERT</c> / DDL).
    /// </summary>
    /// <remarks>
    /// On MySQL/MariaDB the count semantics depend on the
    /// <c>Use Affected Rows</c> connection-string flag — see the v0.7.4
    /// MIGRATION note. SQL Server / PostgreSQL / Oracle / SQLite report
    /// matched-rows by default.
    /// </remarks>
    protected Task<int> ExecuteNonQueryAsync(
        string sql,
        IDictionary<string, object?>? parameters = null,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return ExecuteAsync((c, _) =>
        {
            var paramDict = parameters as IDictionary<string, object>;
            if (parameters is not null && paramDict is null)
                paramDict = parameters.ToDictionary(p => p.Key, p => p.Value!);

            var rows = SqlHelper.ExecuteNonQuery(c, sql, paramDict, logger: null);
            return Task.FromResult(rows);
        }, uow, ct);
    }

    private const string ObsoleteSyncMessage =
        "Sync wrapper kept for migration. Prefer the *Async variant. " +
        "Will be removed once underlying SQL APIs become real-async (~1.0).";

    /// <summary>Sync wrapper for <see cref="CommitOnSuccessAsync{T}"/>.</summary>
    [Obsolete(ObsoleteSyncMessage)]
    protected T CommitOnSuccess<T>(Func<IUnitOfWork, T> work, IUnitOfWork? uow = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        return CommitOnSuccessAsync((u, _) => Task.FromResult(work(u)), uow)
            .GetAwaiter().GetResult();
    }

    /// <summary>Sync wrapper for the non-generic <see cref="CommitOnSuccessAsync(Func{IUnitOfWork, CancellationToken, Task}, IUnitOfWork?, CancellationToken)"/>.</summary>
    [Obsolete(ObsoleteSyncMessage)]
    protected void CommitOnSuccess(Action<IUnitOfWork> work, IUnitOfWork? uow = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        CommitOnSuccessAsync((u, _) => { work(u); return Task.CompletedTask; }, uow)
            .GetAwaiter().GetResult();
    }

    /// <summary>Sync wrapper for <see cref="ExecuteScalarAsync{T}"/>.</summary>
    [Obsolete(ObsoleteSyncMessage)]
    protected T? ExecuteScalar<T>(
        string sql,
        IDictionary<string, object?>? parameters = null,
        IUnitOfWork? uow = null) =>
        ExecuteScalarAsync<T>(sql, parameters, uow).GetAwaiter().GetResult();

    /// <summary>Sync wrapper for <see cref="ExecuteNonQueryAsync"/>.</summary>
    [Obsolete(ObsoleteSyncMessage)]
    protected int ExecuteNonQuery(
        string sql,
        IDictionary<string, object?>? parameters = null,
        IUnitOfWork? uow = null) =>
        ExecuteNonQueryAsync(sql, parameters, uow).GetAwaiter().GetResult();

    /// <summary>Creates a new <see cref="SqlQuery"/> pre-bound to this base's <see cref="Dialect"/>.</summary>
    protected SqlQuery SqlQuery() => new SqlQuery().Dialect(Dialect);

    /// <summary>Creates a new <see cref="SqlInsert"/> pre-bound to this base's <see cref="Dialect"/>.</summary>
    protected SqlInsert SqlInsert(string tableName) => new SqlInsert(tableName).Dialect(Dialect);

    /// <summary>Creates a new <see cref="SqlUpdate"/> pre-bound to this base's <see cref="Dialect"/>.</summary>
    protected SqlUpdate SqlUpdate(string tableName) => new SqlUpdate(tableName).Dialect(Dialect);

    /// <summary>Creates a new <see cref="SqlDelete"/> for the given table.</summary>
    /// <remarks>
    /// Unlike <see cref="SqlQuery"/>/<see cref="SqlInsert"/>/<see cref="SqlUpdate"/>,
    /// Serenity's <see cref="SqlDelete"/> does not expose a chainable
    /// <c>Dialect()</c> setter. The dialect is resolved from the connection
    /// at <c>Execute</c> time, which is correct for any DELETE statement
    /// produced through this factory.
    /// </remarks>
    protected SqlDelete SqlDelete(string tableName) => new(tableName);
}
