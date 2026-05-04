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
    /// Count rows that match the configured query. Pass an empty configure
    /// (<c>_ =&gt; { }</c>) to count every row in the table.
    /// </summary>
    /// <remarks>
    /// Builds <c>SELECT COUNT(*) FROM table</c>, applies the caller's WHERE
    /// (and any joins / group-by, etc.), and reads back a scalar. Pre-bound
    /// to <see cref="SqlServiceBase.Dialect"/> via the <see cref="SqlServiceBase.SqlQuery"/>
    /// factory.
    /// </remarks>
    public virtual Task<int> CountAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return ExecuteAsync((c, _) =>
        {
            // From(IRow) — not From(string) — registers the row's alias (T0
            // by default) so field criteria like Fld.Status == "x" bind to
            // the correct table reference.
            var query = SqlQuery()
                .From(new TRow())
                .Select("COUNT(*)");
            configure(query);
            var result = SqlHelper.ExecuteScalar(c, query, logger: null);
            return Task.FromResult(Convert.ToInt32(result));
        }, uow, ct);
    }

    /// <summary>
    /// Return <c>true</c> when at least one row matches the configured query.
    /// </summary>
    /// <remarks>
    /// More efficient than <c>CountAsync(...) &gt; 0</c> for large tables —
    /// emits <c>SELECT 1 FROM table WHERE ... LIMIT 1</c> so the engine can
    /// short-circuit at the first match instead of counting every matching row.
    /// </remarks>
    public virtual Task<bool> ExistsAsync(
        Action<SqlQuery> configure,
        IUnitOfWork? uow = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return ExecuteAsync((c, _) =>
        {
            var query = SqlQuery()
                .From(new TRow())
                .Select("1")
                .Take(1);
            configure(query);
            var result = SqlHelper.ExecuteScalar(c, query, logger: null);
            return Task.FromResult(result is not null && result != DBNull.Value);
        }, uow, ct);
    }

    /// <summary>
    /// Insert <paramref name="row"/> and return the new identity (or 0 if the row
    /// type does not implement <see cref="IIdRow"/>).
    /// </summary>
    /// <remarks>
    /// Delegates to Serenity's <c>InsertAndGetID</c>, which uses an
    /// IsAssigned-based filter: any field on the row that has been assigned a
    /// value AND has the <c>Insertable</c> flag set goes into the INSERT.
    /// Practical implications:
    /// <list type="bullet">
    /// <item><description><c>[NotMapped]</c> properties declared as plain CLR auto-properties
    /// (no backing <see cref="Field"/> in <c>RowFields</c>) are silently dropped — they
    /// have no SQL representation. This is the production-correct pattern.</description></item>
    /// <item><description><c>[Expression]</c> fields are NOT auto-skipped on writes. If you
    /// assign a value to one, it WILL be included in the INSERT and SQL Server will reject
    /// it with "Invalid column name". Either don't assign Expression fields, or use
    /// <see cref="CreateExcludingAsync"/> / <see cref="CreateAsync(TRow, Serenity.Data.Field[], Serenity.Data.IUnitOfWork?, System.Threading.CancellationToken)"/>
    /// to drop them.</description></item>
    /// <item><description>Identity columns are skipped automatically by Serenity (their
    /// <c>Insertable</c> flag is off).</description></item>
    /// </list>
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
    /// Surgical control over the INSERT column list, bypassing
    /// IsAssigned tracking. Validates each listed field up front:
    /// <list type="bullet">
    /// <item><description>Fields with <c>NotMapped</c> set are rejected (they have no SQL column).</description></item>
    /// <item><description>Fields without the <c>Insertable</c> flag are rejected (covers identity
    /// columns and <c>[Expression]</c> fields that cleared <c>Insertable</c> via <c>[SetFieldFlags]</c>).</description></item>
    /// </list>
    /// Throws <see cref="ArgumentException"/> with the offending field names instead of
    /// generating SQL that the database would reject. If you need to write to such a
    /// field anyway (rare), drop to <c>SqlInsert</c> directly via <c>ExecuteAsync</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="fields"/> is empty, or if any listed field is
    /// <c>NotMapped</c> or has <c>Insertable=false</c>.
    /// </exception>
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

        var rejected = fields
            .Where(f => (f.Flags & FieldFlags.NotMapped) != 0
                     || (f.Flags & FieldFlags.Insertable) != FieldFlags.Insertable)
            .Select(f => f.Name)
            .ToArray();
        if (rejected.Length > 0)
            throw new ArgumentException(
                $"Cannot INSERT non-insertable field(s): {string.Join(", ", rejected)}. " +
                "These are NotMapped, Expression-decorated, identity columns, or otherwise " +
                "have FieldFlags.Insertable=false. Drop to SqlInsert via ExecuteAsync if you " +
                "need to bypass this check.",
                nameof(fields));

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
    /// Filter applied per field, in order: caller's <paramref name="excludeFields"/>,
    /// then <c>IsAssigned</c>, then <c>NotMapped</c>, then <c>Insertable</c> flag.
    /// The <c>Insertable</c>-flag check excludes identity columns and any field
    /// where <c>[SetFieldFlags]</c> has cleared the flag. Note that an
    /// <c>[Expression]</c>-decorated field with the default flag set is NOT
    /// auto-skipped — list it in <paramref name="excludeFields"/> if it might
    /// be assigned on the row instance.
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
    public virtual int Count(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        CountAsync(configure, uow).GetAwaiter().GetResult();

    [Obsolete(ObsoleteSyncMessage)]
    public virtual bool Exists(Action<SqlQuery> configure, IUnitOfWork? uow = null) =>
        ExistsAsync(configure, uow).GetAwaiter().GetResult();

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
