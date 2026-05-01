using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a single Serenity <see cref="IRow"/>. Provides
/// async-first read/list helpers and an <see cref="SqlServiceBase.ExecuteAsync{T}"/>
/// template inherited from <see cref="SqlServiceBase"/>.
/// </summary>
/// <typeparam name="TRow">A Serenity row type.</typeparam>
public class RepositoryBase<TRow> : SqlServiceBase
    where TRow : class, IRow, new()
{
    public RepositoryBase(ISqlConnections sqlConnections) : base(sqlConnections) { }

    /// <summary>Return the first row that matches the configured query, or null.</summary>
    /// <remarks>
    /// The query is pre-bound to <see cref="SqlServiceBase.Dialect"/> before
    /// <paramref name="configure"/> is invoked, so consumers don't need to call
    /// <c>q.Dialect(...)</c> themselves.
    /// </remarks>
    public virtual Task<TRow?> FirstAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        return ExecuteAsync<TRow?>((c, _) =>
            Task.FromResult<TRow?>(c.TryFirst<TRow>(q =>
            {
                q.Dialect(Dialect);
                configure(q);
            })), uow, ct);
    }

    /// <summary>Return all rows that match the configured query.</summary>
    /// <remarks>
    /// The query is pre-bound to <see cref="SqlServiceBase.Dialect"/> before
    /// <paramref name="configure"/> is invoked.
    /// </remarks>
    public virtual Task<List<TRow>> ListAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        return ExecuteAsync((c, _) =>
            Task.FromResult(c.List<TRow>(q =>
            {
                q.Dialect(Dialect);
                configure(q);
            })), uow, ct);
    }
}
