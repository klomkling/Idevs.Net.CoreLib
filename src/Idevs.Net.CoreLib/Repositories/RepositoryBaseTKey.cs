using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a Serenity <see cref="IIdRow"/>. Adds Id-keyed
/// CRUD shortcuts on top of <see cref="RepositoryBase{TRow}"/>.
/// </summary>
/// <typeparam name="TRow">A Serenity row that implements <see cref="IIdRow"/>.</typeparam>
/// <typeparam name="TKey">The Id value type (typically <see cref="int"/> or <see cref="long"/>).</typeparam>
public class RepositoryBase<TRow, TKey>(ISqlConnections sqlConnections) : RepositoryBase<TRow>(sqlConnections)
    where TRow : class, IRow, IIdRow, new()
{
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
        var idList = ids?.ToList() ?? [];
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
    /// <remarks>
    /// Serenity's <c>UpdateById</c> only writes fields that have been "assigned"
    /// on the row instance, and automatically skips fields with
    /// <c>FieldFlags.NotMapped</c>, <c>FieldFlags.Expression</c>, or
    /// <c>FieldFlags.Updatable=false</c>. For explicit column control, use the
    /// <see cref="UpdateAsync(TRow, Serenity.Data.Field[], Serenity.Data.IUnitOfWork?, System.Threading.CancellationToken)"/>
    /// overload.
    /// </remarks>
    public virtual Task<bool> UpdateAsync(
        TRow row,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        return ExecuteAsync((c, _) =>
        {
            var affected = c.UpdateById(row);
            return Task.FromResult(affected > 0);
        }, uow, ct);
    }

    /// <summary>
    /// Update only the listed <paramref name="fields"/> on the row identified by
    /// <paramref name="row"/>'s Id. Returns true when at least one row was affected.
    /// </summary>
    /// <remarks>
    /// Use when the row instance has many assigned fields but you want only a
    /// specific subset to land in the UPDATE — bypasses Serenity's
    /// "assigned-field" tracking. The row's Id must be set before calling.
    /// </remarks>
    public virtual Task<bool> UpdateAsync(
        TRow row,
        Field[] fields,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Length == 0)
            throw new ArgumentException("At least one field must be specified.", nameof(fields));

        var idRow = (IIdRow)row;
        var idValue = idRow.IdField.AsObject(row);
        if (idValue is null)
            throw new InvalidOperationException(
                "Row's Id must be set before calling UpdateAsync(row, fields, ...).");

        return ExecuteAsync((c, _) =>
        {
            var update = SqlUpdate(row.Table);
            foreach (var f in fields)
                update.Set(f, f.AsObject(row));
            update.Where(new BinaryCriteria(
                new Criteria(idRow.IdField),
                CriteriaOperator.EQ,
                new ValueCriteria(idValue)));

            var affected = update.Execute(c);
            return Task.FromResult(affected > 0);
        }, uow, ct);
    }

    /// <summary>
    /// Update all assigned, table-mapped fields on <paramref name="row"/>
    /// EXCEPT those listed in <paramref name="excludeFields"/>. Returns true
    /// when at least one row was affected.
    /// </summary>
    /// <remarks>
    /// Honors the same auto-exclusion rules as the default
    /// <see cref="UpdateAsync(TRow, IUnitOfWork?, CancellationToken)"/>
    /// (skips <c>NotMapped</c>, expression/calculated, and non-updatable
    /// fields) AND additionally skips the explicit
    /// <paramref name="excludeFields"/>. Useful when most of the row should be
    /// updated but a specific column must be preserved (e.g., never overwrite
    /// an audit timestamp from this code path). The row's Id must be set
    /// before calling.
    /// </remarks>
    public virtual Task<bool> UpdateExcludingAsync(
        TRow row,
        Field[] excludeFields,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(excludeFields);

        var idRow = (IIdRow)row;
        var idField = idRow.IdField;
        var idValue = idField.AsObject(row);
        if (idValue is null)
            throw new InvalidOperationException(
                "Row's Id must be set before calling UpdateExcludingAsync(row, excludeFields, ...).");

        var exclude = new HashSet<Field>(excludeFields);

        return ExecuteAsync((c, _) =>
        {
            var update = SqlUpdate(row.Table);
            var anySet = false;
            foreach (var f in row.GetFields())
            {
                // Never SET the Id column itself — it goes in WHERE.
                if (ReferenceEquals(f, idField)) continue;
                if (exclude.Contains(f)) continue;
                if (!row.IsAssigned(f)) continue;
                if ((f.Flags & FieldFlags.NotMapped) != 0) continue;
                if ((f.Flags & FieldFlags.Updatable) != FieldFlags.Updatable) continue;

                update.Set(f, f.AsObject(row));
                anySet = true;
            }

            if (!anySet)
                return Task.FromResult(false);

            update.Where(new BinaryCriteria(
                new Criteria(idField),
                CriteriaOperator.EQ,
                new ValueCriteria(idValue)));

            var affected = update.Execute(c);
            return Task.FromResult(affected > 0);
        }, uow, ct);
    }

    /// <summary>
    /// Delete the row whose Id equals <paramref name="id"/>. Returns true when at
    /// least one row was affected.
    /// </summary>
    public virtual Task<bool> DeleteByIdAsync(
        TKey id,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        return ExecuteAsync((c, _) =>
        {
            var template = new TRow();
            var idField = ((IIdRow)template).IdField;
            var affected = SqlDelete(template.Table)
                .Where(new BinaryCriteria(new Criteria(idField), CriteriaOperator.EQ, new ValueCriteria(id)))
                .Execute(c);
            return Task.FromResult(affected > 0);
        }, uow, ct);
    }

    private const string ObsoleteSyncMessage =
        "Sync wrapper kept for migration. Prefer the *Async variant. " +
        "Will be removed once underlying SQL APIs become real-async (~1.0).";

    [Obsolete(ObsoleteSyncMessage)]
    public virtual TRow? GetById(TKey id, IUnitOfWork? uow = null) =>
        GetByIdAsync(id, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual List<TRow> GetByIds(IEnumerable<TKey> ids, IUnitOfWork? uow = null) =>
        GetByIdsAsync(ids, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual bool Update(TRow row, IUnitOfWork? uow = null) =>
        UpdateAsync(row, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual bool Update(TRow row, Field[] fields, IUnitOfWork? uow = null) =>
        UpdateAsync(row, fields, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual bool UpdateExcluding(TRow row, Field[] excludeFields, IUnitOfWork? uow = null) =>
        UpdateExcludingAsync(row, excludeFields, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual bool DeleteById(TKey id, IUnitOfWork? uow = null) =>
        DeleteByIdAsync(id, uow).GetAwaiter().GetResult();
}
