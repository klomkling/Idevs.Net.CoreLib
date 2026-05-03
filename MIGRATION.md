# Migration Guide

Consolidated upgrade notes for `Idevs.Net.CoreLib`. Newest first.

## Contents

- [v0.7.3 → v0.7.4 — Explicit-fields Create/Update + NotMapped/Expression handling](#v073--v074--explicit-fields-createupdate--notmappedexpression-handling)
- [v0.7.2 → v0.7.3 — Unit of Work Helpers (BeginUnitOfWork + CommitOnSuccessAsync)](#v072--v073--unit-of-work-helpers-beginunitofwork--commitonsuccessasync)
- [v0.7.1 → v0.7.2 — RepositoryBase Criteria-Based Update/Delete + TryFirst Alias](#v071--v072--repositorybase-criteria-based-updatedelete--tryfirst-alias)
- [v0.6.x → v0.7.0 — Source-Generator DI Registration](#v06x--v070--source-generator-di-registration)
- [v0.5.0 → v0.6.0 — RepositoryBase Redesign](#v050--v060--repositorybase-redesign)
- [v0.3.x → v0.5.0 — Package Layout & DI Changes](#v03x--v050--package-layout--di-changes)
- [v0.1.x → v0.2.0 — Autofac Integration](#v01x--v020--autofac-integration)
- [v0.0.x → v0.1.x — Service Registration & Chrome Setup](#v00x--v01x--service-registration--chrome-setup)

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
