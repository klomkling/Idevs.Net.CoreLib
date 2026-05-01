using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a single Serenity <see cref="IRow"/>. Provides
/// async-first read/list helpers and an <see cref="SqlServiceBase.ExecuteAsync{T}"/>
/// template inherited from <see cref="SqlServiceBase"/>.
/// </summary>
/// <typeparam name="TRow">A Serenity row type.</typeparam>
public class RepositoryBase<TRow>(ISqlConnections sqlConnections) : SqlServiceBase(sqlConnections)
    where TRow : class, IRow, new()
{
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
        ArgumentNullException.ThrowIfNull(configure);

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
        ArgumentNullException.ThrowIfNull(configure);

        return ExecuteAsync((c, _) =>
            Task.FromResult(c.List<TRow>(q =>
            {
                q.Dialect(Dialect);
                configure(q);
            })), uow, ct);
    }

    /// <summary>
    /// Insert <paramref name="row"/> and return the new identity (or 0 if the row
    /// type does not implement <see cref="IIdRow"/>).
    /// </summary>
    /// <remarks>
    /// For updates, use <c>UpdateAsync</c>. The explicit Create/Update split mirrors
    /// Serenity's endpoint convention.
    /// </remarks>
    public virtual Task<long> CreateAsync(
        TRow row,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ExecuteAsync((c, _) =>
            Task.FromResult<long>(c.InsertAndGetID(row) ?? 0L),
            uow, ct);
    }

    /// <summary>
    /// Look up a single row by a Serenity field reference and value.
    /// </summary>
    /// <remarks>
    /// Uses the non-generic <see cref="Field"/> base because concrete Serenity field types
    /// (e.g., <see cref="StringField"/>, <see cref="Int32Field"/>) do not all share a common
    /// generic ancestor. The <paramref name="value"/> parameter is typed to <typeparamref name="TValue"/>
    /// to preserve call-site type safety.
    /// </remarks>
    /// <typeparam name="TValue">The field's value type (e.g., <see cref="string"/> for a <see cref="StringField"/>).</typeparam>
    /// <param name="keyField">A Serenity field (e.g., <c>FooRow.Fields.Code</c>).</param>
    /// <param name="value">The value to match.</param>
    /// <param name="uow">Optional unit of work; when supplied, the lookup runs against the caller's connection.</param>
    /// <param name="ct">Cancellation token, propagated to the underlying connection scope.</param>
    public virtual Task<TRow?> GetByAsync<TValue>(
        Field keyField,
        TValue value,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keyField);

        return FirstAsync(q => q
            .SelectTableFields()
            .WhereEqual(keyField, value),
            uow, ct);
    }

    private const string ObsoleteSyncMessage =
        "Sync wrapper kept for migration. Prefer the *Async variant. " +
        "Will be removed once underlying SQL APIs become real-async (~1.0).";

    [Obsolete(ObsoleteSyncMessage)]
    public virtual TRow? First(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        FirstAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual List<TRow> List(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        ListAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual TRow? GetBy<TValue>(Field keyField, TValue value, IUnitOfWork? uow = null) =>
        GetByAsync(keyField, value, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual long Create(TRow row, IUnitOfWork? uow = null) =>
        CreateAsync(row, uow).GetAwaiter().GetResult();
}
