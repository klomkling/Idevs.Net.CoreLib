using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a Serenity <see cref="IIdRow"/>. Adds Id-keyed
/// CRUD shortcuts on top of <see cref="RepositoryBase{TRow}"/>.
/// </summary>
/// <typeparam name="TRow">A Serenity row that implements <see cref="IIdRow"/>.</typeparam>
/// <typeparam name="TKey">The Id value type (typically <see cref="int"/> or <see cref="long"/>).</typeparam>
public class RepositoryBase<TRow, TKey> : RepositoryBase<TRow>
    where TRow : class, IRow, IIdRow, new()
{
    public RepositoryBase(ISqlConnections sqlConnections) : base(sqlConnections) { }

    /// <summary>Fetch a single row by its Id, or null if not found.</summary>
    public virtual Task<TRow?> GetByIdAsync(
        TKey id,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync<TRow?>((c, _) =>
            Task.FromResult<TRow?>(c.TryById<TRow>(id!)),
            uow, ct);
    }

    /// <summary>
    /// Fetch all rows whose Id is contained in <paramref name="ids"/>. Returns an
    /// empty list when <paramref name="ids"/> is null or empty without opening a
    /// connection. Consumers are responsible for chunking large id lists to stay
    /// within engine parameter limits (e.g., SQL Server 2100).
    /// </summary>
    public virtual Task<List<TRow>> GetByIdsAsync(
        IEnumerable<TKey> ids,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        var idList = ids?.ToList() ?? new List<TKey>();
        if (idList.Count == 0) return Task.FromResult(new List<TRow>());

        var idField = ((IIdRow)new TRow()).IdField;
        return ListAsync(q => q
            .SelectTableFields()
            .Where(new Criteria(idField).In(idList.ToArray())),
            uow, ct);
    }

    /// <summary>
    /// Update <paramref name="row"/> by its Id field. Returns true when at least
    /// one row was affected. The row's Id must be set before calling.
    /// </summary>
    public virtual Task<bool> UpdateAsync(
        TRow row,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        if (row is null) throw new ArgumentNullException(nameof(row));

        return ExecuteAsync((c, _) =>
        {
            var affected = c.UpdateById(row);
            return Task.FromResult(affected > 0);
        }, uow, ct);
    }
}
