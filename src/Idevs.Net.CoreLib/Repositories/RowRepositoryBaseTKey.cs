using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a Serenity <see cref="IIdRow"/>. Adds Id-keyed
/// CRUD shortcuts on top of <see cref="RowRepositoryBase{TRow}"/>.
/// </summary>
/// <typeparam name="TRow">A Serenity row that implements <see cref="IIdRow"/>.</typeparam>
/// <typeparam name="TKey">The Id value type (typically <see cref="int"/> or <see cref="long"/>).</typeparam>
public class RowRepositoryBase<TRow, TKey>(ISqlConnections sqlConnections) : RowRepositoryBase<TRow>(sqlConnections)
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
    /// Delegates to Serenity's <c>UpdateById</c>, which uses an
    /// IsAssigned-based filter: any field on the row that has been assigned a
    /// value AND has the <c>Updatable</c> flag set goes into the UPDATE.
    /// Practical implications:
    /// <list type="bullet">
    /// <item><description><c>[NotMapped]</c> properties declared as plain CLR auto-properties
    /// (no backing <see cref="Field"/> in <c>RowFields</c>) are silently dropped — they
    /// have no SQL representation.</description></item>
    /// <item><description><c>[Expression]</c> fields are NOT auto-skipped on writes. If you
    /// assign a value to one, it WILL be included in the UPDATE and SQL Server will reject
    /// it with "Invalid column name". Either don't assign Expression fields, or use
    /// <see cref="UpdateExcludingAsync"/> /
    /// <see cref="UpdateAsync(TRow, Serenity.Data.Field[], Serenity.Data.IUnitOfWork?, System.Threading.CancellationToken)"/>
    /// to drop them.</description></item>
    /// </list>
    /// </remarks>
    public virtual Task<bool> UpdateAsync(
        TRow row,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var rvField = RowVersionMetadata.Find(row);
        if (rvField is null)
        {
            // No [RowVersion] field — preserve today's behaviour exactly.
            return ExecuteAsync((c, _) =>
            {
                var affected = c.UpdateById(row);
                return Task.FromResult(affected > 0);
            }, uow, ct);
        }

        // [RowVersion] guarded path: assemble the SqlUpdate ourselves so we
        // can inject SET RowVersion = RowVersion + 1 and WHERE RowVersion = @captured,
        // run with ExpectedRows.Ignore (we'll check affected ourselves), and
        // throw OptimisticConcurrencyException on conflict.
        return UpdateGuardedAsync(row, rvField, fieldsToSet: null, excludeFields: null, uow, ct);
    }

    /// <summary>
    /// Shared write path used by all three TRow-shaped UpdateAsync overloads
    /// when the row carries a <see cref="RowVersionAttribute"/> field. Builds
    /// a SqlUpdate, applies the caller's field-selection rules, layers the
    /// RowVersion guard on top, and translates affected-rows == 0 into
    /// <see cref="OptimisticConcurrencyException"/>.
    /// </summary>
    /// <remarks>
    /// <c>fieldsToSet</c>: when non-null, only these fields go into SET
    /// (used by the explicit-fields overload). When null, all
    /// assigned/Updatable fields are included.
    /// <c>excludeFields</c>: when non-null, these fields are excluded
    /// from SET (used by UpdateExcludingAsync). Ignored when
    /// <c>fieldsToSet</c> is non-null.
    /// </remarks>
    private Task<bool> UpdateGuardedAsync(
        TRow row,
        Field rvField,
        Field[]? fieldsToSet,
        HashSet<Field>? excludeFields,
        IUnitOfWork? uow,
        CancellationToken ct)
    {
        var idRow = (IIdRow)row;
        var idField = idRow.IdField;
        var idValue = idField.AsObject(row);
        if (idValue is null)
            throw new InvalidOperationException(
                "Row's Id must be set before calling UpdateAsync on a [RowVersion]-guarded row.");

        var capturedObj = rvField.AsObject(row);
        if (capturedObj is null)
            throw new InvalidOperationException(
                $"[RowVersion] field '{row.Table}.{rvField.Name}' is null on the row passed to " +
                "UpdateAsync. Read the row before updating — null means the captured version is " +
                "unknown, which would silently disable the optimistic-concurrency guard.");
        var captured = Convert.ToInt64(capturedObj);

        // Compute the next version once, with overflow protection. Throwing
        // here is preferable to silently wrapping past long.MaxValue and
        // producing negative/colliding versions on the next allocator.
        long nextVersion;
        try { nextVersion = checked(captured + 1); }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException(
                $"[RowVersion] field '{row.Table}.{rvField.Name}' is at long.MaxValue " +
                $"({captured}) and cannot advance. The row is at the end of its version " +
                "space; the table or row needs to be migrated to reset the counter.", ex);
        }

        return ExecuteAsync((c, _) =>
        {
            var update = SqlUpdate(row.Table);

            // Build the SET list per the caller's field-selection rules.
            // RowVersion is set explicitly below to `nextVersion`, so callers
            // who pass it in `fieldsToSet` (or omit it from `excludeFields`)
            // don't get their value used — the guard always wins.
            if (fieldsToSet is not null)
            {
                foreach (var f in fieldsToSet)
                {
                    if (ReferenceEquals(f, rvField)) continue; // set below
                    update.Set(f, f.AsObject(row));
                }
            }
            else
            {
                foreach (var f in row.GetFields())
                {
                    if (ReferenceEquals(f, idField)) continue;
                    if (ReferenceEquals(f, rvField)) continue; // set below
                    if (excludeFields is not null && excludeFields.Contains(f)) continue;
                    if (!row.IsAssigned(f)) continue;
                    if ((f.Flags & FieldFlags.NotMapped) != 0) continue;
                    if ((f.Flags & FieldFlags.Updatable) != FieldFlags.Updatable) continue;
                    update.Set(f, f.AsObject(row));
                }
            }

            // SET RowVersion = @nextVersion (an app-computed constant).
            // Equivalent to SET RowVersion = RowVersion + 1 because the WHERE
            // clause below pins RowVersion to `captured`, so the matched row's
            // current version + 1 IS nextVersion. The constant form is simpler
            // and lets us write the value back to the row instance after the
            // call without an extra round-trip.
            update.Set(rvField, nextVersion);

            update.Where(new BinaryCriteria(
                new Criteria(idField),
                CriteriaOperator.EQ,
                new ValueCriteria(idValue)));
            update.Where(new BinaryCriteria(
                new Criteria(rvField),
                CriteriaOperator.EQ,
                new ValueCriteria(captured)));

            // ExpectedRows.Ignore — we expect 0 (conflict) or 1 (success);
            // either way it's not a "fail loudly on >1" scenario like the
            // unguarded path.
            var affected = update.Execute(c, ExpectedRows.Ignore);
            if (affected == 0)
                throw new OptimisticConcurrencyException(row.Table, idValue, captured);

            // Row stayed put. Write the new RowVersion back so the caller can
            // reuse the same instance for further updates without re-reading.
            rvField.AsObject(row, (object?)nextVersion);
            return Task.FromResult(true);
        }, uow, ct);
    }

    /// <summary>
    /// Update only the listed <paramref name="fields"/> on the row identified by
    /// <paramref name="row"/>'s Id. Returns true when at least one row was affected.
    /// </summary>
    /// <remarks>
    /// Surgical control over the UPDATE column list, bypassing IsAssigned
    /// tracking. Validates each listed field up front:
    /// <list type="bullet">
    /// <item><description>The Id field is rejected (it belongs in WHERE, not SET).</description></item>
    /// <item><description>Fields with <c>NotMapped</c> set are rejected.</description></item>
    /// <item><description>Fields without the <c>Updatable</c> flag are rejected (covers
    /// computed columns, <c>[Expression]</c> fields with <c>Updatable</c> cleared, and any
    /// field marked <c>Updatable=false</c> via <c>[SetFieldFlags]</c>).</description></item>
    /// </list>
    /// Throws <see cref="ArgumentException"/> with the offending field names instead of
    /// generating SQL the database would reject. The row's Id must be set before calling.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="fields"/> is empty, or if any listed field is the Id
    /// column, <c>NotMapped</c>, or has <c>Updatable=false</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the row's Id has not been assigned.
    /// </exception>
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
        var idField = idRow.IdField;
        var idValue = idField.AsObject(row);
        if (idValue is null)
            throw new InvalidOperationException(
                "Row's Id must be set before calling UpdateAsync(row, fields, ...).");

        var rejected = fields
            .Where(f => ReferenceEquals(f, idField)
                     || (f.Flags & FieldFlags.NotMapped) != 0
                     || (f.Flags & FieldFlags.Updatable) != FieldFlags.Updatable)
            .Select(f => ReferenceEquals(f, idField) ? $"{f.Name} (Id column)" : f.Name)
            .ToArray();
        if (rejected.Length > 0)
            throw new ArgumentException(
                $"Cannot UPDATE field(s) in SET list: {string.Join(", ", rejected)}. " +
                "These are the Id column, NotMapped, Expression-decorated, or otherwise " +
                "have FieldFlags.Updatable=false. The Id is used in WHERE; do not include " +
                "it in the SET list. Drop to SqlUpdate via ExecuteAsync to bypass.",
                nameof(fields));

        // [RowVersion]-guarded path: route through the shared write path.
        // The guard is applied even when `fields` doesn't include RowVersion —
        // optimistic concurrency is non-negotiable for versioned rows.
        var rvField = RowVersionMetadata.Find(row);
        if (rvField is not null)
            return UpdateGuardedAsync(row, rvField, fields, excludeFields: null, uow, ct);

        return ExecuteAsync((c, _) =>
        {
            var update = SqlUpdate(row.Table);
            foreach (var f in fields)
                update.Set(f, f.AsObject(row));
            update.Where(new BinaryCriteria(
                new Criteria(idField),
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
    /// Filter applied per field, in order: caller's <paramref name="excludeFields"/>,
    /// then the Id field (which goes in WHERE, not SET), then <c>IsAssigned</c>,
    /// then <c>NotMapped</c>, then <c>Updatable</c> flag. The <c>Updatable</c>-flag
    /// check excludes computed/identity columns and any field where
    /// <c>[SetFieldFlags]</c> has cleared the flag. Note that an
    /// <c>[Expression]</c>-decorated field with the default flag set is NOT
    /// auto-skipped — list it in <paramref name="excludeFields"/> if it might
    /// be assigned on the row instance. The row's Id must be set before calling.
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

        // [RowVersion]-guarded path: route through the shared write path.
        // The guard is applied even if RowVersion is in excludeFields —
        // optimistic concurrency is non-negotiable, can't be excluded.
        var rvField = RowVersionMetadata.Find(row);
        if (rvField is not null)
            return UpdateGuardedAsync(row, rvField, fieldsToSet: null, exclude, uow, ct);

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
