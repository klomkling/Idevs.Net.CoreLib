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
        if (row is null) throw new ArgumentNullException(nameof(row));

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
    public virtual Task<TRow?> GetByAsync<TValue>(
        Field keyField,
        TValue value,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (keyField is null) throw new ArgumentNullException(nameof(keyField));

        return FirstAsync(q => q
            .SelectTableFields()
            .WhereEqual(keyField, value),
            uow, ct);
    }
}
