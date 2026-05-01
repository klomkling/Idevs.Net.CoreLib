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
}
