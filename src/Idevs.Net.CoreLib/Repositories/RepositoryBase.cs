using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Typed repository base for a single Serenity <see cref="IRow"/>. Provides
/// async-first read/list/write helpers and an <see cref="SqlServiceBase.ExecuteAsync{T}"/>
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
    /// <c>q.Dialect(...)</c> themselves. Wraps Serenity's <c>Connection.TryFirst</c>.
    /// </remarks>
    public virtual Task<TRow?> TryFirstAsync(
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
    /// Serenity's <c>InsertAndGetID</c> automatically skips fields with
    /// <c>FieldFlags.NotMapped</c>, <c>FieldFlags.Expression</c>, or
    /// <c>FieldFlags.Insertable=false</c>. Setting a value on those fields is a
    /// no-op as far as the SQL is concerned. For explicit column control, use
    /// the <see cref="CreateAsync(TRow, Serenity.Data.Field[], Serenity.Data.IUnitOfWork?, System.Threading.CancellationToken)"/>
    /// overload.
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
    /// Insert <paramref name="row"/> writing only the listed <paramref name="fields"/>.
    /// Returns the new identity, or 0 when the row has no identity column.
    /// </summary>
    /// <remarks>
    /// Use when you want surgical control over which columns end up in the
    /// INSERT, regardless of which fields are "assigned" on the row instance.
    /// The default <see cref="CreateAsync(TRow, IUnitOfWork?, CancellationToken)"/>
    /// already auto-skips <c>NotMapped</c> / <c>Expression</c> / non-insertable
    /// fields; this overload is for cases where you want to also exclude
    /// regular columns (e.g., let the database fill defaults, or split a row
    /// across multiple inserts).
    /// </remarks>
    public virtual Task<long> CreateAsync(
        TRow row,
        Field[] fields,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Length == 0)
            throw new ArgumentException("At least one field must be specified.", nameof(fields));

        return ExecuteAsync((c, _) =>
        {
            var insert = SqlInsert(row.Table);
            foreach (var f in fields)
                insert.Set(f, f.AsObject(row));
            return Task.FromResult<long>(insert.ExecuteAndGetID(c) ?? 0L);
        }, uow, ct);
    }

    /// <summary>
    /// Insert <paramref name="row"/> writing all assigned, table-mapped fields
    /// EXCEPT those listed in <paramref name="excludeFields"/>. Returns the new
    /// identity, or 0 when the row has no identity column.
    /// </summary>
    /// <remarks>
    /// Honors the same auto-exclusion rules as the default
    /// <see cref="CreateAsync(TRow, IUnitOfWork?, CancellationToken)"/>
    /// (skips <c>NotMapped</c>, <c>Expression</c>, and non-insertable fields)
    /// AND additionally skips the explicit <paramref name="excludeFields"/>.
    /// Useful when most of the row should be inserted but a few columns must
    /// be omitted (e.g., let the database default <c>CreatedAt</c>, skip a
    /// derived column you populated for downstream code).
    /// </remarks>
    public virtual Task<long> CreateExcludingAsync(
        TRow row,
        Field[] excludeFields,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(excludeFields);

        var exclude = new HashSet<Field>(excludeFields);

        return ExecuteAsync((c, _) =>
        {
            var insert = SqlInsert(row.Table);
            foreach (var f in row.GetFields())
            {
                if (exclude.Contains(f)) continue;
                if (!row.IsAssigned(f)) continue;
                if ((f.Flags & FieldFlags.NotMapped) != 0) continue;
                // The Insertable check excludes [Expression]-decorated fields
                // (they are flagged Calculated/Foreign without Insertable),
                // identity columns, computed columns, and any field marked
                // Insertable=false.
                if ((f.Flags & FieldFlags.Insertable) != FieldFlags.Insertable) continue;

                insert.Set(f, f.AsObject(row));
            }
            return Task.FromResult<long>(insert.ExecuteAndGetID(c) ?? 0L);
        }, uow, ct);
    }

    /// <summary>
    /// Run a partial UPDATE with criteria. Returns rows affected.
    /// </summary>
    /// <remarks>
    /// Table name and <see cref="SqlServiceBase.Dialect"/> are pre-bound (via the
    /// <see cref="SqlServiceBase.SqlUpdate"/> factory); the caller only supplies
    /// <c>Set(...)</c> and <c>Where(...)</c> on the provided
    /// <see cref="SqlUpdate"/>. Defaults to <see cref="ExpectedRows.One"/> — fails
    /// loudly when the WHERE clause matches zero or more than one row. For batch
    /// updates pass <see cref="ExpectedRows.Ignore"/> or call
    /// <see cref="UpdateManyAsync"/>.
    /// </remarks>
    public virtual Task<int> UpdateAsync(
        Action<SqlUpdate> configure,
        ExpectedRows expectedRows = ExpectedRows.One,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return ExecuteAsync((c, _) =>
        {
            var update = SqlUpdate(new TRow().Table);
            configure(update);
            return Task.FromResult(update.Execute(c, expectedRows));
        }, uow, ct);
    }

    /// <summary>Run an UPDATE with no row-count assertion (any number of rows accepted).</summary>
    public virtual Task<int> UpdateManyAsync(
        Action<SqlUpdate> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
        => UpdateAsync(configure, ExpectedRows.Ignore, uow, ct);

    /// <summary>
    /// Run a DELETE with criteria. Returns rows affected.
    /// </summary>
    /// <remarks>
    /// Table name is pre-resolved from <typeparamref name="TRow"/>; the caller
    /// only supplies <c>Where(...)</c> on the provided <see cref="SqlDelete"/>.
    /// Dialect is resolved from the connection at <c>Execute</c> time —
    /// Serenity's <see cref="SqlDelete"/> does not expose a chainable
    /// <c>Dialect()</c> setter, but the connection is the authoritative source
    /// for the active dialect, so emitted SQL is correct.
    /// Defaults to <see cref="ExpectedRows.One"/>. For batch deletes pass
    /// <see cref="ExpectedRows.Ignore"/> or call <see cref="DeleteManyAsync"/>.
    /// </remarks>
    public virtual Task<int> DeleteAsync(
        Action<SqlDelete> configure,
        ExpectedRows expectedRows = ExpectedRows.One,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return ExecuteAsync((c, _) =>
        {
            var delete = SqlDelete(new TRow().Table);
            configure(delete);
            return Task.FromResult(delete.Execute(c, expectedRows));
        }, uow, ct);
    }

    /// <summary>Run a DELETE with no row-count assertion (any number of rows accepted).</summary>
    public virtual Task<int> DeleteManyAsync(
        Action<SqlDelete> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
        => DeleteAsync(configure, ExpectedRows.Ignore, uow, ct);

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

        return TryFirstAsync(q => q
            .SelectTableFields()
            .WhereEqual(keyField, value),
            uow, ct);
    }

    private const string ObsoleteFirstAsyncMessage =
        "Use TryFirstAsync. Same behavior; the new name matches Serenity's TryFirst convention. " +
        "Will be removed in 1.0.";

    /// <summary>Deprecated alias for <see cref="TryFirstAsync"/>.</summary>
    [Obsolete(ObsoleteFirstAsyncMessage)]
    public virtual Task<TRow?> FirstAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
        => TryFirstAsync(configure, uow, ct);

    private const string ObsoleteSyncMessage =
        "Sync wrapper kept for migration. Prefer the *Async variant. " +
        "Will be removed once underlying SQL APIs become real-async (~1.0).";

    [Obsolete(ObsoleteSyncMessage)]
    public virtual TRow? TryFirst(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        TryFirstAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteFirstAsyncMessage)]
    public virtual TRow? First(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        TryFirstAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual List<TRow> List(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        ListAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual TRow? GetBy<TValue>(Field keyField, TValue value, IUnitOfWork? uow = null) =>
        GetByAsync(keyField, value, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual long Create(TRow row, IUnitOfWork? uow = null) =>
        CreateAsync(row, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual long Create(TRow row, Field[] fields, IUnitOfWork? uow = null) =>
        CreateAsync(row, fields, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual long CreateExcluding(TRow row, Field[] excludeFields, IUnitOfWork? uow = null) =>
        CreateExcludingAsync(row, excludeFields, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual int Update(
        Action<SqlUpdate> configure,
        ExpectedRows expectedRows = ExpectedRows.One,
        IUnitOfWork? uow = null) =>
        UpdateAsync(configure, expectedRows, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual int UpdateMany(Action<SqlUpdate> configure, IUnitOfWork? uow = null) =>
        UpdateManyAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual int Delete(
        Action<SqlDelete> configure,
        ExpectedRows expectedRows = ExpectedRows.One,
        IUnitOfWork? uow = null) =>
        DeleteAsync(configure, expectedRows, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual int DeleteMany(Action<SqlDelete> configure, IUnitOfWork? uow = null) =>
        DeleteManyAsync(configure, uow).GetAwaiter().GetResult();
}
