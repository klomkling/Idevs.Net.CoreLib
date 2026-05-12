# Migration Guide

Consolidated upgrade notes for `Idevs.Net.CoreLib`. Newest first.

## Contents

- [v0.7.8 → v0.7.9 — RepositoryBase rename + v0.3.3 legacy shim](#v078--v079--repositorybase-rename--v033-legacy-shim)
- [v0.7.7 → v0.7.8 — Optimistic concurrency on UpdateAsync](#v077--v078--optimistic-concurrency-on-updateasync)
- [v0.7.6 → v0.7.7 — ISequenceProvider helper](#v076--v077--isequenceprovider-helper)
- [v0.7.5 → v0.7.6 — Row-lock primitives + InNewTransactionAsync](#v075--v076--row-lock-primitives--innewtransactionasync)
- [v0.7.4 → v0.7.5 — CountAsync + ExistsAsync helpers](#v074--v075--countasync--existsasync-helpers)
- [v0.7.3 → v0.7.4 — Explicit-fields Create/Update + NotMapped/Expression handling](#v073--v074--explicit-fields-createupdate--notmappedexpression-handling)
- [v0.7.2 → v0.7.3 — Unit of Work Helpers (BeginUnitOfWork + CommitOnSuccessAsync)](#v072--v073--unit-of-work-helpers-beginunitofwork--commitonsuccessasync)
- [v0.7.1 → v0.7.2 — RepositoryBase Criteria-Based Update/Delete + TryFirst Alias](#v071--v072--repositorybase-criteria-based-updatedelete--tryfirst-alias)
- [v0.6.x → v0.7.0 — Source-Generator DI Registration](#v06x--v070--source-generator-di-registration)
- [v0.5.0 → v0.6.0 — RepositoryBase Redesign](#v050--v060--repositorybase-redesign)
- [v0.3.x → v0.5.0 — Package Layout & DI Changes](#v03x--v050--package-layout--di-changes)
- [v0.1.x → v0.2.0 — Autofac Integration](#v01x--v020--autofac-integration)
- [v0.0.x → v0.1.x — Service Registration & Chrome Setup](#v00x--v01x--service-registration--chrome-setup)

---

## v0.7.8 → v0.7.9 — RepositoryBase rename + v0.3.3 legacy shim

### What changed

Two coordinated moves so downstream projects can migrate gradually:

| Before (v0.7.8) | After (v0.7.9) |
|---|---|
| `Idevs.Repositories.RepositoryBase<TRow>` | `Idevs.Repositories.RowRepositoryBase<TRow>` |
| `Idevs.Repositories.RepositoryBase<TRow, TKey>` | `Idevs.Repositories.RowRepositoryBase<TRow, TKey>` |
| *(removed in 0.6.0)* | `Idevs.RepositoryBase<T>` re-introduced as `[Obsolete]`, v0.3.3 body verbatim |

The rename is mechanical: primary-constructor shape `(ISqlConnections)`,
every method signature, every behaviour is identical. Only the type name
changed. PublicAPI tracking handled via `*REMOVED*` markers on the
shipped entries plus the new shipped-shape entries on the renamed type.

The re-introduced `Idevs.RepositoryBase<T>` is the v0.3.3 plumbing-only
class — `T` is the `ILogger<T>` category type (NOT a Serenity row). Its
ctor is `(IServiceProvider, ILogger<T>)`. It exposes `ServiceProvider`,
`ExceptionLog`, `SqlConnections`, `Localizer`, `Connection`, `Dialect`,
and the `SqlQuery` / `SqlInsert(t)` / `SqlUpdate(t)` / `SqlDelete(t)`
factories. Marked `[Obsolete]`; scheduled for removal in **v1.0**.

### Why the rename matters

`RepositoryBase` carried two completely incompatible meanings across the
v0.3.x and v0.6.x+ generations — same name, different generic parameter
semantics (`T` as logger category vs. `TRow` as a Serenity `IRow`). Same
name, two shapes, is a footgun for any downstream project that touches
both eras. Renaming the typed-row variant to `RowRepositoryBase<TRow>`
removes the ambiguity.

### What you need to do

**New repositories** — derive from `RowRepositoryBase` from day one:

```csharp
using Idevs.Repositories;

public class OrderRepository(ISqlConnections c)
    : RowRepositoryBase<OrderRow, int>(c), IOrderRepository
{
}
```

**Existing v0.7.x repositories** — rename the base class. Nothing else
moves:

```diff
-public class OrderRepository(ISqlConnections c)
-    : RepositoryBase<OrderRow, int>(c), IOrderRepository { }
+public class OrderRepository(ISqlConnections c)
+    : RowRepositoryBase<OrderRow, int>(c), IOrderRepository { }
```

A regex/sed pass is safe. The `-i` flag differs between BSD sed (macOS)
and GNU sed (most Linux + CI), so pick the variant for your platform:

```bash
# macOS (BSD sed) — empty string after -i
grep -rln 'RepositoryBase<' --include='*.cs' \
    | xargs sed -i '' 's/RepositoryBase</RowRepositoryBase</g'

# Linux / CI (GNU sed) — no argument after -i
grep -rln 'RepositoryBase<' --include='*.cs' \
    | xargs sed -i 's/RepositoryBase</RowRepositoryBase</g'
```

If you need one snippet that runs on both (e.g., in a script shared with
CI), use the explicit-backup form and clean up afterwards:

```bash
grep -rln 'RepositoryBase<' --include='*.cs' \
    | xargs sed -i.bak 's/RepositoryBase</RowRepositoryBase</g' \
    && find . -name '*.bak' -delete
```

Watch for two things during a mechanical replace:

1. **Your own classes named `RepositoryBase*`** (e.g., `AppRepositoryBase`,
   `RepositoryBaseTests`). The pattern above is generic-aware so it only
   touches generic uses, but verify with a `git diff` review.
2. **Code on the v0.3.3 API** — see the next section.

**Existing v0.3.3 repositories** (`Idevs.RepositoryBase<TSelf>` pattern,
ctor `(IServiceProvider, ILogger<T>)`) — keep them on the legacy class
for now; they continue to compile. The `[Obsolete]` warning is the only
behavioural change. Plan migration to `RowRepositoryBase` before v1.0.

### Why the legacy shim is full v0.3.3 body, not a subclass

The two `RepositoryBase` classes have incompatible ctors and protected
surfaces (logger category vs. Serenity row; service-provider injection
vs. `ISqlConnections`). A subclass shim cannot bridge them. The legacy
class is restored verbatim so downstream code that calls
`base(serviceProvider, logger)`, reads `Connection`, or composes
`SqlQuery() / SqlInsert(table)` keeps working unmodified.

### Removal timeline

- **0.7.9** (this release) — rename ships. `RepositoryBase` (typed-row) is
  gone from `Idevs.Repositories`. Legacy `Idevs.RepositoryBase<T>` ships
  as `[Obsolete]`.
- **0.8.x → 0.9.x** — `[Obsolete]` warnings continue. No behaviour change.
- **1.0** — `Idevs.RepositoryBase<T>` removed.

---

## v0.7.7 → v0.7.8 — Optimistic concurrency on UpdateAsync

### What changed

Two additive types and a behavioural opt-in:

| API | Returns / role |
|---|---|
| `[RowVersion]` attribute | Marker on a `long?` property; flags the row's version column. |
| `OptimisticConcurrencyException` | Thrown by `UpdateAsync` when a guarded UPDATE affects 0 rows. Carries `TableName`, `RowId`, `CapturedVersion`. |

When a row carries `[RowVersion]`, the three TRow-shaped UpdateAsync
overloads on `RepositoryBase<TRow, TKey>` automatically apply the
optimistic-concurrency guard:

1. Capture the row's current `RowVersion` value.
2. Add `WHERE RowVersion = @captured` to the UPDATE.
3. Add `SET RowVersion = RowVersion + 1` to the SET list.
4. Run with `ExpectedRows.Ignore` (we manually check affected count).
5. If affected rows == 0, throw `OptimisticConcurrencyException`.
6. Otherwise, write `captured + 1` back to `row.RowVersion` so the
   caller can reuse the row for further updates without re-reading.

Rows without `[RowVersion]` see zero behaviour change. The
criteria-based `UpdateAsync(Action<SqlUpdate>, …)` overload on
`RepositoryBase<TRow>` is also unchanged — that overload takes an
arbitrary builder where the caller owns the WHERE.

### Caller-side example

```csharp
public sealed class OrderRow : Row<OrderRow.RowFields>, IIdRow
{
    [Identity, IdProperty]
    public int? Id { get => fields.Id[this]; set => fields.Id[this] = value; }

    [Size(50), NotNull]
    public string? CustomerCode
    {
        get => fields.CustomerCode[this];
        set => fields.CustomerCode[this] = value;
    }

    [NotNull, RowVersion]                              // <-- one attribute
    public long? RowVersion
    {
        get => fields.RowVersion[this];
        set => fields.RowVersion[this] = value;
    }

    public sealed class RowFields : RowFieldsBase
    {
        public Int32Field Id;
        public StringField CustomerCode;
        public Int64Field RowVersion;
    }
}

// Caller code — same shape as before, with one extra catch:
var order = await repo.GetByIdAsync(orderId);
order.CustomerCode = newCode;
try
{
    await repo.UpdateAsync(order);
}
catch (OptimisticConcurrencyException)
{
    // Stale write. Re-read, re-apply, retry — pattern is consumer's choice.
}
```

### Schema

Application-managed `BIGINT`, portable across all supported engines.
Add to existing tables that need optimistic concurrency:

#### SqlServer

```sql
ALTER TABLE Orders ADD RowVersion BIGINT NOT NULL DEFAULT 0;
```

#### MySQL / MariaDB

```sql
ALTER TABLE Orders ADD COLUMN RowVersion BIGINT NOT NULL DEFAULT 0;
```

#### PostgreSQL

```sql
ALTER TABLE Orders ADD COLUMN RowVersion BIGINT NOT NULL DEFAULT 0;
```

The library does not use SqlServer's native `rowversion` / `timestamp`
type. Reasons: not portable (`varbinary(8)` only exists on SqlServer),
changes the field type story (would need `byte[]?` instead of `long?`),
and a 64-bit application-managed counter has more than enough range
(at one increment per nanosecond it would take ~292 years to exhaust).

### The retry pattern

The library does not ship a built-in retry helper — number of retries,
backoff, and "what should the user see when retries fail" are all
policy decisions. Use the standard manual shape:

```csharp
async Task<int> UpdateWithRetryAsync(int orderId, Action<OrderRow> mutate, int maxAttempts = 3)
{
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var order = await _repo.GetByIdAsync(orderId);
        mutate(order);
        try
        {
            await _repo.UpdateAsync(order);
            return order.RowVersion!.Value;
        }
        catch (OptimisticConcurrencyException) when (attempt < maxAttempts)
        {
            // Re-read on next iteration. Polly callers can also catch
            // OptimisticConcurrencyException and apply their own policy.
        }
    }

    throw new OptimisticConcurrencyException(
        tableName: "Orders", rowId: orderId, capturedVersion: -1);
}
```

A built-in `RetryOnConcurrencyConflictAsync` may land in 0.8.0+ if
real demand emerges; for now, document the manual pattern.

### Validation — the library fails loud on misuse

These all throw `InvalidOperationException` at the first guarded
UPDATE call (and the result is cached so subsequent calls don't pay
the reflection cost):

| Misuse | Error |
|---|---|
| Two properties carry `[RowVersion]` | "Row type 'X' has 2 [RowVersion] properties (A, B); expected at most one." |
| `[RowVersion]` on a non-`long?` property | "Property 'X.A' must be of type 'long?'; got 'int?'. Use `public long? A { get; set; }` …" |
| `[RowVersion]` property has no matching `Field` in `RowFields` | "Property 'X.A' has no matching Field in RowFields. Add `public Int64Field A;` …" |
| `[RowVersion]` field has `Updatable` flag cleared | "Field 'X.A' is not Updatable. The library increments this field on every guarded UPDATE; clearing the Updatable flag would prevent that." |
| `row.RowVersion` is `null` at update time | "RowVersion is null on the row passed to UpdateAsync. Read the row before updating — null means the captured version is unknown." |

The last one catches "constructed by hand" bugs early. If a caller
populates a row from a cache or maps from a DTO without setting
`RowVersion`, the call fails with a clear message instead of silently
running an unguarded UPDATE.

### Important caveats

- **The criteria-based `UpdateAsync(Action<SqlUpdate>, …)` overload on
  `RepositoryBase<TRow>` is NOT guarded.** If you use that overload on
  a versioned row, you own the `WHERE RowVersion = …` clause. Add
  it manually or use the row-shaped overloads.
- **Backfill `RowVersion` on existing tables.** When you add the
  column to existing tables with the DDL above, the `DEFAULT 0` fills
  in for existing rows — they all get version 0. The first update on
  any of those rows captures 0 and advances to 1. No data migration
  needed.
- **Reflection, but cached.** `[RowVersion]` is detected via
  reflection at first lookup per row type, then cached as a `Field`
  reference in a `ConcurrentDictionary<Type, Field?>`. Steady-state
  cost is one dictionary read per UPDATE.
- **No interference with `ISequenceProvider`.** Sequence rows
  (`IdevsSequenceRow`) don't carry `[RowVersion]` and won't pick up
  the guard. The two features are independent.
- **`CreateAsync` doesn't set RowVersion.** Leave the property `null`
  on the row instance you pass to `CreateAsync`; the database default
  (`0`) takes over. Subsequent UpdateAsync calls capture 0 and
  advance from there.

---

## v0.7.6 → v0.7.7 — ISequenceProvider helper

### What changed

A higher-level helper for atomic sequence allocation. Eliminates the
need to write the `InNewTransactionAsync` + `ForUpdate()` boilerplate
in every consumer site that allocates document/invoice/order numbers.

| API | Returns | What it does |
|---|---|---|
| `ISequenceProvider.NextAsync(string sequenceKey, ct)` | `Task<long>` | Allocate the next value; throws if the sequence isn't seeded. |
| `ISequenceProvider.NextRangeAsync(string sequenceKey, int count, ct)` | `Task<IReadOnlyList<long>>` | Allocate `count` contiguous values atomically. |
| `ISequenceProvider.EnsureSequenceAsync(string sequenceKey, long startValue = 1, ct)` | `Task` | Idempotently seed a sequence row. No-op if it already exists. |

The default implementation (`SqlSequenceProvider`) is registered
automatically via `[Scoped(ServiceType = typeof(ISequenceProvider))]`
when the source generator runs. Manual DI hosts call
`services.AddIdevsSequenceProvider()`.

### Why

The 0.7.6 primitives (`ForUpdate()` + `InNewTransactionAsync`) unblock
the race; this helper turns the allocation pattern into a single line
of caller code and removes the decision points (which uow? which lock
mode? when to commit?) that produced the bug in the first place.

### Schema

Consumers create the storage table in their own migration pipeline.
DDL by engine:

#### SqlServer

```sql
CREATE TABLE [IdevsSequences] (
    [SequenceKey] NVARCHAR(100) NOT NULL PRIMARY KEY,
    [NextValue]   BIGINT        NOT NULL
);
```

#### MySQL / MariaDB

```sql
CREATE TABLE IdevsSequences (
    SequenceKey VARCHAR(100) NOT NULL PRIMARY KEY,
    NextValue   BIGINT       NOT NULL
) ENGINE=InnoDB;
```

#### PostgreSQL

```sql
CREATE TABLE IdevsSequences (
    SequenceKey VARCHAR(100) NOT NULL PRIMARY KEY,
    NextValue   BIGINT       NOT NULL
);
```

The library does not ship migrations.

### Migrating from manual SELECT-then-UPDATE

Existing 37-callsite shape using the 0.7.6 primitives directly:

```csharp
// 0.7.6 — primitives wired by hand
public Task<string> GetNextDocNoAsync(string docCode, CancellationToken ct = default) =>
    InNewTransactionAsync(async (uow, token) =>
    {
        var row = await TryFirstAsync(
            q => q.SelectTableFields()
                  .Where(DocumentNumberRow.Fields.DocumentCode == docCode)
                  .ForUpdate(),
            uow, token);
        if (row is null) throw new InvalidOperationException(...);

        row.NextDocumentNo += 1;
        await UpdateAsync(row, uow, token);
        return Format(docCode, row.NextDocumentNo!.Value);
    }, ct);
```

Becomes, in 0.7.7:

```csharp
// 0.7.7 — helper does the locking, transaction, and increment
public sealed class DocumentNumberService(ISequenceProvider sequences)
{
    public async Task<string> GetNextDocNoAsync(
        string docCode, CancellationToken ct = default)
    {
        var n = await sequences.NextAsync($"DocNo:{docCode}", ct);
        return Format(docCode, n);
    }
}
```

### Seeding

Before the first `NextAsync`, the sequence row must exist. Two options:

```csharp
// Option 1: at app startup, ensure all known sequences.
await sequences.EnsureSequenceAsync("DocNo:SaleOrder", startValue: 1);
await sequences.EnsureSequenceAsync("DocNo:Invoice", startValue: 1);

// Option 2: in your migration script, INSERT directly:
//   INSERT INTO IdevsSequences (SequenceKey, NextValue)
//   VALUES ('DocNo:SaleOrder', 1), ('DocNo:Invoice', 1);
```

`EnsureSequenceAsync` is a no-op if the row already exists — calling
it on every app startup is safe and idempotent. The `startValue`
argument is ignored when the row exists; it does NOT reset the counter.

### Caller signature change

Note: the helper does NOT accept an `IUnitOfWork` parameter. Callers
that previously threaded a `uow` through the allocation flow drop it
at the `NextAsync` call site:

| Before (0.7.6 caller) | After (0.7.7 caller) |
|---|---|
| `await GetNextDocNoAsync(uow, "SO", ct)` | `await sequences.NextAsync("DocNo:SO", ct)` |

If the caller was passing `uow` to other operations in the same
business flow (e.g. writing the SaleOrder row), keep doing that — only
the sequence-allocation call drops the `uow`.

### Sequence-key naming

The library has no opinion on the key format. Recommended convention:
namespace with colons, deepest segment first.

| Pattern | Use |
|---|---|
| `"DocNo:SaleOrder"` | Per-document-type counters that don't restart. |
| `"DocNo:Invoice:2026"` | Per-document-type, per-year counters. |
| `"InternalId:Customer"` | Surrogate-key allocators. |

Avoid raw user input in the key. The allocator does NOT sanitize — a
`SequenceKey` is whatever string the caller passes.

### Important caveats

- **Independent commit semantics.** Inherited from 0.7.6's
  `InNewTransactionAsync`. If your outer business transaction fails
  AFTER `NextAsync` returns, the allocated number remains. For
  document numbers that's correct (gaps are normal, duplicates are
  catastrophic). For anything where the inner allocation must roll
  back with the outer flow, do NOT use `ISequenceProvider` — implement
  your own row using the caller's `uow`.
- **Connection pool sizing.** Each `NextAsync` opens a separate
  connection (via `InNewTransactionAsync`). At very high allocation
  rates this is wasteful — the 0.8.0/0.9.0 work tracks adding a
  HiLo-style batched provider, but for now verify peak concurrency × 2
  ≤ your pool's `Max Pool Size`.
- **No bulk-insert wiring.** If you're allocating a block to assign to
  many rows in one `INSERT...VALUES (...)`, use `NextRangeAsync` to
  reserve the block in one round trip; allocating one-at-a-time inside
  a loop is correct but issues N round trips.
- **Concurrent `EnsureSequenceAsync`.** Two callers ensuring the same
  key at the same time both succeed without raising an error. The
  implementation issues a single dialect-aware no-error UPSERT per
  call — `INSERT IGNORE` (MySQL/MariaDB), `INSERT ... ON CONFLICT
  DO NOTHING` (PostgreSQL), `INSERT OR IGNORE` (SQLite), an
  `INSERT ... WHERE NOT EXISTS` with `WITH (UPDLOCK, HOLDLOCK)` on
  the existence check (SqlServer), or `MERGE` (Oracle). No
  PK-violation exception is raised in any of these dialects, so the
  surrounding transaction stays valid even under contention. Safe to
  call from app startup in load-balanced fleets.

---

## v0.7.5 → v0.7.6 — Row-lock primitives + InNewTransactionAsync

### What changed

Two new primitives that together unblock the classic SELECT-then-UPDATE
race in caller code (e.g. `DocumentNumberRepository.GetNextDocNoAsync`):

| Primitive | Where | What it does |
|---|---|---|
| `SqlQuery.ForUpdate(LockMode mode = Update)` | extension method | Marks the query for row-level locking. Hint applied dialect-correctly when materialised through Idevs repository helpers. |
| `SqlServiceBase.InNewTransactionAsync<T>` | `protected` on `SqlServiceBase` | Runs work in a fresh connection + transaction, ignoring any ambient `IUnitOfWork`. Commits on success, rolls back on throw. |

Plus a `LockMode` enum (`Update` / `Share` / `UpdateNoWait` / `UpdateSkip`)
and behavioural changes to `RepositoryBase<TRow>.TryFirstAsync` (it now
detects the `ForUpdate()` flag at execution time).

### Why

Before 0.7.6, locking a row for read-then-write required either dropping
to raw Dapper or accepting that the lock window covers the entire
caller's transaction. Both are footguns:

```csharp
// Before — race condition. Two concurrent callers can both read 41 and
// both write 42, producing duplicate document numbers.
public Task<long> GetNextDocNoAsync(IUnitOfWork uow, ...)
{
    var doc = await TryFirstAsync(q => q.SelectTableFields().Where(...), uow, ct);
    doc.NextDocumentNo += 1;
    await UpdateAsync(doc, uow, ct);
    return doc.NextDocumentNo.Value;
}
```

After 0.7.6:

```csharp
public Task<long> GetNextDocNoAsync(IUnitOfWork? uow, ...)
{
    // InNewTransactionAsync deliberately ignores the caller's uow so the
    // lock window stays small. The number is allocated even if the
    // outer business transaction subsequently rolls back — gaps are OK,
    // duplicates are catastrophic.
    return InNewTransactionAsync(async (innerUow, token) =>
    {
        var doc = await TryFirstAsync(
            q => q.SelectTableFields()
                  .Where(...)
                  .ForUpdate(),                  // <-- new
            innerUow, token);

        doc.NextDocumentNo += 1;
        await UpdateAsync(doc, innerUow, token);
        return doc.NextDocumentNo.Value;
    }, ct);
}
```

### `LockMode` reference

| Mode | SqlServer | MySQL / MariaDB | Postgres | Oracle | SQLite |
|---|---|---|---|---|---|
| `Update` | `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` | `FOR UPDATE` | `FOR UPDATE` | `FOR UPDATE` | throws |
| `Share` | `WITH (HOLDLOCK, ROWLOCK)` | `LOCK IN SHARE MODE` | `FOR SHARE` | not portable; throws | throws |
| `UpdateNoWait` | **throws** (no in-query NOWAIT) | `FOR UPDATE NOWAIT` (8.0+ / 10.6+) | `FOR UPDATE NOWAIT` | `FOR UPDATE NOWAIT` | throws |
| `UpdateSkip` | `WITH (UPDLOCK, HOLDLOCK, ROWLOCK, READPAST)` | `FOR UPDATE SKIP LOCKED` (8.0+ / 10.6+) | `FOR UPDATE SKIP LOCKED` (9.5+) | `FOR UPDATE SKIP LOCKED` | throws |

### `InNewTransactionAsync` semantics — read this before using

The helper opens a **separate** connection and transaction. It commits on
success and rolls back on exception, **regardless of any ambient**
`IUnitOfWork` the caller has open.

The trade-off is intentional: if the outer caller throws after this
helper returns, the work committed inside the helper **remains**. For
sequence allocation that's the correct behaviour — gaps are normal,
duplicates are catastrophic. For anything where the inner write must
roll back with the outer flow, do NOT use `InNewTransactionAsync`: pass
the caller's UoW through and let the outer transaction own the commit.

### Migrating existing call sites

If you currently:

| Pattern | Migration |
|---|---|
| Drop to raw Dapper for `SELECT … FOR UPDATE` | Use `q.ForUpdate()` on the existing `SqlQuery` builder. |
| Use `CommitOnSuccessAsync(work, uow: null, ct)` to bypass an ambient UoW | Use `InNewTransactionAsync(work, ct)` — same behaviour, self-documenting name. |
| Hand-roll connection + `BeginTransaction()` for sequence allocators | Use `InNewTransactionAsync` and put `q.ForUpdate()` on the SELECT inside the body. |

The original `CommitOnSuccessAsync` overload is unchanged — keep using
it for the case where you want to "join the caller's UoW or open a fresh
one" (the default behaviour). `InNewTransactionAsync` is the new helper
for the explicitly-fresh case.

### Behavioural change to `TryFirstAsync`

Queries that don't call `ForUpdate()` execute exactly as before — through
Serenity's `connection.TryFirst<TRow>(q => …)` lambda path. Queries that
DO call `ForUpdate()` take a different code path:

1. The SELECT is materialised via `query.ToString()`.
2. The dialect-correct lock hint is injected via the internal
   `RowLockSqlBuilder`.
3. Execution goes through `SqlHelper.ExecuteReader(connection, sql, params, logger)`
   so transaction propagation through Serenity's `WrappedConnection`
   is preserved.
4. Row mapping uses `SqlQuery.GetFromReader(reader)` — the same
   primitive Serenity's lambda path uses internally.

The lock-aware path requires a non-null `uow`. Calling
`TryFirstAsync(q => q.…ForUpdate(), uow: null)` throws
`InvalidOperationException`.

### Important caveats

- **Direct Serenity execution paths do NOT honour `ForUpdate()`.** If
  you write `connection.TryFirst<TRow>(q => q.…ForUpdate())`,
  `connection.Query<TRow>(q => q.…ForUpdate())`, or any other Serenity
  extension method that takes a `SqlQuery`, the flag is silently
  ignored and the SELECT runs without a lock. Always call through
  Idevs repository helpers when locking matters.
- **`UpdateNoWait` is unsupported on SqlServer.** SqlServer has no
  in-query NOWAIT hint. The library throws. Workaround:
  `SET LOCK_TIMEOUT 0` at the session/transaction level before the
  locking SELECT, then catch SQL error 1222.
- **`UpdateSkip` requires READ COMMITTED or REPEATABLE READ on
  SqlServer.** READPAST is rejected at higher isolation levels with
  error 16957. Verify the active transaction's isolation level before
  relying on `UpdateSkip` against SqlServer.
- **Connection pool sizing.** `InNewTransactionAsync` opens a second
  connection during the call. Verify peak concurrency × 2 ≤ your
  configured `Max Pool Size`.
- **No identity-return path for bulk operations.** If you need the
  identity values from a multi-row operation, do not bulk-merge
  through this helper.

### Verifying production data

If you suspect a race condition has fired silently before adopting this
fix, run a duplicate check against your sequence-counter tables:

```sql
SELECT DocumentCode, NextDocumentNo, COUNT(*)
FROM DocumentNumber
GROUP BY DocumentCode, NextDocumentNo
HAVING COUNT(*) > 1;
```

Any results indicate the race has produced duplicates that need
backfilling before deployment.

---

## v0.7.4 → v0.7.5 — CountAsync + ExistsAsync helpers

### What changed

Two new read-side helpers on `RepositoryBase<TRow>` that complete the
read surface alongside the existing `TryFirstAsync` / `ListAsync` /
`GetByAsync`:

| Helper | Returns | Emits | Use for |
|---|---|---|---|
| `CountAsync(Action<SqlQuery>, ...)` | `Task<long>` | `SELECT COUNT(*) FROM table WHERE ...` | Counting matching rows. Pass `_ => { }` for total. |
| `ExistsAsync(Action<SqlQuery>, ...)` | `Task<bool>` | `SELECT 1 FROM table WHERE ...` + a dialect-specific row-limit clause (`TOP 1` on SQL Server, `LIMIT 1` on MySQL/PostgreSQL/SQLite, `FETCH FIRST 1 ROWS ONLY` on Oracle), via `SqlQuery.Take(1)` | Existence check; short-circuits at first match. |

Both share the same shape as `ListAsync` — caller adds `Where(...)` (and
optional joins, group-by, etc.) inside the lambda.

### Examples

```csharp
// Count — returns long
long activeCount = await repo.CountAsync(q => q
    .Where(SaleOrderRow.Fields.Status == "Active"), uow, ct);

long todayPending = await repo.CountAsync(q => q
    .Where(SaleOrderRow.Fields.OrderDate == DateTime.Today
        && SaleOrderRow.Fields.Status == "Pending"), uow, ct);

long totalRows = await repo.CountAsync(_ => { }, uow, ct);

// Existence check — efficient on large tables (engine short-circuits at
// the first match via SqlQuery.Take(1), which emits TOP/LIMIT/FETCH FIRST
// depending on the active dialect)
var hasOrder = await repo.ExistsAsync(q => q
    .Where(SaleOrderRow.Fields.CustomerCode == code), uow, ct);
```

### Replacing existing patterns

If your code currently does any of these, the new helpers are clearer:

| Before | After |
|---|---|
| `(await repo.ListAsync(q => q.Where(...))).Count` | `await repo.CountAsync(q => q.Where(...))` (avoids materializing rows) |
| `(await repo.TryFirstAsync(q => q.Where(...))) is not null` | `await repo.ExistsAsync(q => q.Where(...))` (smaller projection, dialect-specific row-limit clause) |
| Hand-built `SqlHelper.ExecuteScalar` for counts | `await repo.CountAsync(...)` |

### Why `Task<long>` (not `Task<int>`)

`COUNT(*)` returns a 64-bit value on PostgreSQL and on MySQL (`BIGINT
UNSIGNED`). SQL Server's `COUNT(*)` is 32-bit but upcasts cleanly. Using
`long` for the return type avoids `OverflowException` on large tables on
non-SQL-Server providers, with no downside on SQL Server. Cast to `int`
at the call site if you need it (and you know your count fits):

```csharp
int n = (int)await repo.CountAsync(_ => { });   // explicit narrowing
```

### Limitations

- **No `GROUP BY` / `HAVING` support.** Both clauses make the underlying
  query return multiple rows; `SqlHelper.ExecuteScalar` only reads the
  first row's value, so a grouped count would silently return only the
  first group's count. For grouped counts, use `ListAsync` + LINQ
  `GroupBy`, or build the query manually via `ExecuteAsync` with a
  wrapping `SELECT COUNT(*) FROM (...) g` subquery.

### Sync wrappers

`Count(Action<SqlQuery>, IUnitOfWork?)` (returns `long`) and
`Exists(Action<SqlQuery>, IUnitOfWork?)` (returns `bool`) exist and are
marked `[Obsolete]` — use the async variants in new code.

### Bonus: raw-SQL helpers on `SqlServiceBase`

For codebases that use raw SQL frequently (GeniuzPOS-style) two new
helpers on `SqlServiceBase` cut the typical 5-line `ExecuteAsync` +
`SqlHelper.ExecuteScalar/NonQuery` boilerplate to a single expression:

| Helper | Returns | Use for |
|---|---|---|
| `ExecuteScalarAsync<T>(string sql, IDictionary<string, object?>? parameters = null, ...)` | `Task<T?>` | Raw `SELECT` returning a single value. Returns `default(T)` for `null`/`DBNull`. |
| `ExecuteNonQueryAsync(string sql, IDictionary<string, object?>? parameters = null, ...)` | `Task<int>` | Raw `UPDATE` / `DELETE` / `INSERT` / DDL. Returns affected-row count. |

Both compose with the same `IUnitOfWork? uow = null` and
`CancellationToken ct = default` slots as every other helper.

**Before:**

```csharp
public async Task<int> ArchiveOldOrdersAsync(DateTime cutoff,
    IUnitOfWork? uow = null, CancellationToken ct = default)
{
    return await ExecuteAsync((c, _) =>
    {
        var n = SqlHelper.ExecuteNonQuery(c,
            "DELETE FROM SaleOrders WHERE CreatedAt < @cutoff",
            new Dictionary<string, object?> { ["@cutoff"] = cutoff });
        return Task.FromResult(n);
    }, uow, ct);
}
```

**After:**

```csharp
public Task<int> ArchiveOldOrdersAsync(DateTime cutoff,
    IUnitOfWork? uow = null, CancellationToken ct = default) =>
    ExecuteNonQueryAsync(
        "DELETE FROM SaleOrders WHERE CreatedAt < @cutoff",
        new Dictionary<string, object?> { ["@cutoff"] = cutoff },
        uow, ct);
```

#### MySQL note (DOES apply for `ExecuteNonQueryAsync`)

The matched-rows-vs-changed-rows discrepancy from the v0.7.4 MIGRATION
note also affects raw `UPDATE`/`DELETE` row counts returned by
`ExecuteNonQueryAsync`. If you're on MySQL/MariaDB, ensure
`Use Affected Rows=false;` is in your connection string so the count
reflects matched-rows semantics consistent with SQL Server. Pure
`SELECT` scalars returned by `ExecuteScalarAsync` are unaffected by
this flag.

#### What's intentionally NOT included

- **`QueryAsync<T>` for typed-row raw SELECTs.** Dapper's
  `c.Query<T>(sql, params)` is already a one-liner inside the existing
  `ExecuteAsync` template; adding a wrapper would lock CoreLib into
  Dapper as an explicit dependency. Will revisit if real demand emerges.
- **Anonymous-object parameters (`new { x = 1 }`).** The dictionary form
  matches Serenity's `SqlHelper` convention and avoids surprises around
  how Dapper-style anonymous-object property names map to `@param`
  placeholders.

### MySQL note (does NOT apply here)

The matched-rows-vs-changed-rows discrepancy from the v0.7.4 MIGRATION
note only affects UPDATE/DELETE/INSERT row-count results. `CountAsync`
and `ExistsAsync` are SELECT-based and return scalar values that are
identical across providers. The `Use Affected Rows=false` flag has no
effect on them.

---

## v0.7.3 → v0.7.4 — Explicit-fields Create/Update + NotMapped/Expression handling

### What changed

Four new helpers on `RepositoryBase` for surgical control over which columns
end up in INSERT/UPDATE statements. Plus integration-test-verified behavior
documentation for `[NotMapped]` and `[Expression]` fields.

| Helper | Lives on | When to use |
|---|---|---|
| `CreateAsync(TRow row, Field[] fields, ...)` | `RepositoryBase<TRow>` | Insert ONLY the listed columns. Bypasses Serenity's IsAssigned tracking. |
| `CreateExcludingAsync(TRow row, Field[] excludeFields, ...)` | `RepositoryBase<TRow>` | Insert all assigned, table-mapped fields EXCEPT the listed ones. |
| `UpdateAsync(TRow row, Field[] fields, ...)` | `RepositoryBase<TRow, TKey>` | Update ONLY the listed columns. The row's Id must be set. |
| `UpdateExcludingAsync(TRow row, Field[] excludeFields, ...)` | `RepositoryBase<TRow, TKey>` | Update all assigned, table-mapped fields EXCEPT the listed ones. |

### NotMapped / Expression — the actual behavior (verified end-to-end)

A common assumption is that Serenity automatically skips `[NotMapped]` and
`[Expression]` fields from INSERT/UPDATE. The truth is more nuanced:

#### `[NotMapped]` properties

**Production-correct pattern: declare them as plain CLR auto-properties WITH
NO backing `Field` in `RowFields`.**

```csharp
[NotMapped]
public string? TransientNote { get; set; }   // no fields.X[this] backing
```

The property exists on the row in memory, but Serenity has no `Field`
metadata for it, so it cannot appear in any SQL. Setting it is silent.

If you instead declare a `Field` for it (e.g., `public StringField TransientNote;`
in `RowFields`), Serenity's writes WILL include it (default flag set has
`Insertable | Updatable` on by default, which the `[NotMapped]` attribute does
not clear automatically). To make a backing-field NotMapped column actually
not write, pair it with `[SetFieldFlags(FieldFlags.None, FieldFlags.Insertable | FieldFlags.Updatable)]`.

#### `[Expression]` fields

These ARE meant for SELECT-time materialization (e.g., joined columns,
computed values). Serenity reads them. **But Serenity's `InsertAndGetID` /
`UpdateById` use an IsAssigned-based filter** — if you ASSIGN a value to an
Expression field on the row instance, Serenity will include it in the
INSERT/UPDATE column list, and SQL Server will reject it with "Invalid
column name 'X'".

Three ways to avoid the trap:

1. **Don't assign Expression fields on a write path** (the natural pattern —
   they're outputs of SELECT, not inputs).
2. **Use `CreateExcludingAsync` / `UpdateExcludingAsync`** to drop them
   explicitly even when assigned.
3. **Use the include-only `CreateAsync(row, fields)` / `UpdateAsync(row, fields)`**
   to specify the column list explicitly.

### Migration steps

1. Bump to `0.7.4` in your consumer project:
   ```xml
   <PackageReference Include="Idevs.Net.CoreLib" Version="0.7.4" />
   ```
2. **Audit any rows with `[Expression]` properties.** If your code paths
   assign these (e.g., during deserialization from a DTO that includes the
   computed value), either stop assigning them or switch the call to one
   of the new exclude/include helpers.
3. **(Optional) Replace partial-update patterns** that previously used the
   criteria-based `UpdateAsync(Action<SqlUpdate>)` with the new
   `UpdateAsync(TRow row, Field[] fields)` for cases where the value comes
   from an existing row instance.

### Examples

**Insert only specific columns:**

```csharp
await repo.CreateAsync(row, [MyRow.Fields.Code, MyRow.Fields.Total],
    uow: uow, ct: ct);
```

**Insert everything except a few:**

```csharp
await repo.CreateExcludingAsync(row, [MyRow.Fields.CreatedAt],
    uow: uow, ct: ct);
```

**Update only one column on an existing row:**

```csharp
row.Total = recalculated;
await repo.UpdateAsync(row, [MyRow.Fields.Total], uow: uow, ct: ct);
```

**Update everything except an audit column:**

```csharp
await repo.UpdateExcludingAsync(row, [MyRow.Fields.LastEditedBy],
    uow: uow, ct: ct);
```

### Sync wrappers

Each new async helper has a sync `[Obsolete]` companion (`Create`,
`CreateExcluding`, `Update`, `UpdateExcluding`) following the existing
migration pattern.

### Test infrastructure note

0.7.4 also adds Testcontainers-based integration tests against a real SQL
Server 2022 container. They live under `tests/Idevs.Net.CoreLib.Tests/Integration/`
and are tagged `[Trait("Category", "Integration")]`. Skip them with
`dotnet test --filter "Category!=Integration"` if Docker isn't available.

### Critical portability note for MySQL / MariaDB consumers

The new criteria-based helpers (`UpdateAsync(Action<SqlUpdate>, ...)`,
`UpdateManyAsync`, etc., and by extension the entity-row variants) all
return rows affected as reported by the underlying ADO.NET provider. **MySQL
and MariaDB report a different number than every other engine for UPDATE
statements where the new value equals the existing value:**

| Engine | `UPDATE … SET BlSent=1 WHERE Id=5` when row already has `BlSent=1` |
|---|---|
| SQL Server / PostgreSQL / Oracle / SQLite | `1` (matched-rows) |
| MySQL / MariaDB **default** | `0` (changed-rows — the value didn't actually change) |
| MySQL / MariaDB with `Use Affected Rows=false` | `1` (matched-rows — same as above) |

If your code does anything like `affected > 0` to mean "row exists and is
in the target state" (a common idempotent "mark as done" pattern), it
silently breaks on MySQL: the second call to mark the same row returns
`false`, which callers typically interpret as "operation failed" or "row
not found".

**Recommended fix for MySQL/MariaDB consumers (one-line change):** add
`Use Affected Rows=false;` to your connection string. This brings MySQL
into line with SQL Server semantics — `affected = matched-rows` regardless
of whether values changed.

**Before (problematic on MySQL):**

```
Server=<your-rds-host>.<region>.rds.amazonaws.com;Port=3306;Database=<db>;Uid=<user>;Pwd={secret};ConvertZeroDateTime=True;
```

**After (matched-rows semantics, portable across providers):**

```
Server=<your-rds-host>.<region>.rds.amazonaws.com;Port=3306;Database=<db>;Uid=<user>;Pwd={secret};ConvertZeroDateTime=True;Use Affected Rows=false;
```

Apply this to **every environment's connection string** (dev, staging,
production) so behavior is uniform across deployments. Both `MySqlConnector`
and `MySql.Data` providers honor this flag.

#### Quick verification

Run this once after the deploy:

```sql
-- Pick a row that already has the target value:
UPDATE SaleOrders SET BlSent = 1 WHERE Id = 5 AND BlSent = 1;

-- Expected with the flag set:    1 row(s) affected (matched mode)
-- Without the flag (broken):     0 row(s) affected (changed mode)
```

#### Alternative pattern when you can't change the connection string

If the connection string is locked down (managed config, customer-controlled,
etc.), use a value-aware WHERE clause that won't match already-flipped rows.
This is portable on every provider:

```csharp
// Instead of: WHERE Id = @id
// Use:        WHERE Id = @id AND BlSent != true
await UpdateAsync(q => q
    .Set(SaleOrderRow.Fields.BlSent, true)
    .Where(SaleOrderRow.Fields.Id == id
        && SaleOrderRow.Fields.BlSent != true),
    ExpectedRows.ZeroOrOne, uow, ct);
```

With this WHERE, `affected > 0` reliably means "I just transitioned this
row from false → true" on every engine. The trade-off: `affected == 0`
means *either* "already true" *or* "row missing" — distinguish with a
follow-up `TryFirstAsync` only if your caller branches on it.

---

## v0.7.2 → v0.7.3 — Unit of Work Helpers (BeginUnitOfWork + CommitOnSuccessAsync)

### What changed

Two new helpers on `SqlServiceBase` (so they're available on every typed
repository and every custom service that extends it) that close a real
gap: a parent repository method that didn't accept an `IUnitOfWork`
parameter previously had no way to coordinate atomic writes across
child repositories — each call opened its own connection and committed
independently.

| Helper | Shape | When to use |
|---|---|---|
| `BeginUnitOfWork(uow?)` → `UnitOfWorkScope` | Scope (`using` block + explicit `Commit()`) | Long methods, sequential statements, conditional returns |
| `CommitOnSuccessAsync(work, uow?, ct?)` | Lambda (auto-commit on return / rollback on throw) | Short blocks; the whole transaction body fits in one expression |

Both share the same caller-provides-or-we-own semantics — pick by code
shape, not semantic.

### When you DON'T need to migrate

If your existing repository methods either (a) always receive an
`IUnitOfWork` from the caller and pass it through, or (b) only do a
single write and don't need to coordinate with child repos, nothing
changes for you. The existing `IUnitOfWork? uow = null` parameter on
every CoreLib repository method continues to work as before.

### When you SHOULD migrate

Any method that:

1. Takes no `IUnitOfWork` parameter (or accepts an optional one), AND
2. Calls into more than one repository / service to make changes that
   should be atomic.

Before:

```csharp
public bool UpdateAll(MyRow row)
{
    UpdateAsync(...).GetAwaiter().GetResult();          // connection #1
    childRepo.UpdateChild(row.Id);                      // connection #2 — different transaction!
    auditRepo.LogChange(row.Id, "updated");             // connection #3 — different transaction!
    return true;
}
```

After (long-method shape — `BeginUnitOfWork`):

```csharp
public async Task<bool> UpdateAllAsync(
    MyRow row,
    IUnitOfWork? uow = null,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(row);

    using var scope = BeginUnitOfWork(uow);

    await UpdateAsync(u => u
        .Set(MyRow.Fields.Name, row.Name)
        .Where(MyRow.Fields.Id == row.Id), uow: scope.Uow, ct: ct);

    await childRepo.UpdateChildAsync(row.Id!.Value, uow: scope.Uow, ct: ct);
    await auditRepo.LogChangeAsync(row.Id!.Value, "updated", uow: scope.Uow, ct: ct);

    scope.Commit();   // explicit; forgetting this rolls back
    return true;
}
```

After (short-block shape — `CommitOnSuccessAsync`):

```csharp
public Task<bool> RenameAndStampAsync(
    int id, string name,
    IUnitOfWork? uow = null,
    CancellationToken ct = default)
{
    return CommitOnSuccessAsync(async (txn, token) =>
    {
        await UpdateAsync(u => u.Set(MyRow.Fields.Name, name).Where(MyRow.Fields.Id == id),
            uow: txn, ct: token);
        await auditRepo.LogChangeAsync(id, "rename", uow: txn, ct: token);
        return true;
    }, uow, ct);
}
```

### One trap to know

When using `BeginUnitOfWork`, **every call inside the using block must
pass `scope.Uow`**. If you forget on one call, that call opens its own
connection and runs outside the transaction — silent atomicity bug. The
lambda form has the same trap with `txn`. Code review should flag any
repo call inside one of these blocks that doesn't thread the UoW
through.

### Sync wrappers

Both `CommitOnSuccess<T>(Func<IUnitOfWork, T>, IUnitOfWork?)` and
`CommitOnSuccess(Action<IUnitOfWork>, IUnitOfWork?)` exist and are
marked `[Obsolete]` with the standard migration message. Use them only
during the transition; prefer the async versions in new code.

---

## v0.7.1 → v0.7.2 — RepositoryBase Criteria-Based Update/Delete + TryFirst Alias

### What changed

- New async helpers on `RepositoryBase<TRow>`:
  - `TryFirstAsync(Action<SqlQuery>, ...)` — semantic alias for the existing
    `FirstAsync` (which returns `Task<TRow?>`). Name matches Serenity's
    `Connection.TryFirst` convention.
  - `UpdateAsync(Action<SqlUpdate>, ExpectedRows, ...)` — criteria-based partial
    UPDATE. Table name is auto-resolved from `TRow`. Defaults to
    `ExpectedRows.One` so a wrong WHERE clause fails loudly.
  - `UpdateManyAsync(Action<SqlUpdate>, ...)` — alias for
    `UpdateAsync(..., ExpectedRows.Ignore, ...)`.
  - `DeleteAsync(Action<SqlDelete>, ExpectedRows, ...)` — symmetric to
    `UpdateAsync`.
  - `DeleteManyAsync(Action<SqlDelete>, ...)` — alias for batch deletes.
- Sync `[Obsolete]` wrappers (`TryFirst`, `Update`, `UpdateMany`, `Delete`,
  `DeleteMany`) follow the existing migration pattern and will be removed
  alongside the other sync wrappers in 1.0.
- `FirstAsync(Action<SqlQuery>, ...)` and its sync sibling `First(...)` are
  marked `[Obsolete]`. Same behavior; they now delegate to `TryFirstAsync`.
  Will be removed in 1.0.

### Why `ExpectedRows.One` is the default

Most domain operations target exactly one row (update a specific document,
delete a specific record). Default-to-`One` makes incorrect WHERE clauses fail
loudly at runtime instead of silently corrupting data (the 0-rows case) or
wreaking havoc (the N-rows case). This matches Serenity's own `Execute(connection)`
overload default. Callers wanting batch behavior must opt in explicitly via
`*ManyAsync` or `ExpectedRows.Ignore`.

### Migration steps

1. Bump to 0.7.2 in your consumer project:
   ```xml
   <PackageReference Include="Idevs.Net.CoreLib" Version="0.7.2" />
   ```

2. **(Optional) Rename existing `FirstAsync`/`First` calls to `TryFirstAsync`/`TryFirst`.**
   Behavior is identical; only the name changes. The deprecation is non-breaking
   for 0.7.x and removal is planned for 1.0:
   ```csharp
   - var row = await repo.FirstAsync(q => q.SelectTableFields().Where(...), uow, ct);
   + var row = await repo.TryFirstAsync(q => q.SelectTableFields().Where(...), uow, ct);
   ```

3. **Replace inline `new SqlUpdate(...).Execute(...)` patterns with `UpdateAsync`.**
   The repository version pre-resolves the table name and defaults to
   `ExpectedRows.One`:
   ```csharp
   - new SqlUpdate(MappingLotSelectionRow.Fields.TableName)
   -     .Dialect(dialect)
   -     .Set(cFld.McApproveQty, qty)
   -     .Where(cFld.DocNo == docno && cFld.ProductId == productId)
   -     .Execute(uow.Connection);
   + await mappingLotRepo.UpdateAsync(u => u
   +     .Set(cFld.McApproveQty, qty)
   +     .Where(cFld.DocNo == docno && cFld.ProductId == productId),
   +     uow: uow, ct: ct);
   ```
   If your previous code was effectively unchecked (no row-count assertion
   around the `Execute` call) and the WHERE clause may match many rows,
   call `UpdateManyAsync` instead — or pass `ExpectedRows.Ignore` explicitly.

4. **Same pattern for `SqlDelete` → `DeleteAsync` / `DeleteManyAsync`.**
   ```csharp
   - new SqlDelete(MappingLotSelectionRow.Fields.TableName)
   -     .Where(cFld.DocNo == docno)
   -     .Execute(uow.Connection);
   + await mappingLotRepo.DeleteManyAsync(d => d
   +     .Where(cFld.DocNo == docno),
   +     uow: uow, ct: ct);
   ```
   `DeleteManyAsync` is the right choice here because a `DocNo`-only filter
   typically removes multiple rows. Use plain `DeleteAsync` only when you
   *expect* exactly one row to be removed.

5. **Audit existing single-row updates for hidden bugs.**
   After switching to `UpdateAsync`, the `ExpectedRows.One` default may surface
   pre-existing issues — for example, a `Where` clause that matches zero rows
   because the row was already updated, or matches multiple rows because the
   filter is too loose. These now throw at runtime instead of silently
   succeeding. That is the intended behavior; treat each throw as a real bug.

### Notes

- `CountAsync` and `ExistsAsync` were considered for 0.7.2 but deferred until
  the right Serenity idiom is identified. Track the follow-up in the next
  patch.
- The keyed variant `RepositoryBase<TRow, TKey>` already exposes
  `UpdateAsync(TRow row, ...)` (by-Id, full-row update). The new
  `UpdateAsync(Action<SqlUpdate>, ...)` overload coexists by signature; both
  work side-by-side without ambiguity.

---

## v0.6.x → v0.7.0 — Source-Generator DI Registration

### What changed
- `services.AddIdevsCorelibServices()` is `[Obsolete]`. The replacement is
  `services.AddIdevsServices()`, generated by a Roslyn source generator
  bundled into the main nupkg.
- Reflection-based assembly scanning at startup is replaced by compile-time
  registration emission. No `AppDomain.GetAssemblies()` at runtime.
- New marker interfaces (`IScopedService`/etc.) and `IIdevsServiceRegistrar`
  provide alternative discovery paths.

### Migration steps

1. Bump to 0.7.0:
   ```xml
   <PackageReference Include="Idevs.Net.CoreLib" Version="0.7.0" />
   ```
2. Replace the call in `Program.cs`:
   ```csharp
   - builder.Services.AddIdevsCorelibServices();
   + builder.Services.AddIdevsServices();
   ```
3. (Optional, recommended) Adopt marker interfaces on a base class to drop
   per-type `[Scoped]`/`[ScopedRegistration]` attributes:
   ```csharp
   public class RepositoryBase<TRow, TKey> : ..., IScopedService { ... }
   ```
   All derived repositories register automatically.
4. (Optional safety net) If the generator misbehaves during transition:
   ```xml
   <IdevsCoreLibUseSourceGenerator>false</IdevsCoreLibUseSourceGenerator>
   ```
   This routes `AddIdevsServices` through the legacy reflection scan. Removed
   in 0.8.0.

### Legacy attributes
`[ScopedRegistrationAttribute]`/etc. are still supported in 0.7.0 but emit
`IDEVSGEN010` warnings at every use site with a Roslyn codefix to replace
them with `[Scoped]`/etc. Promoted to errors in 0.9.0; removed in 1.0.0.

---

## v0.5.0 → v0.6.0 — RepositoryBase Redesign

Version 0.6.0 introduces a breaking redesign of `RepositoryBase`. This section walks through the changes and shows before/after for the common method shapes.

### Summary

| Old (≤ 0.5.0) | New (0.6.0) |
|---|---|
| `Idevs.RepositoryBase<T>` (one type — plumbing only) | Three classes: `Idevs.Repositories.SqlServiceBase`, `Idevs.Repositories.RepositoryBase<TRow>`, `Idevs.Repositories.RepositoryBase<TRow, TKey>` |
| Constructor: `(IServiceProvider sp, ILogger<T> logger)` | Constructor: `(ISqlConnections sqlConnections)` |
| Properties: `ExceptionLog`, `Localizer`, `ServiceProvider`, `Connection`, `Dialect`, `SqlQuery`, `SqlInsert(t)`, `SqlUpdate(t)`, `SqlDelete(t)` | Properties: `SqlConnections`, `ConnectionKey`, `Dialect` (lazy). Methods: `SqlQuery()`, `SqlInsert(t)`, `SqlUpdate(t)`, `SqlDelete(t)`, `ExecuteAsync<T>(work, uow?, ct)` |
| Sync only | Async-first; `[Obsolete]` sync wrappers for migration |
| Hardcoded `"Default"` connection key | Virtual `ConnectionKey` property + `[ConnectionKey("...")]` attribute |
| No typed CRUD | `FirstAsync`, `ListAsync`, `GetByAsync<TValue>`, `CreateAsync` (any IRow); `GetByIdAsync`, `GetByIdsAsync`, `UpdateAsync`, `DeleteByIdAsync` (IIdRow) |

### Migration steps

#### 1. Update the constructor

**Before:**
```csharp
[ScopedRegistration]
public class CustomerRepository(IServiceProvider serviceProvider, ILogger<CustomerRow> logger)
    : RepositoryBase<CustomerRow>(serviceProvider, logger), ICustomerRepository
{
    // ...
}
```

**After:**
```csharp
[ScopedRegistration]
public class CustomerRepository(ISqlConnections sqlConnections, ILogger<CustomerRepository> logger)
    : RepositoryBase<CustomerRow, int>(sqlConnections), ICustomerRepository
{
    private readonly ILogger<CustomerRepository> _logger = logger;
    // Inject ITextLocalizer, ITwoLevelCache, etc., separately if needed.
}
```

Add the `using Idevs.Repositories;` directive.

#### 2. Replace `GetById(int)` → inherited `GetByIdAsync(int)`

**Before:**
```csharp
public CustomerRow GetById(int id)
{
    using var uow = new UnitOfWork(Connection);
    try
    {
        return uow.Connection.TryFirst<CustomerRow>(q => q
            .Dialect(Connection.GetDialect())
            .SelectTableFields()
            .Where(CustomerRow.Fields.Id == id));
    }
    catch (Exception e)
    {
        ExceptionLog.LogCritical(e, "{message}", e.Message);
        throw;
    }
    finally
    {
        uow.Connection.Close();
    }
}
```

**After:** delete the method entirely. The base provides `GetByIdAsync(id)`.

If you need a sync API for now, the base also provides an `[Obsolete]` `GetById(id)` sync wrapper that delegates to the async path.

#### 3. `GetByCode(string code)` → one-liner using `GetByAsync`

**Before:** ~12 lines of `using var uow = ... try { ... } catch { ... } finally { ... }`.

**After:**
```csharp
public Task<CustomerRow?> GetByCodeAsync(string code, CancellationToken ct = default) =>
    GetByAsync(CustomerRow.Fields.CustomerCode, code, ct: ct);
```

#### 4. Custom queries → `ExecuteAsync` template

**Before:**
```csharp
public List<CustomerRow> GetCustomers(string? containsText, string[]? departmentCodes)
{
    using var uow = new UnitOfWork(Connection);
    try
    {
        return uow.Connection.List<CustomerRow>(q => q
            .Dialect(Connection.GetDialect())
            .SelectTableFields()
            .Where( /* ... */ ));
    }
    catch (Exception e)
    {
        ExceptionLog.LogCritical(e, "{message}", e.Message);
        throw;
    }
    finally
    {
        uow.Connection.Close();
    }
}
```

**After:**
```csharp
public Task<List<CustomerRow>> GetCustomersAsync(
    string? containsText,
    string[]? departmentCodes,
    IUnitOfWork? uow = null,
    CancellationToken ct = default) =>
    ListAsync(q => q
        .SelectTableFields()
        .Where( /* ... */ ),
        uow, ct);
```

#### 5. Multi-step transactional methods → pass `UnitOfWork`

**Before:**
```csharp
public bool UpdateFinanceStatus(UnitOfWork uow, string customerCode, string status) {
    /* try/catch/finally with uow.Connection */
}
```

**After:**
```csharp
public Task<bool> UpdateFinanceStatusAsync(
    IUnitOfWork uow,
    string customerCode,
    string status,
    CancellationToken ct = default) =>
    ExecuteAsync(async (c, _) =>
    {
        var affected = SqlUpdate(CustomerRow.Fields.TableName)
            .Set(CustomerRow.Fields.FinanceStatus, status)
            .Where(CustomerRow.Fields.CustomerCode == customerCode)
            .Execute(c);
        return affected > 0;
    }, uow, ct);
```

The CRUD signatures accept `IUnitOfWork?` (interface) so any unit-of-work implementation composes; Serenity's concrete `UnitOfWork` class implements `IUnitOfWork`.

#### 6. Services that inherited `RepositoryBase<T>` only for plumbing

If your service was inheriting `RepositoryBase<T>` not because it manages a typed row but because it needed `Connection` / `SqlInsert` / etc. (e.g., a `Setting` service that touches multiple rows), inherit `SqlServiceBase` instead:

```csharp
public class Setting(ISqlConnections sqlConnections) : SqlServiceBase(sqlConnections), ISetting
{
    public Task<bool> IsKeyExistsAsync(string key, CancellationToken ct = default) =>
        ExecuteAsync(async (c, _) =>
            c.TryFirst<SettingRow>(q => q
                .SelectTableFields()
                .Where(SettingRow.Fields.SettingKey == key)) is not null,
            ct: ct);
}
```

#### 7. Strip `try/catch/LogCritical/throw` pairs

The new base does not log exceptions inside `ExecuteAsync`. ASP.NET Core middleware (or your background-job runner) is the right layer to log with full request context. If you specifically need per-repo structured logging, override `ExecuteAsync` in your derived class.

#### 8. Drop hand-rolled async-via-default-interface wrappers

If you wrote interfaces like:
```csharp
public interface ICsParaRepository {
    List<CsParaRow> GetListByBizIds(List<int> bizIds);
    Task<List<CsParaRow>> GetListByBizIdsAsync(List<int> bizIds) =>
        Task.FromResult(GetListByBizIds(bizIds));
}
```

Drop the sync method and let the implementation be the natural `Task<T> ...Async`.

### Caching

Caching is no longer a base-class concern. Use the new `Idevs.Caching.TwoLevelCacheExtensions`:

```csharp
public Task<CustomerRow?> GetByIdCachedAsync(int id, CancellationToken ct = default) =>
    cache.GetLocalCachedAsync(
        $"{CacheKey.Frequency.Customer}.{id}",
        CacheKey.Frequency.DefaultCacheDuration,
        CacheKey.Frequency.GroupKey,
        innerCt => GetByIdAsync(id, ct: innerCt));
```

The cache wrap and the repo method compose explicitly. Invalidation stays in your hands (e.g., on `Save`/`Delete` you call `cache.RemoveGroup(...)` as before).

### On the `[Obsolete]` sync wrappers

Each new async method has a sync wrapper marked `[Obsolete]` to ease migration. They are safe today (the underlying SQL calls are sync, so the wrappers are sync-over-fake-async — no deadlock risk). They will be **removed in 1.0** when Serenity ships real-async fluent SQL — at which point sync-over-real-async would become deadlock-prone. Treat the `[Obsolete]` warnings as a migration prompt, not a permanent state.

### Connection-key configuration

For multi-DB consumers, override the connection key per repo:

```csharp
[ConnectionKey("Warehouse")]
public class StockRepository(ISqlConnections c) : RepositoryBase<StockRow, int>(c) { }
```

Or override the virtual property if the key needs to be computed:

```csharp
public class StockRepository : RepositoryBase<StockRow, int>
{
    private readonly string _key;
    public StockRepository(ISqlConnections c, IRuntimeConfig cfg) : base(c) { _key = cfg.WarehouseKey; }
    protected override string ConnectionKey => _key;
}
```

The override wins over the attribute.

---

## v0.3.x → v0.5.0 — Package Layout & DI Changes

#### 1. Standard DI is now the default

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdevsCorelibServices();
```

#### 2. Autofac moved to `Idevs.Net.CoreLib.Autofac`

```bash
dotnet add package Idevs.Net.CoreLib.Autofac
```

```csharp
builder.UseIdevsAutofac();
```

#### 3. Serilog support is optional via `Idevs.Net.CoreLib.Serilog`

```bash
dotnet add package Idevs.Net.CoreLib.Serilog
```

```csharp
app.UseIdevsSerilogLogManager();
```

#### 4. `StaticServiceProvider` removed

`StaticServiceProvider` was removed in `0.5.0`. Use constructor dependency injection first. For legacy static integration points, use `StaticServiceLocator`.

---

## v0.1.x → v0.2.0 — Autofac Integration

- **Better Performance**: Autofac provides superior dependency resolution performance.
- **Advanced Features**: Support for decorators, interceptors, and advanced lifetime scopes.
- **Module System**: Organized service registration through modules.
- **Attribute-Based Registration**: Automatic service discovery and registration.

---

## v0.0.x → v0.1.x — Service Registration & Chrome Setup

#### 1. Service Registration

Replace manual service registration with `AddIdevsCorelibServices()`:

```csharp
// Old way
services.AddScoped<IViewPageRenderer, ViewPageRenderer>();
services.AddScoped<IIdevsPdfExporter, IdevsPdfExporter>();
services.AddScoped<IIdevsExcelExporter, IdevsExcelExporter>();

// New way
services.AddIdevsCorelibServices();
```

#### 2. Chrome Setup

Add Chrome download to startup:

```csharp
// Add this to Program.cs
ChromeHelper.DownloadChrome();
```

#### 3. Static Service Provider

`StaticServiceProvider` was removed in `0.5.0`. Use constructor dependency injection first. For legacy static integration points that cannot receive dependencies through DI, use `StaticServiceLocator`.

```csharp
var app = builder.Build();
app.UseIdevsStaticServiceLocator();
var service = StaticServiceLocator.Resolve<IMyService>();

// Or manual initialization
// StaticServiceLocator.Initialize(app.Services);
```

##### `StaticServiceLocator` benefits

- **Legacy bridge**: supports static or legacy code while you migrate toward constructor DI.
- **Better error handling**: more descriptive error messages.
- **Scoped resolution**: support for creating service scopes.
- **Singleton cache**: optional caching for services known to be registered as singletons.
