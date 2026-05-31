# Changelog

## 0.8.0 (2026-05-31)

### Added

- Four Roslyn misuse analyzers (`Idevs.DI` category, `.editorconfig`-configurable):
  - `IDEVSGEN100` (Warning) — method opens 2+ DB connections without a shared UnitOfWork.
  - `IDEVSGEN101` (Warning) — catch block logs and rethrows (let the top-level handler log).
  - `IDEVSGEN102` (Warning) — `Task`-returning method wraps sync work in `Task.FromResult`.
  - `IDEVSGEN103` (Info) — manual `MAX()+1` sequence; **codefix** rewrites to `ISequenceProvider.NextAsync`.

### Removed (breaking)

- `ServiceExtensions.AddIdevsCorelibServices()` and `RegisterServices()` — use the
  source-generated `AddIdevsServices()`.
- `CoreLibBootstrapper.AddIdevsCorelibLegacyScan()` and the runtime reflection scan.
- The `IdevsCoreLibUseSourceGenerator=false` MSBuild escape hatch; source-generated
  DI is now the only runtime registration path.

### Notes

- Legacy `[ScopedRegistration]`/`[SingletonRegistration]`/`[TransientRegistration]`
  attributes still work via the generator (`IDEVSGEN010`); they become errors in
  0.9.0 and are removed in 1.0.0.
- Still on the Serenity 8.8.9 / `0.x` lane; npm `@idevs/corelib` 1.x pin unchanged.

## 0.7.10 (2026-05-23)

### Changed

- **`Idevs.Net.CoreLib.targets`** — `npm install @idevs/corelib` now pins to
  the 1.x major (`@idevs/corelib@1`, npm's partial-version shorthand for
  `>=1.0.0 <2.0.0`). Previously the install resolved to the `latest` npm
  dist-tag, which would have pulled `@idevs/corelib 2.x` (Serenity 10 lane)
  the moment it shipped, breaking CI on net8 / Serenity 8.8.9 consumers.
  The bare `@1` form is used (instead of `">=1.0.0 <2.0.0"` or `^1.0.0`) so
  the MSBuild `<Exec>` command stays safe on Windows, where `cmd.exe` treats
  `<`/`>` as redirection operators and `^` as an escape character.
- **`Idevs.Net.CoreLib.targets`** — `CopyContentToProject` now passes
  `SkipUnchangedFiles="true"` to the `<Copy>` task. Unchanged CSS files are
  skipped per-file by size + timestamp, so subsequent builds no longer
  re-copy the `@idevs/corelib/css/*.css` catalog on every build.

### Notes

- No code changes. Patch-level, fully backwards-compatible.
- The 1.x pin also covers planned 0.8.x and 0.9.x releases (still on the
  Serenity 8.8.9 lane). When this package advances to `1.0.0` for the
  .NET 10 / Serenity 10 lane, update the pin to `>=2.0.0 <3.0.0` in
  lockstep with `@idevs/corelib 2.0`.

## 0.7.9 (2026-05-12)

### Changed

- Renamed `Idevs.Repositories.RepositoryBase<TRow>` → `RowRepositoryBase<TRow>`
  and `Idevs.Repositories.RepositoryBase<TRow, TKey>` → `RowRepositoryBase<TRow, TKey>`.
  Behaviour is identical; only the type name changed. The `*REMOVED*` /
  added entries are tracked in `PublicAPI.Unshipped.txt`.

### Added

- Re-introduced `Idevs.RepositoryBase<T>` (the v0.3.3 plumbing-only base
  where `T` is the `ILogger<T>` category) as an `[Obsolete]` legacy class.
  Body is identical to v0.3.3 — `ServiceProvider`, `ExceptionLog`,
  `SqlConnections`, `Localizer`, `Connection`, `Dialect`, and the
  `SqlQuery` / `SqlInsert(t)` / `SqlUpdate(t)` / `SqlDelete(t)` factories.
  Downstream projects still on the v0.3.3 shape (notably PowerACC) compile
  unchanged under `using Idevs;`. Scheduled for removal in v1.0.

### Migration

- New repositories: derive from `Idevs.Repositories.RowRepositoryBase<TRow>` /
  `RowRepositoryBase<TRow, TKey>`.
- Existing repositories on the v0.7.x API: rename the base class — the
  primary-constructor shape `(ISqlConnections)` and every method signature
  are unchanged.
- Existing repositories on the v0.3.3 API: keep using `Idevs.RepositoryBase<T>`
  during migration. Move to `RowRepositoryBase` before v1.0.

## 0.7.8 (2026-05-08)

### Added

- `Idevs.Repositories.RowVersionAttribute` — opt-in marker for a
  `long?` property that the library uses as the row's
  optimistic-concurrency version counter. Apply once per row.
- `Idevs.Repositories.OptimisticConcurrencyException` — thrown when a
  guarded UPDATE affects zero rows. Carries `TableName`, `RowId`, and
  `CapturedVersion` for diagnostics; intentionally does not carry the
  current database version (re-reading the row is the only safe path
  to recovery, exposing the current value here would invite naive
  retries).

### Changed

- `RepositoryBase<TRow,TKey>.UpdateAsync(TRow row, …)` now detects a
  `[RowVersion]` field on the row type. When present, the SQL gains
  `WHERE RowVersion = @captured` and `SET RowVersion = RowVersion + 1`,
  the call runs with `ExpectedRows.Ignore`, and 0 affected rows
  becomes `OptimisticConcurrencyException`. After a successful update,
  the new version is written back to the row instance so the caller
  can reuse it for further updates without re-reading.
- `UpdateAsync(TRow row, Field[] fields, …)` and
  `UpdateExcludingAsync(TRow row, Field[] excludeFields, …)` apply the
  same guard — opting out of RowVersion via field selection is not
  possible by design (optimistic concurrency is non-negotiable for
  versioned rows).
- Rows without `[RowVersion]` are unchanged from 0.7.7 — no WHERE
  clause added, no exception type changes, identical behaviour.

### Validation (fail-loud at first lookup, cached forever)

- More than one `[RowVersion]` property on a row throws
  `InvalidOperationException` with the offending property names.
- `[RowVersion]` on a non-`long?` property throws with a type-mismatch
  message pointing at the right shape.
- `[RowVersion]` property without a matching `Int64Field` in
  `RowFields` throws with the exact field declaration the consumer
  needs to add.
- A `[RowVersion]` field with `FieldFlags.Updatable` cleared throws —
  the library increments it on every guarded UPDATE, so making it
  non-Updatable would silently disable the guard.
- `null` `RowVersion` value at update time throws — the captured
  version must be known, which means the caller must read the row
  before updating. Catches "constructed by hand" bugs early.

### Verified (xUnit + Testcontainers vs SQL Server 2022)

- 8 integration tests in `OptimisticConcurrencyTests` cover: happy
  path increment via all three TRow-shaped UpdateAsync overloads;
  conflict detection on stale RowVersion (single-conflict + 50-way
  stampede); manual retry pattern (5 concurrent +1 increments
  resolved via re-read loop); precondition checks (null RowVersion);
  non-versioned regression sanity test (rows without `[RowVersion]`
  unchanged).

### Notes

- **No built-in retry helper.** Optimistic concurrency is a
  policy-level concern (how many retries, how long to wait, what
  conflicts mean for the business). Document the manual try/retry
  pattern; revisit if real demand emerges. See MIGRATION.md (v0.7.7 →
  v0.7.8) for the canonical retry shape.
- **Application-managed `BIGINT`, not native `rowversion`.** The
  schema is portable across SqlServer / MySQL / MariaDB / PostgreSQL
  / Oracle / SQLite — a plain `BIGINT NOT NULL DEFAULT 0`. SqlServer's
  native `rowversion` / `timestamp` type isn't used because it's
  `varbinary(8)`, not portable, and a `bigint` counter has plenty of
  range for any realistic workload.
- **Criteria-based `UpdateAsync(Action<SqlUpdate>, …)` is unchanged.**
  The caller owns the WHERE clause in that overload — adding magic
  guards would surprise. Add the `WHERE RowVersion = ...` manually if
  needed.

## 0.7.7 (2026-05-07)

### Added

- `Idevs.Repositories.Sequences.ISequenceProvider` — atomic numeric
  sequence allocator. Each call to `NextAsync(string sequenceKey, ct)`
  returns a value that no other concurrent caller can return for the
  same key. `NextRangeAsync(key, count, ct)` allocates a contiguous
  block atomically (useful for bulk imports). `EnsureSequenceAsync(key,
  startValue = 1, ct)` idempotently seeds a sequence row.
- `SqlSequenceProvider` — default SQL-backed implementation, registered
  via `[Scoped(ServiceType = typeof(ISequenceProvider))]`. Uses 0.7.6's
  `InNewTransactionAsync` + `ForUpdate()` so the lock window is the
  duration of the SELECT…FOR UPDATE + UPDATE on a single sequence row;
  on a same-region database that's sub-millisecond. Backed by an
  `IdevsSequences` table (`SequenceKey NVARCHAR(100) PRIMARY KEY`,
  `NextValue BIGINT NOT NULL`); consumers create the table in their
  own migration pipeline (schema in MIGRATION.md).
- `IdevsSequenceRow` — public Serenity row backing the storage table.
- `SequenceServiceCollectionExtensions.AddIdevsSequenceProvider()` —
  manual DI registration for hosts that don't run the source generator
  (test hosts, console apps).

### Notes

- Allocation is **independent of any ambient `IUnitOfWork`** by design.
  A value committed inside `NextAsync` does NOT roll back if the outer
  caller's business transaction subsequently fails. This is the
  intended semantic for document-number / invoice-number / order-number
  sequences — gaps are normal, duplicate numbers are catastrophic.
- The interface returns `long`; callers format to whatever scheme their
  domain requires (`INV-2026-00042`, `SO/2026/00001`, etc.). The
  provider does not know about formatting.
- See MIGRATION.md (v0.7.6 → v0.7.7) for the caller-side migration
  pattern from the manual SELECT-then-UPDATE shape, including the
  schema DDL for SqlServer / MySQL / PostgreSQL.

### Verified (xUnit + Testcontainers vs SQL Server 2022)

- 14 integration tests in `SqlSequenceProviderTests` covering: ensure
  semantics (new key, existing key preserves NextValue, default vs
  custom start value), `NextAsync` sequencing and per-key isolation,
  argument validation (null/empty key throws, non-positive count
  throws, missing key throws), `NextRangeAsync` block allocation
  (advance count, large block of 1000), 50 concurrent allocators
  produce 50 distinct values (the underlying race fix re-pinned at the
  helper level), and outer-rollback survival.

## 0.7.6 (2026-05-07)

### Added

- `LockMode` enum with four members: `Update` (FOR UPDATE / UPDLOCK +
  HOLDLOCK + ROWLOCK), `Share` (FOR SHARE / HOLDLOCK), `UpdateNoWait`
  (FOR UPDATE NOWAIT — Postgres / MySQL 8+ / MariaDB 10.6+ / Oracle only;
  SqlServer throws), and `UpdateSkip` (FOR UPDATE SKIP LOCKED / READPAST
  on SqlServer — queue-consumer pattern).
- `SqlQuery.ForUpdate(mode = LockMode.Update)` fluent extension that
  flags a SELECT for row-level locking. The dialect-correct clause is
  applied at execution time when the query is materialised through
  Idevs repository helpers (`RepositoryBase<TRow>.TryFirstAsync` today;
  more in 0.8.0). Direct Serenity execution paths
  (`connection.TryFirst<TRow>(query)`) DO NOT honour the flag and
  silently produce non-locking SQL — always go through Idevs helpers
  when locking matters.
- `SqlServiceBase.InNewTransactionAsync<T>(work, ct)` and the void
  overload — explicit "fresh transaction" helper for short-lived
  operations that must commit (or fail) independently of any ambient
  `IUnitOfWork`. Use for sequence/document-number allocation, audit
  log writes, and idempotency-key reservation. Replaces the implicit
  `CommitOnSuccessAsync(work, uow: null, ct)` trick with a
  self-documenting name.

### Changed

- `RepositoryBase<TRow>.TryFirstAsync` now detects the `ForUpdate()`
  flag at execution time. When set, it materialises the SELECT itself,
  injects the dialect-correct lock hint via the internal
  `RowLockSqlBuilder`, executes through `SqlHelper.ExecuteReader`
  (which honours Serenity's `WrappedConnection` transaction
  propagation), and maps the result row via `SqlQuery.GetFromReader`.
  Behaviour is unchanged for queries that don't call `ForUpdate()` —
  the existing Serenity `TryFirst<TRow>` lambda path is preserved.
- `TryFirstAsync` with a flagged query MUST be called inside an
  active transaction. Calling it without a non-null `uow` throws
  `InvalidOperationException` — taking a row lock outside a
  transaction is meaningless on every supported engine.

### Verified (xUnit + Testcontainers)

- 27 unit tests cover `RowLockSqlBuilder` across SqlServer, MySQL/MariaDB,
  Postgres, Oracle (ANSI fallback), and SQLite (always throws), plus
  argument-validation edges.
- 6 SqlServer integration tests (5 passing + 1 documented skip) verify:
  `ForUpdate` without a transaction throws; inside a transaction
  acquires the row lock and returns the row; a second locker blocks
  until the first commits; `UpdateNoWait` throws as documented; and
  the standard non-locking path is untouched.
- `ConcurrentSequenceAllocationTests` pins the actual race fix that
  motivated the work: 50 parallel allocators against the same
  sequence row produce 50 distinct values.
- `InNewTransactionAsyncTests` pins the independent-commit semantic:
  inner writes survive an outer rollback; inner exceptions roll back
  only the inner write.

### Notes / known limitations

- **`LockMode.UpdateNoWait` on SqlServer**: SqlServer has no in-query
  NOWAIT hint. The library throws `NotSupportedException` rather than
  emit incorrect SQL. Workaround: `SET LOCK_TIMEOUT 0` at the session
  or transaction level before the locking SELECT, then catch SQL
  error 1222 (lock-request timeout).
- **`LockMode.UpdateSkip` on SqlServer**: emits
  `WITH (UPDLOCK, HOLDLOCK, ROWLOCK, READPAST)`. READPAST requires
  the active transaction to be in `READ COMMITTED` or `REPEATABLE READ`
  isolation. The default isolation in some SqlServer container
  configurations is incompatible — verify your transaction's isolation
  before relying on `UpdateSkip` against SqlServer. Postgres and
  MySQL don't have this caveat.
- **SQLite**: row-level locking is not supported; every `LockMode`
  throws `NotSupportedException`. Use `BEGIN IMMEDIATE` for a
  database-wide write lock instead.
- **Independent-commit trade-off in `InNewTransactionAsync`**: work
  committed inside the helper does NOT roll back if the caller's outer
  business transaction subsequently fails. This is intentional —
  document-number allocations have to survive outer rollback (gaps in
  the sequence are normal, duplicate numbers are catastrophic). For
  anything where the inner write must roll back with the outer flow,
  do NOT use `InNewTransactionAsync`; pass the caller's UoW through
  and let the outer transaction own the commit.

## 0.7.5 (2026-05-04)

### Added

- `RepositoryBase<TRow>.CountAsync(Action<SqlQuery>, ...)` — returns
  `Task<long>` (not `Task<int>`, to accommodate 64-bit `COUNT(*)` columns
  on PostgreSQL / MySQL `BIGINT UNSIGNED`). Emits `SELECT COUNT(*) FROM
  table WHERE ...` via `SqlHelper.ExecuteScalar`. Pass `_ => { }` to count
  every row. Does NOT support `GROUP BY` / `HAVING` (would silently return
  only the first group's count) — use `ListAsync` + LINQ `GroupBy` or a
  manual subquery via `ExecuteAsync` for grouped counts.
- `RepositoryBase<TRow>.ExistsAsync(Action<SqlQuery>, ...)` — returns
  `Task<bool>`. Emits `SELECT 1 FROM table WHERE ...` plus a dialect-
  specific row-limit clause (`TOP 1` on SQL Server, `LIMIT 1` on
  MySQL/PostgreSQL/SQLite, `FETCH FIRST 1 ROWS ONLY` on Oracle) via
  `SqlQuery.Take(1)`, so the engine can short-circuit at the first match.
- Sync `[Obsolete]` wrappers (`Count` returns `long`, `Exists` returns
  `bool`) following the existing migration pattern.
- `SqlServiceBase.ExecuteScalarAsync<T>(string sql, IDictionary?, ...)` —
  thin wrapper over `SqlHelper.ExecuteScalar` composed with `ExecuteAsync`
  for connection lifetime + UoW participation. Returns `default(T)` when
  the result is `null` / `DBNull.Value`; otherwise converts via
  `Convert.ChangeType`. Cuts the typical 5-line raw-SQL boilerplate down
  to one expression — added for GeniuzPOS-style codebases that read
  scalars via raw SQL frequently.
- `SqlServiceBase.ExecuteNonQueryAsync(string sql, IDictionary?, ...)` —
  symmetric for `UPDATE` / `DELETE` / `INSERT` / DDL. Returns the affected-
  row count reported by the provider. MySQL/MariaDB consumers should
  apply the `Use Affected Rows=false` flag from the v0.7.4 MIGRATION
  note since the count semantics for raw `UPDATE`/`DELETE` follow the
  same matched-vs-changed-rows distinction.
- Sync `[Obsolete]` wrappers (`ExecuteScalar<T>`, `ExecuteNonQuery`)
  following the existing migration pattern.

### Verified (integration tests against SQL Server 2022)

- `CountAsync` returns 0 for empty table, total rows for empty configure,
  exact counts for simple and composite WHERE clauses.
- `ExistsAsync` returns false for empty table / no match, true for one or
  many matching rows, and honors composite WHERE predicates.

### Note

These were originally proposed for 0.7.2 but deferred because the right
Serenity API path wasn't immediately reachable. The implementation uses
`SqlQuery().From(IRow).Select(...)` (not `From(string)`) so that field
criteria like `Fld.Status == "Active"` bind to the correct table alias.

## 0.7.4 (2026-05-04)

### Added

- `RepositoryBase<TRow>.CreateAsync(TRow row, Field[] fields, ...)` — insert
  only the listed columns. Use when you want surgical control over the
  INSERT regardless of which fields are "assigned" on the row instance.
- `RepositoryBase<TRow>.CreateExcludingAsync(TRow row, Field[] excludeFields, ...)`
  — insert all assigned, table-mapped fields EXCEPT the listed ones. Honors
  Serenity's auto-exclusion rules and additionally drops the explicit
  excludes.
- `RepositoryBase<TRow, TKey>.UpdateAsync(TRow row, Field[] fields, ...)` —
  update only the listed columns, bypassing Serenity's "assigned-field"
  tracking. The row's Id must be set.
- `RepositoryBase<TRow, TKey>.UpdateExcludingAsync(TRow row, Field[] excludeFields, ...)`
  — update all assigned, table-mapped fields EXCEPT the listed ones.
- Sync `[Obsolete]` wrappers for each new async method following the
  existing migration pattern.
- New test infrastructure: Testcontainers-based SQL Server 2022 fixture
  (`MsSqlContainerFixture`) shared across integration test classes via
  `MsSqlContainerCollection`. Integration tests are tagged
  `[Trait("Category", "Integration")]` for easy filtering.

### Verified (integration tests against SQL Server 2022, pinned image tag)

- `[NotMapped]` properties (declared without a backing `Field` in
  `RowFields`) are silently dropped from INSERT/UPDATE — the field has no
  SQL representation, so Serenity has nothing to write.
- `[Expression]` fields are read-only outputs of the SELECT projection.
  When **assigned** on a write path they DO end up in the SQL (per
  Serenity's IsAssigned-based filter) and SQL Server rejects them with
  "Invalid column name". The cure: do not assign Expression fields, or use
  `CreateExcludingAsync` / `UpdateExcludingAsync` to drop them explicitly.
- Both include-only and exclude variants emit exactly the expected column
  lists end-to-end. Trap and cure are demonstrated symmetrically for both
  INSERT and UPDATE paths.

### Validation guards on the include-only overloads

- `CreateAsync(row, fields)` rejects fields with `NotMapped` set or without
  the `Insertable` flag (covers identity columns and `[Expression]` fields
  with `Insertable=false`). Throws `ArgumentException` listing offending
  field names — fail at API boundary instead of generating SQL the database
  rejects.
- `UpdateAsync(row, fields)` additionally rejects the Id field (which
  belongs in WHERE, not SET).

## 0.7.3 (2026-05-03)

### Added

- `SqlServiceBase.BeginUnitOfWork(IUnitOfWork? uow = null)` returns a new
  `UnitOfWorkScope` (`IDisposable` + `IAsyncDisposable`). When a caller-owned
  `IUnitOfWork` is supplied, the scope wraps it without taking ownership
  (Commit/Dispose are no-ops). Otherwise the scope opens a fresh connection
  + `UnitOfWork`; the caller MUST call `Commit()` before the using block
  exits, otherwise dispose rolls back. Designed for long methods with
  sequential statements, conditional branches, and early returns.
- `SqlServiceBase.CommitOnSuccessAsync<T>(work, uow?, ct?)` and the
  non-generic `CommitOnSuccessAsync(work, uow?, ct?)` — lambda form that
  opens a scope, runs the delegate, commits on normal return, and rolls
  back on exception. Designed for short blocks where the whole transaction
  body fits in one expression.
- `SqlServiceBase.CommitOnSuccess<T>` / `CommitOnSuccess` sync
  `[Obsolete]` wrappers following the existing migration pattern.
- New public type `Idevs.Repositories.UnitOfWorkScope`.

### Why

Closes a real gap: a parent repository method that didn't accept an
`IUnitOfWork` parameter could not coordinate atomic writes across child
repositories — each call opened its own connection and ran in a separate
transaction. The new helpers implement the canonical "use caller's UoW
if provided, otherwise open one and commit/rollback" pattern. Pick the
shape that matches the call site: lambda for short blocks, scope for
long methods.

## 0.7.2 (2026-05-02)

### Added

- `RepositoryBase<TRow>.TryFirstAsync(Action<SqlQuery>, ...)` — semantic alias
  for the existing `FirstAsync` (which returns `Task<TRow?>`). Matches Serenity's
  `Connection.TryFirst` naming convention.
- `RepositoryBase<TRow>.UpdateAsync(Action<SqlUpdate>, ExpectedRows, ...)` —
  criteria-based partial UPDATE. Table name is pre-resolved from the row type;
  caller only supplies `Set(...)` and `Where(...)`. Defaults to
  `ExpectedRows.One` for safe single-row semantics.
- `RepositoryBase<TRow>.UpdateManyAsync(Action<SqlUpdate>, ...)` — convenience
  alias for `UpdateAsync(..., ExpectedRows.Ignore, ...)` (any number of rows).
- `RepositoryBase<TRow>.DeleteAsync(Action<SqlDelete>, ExpectedRows, ...)` —
  criteria-based DELETE with the same `ExpectedRows.One` default as
  `UpdateAsync`.
- `RepositoryBase<TRow>.DeleteManyAsync(Action<SqlDelete>, ...)` — convenience
  alias for batch deletes.
- Sync `[Obsolete]` wrappers for each new async method (`TryFirst`, `Update`,
  `UpdateMany`, `Delete`, `DeleteMany`) following the existing migration
  pattern.

### Deprecated

- `RepositoryBase<TRow>.FirstAsync(Action<SqlQuery>, ...)` — marked
  `[Obsolete]` redirecting to `TryFirstAsync`. Same behavior; will be removed
  in 1.0.
- `RepositoryBase<TRow>.First(Action<SqlQuery>, ...)` — sync wrapper, also
  redirected to `TryFirstAsync`.

## 0.7.1 (2026-05-02)

### Added

- GitHub Actions CI/CD workflows under `.github/workflows/`:
  - `ci.yml` — build & test on .NET 8 and .NET 10 for every PR; requests
    GitHub Copilot as a reviewer on PR open/reopen/ready_for_review.
  - `tag-on-merge.yml` — on merge to `main`, reads `<Version>` from
    `Idevs.Net.CoreLib.csproj` and creates an annotated `v{version}` tag
    (idempotent; skips if the tag already exists).
  - `publish-nuget.yml` — on `v*` tag push, packs all four public projects
    and publishes them to nuget.org via the `nuget` deployment environment
    (`--skip-duplicate`). Uploads `.nupkg` artifacts for traceability.

## 0.7.0 (2026-05-02)

### Breaking Changes

- **`AddIdevsCorelibServices()` is `[Obsolete]`** — use the source-generator-emitted
  `services.AddIdevsServices()` instead. The old method now delegates to
  `AddIdevsCorelibCore()` + `AddIdevsCorelibLegacyScan()`. Removed in 0.8.0.

### Added

- New source generator `Idevs.Net.CoreLib.Generators` (bundled into the main nupkg
  under `analyzers/dotnet/cs/`). Emits compile-time DI registrations from
  attribute, marker-interface, and `IIdevsServiceRegistrar` discovery paths.
- New package `Idevs.Net.CoreLib.Generators.Abstractions` — Roslyn helpers
  (scanning, validation, emission, diagnostics) for any consumer-authored
  source generator.
- New marker interfaces in `Idevs.Repositories`: `IScopedService`,
  `ISingletonService`, `ITransientService`, plus generic variants with `<TService>`.
- New runtime hook `IIdevsServiceRegistrar` for arbitrary imperative DI registration.
- New extension class `Idevs.Extensions.CoreLibBootstrapper` exposing
  `AddIdevsCorelibCore` (hand-coded services only) and
  `AddIdevsCorelibLegacyScan` (transitional reflection-based scan).
- 10 Roslyn diagnostics `IDEVSGEN001`–`IDEVSGEN010` covering attribute/marker
  conflicts, ambiguous service types, registrar validation, and legacy
  attribute usage.
- `<IdevsCoreLibUseSourceGenerator>` MSBuild flag (default `true`); set to `false`
  to fall back to the legacy scan during transition.
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` enabled on the four public packages.
  `PublicAPI.Shipped.txt` baselined at 0.6.1.

### Migration

See [MIGRATION.md](MIGRATION.md#v06x--v070--source-generator-di-registration).

## 0.6.1 (2026-05-01)

### Changed (internal modernization, no public API change)

- Adopt C# 12 primary constructors on `RepositoryBase<TRow>` and
  `RepositoryBase<TRow, TKey>`.
- Adopt collection expressions (`[]`) for empty/static lists and arrays
  throughout `src/`.
- Standardise on `ArgumentNullException.ThrowIfNull(x)` everywhere the
  classic `if (x is null) throw …` pattern remained — completes the
  Ardalis-removal sweep.
- Use C# 14 `extension(T receiver) { … }` declarations to group related
  extension methods in `NumberExtensions`, `TextLocalizerExtensions`,
  `WebApplicationExtensions`, and (Autofac) `WebApplicationBuilderExtensions`.
  Compiled IL is unchanged; existing call sites continue to work.
- Rename internal `LogManager.loggerFactory` field to `_loggerFactory` to
  match the underscore-prefix convention (private; no API impact).
- Minor refactors in `IdevsPdfExporter`, `SmartPagination`, `CloudUploadStorage`,
  `IdevsExcelExporter` for readability (early-return inversions, ternary
  `throw` patterns).



### Breaking Changes

- **Removed `Idevs.RepositoryBase<T>`.** Replaced by three new classes under
  `Idevs.Repositories`: `SqlServiceBase` (DB plumbing only), `RepositoryBase<TRow>`
  (typed Serenity row CRUD), `RepositoryBase<TRow, TKey>` (Id-keyed CRUD on `IIdRow`).
- **Constructor signature changed.** Old: `(IServiceProvider, ILogger<T>)`.
  New: `(ISqlConnections)`. Consumers inject `ILogger<T>`, `ITextLocalizer`, etc.
  themselves.
- **`ExceptionLog`, `Localizer`, `ServiceProvider`, `Connection` properties removed.**
- **Auto-detect `Save` removed.** Use `CreateAsync` (insert) and `UpdateAsync`
  (update by Id) explicitly — mirrors Serenity's endpoint convention.
- **`SqlQuery` is now a method**, not a property.
- The `uow` parameter on every method is `IUnitOfWork?` (interface) so derived
  consumers can pass any unit-of-work implementation; Serenity's concrete
  `UnitOfWork` class implements `IUnitOfWork`.

### Added

- Async-first typed CRUD: `FirstAsync`, `ListAsync`, `GetByAsync<TValue>`,
  `CreateAsync`, `GetByIdAsync`, `GetByIdsAsync`, `UpdateAsync`, `DeleteByIdAsync`.
- `[Obsolete]` sync wrappers for each, transitional until ~1.0.
- Optional `IUnitOfWork? uow = null` parameter on every CRUD method for
  transaction composition.
- `[ConnectionKey("...")]` attribute and virtual `ConnectionKey` property on
  `SqlServiceBase`.
- Lazy thread-safe `Dialect` cache via `Lazy<ISqlDialect>`.
- `Idevs.Caching.TwoLevelCacheExtensions` — async wrappers around Serenity
  `ITwoLevelCache` (read-through `GetLocalCachedAsync` and `GetGloballyCachedAsync`
  for reference types).

### Migration

See [MIGRATION.md](MIGRATION.md#v050--v060--repositorybase-redesign).

## 0.5.0 (2026-05-01)

### Breaking Changes

- Restructured repository projects under `src/` and `tests/`.
- Removed `StaticServiceProvider`; use constructor DI or `StaticServiceLocator`.
- Moved Autofac integration to `Idevs.Net.CoreLib.Autofac`.
- Removed Autofac dependencies from `Idevs.Net.CoreLib`.

### Added

- Added optional `Idevs.Net.CoreLib.Autofac` package.
- Added optional `Idevs.Net.CoreLib.Serilog` package.
- Added CloudUploadStorage support for AWS S3 and Cloudflare R2.
- Added provider-neutral `LogManager` based on `Microsoft.Extensions.Logging`.
- Consolidated registration attributes into `ComponentModels/ServiceRegistrationAttributes.cs`
  with a new `IServiceRegistrationAttribute` interface implemented by
  `ScopedAttribute`, `SingletonAttribute`, and `TransientAttribute`. Legacy
  `ScopedRegistrationAttribute`, `SingletonRegiatrationAttribute`, and
  `TransientRegistrationAttribute` types remain available under
  `ComponentModels/Obsolete/` and are still recognised by the DI scanner.
- Initial test scaffolding under `tests/Idevs.Net.CoreLib.Tests/` covering
  `ChromeHelper`, `IdevsContentResult`, `ServiceExtensions`, and the
  attribute-namespace contract.

### Build & Tooling

- Adopted Central Package Management via `Directory.Packages.props` and shared
  build defaults via `Directory.Build.props`.
- Added a repo-local `nuget.config` with explicit `nuget.org` source mapping;
  removed the intermittently-unavailable `serenity.is` private feed
  (Serenity packages also publish to nuget.org).
- Hardened the `build/Idevs.Net.CoreLib.targets` `NpmInstall` step:
  cross-platform paths, npm presence detection, opt-out documentation,
  explicit `WorkingDirectory`.
- Bumped `Serenity.Net.Services` to `8.8.9` (net8.0) and `10.3.1` (net10.0).

### Fixed

- `ChromeHelper.DownloadChrome` no longer wraps the async fetch in a redundant
  `Task.Run(...).GetAwaiter().GetResult()`. Added a true async overload
  `DownloadChromeAsync(CancellationToken)`.
- `RepositoryBase<T>.Dialect` is now lazy-cached; it no longer opens and
  disposes a connection on every access.
- `ServiceExtensions.AddIdevsCorelibServices` now logs a `Trace` warning when
  an assembly type-load fails (instead of silently dropping types) and warns
  when a legacy-attributed type is skipped because it lacks the conventional
  `I{ClassName}` interface.
- `IdevsContentResult.CreatePdfViewResult` accepts `string? downloadName = null`
  with a guard against empty/whitespace input; eliminates a dead null-check on
  the previously non-nullable parameter.

### Migration

- Replace direct `StaticServiceProvider` usage with constructor DI or `StaticServiceLocator`.
- Add `Idevs.Net.CoreLib.Autofac` if the app uses `UseIdevsAutofac`, `IdevsModule`, or keyed service registration.
- Add `Idevs.Net.CoreLib.Serilog` if the app wants Serilog-specific CoreLib logging setup.
- Replace `[ScopedRegistration]` / `[SingletonRegiatration]` / `[TransientRegistration]`
  with the new `[Scoped]` / `[Singleton]` / `[Transient]` attributes from
  `Idevs.ComponentModels`. The legacy attributes still work but are marked obsolete.

## 0.3.3 (2025-10-02)

### Rollback to PuppeteerSharp Only

- Removed Microsoft.Playwright dependency and related code
- Simplified `IdevsPdfExporter` to use only PuppeteerSharp for PDF generation
- Ensured all previous Playwright features are either removed or adapted to PuppeteerSharp
- Updated documentation to reflect the change back to PuppeteerSharp only

~~## 0.3.2 (2025-01-15)~~

~~### Fixed~~

~~- **Playwright Driver Self-Heal**: `ChromeHelper.EnsurePlaywrightBrowsersInstalled()` now validates the node driver, reinstalls Chromium when missing, and wires the required Playwright environment variables so PDF exports no longer fail with "Driver not found" errors.~~

~~### Improved~~

~~- **Runtime Setup**: `IdevsPdfExporter` reuses the enhanced helper ensuring Playwright's runtime is ready before each export, removing manual setup requirements in consuming applications.~~

~~## 0.3.1 (2025-01-14)~~

~~### Added~~

~~- **Playwright Export Option**: `IIdevsPdfExporter` now supports Microsoft.Playwright alongside PuppeteerSharp through the new `PdfExportEngine` flag.~~
~~- **Playwright Dependency**: Bundled `Microsoft.Playwright` to enable browser automation without external setup.~~

~~### Improved~~

~~- **Shared Browser Settings**: Centralized Chromium launch configuration in `ChromeHelper` so PuppeteerSharp and Playwright reuse the same executable and sandbox settings.~~

## 0.3.0 (2025-01-13)

### Breaking

- **Handlebars Removal**: Dropped all template compilation helpers and generic response APIs; the PDF exporter now accepts only pre-rendered HTML plus optional header/footer markup.

### Performance

- **Shared Browser Lifecycle**: Reworked `IdevsPdfExporter` into a singleton that reuses a single Puppeteer browser instance, cutting per-export startup time after the initial warm-up.

### Dependency Injection

- **Singleton Registration**: Updated Autofac and default service registrations so `IIdevsPdfExporter` is provided as a singleton matching the new lifecycle.

## 0.2.11 (2025-01-12)

### Simplified

- **Code Simplification**: Streamlined PDF export logic for better maintainability
    - Simplified template handling by using single dot `"."` placeholders consistently
    - Removed complex conditional PdfOptions assignment logic for cleaner code flow
    - Reverted to direct template usage in template compilation for better predictability
    - Reduced code complexity while maintaining full functionality

- **Template Processing**: Enhanced template handling consistency
    - Uniform placeholder handling across all template scenarios
    - More predictable template compilation behavior
    - Simplified debugging and maintenance workflows

### Developer Experience

- **Cleaner Codebase**: Reduced complexity for easier understanding and maintenance
- **Consistent Behavior**: More predictable PDF generation across different use cases
- **Improved Readability**: Streamlined logic flows for better code comprehension

## 0.2.10 (2025-01-12)

### Improved

- **Template Placeholder Enhancement**: Refined template handling in PDF generation
    - Use single dot `"."` placeholder instead of empty strings for null/empty header/footer templates
    - Better template processing compatibility with Handlebars compilation
    - More consistent template rendering behavior across different scenarios
    - Improved reliability when templates are conditionally empty

### Developer Experience

- **Cleaner Template Logic**: Simplified conditional template assignment with better fallback handling
- **Enhanced Reliability**: More predictable PDF generation when templates are missing or empty

## 0.2.9 (2025-01-12)

### Fixed

- **PDF Template Handling**: Improved template processing in PdfOptionsBuilder
    - Fixed empty template handling by using single space `" "` instead of `string.Empty`
    - Better compatibility with PuppeteerSharp template rendering
    - Prevents potential issues with completely empty header/footer templates

- **PDF Export Logic**: Corrected conditional logic in IdevsPdfExporter
    - Fixed boolean logic error in template path checking condition
    - Changed `||` (OR) to correct logic for proper template selection
    - Removed redundant null coalescing assignment that was unreachable
    - Improved automatic PdfOptions selection based on header/footer presence

### Improved

- **Header/Footer Margin Handling**: Enhanced automatic margin calculation
    - Better detection of when header/footer templates should be applied
    - Improved logic for choosing between CreateWithTemplates vs CreateBusiness options
    - More reliable PDF generation without header cutoff issues

### Developer Experience

- **Cleaner Code Logic**: Simplified conditional flows in PDF export methods
- **Better Template Detection**: More accurate automatic template handling
- **Reduced Code Duplication**: Eliminated unreachable code paths

## 0.2.8 (2025-01-12)

### Enhanced

- **Advanced PDF Options Support**: Comprehensive PdfOptions integration for flexible PDF generation
    - All `ExportByteArrayAsync()` and `CreateResponseAsync()` methods now accept optional `PdfOptions` parameter
    - New dedicated `ExportByteArrayAsync(string html, PdfOptions pdfOptions)` overload with required PdfOptions
    - Enhanced default options with dynamic margins based on header/footer presence
    - Better error handling with descriptive exceptions for failed PDF generation

- **PdfOptionsBuilder Helper Class**: New utility class for creating common PdfOptions configurations
    - `CreateClean()`: Generates PDFs without default browser headers/footers and minimal margins
    - `CreateBusiness()`: Standard business document format with 10mm margins
    - `CreateWithTemplates()`: Custom header/footer template support with dynamic margin calculation
    - Support for custom paper formats, margins, and template configurations

- **Improved Header/Footer Handling**: Smart margin and template management
    - **Automatic Margin Adjustment**: 20mm margins when header/footer present, 0mm when absent
    - **Default Footer Removal**: Eliminates browser-generated page numbers and URLs by default
    - **Clean PDF Generation**: `DisplayHeaderFooter = false` when no custom templates provided
    - Better template validation and error reporting

### Added

- **Documentation**: Comprehensive PDF Footer Removal Guide
    - Step-by-step instructions for removing unwanted default browser footers
    - Migration guide from previous versions with code examples
    - Troubleshooting section for common footer-related issues
    - CSS-based footer control techniques and best practices

### Changed

- **Enhanced Method Signatures**: All PDF export methods now support PdfOptions
    - `ExportByteArrayAsync()` enhanced with optional `PdfOptions` parameter
    - `CreateResponseAsync()` methods support custom PDF generation options
    - `CreateResponseAsync<TModel, TDetail>()` uses `PdfOptionsBuilder.CreateBusiness()` as default
    - Backward compatibility maintained through optional parameters

- **Improved Default Behavior**: Better out-of-the-box PDF generation
    - Automatic detection of header/footer presence for margin calculation
    - Enhanced error handling with null checks and descriptive exception messages
    - Better resource management in PDF generation process

### Developer Experience

- **Simplified Clean PDF Generation**: Easy removal of unwanted browser footers
    ```csharp
    // Automatic clean generation (no default footers)
    var response = await exporter.CreateResponseAsync<MyModel, MyDetail>(model, templatePath);
    
    // Explicit clean options
    var cleanOptions = PdfOptionsBuilder.CreateClean();
    var bytes = await exporter.ExportByteArrayAsync(htmlContent, cleanOptions);
    ```

- **Flexible Configuration**: Multiple ways to customize PDF generation
    ```csharp
    // Business documents with standard margins
    var businessOptions = PdfOptionsBuilder.CreateBusiness();
    
    // Custom configuration
    var customOptions = PdfOptionsBuilder.CreateClean(
        format: PaperFormat.A4,
        margins: ("5mm", "5mm", "10mm", "10mm")
    );
    ```

### Performance

- **Better Resource Management**: Enhanced browser instance handling in PDF generation
- **Improved Error Handling**: More descriptive error messages and validation

## 0.2.7 (2025-09-12)

### Added

- **Smart Pagination System**: Comprehensive pagination utility for multi-page PDF reports
    - New `SmartPagination` class with intelligent page distribution algorithms
    - Support for different page capacities (first page, regular pages, last page)
    - Automatic calculation of optimal page distribution with reserved footer rows
    - Built-in logging for pagination analysis and debugging
    - Line number tracking across pages for professional document formatting
    - Filler row generation for consistent page heights

- **IReportBaseModel Interface**: Type-safe contract for paginated report models
    - Generic `IReportBaseModel<T>` interface for strong typing
    - Eliminates reflection-based pagination detection
    - Clean separation between model structure and pagination logic
    - Support for any detail item type through generics

- **Advanced PDF Template System**: Enhanced template compilation with automatic pagination
    - New `CreateResponseAsync<TModel, TDetail>()` with built-in pagination support
    - Automatic header and footer template compilation from file paths
    - Model-first API design for better developer experience
    - Configurable pagination settings with sensible defaults
    - Extended model creation that merges original data with pagination metadata

### Enhanced

- **IdevsPdfExporter API Improvements**: Modernized method signatures and parameter ordering
    - Model-first parameter ordering for more intuitive usage
    - Optional `PaginationConfig` parameter with smart defaults (25/29/9 configuration)
    - Enhanced error handling with detailed context information
    - Better separation of concerns between templating and PDF generation

- **Template File Support**: Robust template file handling and compilation
    - Automatic template file existence validation
    - Template content validation with meaningful error messages
    - Template caching for improved performance
    - Support for optional header and footer templates

### Changed

- **Breaking API Changes**: Improved method signatures for better usability
    - `CreateResponseAsync` now uses model-first parameter order
    - Added generic type constraints for type safety
    - Enhanced parameter naming and documentation
    - Removed need for manual pagination data creation

### Performance

- **Template Caching**: Improved performance through intelligent template caching
    - `ConcurrentDictionary` for thread-safe template storage
    - Automatic template compilation and reuse
    - Reduced file I/O operations for frequently used templates

### Developer Experience

- **Simplified API Usage**: Streamlined workflow for paginated PDF generation
    - Single method call replaces multiple manual steps
    - Automatic model extension with pagination data
    - Built-in template compilation eliminates boilerplate code
    - Type-safe contracts prevent runtime errors

### Migration Guide

**Before (Manual Pagination):**
```csharp
// Manual pagination and template compilation
var paginatedData = SmartPagination.CreatePaginatedData(items, 25, 29, 9);
var extendedModel = /* manual model creation */;
var headerHtml = /* manual template compilation */;
var response = await exporter.CreateResponseAsync(templatePath, extendedModel, headerHtml, footerHtml);
```

**After (Automatic Pagination):**
```csharp
// Automatic pagination and template compilation
var response = await exporter.CreateResponseAsync<MyModel, MyDetailItem>(
    model, templatePath, headerTemplatePath, footerTemplatePath);
```

**Model Implementation:**
```csharp
public class MyReportModel : IReportBaseModel<MyDetailItem>
{
    public IEnumerable<MyDetailItem> Details { get; set; }
    // ... other properties
}
```

## 0.2.6 (2025-08-28)

### Fixed

- **Critical Browser Connection Fix**: Resolved `PuppeteerSharp.TargetClosedException` when generating multiple PDF reports
    - Changed from static browser instance to per-request browser instances for better reliability
    - Fixed "Target closed" error that occurred on subsequent PDF generations
    - Removed shared browser connection that was causing WebSocket connection issues
    - Improved browser lifecycle management to prevent connection state conflicts
- **Compilation Errors**: Fixed compilation errors related to unused browser management code
    - Removed obsolete `InitializeBrowserAsync` method that referenced removed static fields
    - Cleaned up references to `BrowserLock`, `_browser`, and `_lastBrowserUse` variables
    - Resolved build errors after switching to per-request browser architecture

### Improved

- **PDF Generation Reliability**: Enhanced error handling and resource management
    - Each PDF generation now uses a fresh browser instance
    - Eliminated race conditions from shared browser connections
    - Better resource disposal with proper async using patterns
- **Base64 Conversion Safety**: Added comprehensive error handling for PDF content conversion
    - Enhanced error reporting for Base64 conversion failures
    - Better exception context for debugging PDF generation issues

## 0.2.5 (2025-08-28)

### Added

- **IdevsPdfExporter Template Support**: Added Handlebars.NET integration for template-based PDF generation
    - New `CompileTemplateAsync<TModel>()` method for template compilation with data models
    - New `CreateResponseAsync<TModel>()` method for template-based response generation
    - Template caching with `ConcurrentDictionary` for improved performance
    - Support for custom Handlebars helpers registration via `RegisterCustomHelpers()`

### Enhanced

- **Comprehensive Handlebars Helpers**: Added rich formatting helpers for PDF templates
    - `formatNumber`: Number formatting with culture and precision support
    - `formatCurrency`: Currency formatting with custom symbols and culture support  
    - `formatDate`: Date formatting with pattern and culture support
    - `formatDateTime`: DateTime formatting with pattern and culture support
    - `conditionalClass`: CSS class conditional logic for styling
    - `eq`: Equality comparison helper for template conditions

### Improved

- **Enhanced Error Handling**: Comprehensive null safety and validation improvements
    - Added Guard clauses for all public methods using Ardalis.GuardClauses
    - Enhanced template file validation with existence and content checks
    - Improved browser instance validation and error reporting
    - Added CultureNotFoundException handling in all formatting helpers
    - Better null handling in date parsing and string operations

- **PDF Generation Robustness**: Enhanced browser management and PDF creation reliability
    - Added browser cleanup timer infrastructure for resource management
    - Improved PDF options with better header/footer handling
    - Enhanced browser launch options for better compatibility
    - Added comprehensive validation for PDF generation results

### Changed

- **Method Signatures**: Updated method names for better clarity and consistency
    - Renamed `Export()` to `ExportByteArray()` for clearer intent
    - Renamed `ExportAsync()` to `ExportByteArrayAsync()` for consistency
    - Enhanced all methods with proper CancellationToken support

## 0.2.4 (2025-08-27)

### Improved

- **IdevsPdfExporter Performance**: Enhanced PDF generation with improved browser lifecycle management
    - Optimized Puppeteer browser instance handling with proper async disposal
    - Added network idle state waiting for better content rendering reliability
    - Improved PDF options configuration for better CSS page size handling

## 0.2.3 (2025-08-26)

### Improved

- **ChromeHelper Apple Silicon Detection**: Enhanced Apple Silicon (ARM64) detection with more reliable fallback mechanisms
    - Added native system call detection using `sysctl hw.optional.arm64`
    - Improved fallback detection using `RuntimeInformation.OSArchitecture` and environment variables
    - Better compatibility across different macOS versions and execution contexts

## 0.2.2 (2025-08-26)

### Added

- **ChromeHelper ARM64 Support**: Added support for ARM64 architecture on macOS (Apple Silicon)
- **ChromeHelper Linux Support**: Added support for Linux operating system with both x64 and ARM64 architectures
- Enhanced architecture detection using `RuntimeInformation.ProcessArchitecture` for proper Chrome binary selection

### Changed

- **ChromeHelper.GetChromePath()**: Updated to detect system architecture and select appropriate Chrome binary paths:
  - macOS: `chrome-mac-arm64` for Apple Silicon, `chrome-mac-x64` for Intel
  - Linux: `chrome-linux-arm64` for ARM64, `chrome-linux64` for x64
  - Windows: Continues to use `chrome-win64` (compatible with ARM64 through emulation)

## 0.2.1 (2025-08-20)

## 0.2.0 (2025-08-16)

### Breaking Changes

- **Autofac Integration**: Introduced Autofac as the primary dependency injection container
- **ServiceExtensions Modernization**: Replaced traditional ServiceExtensions with Autofac modules
- **Enhanced Registration**: Improved service registration with better lifetime management

### Added

- `IdevsModule`: New Autofac module for automatic service registration
- `UseIdevsAutofac()`: Extension method for WebApplicationBuilder to configure Autofac
- `StaticServiceLocator`: New thread-safe static service locator with Autofac support
- `UseIdevsStaticServiceLocator()`: Extension method for WebApplication to initialize static service resolution
- **Enhanced Registration Attributes**: New standard attributes (`[Scoped]`, `[Singleton]`, `[Transient]`) with advanced features
- **Named Service Registration**: Support for service keys in Autofac (e.g., `[Scoped(ServiceKey = "mykey")]`)
- **Explicit Service Types**: Ability to specify exact service interfaces (`[Scoped(ServiceType = typeof(IMyService))]`)
- **Self-Registration**: Option to register services without interfaces (`[Scoped(AllowSelfRegistration = true)]`)
- Multiple Autofac configuration overloads for advanced scenarios
- Support for custom container configuration
- Better performance through Autofac's optimized dependency resolution
- Static service resolution with scoping support
- Automatic container type detection (Autofac vs traditional DI)

### Changed

- **Service Registration**: `AddIdevsCorelibServices()` now supports both Autofac and fallback scenarios
- **Lifetime Management**: Improved service lifetime scoping with Autofac
- **Module-Based Architecture**: Services organized into logical modules
- **Backward Compatibility**: Legacy ServiceExtensions still supported

### Deprecated

- `RegisterServices()`: Marked as obsolete, functionality merged into `AddIdevsCorelibServices()`

### Migration Guide

**Recommended (New Autofac approach):**
```csharp
// Replace this
builder.Services.AddIdevsCorelibServices();

// With this
builder.UseIdevsAutofac();
```

**Legacy Support:**
```csharp
// This still works for backward compatibility
builder.Services.AddIdevsCorelibServices();
```

## 0.1.1 (2025-07-21)

### Refactor

- Update to use Serene 8.8.6
- Refactor all

## 0.1.0 (2024-12-05)

### Breaking Changes

- Support only dotnet 8 (if you want to use dotnet 6, please use version 0.0.92)
- Start using Serene 8.8.1
- Refactor project structure, namespace, and class name
- Remove unnecessary code

## 0.0.92 (2024-09-03)

### Add

- Add ContentResponse to support string return response

## 0.0.91 (2024-08-29)

### Changes

- Change CreatePdfViewResult to use FileStreamResult instead of FileContentResult

## 0.0.90 (2024-08-29)

### Add

- Add CreatePdfViewResult to PdfExporter for direct open in browser instead of download first

## 0.0.89 (2024-08-28)

### Update

- Add --no-sandbox and --disable-extensions to launchoption on puppeteer

## 0.0.88 (2024-08-26)

### Update

- Update libraries

## 0.0.87 (2024-08-17)

### Update

- Add browserPath to PdfExporter.Export to use existing Chrome installed instead of download everytime.

## 0.0.86 (2024-08-17)

### Fixed

- Fixed RegisterService throw System.Reflection.ReflectionTypeLoadException with 'SqlGuidCaster'

## 0.0.85 (2024-08-17)

### Updates

- Update library version
- Update RepositoryBase, move default dialect instance on DotNet 8

## 0.0.84 (2024-03-24)

### Updates

- Update library version
- Update RepositoryBase to share ServiceProvider on DotNet 8

## 0.0.83 (2024-01-27)

### Add

- Add EnumEditorAttribute that support EnumKey

### Updates

- Update Serenity package to version 8.2.2 for DotNet 8

## 0.0.82 (2024-01-23)

### Changes

- Add different ExceptionLog for .Net6 and .Net8

## 0.0.81 (2024-01-23)

### Try

- Remove get required service for ExceptionLog for DotNet 8

## 0.0.80 (2024-01-15)

### Fixed

- Fixed RepositoryBase for DotNet 8

## 0.0.79 (2024-01-14)

### Changes

- Rollback to original with some refactor. Error from Microsoft.Data.SqlClient have to install new version

## 0.0.78 (2024-01-14)

### Changes

- Refactory RegisterServices again

## 0.0.77 (2024-01-14)

### Changes

- Refactor RegisterServices

## 0.0.76 (2024-01-14)

### Changes

- Remove IConfiguration from RegisterServices
- Rename AddIdevCoreLibServices and also call RegisterServices

## 0.0.75 (2024-01-13)

### Updates

- Add service register

## 0.0.74 (2024-01-13)

### Changes

- Add support DotNet 8.0 with serenity 8.1.5
- Remove unnecessary part
- Remove support aggregate columns from ExcelExporter it's cause performance issue

## 0.0.73 (2023-10-23)

### Updates

- Update CheckboxButtonEditorAttribute add option IsStringId

## 0.0.72 (2023-10-15)

### Changes

- Rollback serenity to version 6.5.1
- Rollback ILogger to IExceptionLogger may be implement after test
- Update RepositoryBase
- Update ExcelExporter
- Upgrade library

## 0.0.71 (2023-10-15)

### Changes

- Remove PugPDF.Core
- Update IdevsExportRequest, PageMargin
- Add PageSize with PageSizes enum and PageOrientations enum

## 0.0.70 (2023-10-14)

### Updates

- Add method GetPageSize and GetMargin to IdevsExportRequest

## 0.0.69 (2023-10-14)

### Updates

- Update IdevsExportRequest by add property PageSize and PageMargin

## 0.0.68 (2023-10-14)

### Updates

- Add PuppeteerSharp library for export to pdf and will be remove PugPDF.Core and WkHtmlToPdf later.

## 0.0.67 (2023-08-27)

### Updates

- Add default font for ClosedXML

## 0.0.66 (2023-06-10)

### Updates

- Updates mapping display format to cell format in Excel Exporter

## 0.0.65 (2023-06-10)

### Updates

- Add space before %

## 0.0.64 (2023-06-10)

### Added

- Add support display percent for Excel Export
- Add DisplayPercentageAttribute

## 0.0.63 (2023-06-08)

### Fixed

- Fixed excel export with group that not group on the first column

## 0.0.62 (2023-06-04)

### Updates

- Remove blank header line from report headers when generate excel

## 0.0.61 (2023-06-03)

### Added

- Add conditionRange to IdevsExportRequest

## 0.0.60 (2023-06-01)

### Fixed

- Rename and remove TableTheme property

## 0.0.59 (2023-06-01)

## Changes

- Changes ExcelExporter Export signature

## 0.0.58 (2023-06-01)

### Fixed

- Fixed nullable type for theme style

## 0.0.57 (2023-06-01)

### Added

- Add support customize table theme style for ExcelExporter

## 0.0.56 (2023-05-31)

### Fixed

- Finally fixed grouping for ExcelExporter

## 0.0.55 (2023-05-31)

### Fixed

- Fixed grouping for ExcelExporter again #2

## 0.0.54 (2023-05-31)

### Fixed

- Fixed grouping for ExcelExporter again

## 0.0.53 (2023-05-31)

### Fixed

- Fixed mistake for grouping on ExcelExporter

## 0.0.52 (2023-05-31)

### Fixed

- Fixed grouping for ExcelExporter

## 0.0.51 (2023-05-31)

### Fixed

- Fixed row calculation for Group on ExcelExporter

## 0.0.50 (2023-05-31)

### Added

- Add feature for ExcelExporter to support group in aggregate columns

## 0.0.49 (2023-05-27)

### Fixed

- Fixed summary row title again

## 0.0.48 (2023-05-27)

### Fixed

- Fixed summary row title

## 0.0.47 (2023-05-27)

### Updates

- Added entity property to IdevsExportRequest

## 0.0.46 (2023-05-27)

### Updates

- Add support label for aggregate columns
- Fixed boundary range table

## 0.0.45 (2023-05-26)

### Changes

- Changes to use total rows approach fo aggregate columns

## 0.0.44 (2023-05-26)

### Fixed

- Fixed error result for aggregate columns

## 0.0.43 (2023-05-26)

### Added

- Add enum type for Aggregate

## 0.0.42 (2023-05-26)

### Updates

- Prevent ambiguous call for export with aggregate column

## 0.0.41 (2023-05-26)

### Updates

- Add support aggregate column for summary row
- Remove unnecessary property for ExcelExportRequest

## 0.0.40 (2023-05-24)

### Updates

- Add property to IdevsExportRequest

## 0.0.39 (2023-04-16)

### Updates

- Move excel header to after AdjustToContents

## 0.0.38 (2023-04-15)

### Added

- Add EnumExtensions.GetDescription

## 0.0.37 (2023-04-14)

### Updates

- Update export excel report arguments

## 0.0.36 (2023-04-14)

### Added

- Added export excel report using CloxedXML.Report

## 0.0.35 (2023-04-14)

### Fixed

- Final fix error Excel export with number formatting

## 0.0.34 (2023-04-12)

### Fixed

- Fixed error Excel export with format again

## 0.0.33 (2023-04-12)

### Fixed

- Fixed error Excel export with format

## 0.0.32 (2023-04-12)

### Fixed

- Fixed error with register service using reflection

## 0.0.31 (2023-04-12)

### Added

- Add register service using reflection

## 0.0.30 (2023-04-12)

### Updated

- Update ExcelExporter to display correct format

## 0.0.29 (2023-04-03)

### Added

- Add ResponseModel

## 0.0.28 (2023-04-02)

### Added

- Add DateMonthFormatterAttribute

## 0.0.27 (2023-04-02)

### Added

- Added DateMonthEditorAttribute

## 0.0.26 (2023-03-26)

### Fixed

- Fixed error from TrimModel

## 0.0.25 (2023-03-26)

### Added

- ModelExtensions TrimModel

## 0.0.24 (2023-03-24)

### Fixed

- Fixed starting row

## 0.0.23 (2023-03-24)

### Added

- Added ExcelExport parameter to add headers

## 0.0.22 (2023-03-24)

### Changes

- change ExcelExport function Generate from private to public to allow changes column title

## 0.0.21 (2023-03-23)

### Changes

- Add more property to IdevsExportRequest

## 0.0.20 (2023-03-22)

### Changes

- Add interface IIdevsExcelExporter inherited from IExcelExporter
- Rename ExcelExporter to IdevsExcelExporter
- Rename PdfExporter to IdevsPdfExporter

## 0.0.19 (2023-03-22)

### Updated

- IdevsExportRequest added Filters property

## 0.0.18 (2023-03-13)

### Added

- LookupFormatterAttribute

## 0.0.17 (2023-03-09)

### Added

- CheckboxButtonEditorAttribute

## 0.0.16 (2023-03-05)

## Fixed

Fixed targets to install dependencies packages

## 0.0.15 (2023-03-04)

## Fixed

Fixed targets for install npm packages idevs.corlib

## 0.0.14 (2023-03-04)

### Changed

Revert to previous targets and let user install dependencies himself

## 0.0.13 (2023-03-04)

### Changed

Update targets to install needed dependencie packages

## 0.0.12 (2023-03-04)

### Added

- ComponentModes/DisplayNumberFormatAttribute

### Changed

Rename folder ComponentModel to ComponentModels

## 0.0.11 (2023-03-03)

### Fixed

Fixed project files to copy scripts

## 0.0.10 (2023-03-03)

### Changes

All new changes

## 0.0.9 (2023-03-01)

### Added

- Repositories/RepositoryBase
- Helpers/ViewRenderer
- Models/PdfContentResult
- Services/ExcelExporter
- Services/PdfExporter
- Services/StaticServiceProvider

### Changes

- Extensions/TextLocalizerExtensions -> change name and add more methods

### Fixed

Fixed ZeroDisplayFormatterAttribute and CheckboxFormatterAttribute

## 0.0.8 (2023-02-28)

### Fixed

Try to fixed display text not show on grid

## 0.0.7 (2023-02-28)

### Changed

Update arguments assignments for formatter attributes

## 0.0.6 (2023-02-28)

### Changed

Try to change arguments assignment

## 0.0.5 (2023-02-28)

## Fixed

- Fixed ZeroDisplayFormatterAttribute's arguments
- Fixed CheckboxFormatterAttribute's attributes

## 0.0.4 (2023-02-28)

## Changed

- Update ZeroDisplayFormatterAttribute options
- Update CheckboxFormatterAttribute options

## 0.0.3 (2023-02-28)

## Added

- ZeroDisplayFormatterAttribute

## Changed

- Removed ZeroToBlankFormatterAttribute

## 0.0.2 (2023-02-26)

### Changed

- Removed Content/css from nuget package and use from npm package @idevs/corelib instead

## 0.0.1 (2023-02-26)

First published on nuget.org.

### Added

- ComponentModel

    - DisplayDateFormatAttribute with default format dd/MM/yyyy
    - DisplayDateTimeFormatAttribute with default format dd/MM/yyyy HH:mm:ss or dd/MM/yyyy HH:mm on your choice
    - DisplayTimeFormatAttribute with default format HH:mm:ss or HH:mm:ss on your choice
    - ColumnWidthAttribute extends FormWidthAttribute to support bootstrap's col-xxl
    - FullColumnWidthAttribute inherits from ColumnWidthAttribute
    - HalfColumnWidthAttribute inherits from ColumnWidthAttribute
    - CheckboxFormatterAttribute
    - ZeroToBlankFormatterAttribute

- Extensions

    - ControllerExtensions
    - EntityQueryExtensions
    - NumberExtensions
    - TextLocalizerExtensions

- Content/css
    - idevs.dropdown.css
    - idevs.font.css
    - idevs.print.css
