using System.Data;
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
    public virtual Task<TRow?> FirstAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        return ExecuteAsync((c, _) =>
        {
            return Task.FromResult(c.TryFirst<TRow>(configure));
        }, uow, ct);
    }

    /// <summary>Return all rows that match the configured query.</summary>
    public virtual Task<List<TRow>> ListAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        return ExecuteAsync((c, _) =>
        {
            return Task.FromResult(c.List<TRow>(configure));
        }, uow, ct);
    }
}
