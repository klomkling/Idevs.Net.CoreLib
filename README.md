# Idevs.Net.CoreLib

[![CI](https://github.com/klomkling/Idevs.Net.CoreLib/actions/workflows/ci.yml/badge.svg)](https://github.com/klomkling/Idevs.Net.CoreLib/actions/workflows/ci.yml)
[![Release](https://github.com/klomkling/Idevs.Net.CoreLib/actions/workflows/release.yml/badge.svg)](https://github.com/klomkling/Idevs.Net.CoreLib/actions/workflows/release.yml)
[![NuGet Version](https://img.shields.io/nuget/v/Idevs.Net.CoreLib.svg?label=nuget)](https://www.nuget.org/packages/Idevs.Net.CoreLib)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Idevs.Net.CoreLib.svg)](https://www.nuget.org/packages/Idevs.Net.CoreLib)
![Latest](https://img.shields.io/github/v/tag/klomkling/Idevs.Net.CoreLib?sort=semver&label=latest)
![.NET](https://img.shields.io/badge/.NET-8%20%7C%2010-blueviolet)
![Serenity](https://img.shields.io/badge/Serenity-Net.Services-0078D4)
[![License: MIT](https://img.shields.io/github/license/klomkling/Idevs.Net.CoreLib?label=license)](https://opensource.org/licenses/MIT)
[![Sponsor](https://img.shields.io/badge/Sponsor-Buy%20me%20a%20coffee-ff813f)](https://buymeacoffee.com/klomkling)

A focused extension library for the [Serenity Framework](https://serenity.is/) that provides compile-time DI registration, async-first repositories, Excel/PDF export, S3-compatible upload storage, two-level caching helpers, and a curated set of form/grid attributes.

Targets **.NET 8** and **.NET 10**.

## Contents

- [Packages](#packages)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Features](#features)
  - [Service registration (source generator)](#service-registration-source-generator)
  - [Autofac integration](#autofac-integration)
  - [Repositories](#repositories)
  - [Two-level cache helpers](#two-level-cache-helpers)
  - [Excel export](#excel-export)
  - [PDF export](#pdf-export)
  - [Cloud upload storage](#cloud-upload-storage)
  - [Logging — LogManager + Serilog](#logging--logmanager--serilog)
  - [UI attributes — formatters, editors, column widths](#ui-attributes--formatters-editors-column-widths)
  - [Smart pagination](#smart-pagination)
  - [Static service locator](#static-service-locator)
- [Troubleshooting](#troubleshooting)
- [Migration guide](#migration-guide)
- [Contributing](#contributing)
- [Support](#support)
- [License](#license)

## Packages

| Package | Purpose | Required? |
|---|---|---|
| **`Idevs.Net.CoreLib`** | Main library — DI generator output, repositories, exporters, storage, attributes, helpers. | Yes |
| **`Idevs.Net.CoreLib.Generators.Abstractions`** | Roslyn helpers for consumer-authored source generators that follow CoreLib's DI conventions. | Optional |
| **`Idevs.Net.CoreLib.Autofac`** | Autofac integration for CoreLib's DI registration model. | Optional |
| **`Idevs.Net.CoreLib.Serilog`** | `LogManager` bridge for Serilog logger factories. | Optional |

The Roslyn source generator that emits compile-time DI registrations is **bundled** inside `Idevs.Net.CoreLib` (under `analyzers/dotnet/cs/`). You do not need to install a separate generator package.

## Installation

```bash
dotnet add package Idevs.Net.CoreLib
```

Add optional packages as needed:

```bash
dotnet add package Idevs.Net.CoreLib.Autofac
dotnet add package Idevs.Net.CoreLib.Serilog
dotnet add package Idevs.Net.CoreLib.Generators.Abstractions
```

## Quick start

```csharp
using Idevs.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Compile-time DI registration emitted by the bundled Roslyn source generator.
// Replaces the deprecated AddIdevsCorelibServices() runtime scan.
builder.Services.AddIdevsServices();

builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseRouting();
app.MapControllers();
app.Run();
```

> **Upgrading from 0.6.x?** `AddIdevsCorelibServices()` is `[Obsolete]` and delegates to the new path. See the [migration guide](MIGRATION.md#v06x--v070--source-generator-di-registration).

## Features

### Service registration (source generator)

From **0.7.0** onward, all service registrations are emitted at compile time. There is no `AppDomain.GetAssemblies()` scan at startup. Three discovery paths are supported and can be mixed freely:

**1. Attribute** — `[Scoped]`, `[Singleton]`, `[Transient]` on the implementation class.

```csharp
[Scoped]
public class OrderService : IOrderService { }

[Scoped(typeof(IInvoiceService))]              // explicit service type
public class InvoiceService : IInvoiceService { }

[Scoped(AllowSelfRegistration = true)]         // register the concrete type
public class UtilityService { }

[Transient(ServiceKey = "smtp")]               // named registration (Autofac only)
public class SmtpEmailService : IEmailService { }
```

**2. Marker interface** — implement `IScopedService` / `ISingletonService` / `ITransientService`, or their generic `<TService>` variants. Best when applied to a base class so derived types are auto-registered without per-type attributes:

```csharp
using Idevs.Repositories;

public abstract class AppRepositoryBase<TRow, TKey>(ISqlConnections c)
    : RepositoryBase<TRow, TKey>(c), IScopedService { }

// All derived repositories are auto-registered.
public class OrderRepository(ISqlConnections c) : AppRepositoryBase<OrderRow, int>(c) { }
```

**3. Registrar** — implement `IIdevsServiceRegistrar` for arbitrary imperative registrations that don't fit attribute or marker patterns:

```csharp
public class CustomRegistrar : IIdevsServiceRegistrar
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IFoo>(sp => new Foo(sp.GetRequiredService<IBar>()));
    }
}
```

The generator emits 10 diagnostics (`IDEVSGEN001`–`IDEVSGEN010`) for misuse — attribute/marker conflicts, ambiguous service types, registrar validation, legacy attribute usage, and more. Diagnostics fire at compile time so problems are caught before runtime.

#### Legacy attributes

`[ScopedRegistration]`, `[SingletonRegiatration]`, `[TransientRegistration]` are still recognized but `[Obsolete]`. The generator emits `IDEVSGEN001` warning suggesting the standard names.

#### Opting out / falling back

If the generator misbehaves in your build, set `<IdevsCoreLibUseSourceGenerator>false</IdevsCoreLibUseSourceGenerator>` in your `.csproj` to fall back to the runtime scan via `AddIdevsCorelibLegacyScan()`. Planned for removal in 0.8.0.

### Autofac integration

```bash
dotnet add package Idevs.Net.CoreLib.Autofac
```

```csharp
using Idevs.Extensions;

builder.UseIdevsAutofac();

// With custom registrations
builder.UseIdevsAutofac(c =>
{
    c.RegisterType<MyCustomService>().As<IMyCustomService>().InstancePerLifetimeScope();
});

// With Autofac modules
builder.UseIdevsAutofac(new MyCustomModule(), new AnotherModule());
```

Named-key registrations (`[Transient(ServiceKey = "smtp")]`) resolve through Autofac's keyed services.

### Repositories

Three layered base classes for data access:

- **`SqlServiceBase`** — for services that need raw SQL access without being a typed repository. Provides `ISqlConnections`, lazy `Dialect`, dialect-pre-bound `SqlQuery()` / `SqlInsert(t)` / `SqlUpdate(t)` / `SqlDelete(t)` factories, and a uniform `ExecuteAsync<T>` template that manages connection lifetime and composes with an optional `UnitOfWork`.

- **`RepositoryBase<TRow>`** — typed read/list/getby/create/update/delete on a Serenity `IRow`:

  | Group | Methods |
  |---|---|
  | **Reads** | `TryFirstAsync(Action<SqlQuery>)`, `ListAsync(Action<SqlQuery>)`, `GetByAsync<TValue>(Field, value)` |
  | **Writes** | `CreateAsync(TRow)`, `UpdateAsync(Action<SqlUpdate>, ExpectedRows)`, `UpdateManyAsync(Action<SqlUpdate>)`, `DeleteAsync(Action<SqlDelete>, ExpectedRows)`, `DeleteManyAsync(Action<SqlDelete>)` |
  | **Deprecated** | `FirstAsync` (use `TryFirstAsync`); all sync wrappers (`*` without `Async`) |

  All write methods default to `ExpectedRows.One` so a wrong WHERE clause fails loudly. Use the `*Many` variants or pass `ExpectedRows.Ignore` for batch operations.

- **`RepositoryBase<TRow, TKey>`** — adds Id-keyed CRUD on `IIdRow`: `GetByIdAsync(TKey)`, `GetByIdsAsync(IEnumerable<TKey>)`, `UpdateAsync(TRow row)` (entity-by-id), `DeleteByIdAsync(TKey)`. Inherits all criteria-based methods from `RepositoryBase<TRow>`. The `UpdateAsync(TRow)` and `UpdateAsync(Action<SqlUpdate>, ...)` overloads coexist by signature.

Connection key is configurable via the virtual `ConnectionKey` property or the `[ConnectionKey("Warehouse")]` attribute (resolved on the derived class).

#### Example

```csharp
using Idevs.ComponentModels;
using Idevs.Repositories;

public interface IMappingLotRepository
{
    Task<MappingLotSelectionRow?> FindByDocAndProductAsync(
        string docNo, int productId, IUnitOfWork uow, CancellationToken ct);

    Task UpdateApproveQtyAsync(
        string docNo, int productId, decimal qty, IUnitOfWork uow, CancellationToken ct);
}

[Scoped(typeof(IMappingLotRepository))]
public class MappingLotRepository(ISqlConnections c)
    : RepositoryBase<MappingLotSelectionRow>(c), IMappingLotRepository
{
    private static readonly MappingLotSelectionRow.RowFields cFld = MappingLotSelectionRow.Fields;

    public Task<MappingLotSelectionRow?> FindByDocAndProductAsync(
        string docNo, int productId, IUnitOfWork uow, CancellationToken ct)
        => TryFirstAsync(q => q
            .SelectTableFields()
            .Where(cFld.DocNo == docNo && cFld.ProductId == productId),
            uow, ct);

    public Task UpdateApproveQtyAsync(
        string docNo, int productId, decimal qty, IUnitOfWork uow, CancellationToken ct)
        => UpdateAsync(u => u
            .Set(cFld.McApproveQty, qty)
            .Where(cFld.DocNo == docNo && cFld.ProductId == productId),
            uow: uow, ct: ct);
}
```

### Two-level cache helpers

`Idevs.Caching.TwoLevelCacheExtensions` adds async wrappers around Serenity's `ITwoLevelCache`, plus convenience methods for memory-only and remote-only access patterns:

```csharp
using Idevs.Caching;

// Memory-only cache (per-process, never hits remote).
var amphurs = cache.GetLocalStoreOnly(
    CacheKey.Base.Amphur,
    CacheKey.Base.DefaultCacheDuration,
    CacheKey.Base.GroupKey,
    () => repo.List(q => q.SelectTableFields()));

// Async variant with cancellation token.
var amphursAsync = await cache.GetLocalStoreOnlyAsync(
    CacheKey.Base.Amphur,
    CacheKey.Base.DefaultCacheDuration,
    CacheKey.Base.GroupKey,
    () => repo.ListAsync(q => q.SelectTableFields(), ct: ct),
    ct);
```

### Excel export

```csharp
public class OrderEndpoint : ServiceEndpoint
{
    private readonly IIdevsExcelExporter _excel;

    public OrderEndpoint(IIdevsExcelExporter excel) => _excel = excel;

    [HttpPost]
    public IActionResult Export(ListRequest request)
    {
        var orders = GetOrders(request);

        // Simple export against a Serenity columns class.
        var bytes = _excel.Export(orders, typeof(OrderColumns));

        return IdevsContentResult.Create(bytes, IdevsContentType.Excel, "orders.xlsx");
    }

    [HttpPost]
    public IActionResult ExportWithReportHeader(ListRequest request)
    {
        var orders = GetOrders(request);
        var headers = new[]
        {
            new ReportHeader { HeaderLine = "Order Report" },
            new ReportHeader { HeaderLine = $"Generated: {DateTime.Now:yyyy-MM-dd}" },
            new ReportHeader { HeaderLine = "" },
        };

        var bytes = _excel.Export(orders, typeof(OrderColumns), headers);
        return IdevsContentResult.Create(bytes, IdevsContentType.Excel, "order-report.xlsx");
    }
}
```

Customize via `IdevsExportRequest`:

```csharp
var request = new IdevsExportRequest
{
    TableTheme = TableTheme.TableStyleMedium15,
    CompanyName = "My Company",
    ReportName = "Sales Report",
    PageSize = new PageSize(PageSizes.A4, PageOrientations.Landscape),
};
```

Aggregations: pass `AggregateColumn[]` with `AggregateType.Sum`/`Avg`/`Count`/`Min`/`Max` to attach total rows.

### PDF export

PDF generation uses [PuppeteerSharp](https://github.com/hardkoded/puppeteer-sharp). Render HTML upstream (e.g., via Razor with `IViewPageRenderer`) and pass it to `IIdevsPdfExporter`.

```csharp
public class ReportEndpoint : ServiceEndpoint
{
    private readonly IIdevsPdfExporter _pdf;
    private readonly IViewPageRenderer _view;

    public ReportEndpoint(IIdevsPdfExporter pdf, IViewPageRenderer view)
    {
        _pdf = pdf;
        _view = view;
    }

    [HttpPost]
    public async Task<IActionResult> Generate(ReportRequest request, CancellationToken ct)
    {
        var model = GetReportData(request);
        var html = await _view.RenderViewAsync("Reports/OrderReport", model);

        var bytes = await _pdf.ExportByteArrayAsync(
            html,
            "<div style='text-align:center;'>Order Report</div>",                       // header
            "<div style='text-align:center;'>Page <span class='pageNumber'></span></div>"); // footer

        return IdevsContentResult.Create(bytes, IdevsContentType.Pdf, "report.pdf");
    }
}
```

#### Chrome download

Chromium must be available on the host. Trigger the download once at startup:

```csharp
public static void Main(string[] args)
{
    if (!ChromeHelper.IsChromeDownloaded())
        ChromeHelper.DownloadChrome();

    CreateHostBuilder(args).Build().Run();
}
```

#### PDF options

Use `PdfOptionsBuilder` for fluent configuration, or pass a PuppeteerSharp `PdfOptions` directly:

```csharp
var options = new PdfOptions
{
    Format = PaperFormat.A4,
    PreferCSSPageSize = true,
    MarginOptions = new MarginOptions { Top = "1in", Right = "1in", Bottom = "1in", Left = "1in" },
};
```

> **From 0.3.0**: the exporter expects pre-rendered HTML. Razor rendering is the caller's responsibility.

### Cloud upload storage

Replaces Serenity's default upload storage with an S3-compatible backend. Supports **AWS S3**, **Cloudflare R2**, and **Local** (passthrough).

```csharp
using Idevs.Extensions;

builder.Services.AddCloudUploadStorage(builder.Configuration);
builder.Services.AddUploadStorage();
```

**AWS S3** (`appsettings.json`):

```json
{
  "CloudUploadStorage": {
    "Provider": "AWS",
    "BucketName": "my-bucket/uploads",
    "Region": "ap-southeast-1",
    "KeyPrefix": "tenant-a"
  }
}
```

**Cloudflare R2:**

```json
{
  "CloudUploadStorage": {
    "Provider": "CloudflareR2",
    "BucketName": "my-bucket",
    "CloudflareAccountId": "account-id",
    "AccessKey": "access-key",
    "SecretKey": "secret-key"
  }
}
```

**Local** (no cloud — keep Serenity's default behavior):

```json
{ "CloudUploadStorage": { "Provider": "Local" } }
```

AWS credentials follow the standard AWS SDK chain (env vars, profile, IAM role). Set `KeyPrefix` to namespace uploads per tenant or environment.

### Logging — LogManager + Serilog

`LogManager` is a provider-neutral bridge for code that cannot receive `ILogger<T>` through DI (static methods, legacy code paths).

```csharp
using Idevs.Logging;

LogManager.SetLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());

var logger = LogManager.GetLogger<MyClass>();
logger.LogInformation("Started.");
```

For Serilog hosts:

```bash
dotnet add package Idevs.Net.CoreLib.Serilog
```

```csharp
using Idevs.Extensions;

app.UseIdevsSerilogLogManager();
```

The core package always uses `Microsoft.Extensions.Logging` abstractions; the Serilog package only wires up the factory.

### UI attributes — formatters, editors, column widths

Curated Serenity-compatible attributes for grid columns and form fields:

**Display formatters**
- `[DisplayDateFormat]` — `dd/MM/yyyy`
- `[DisplayDateTimeFormat(withSeconds: true)]`
- `[DisplayTimeFormat(withSeconds: true)]`
- `[DisplayNumberFormat(scale: 2)]`
- `[DisplayPercentage(scale: 2)]`
- `[ZeroDisplayFormatter]` — render `0` as blank
- `[CheckboxFormatter(TrueText, FalseText, TrueValueIcon, FalseValueIcon)]`
- `[LookupFormatter]`
- `[DateMonthFormatter]`

**Editors**
- `[CheckboxButtonEditor(EnumKey, EnumType, IsStringId)]`
- `[EnumEditorWithKey]`
- `[DateMonthEditor]`

**Column widths (Bootstrap-aware)**
- `[ColumnWidth(ExtraSmall, Small, Medium, Large, ExtraLarge)]`
- `[FullColumnWidth]`
- `[HalfColumnWidth]`

```csharp
public class OrderColumns
{
    [DisplayName("Order ID"), ColumnWidth(ExtraLarge = 2)]
    public string OrderId { get; set; }

    [DisplayName("Customer"), FullColumnWidth]
    public string CustomerName { get; set; }

    [DisplayName("Order Date"), DisplayDateFormat, HalfColumnWidth]
    public DateTime OrderDate { get; set; }

    [DisplayName("Amount"), DisplayNumberFormat(scale: 2)]
    public decimal Amount { get; set; }

    [DisplayName("Status"), CheckboxFormatter(TrueText = "Completed", FalseText = "Pending")]
    public bool IsCompleted { get; set; }
}
```

### Smart pagination

`Idevs.Utilities.SmartPagination` produces page-break-aware row groups for paginated reports (e.g., a 30-row table that needs to break across A4 pages with a header on each page). Includes filler-row support to keep page heights consistent.

```csharp
var pages = SmartPagination.Build(orders, pageSize: 25, fillToPageSize: true);

foreach (var page in pages)
{
    // page.Items, page.PageNumber, page.TotalPages, page.IsFiller
}
```

### Static service locator

Last-resort bridge for legacy code paths that cannot receive DI. Prefer constructor injection for new code.

```csharp
var app = builder.Build();
app.UseIdevsStaticServiceLocator();

public static class LegacyHelper
{
    public static void DoWork()
    {
        var excel  = StaticServiceLocator.Resolve<IIdevsExcelExporter>();
        var maybe  = StaticServiceLocator.TryResolve<IOptionalService>();
        var single = StaticServiceLocator.ResolveSingleton<IMyCachedService>();

        using var scope = StaticServiceLocator.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<IScopedThing>();
    }
}
```

> Static resolution hides dependencies and complicates lifetime debugging. Treat it as a bridge, not a primary tool.

## Troubleshooting

### PDF export fails with "Chrome not found"

```csharp
if (!ChromeHelper.IsChromeDownloaded())
    ChromeHelper.DownloadChrome();
```

### PDF export hangs or times out

External CSS/JS that can't be loaded blocks rendering. Inline styles instead:

```html
<style>/* your styles */</style>
```

### Excel column formatting not applied

Make sure formatter attributes are on the column class (the `typeof(...)` you pass to `Export`), not the row.

### Excel export memory pressure on large datasets

Process in batches and concatenate worksheets:

```csharp
const int batch = 10_000;
for (var i = 0; i < total; i += batch)
{
    var slice = GetBatch(i, batch);
    // accumulate or stream to disk
}
```

### Source generator emits unexpected diagnostics

Each `IDEVSGEN001`–`IDEVSGEN010` diagnostic includes the offending type/member and an explanatory message. The most common ones:

- `IDEVSGEN001` — class uses a legacy registration attribute. Switch to `[Scoped]` / `[Singleton]` / `[Transient]`.
- `IDEVSGEN002` / `IDEVSGEN003` — multiple registration attributes on the same class, or attribute + marker interface conflict.
- `IDEVSGEN006` — the declared `ServiceType` is not implemented by the class.

If the generator misbehaves on a specific build, set `<IdevsCoreLibUseSourceGenerator>false</IdevsCoreLibUseSourceGenerator>` in the consumer csproj to fall back to runtime scanning, then file an issue with a repro.

## Migration guide

Detailed upgrade notes for every minor and major version live in [MIGRATION.md](MIGRATION.md). Latest transitions:

- [v0.7.1 → v0.7.2 — RepositoryBase Criteria-Based Update/Delete + TryFirst Alias](MIGRATION.md#v071--v072--repositorybase-criteria-based-updatedelete--tryfirst-alias)
- [v0.6.x → v0.7.0 — Source-Generator DI Registration](MIGRATION.md#v06x--v070--source-generator-di-registration)
- [v0.5.0 → v0.6.0 — RepositoryBase Redesign](MIGRATION.md#v050--v060--repositorybase-redesign)
- [v0.3.x → v0.5.0 — Package Layout & DI Changes](MIGRATION.md#v03x--v050--package-layout--di-changes)
- [v0.1.x → v0.2.0 — Autofac Integration](MIGRATION.md#v01x--v020--autofac-integration)
- [v0.0.x → v0.1.x — Service Registration & Chrome Setup](MIGRATION.md#v00x--v01x--service-registration--chrome-setup)

## Contributing

Contributions are welcome. Workflow:

1. Fork the repo (or push a branch if you have write access).
2. Open a PR against `main`. The repo enforces these rules via a ruleset:
   - CI must pass (`Build & Test` on .NET 8 + .NET 10).
   - GitHub Copilot is auto-requested as a reviewer.
   - Force-pushes and direct commits to `main` are blocked.
3. The maintainer reviews and merges. Squash or rebase, your choice.

### Code conventions

- Match existing patterns in the file you're editing.
- Keep public-API changes additive when possible. For breaking changes, update `PublicAPI.Unshipped.txt` in the affected project so the analyzer surfaces the diff.
- Tests live under `tests/` and follow the existing `CapturingRepo`-style dispatch pattern for repository tests.

## Support

If this library has been useful to you, consider supporting its development:

<a href="https://buymeacoffee.com/klomkling" target="_blank">
  <img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" height="60" width="217" />
</a>

## License

MIT — see [LICENSE](LICENSE).

## Author

[@klomkling](https://github.com/klomkling) — Sarawut Phaekuntod

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

---

**Made for the Serenity Framework community.**
