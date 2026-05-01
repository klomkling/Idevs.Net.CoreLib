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
        UnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (work is null) throw new ArgumentNullException(nameof(work));
        ct.ThrowIfCancellationRequested();

        if (uow is not null)
            return await work(uow.Connection, ct).ConfigureAwait(false);

        using var connection = SqlConnections.NewByKey(ConnectionKey);
        return await work(connection, ct).ConfigureAwait(false);
    }
}
