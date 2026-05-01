

# Idevs.Net.CoreLib

[![NuGet Version](https://img.shields.io/nuget/v/Idevs.Net.CoreLib.svg)](https://www.nuget.org/packages/Idevs.Net.CoreLib)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A comprehensive extension library for the Serenity Framework that provides enhanced functionality for data export, PDF generation, UI components, and more.

## Features

- 📊 **Excel Export**: Advanced Excel generation with formatting, themes, and aggregation support
- 📄 **PDF Export**: HTML-to-PDF conversion using Puppeteer Sharp
- 🎨 **UI Components**: Extended form controls and formatters for Serenity
- 🔄 **Service Registration**: Automatic dependency injection with attributes
- 📐 **Bootstrap Grid**: Enhanced column width controls with Bootstrap 5 support
- 🌍 **Localization**: Enhanced text localization extensions

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Idevs.Net.CoreLib
```

Or via Package Manager Console:

```powershell
Install-Package Idevs.Net.CoreLib
```

## Quick Start

### 1. Service Registration

Idevs.Net.CoreLib uses standard `Microsoft.Extensions.DependencyInjection` by default. Add the following to your `Program.cs`:

```csharp
using Idevs.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdevsCorelibServices();

// Your other service registrations
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Your middleware configuration
app.UseRouting();
app.MapControllers();

app.Run();
```

### 1.1. Autofac Integration

Autofac support is available from the optional `Idevs.Net.CoreLib.Autofac` package:

```bash
dotnet add package Idevs.Net.CoreLib.Autofac
```

```csharp
using Idevs.Extensions;

builder.UseIdevsAutofac();
```

### 1.2. Advanced Autofac Configuration

After installing `Idevs.Net.CoreLib.Autofac`, you can customize the Autofac container:

```csharp
// With custom container configuration
builder.UseIdevsAutofac(containerBuilder =>
{
    // Your custom registrations
    containerBuilder.RegisterType<MyCustomService>()
        .As<IMyCustomService>()
        .InstancePerLifetimeScope();
});

// With additional modules
builder.UseIdevsAutofac(new MyCustomModule(), new AnotherModule());
```

### 2. Chrome Setup for PDF Export

**Important**: For PDF export functionality, you need to download Chrome/Chromium:

```csharp
// In Program.cs Main method (before starting the application)
public static void Main(string[] args)
{
    // Download Chrome if not already present
    ChromeHelper.DownloadChrome();
    
    CreateHostBuilder(args).Build().Run();
}
```

## Usage Examples

### Excel Export

```csharp
public class OrderController : ServiceEndpoint
{
    private readonly IIdevsExcelExporter _excelExporter;
    
    public OrderController(IIdevsExcelExporter excelExporter)
    {
        _excelExporter = excelExporter;
    }
    
    [HttpPost]
    public IActionResult ExportToExcel(ListRequest request)
    {
        var orders = GetOrders(request); // Your data retrieval logic
        
        // Simple export
        var excelBytes = _excelExporter.Export(orders, typeof(OrderColumns));
        
        return IdevsContentResult.Create(
            excelBytes, 
            IdevsContentType.Excel, 
            "orders.xlsx"
        );
    }
    
    [HttpPost]
    public IActionResult ExportWithHeaders(ListRequest request)
    {
        var orders = GetOrders(request);
        var headers = new[]
        {
            new ReportHeader { HeaderLine = "Order Report" },
            new ReportHeader { HeaderLine = $"Generated: {DateTime.Now:yyyy-MM-dd}" },
            new ReportHeader { HeaderLine = "" } // Empty line
        };
        
        var excelBytes = _excelExporter.Export(orders, typeof(OrderColumns), headers);
        
        return IdevsContentResult.Create(excelBytes, IdevsContentType.Excel, "order-report.xlsx");
    }
}
```

### PDF Export

```csharp
public class ReportController : ServiceEndpoint
{
    private readonly IIdevsPdfExporter _pdfExporter;
    private readonly IViewPageRenderer _viewRenderer;
    
    public ReportController(IIdevsPdfExporter pdfExporter, IViewPageRenderer viewRenderer)
    {
        _pdfExporter = pdfExporter;
        _viewRenderer = viewRenderer;
    }
    
    [HttpPost]
    public async Task<IActionResult> GenerateReport(ReportRequest request)
    {
        // Render HTML from Razor view
        var model = GetReportData(request);
        var html = await _viewRenderer.RenderViewAsync("Reports/OrderReport", model);
        
        // Convert to PDF
        var pdfBytes = await _pdfExporter.ExportByteArrayAsync(
            html,
            "<div style='text-align: center;'>Order Report</div>", // Header
            "<div style='text-align: center;'>Page <span class='pageNumber'></span></div>" // Footer
        );
        
        return IdevsContentResult.Create(pdfBytes, IdevsContentType.Pdf, "report.pdf");
    }
}
```

> **Note**
> From version 0.3.0 onward the PDF exporter expects pre-rendered HTML. Use your preferred templating solution (e.g., Razor via `IViewPageRenderer`) before calling `ExportByteArrayAsync` or `CreateResponseAsync`.

### UI Components

```csharp
// Enhanced column attributes
public class OrderColumns
{
    [DisplayName("Order ID"), ColumnWidth(ExtraLarge = 2)]
    public string OrderId { get; set; }
    
    [DisplayName("Customer"), FullColumnWidth]
    public string CustomerName { get; set; }
    
    [DisplayName("Order Date"), DisplayDateFormat, HalfWidth]
    public DateTime OrderDate { get; set; }
    
    [DisplayName("Amount"), DisplayNumberFormat("n2")]
    public decimal Amount { get; set; }
    
    [DisplayName("Status"), CheckboxFormatter(TrueText = "Completed", FalseText = "Pending")]
    public bool IsCompleted { get; set; }
}
```

### Service Registration with Attributes

Idevs.Net.CoreLib supports standard attributes for service registration:

#### Standard Attributes

```csharp
// Basic usage - auto-discovers I{ClassName} interface
[Scoped]
public class OrderService : IOrderService
{
    // Scoped service implementation
}

// Explicit service type specification
[Singleton(ServiceType = typeof(ICacheService))]
public class CacheService : ICacheService, IDisposable
{
    // Singleton service with explicit interface
}

// Named registrations (Autofac only)
[Transient(ServiceKey = "smtp")]
public class EmailService : IEmailService
{
    // SMTP email implementation
}
```

#### Legacy Attributes (Backward Compatibility)

The legacy `[ScopedRegistration]`, `[SingletonRegiatration]`, and `[TransientRegistration]`
attributes are still supported but obsolete. Prefer `[Scoped]`, `[Singleton]`, and `[Transient]`
for new code.

#### Advanced Features

```csharp
// Named registrations (Autofac only)
[Transient(ServiceKey = "smtp")]
public class SmtpEmailService : IEmailService
{
    // SMTP email implementation
}

[Transient(ServiceKey = "sendgrid")]
public class SendGridEmailService : IEmailService
{
    // SendGrid email implementation
}

// Self-registration without interface
[Scoped(AllowSelfRegistration = true)]
public class UtilityService
{
    public void DoWork() { }
}
```

#### Attribute Comparison

| Feature | Legacy Attributes | Standard Attributes |
|---------|-------------------|---------------------|
| Interface Discovery | `I{ClassName}` only | `I{ClassName}` + any interface + self-registration |
| Service Keys | Not supported | Supported (Autofac only) |
| Explicit Service Type | Not supported | Supported |
| Self-registration | Not supported | Supported |
| Status | Obsolete | Recommended |

### Static Service Resolution

Prefer constructor dependency injection for new code. `StaticServiceLocator` is still supported, but it should be treated as a last-resort compatibility bridge for static methods or legacy code paths that cannot receive dependencies through DI.

```csharp
// Initialize in your Program.cs only if static or legacy code needs it
var app = builder.Build();
app.UseIdevsStaticServiceLocator();

// Use in static methods or legacy code
public static class LegacyHelper
{
    public static void ProcessData()
    {
        // Resolve services statically
        var excelExporter = StaticServiceLocator.Resolve<IIdevsExcelExporter>();
        var pdfExporter = StaticServiceLocator.Resolve<IIdevsPdfExporter>();
        
        // Use try resolve for optional services
        var optionalService = StaticServiceLocator.TryResolve<IOptionalService>();
        if (optionalService != null)
        {
            // Use the service
        }
        
        // Use scoped resolution for per-request services
        using var scope = StaticServiceLocator.CreateScope();
        var scopedService = scope.ServiceProvider.GetService<IScopedService>();
    }
    
    public static void ProcessDataWithCaching()
    {
        // Cache only services that are registered as singletons
        var cachedService = StaticServiceLocator.ResolveSingleton<IMySingletonService>();
    }
}
```

**Important**: Do not use `StaticServiceLocator` from normal application services. Static resolution hides dependencies and can make service lifetime problems harder to diagnose.

## Configuration Options

### Excel Export Customization

```csharp
// Custom theme
var request = new IdevsExportRequest
{
    TableTheme = TableTheme.TableStyleMedium15,
    CompanyName = "My Company",
    ReportName = "Sales Report",
    PageSize = new PageSize(PageSizes.A4, PageOrientations.Landscape)
};
```

### PDF Export Options

```csharp
// Custom page settings in your CSS
@page {
    size: A4;
    margin: 1in;
}

// Or use PuppeteerSharp options directly
var pdfOptions = new PdfOptions
{
    Format = PaperFormat.A4,
    MarginOptions = new MarginOptions
    {
        Top = "1in",
        Right = "1in",
        Bottom = "1in",
        Left = "1in"
    },
    PreferCSSPageSize = true
};
```

## Troubleshooting

### PDF Export Issues

**Problem**: PDF generation fails with "Chrome not found" error
**Solution**: Ensure Chrome is downloaded:

```csharp
// Check if Chrome is available
if (!ChromeHelper.IsChromeDownloaded())
{
    ChromeHelper.DownloadChrome();
}
```

**Problem**: PDF export hangs or times out
**Solution**: Ensure your HTML doesn't have external dependencies that can't be loaded:

```html
<!-- Use inline CSS instead of external links -->
<style>
  /* Your styles here */
</style>
```

### Excel Export Issues

**Problem**: Column formatting not applied
**Solution**: Use proper format attributes:

```csharp
[DisplayNumberFormat("#,##0.00")] // For numbers
[DisplayDateFormat] // For dates (dd/MM/yyyy)
[DisplayPercentage] // For percentages
```

**Problem**: Large datasets cause memory issues
**Solution**: Process data in chunks or use streaming:

```csharp
// Process in smaller batches
const int batchSize = 10000;
for (int i = 0; i < totalRecords; i += batchSize)
{
    var batch = GetDataBatch(i, batchSize);
    // Process batch
}
```

## Migration Guide

### From v0.3.x to v0.5.0

#### Package Layout and DI Changes

1. **Standard DI is now the default**:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdevsCorelibServices();
```

2. **Autofac moved to `Idevs.Net.CoreLib.Autofac`**:

```bash
dotnet add package Idevs.Net.CoreLib.Autofac
```

```csharp
builder.UseIdevsAutofac();
```

3. **Serilog support is optional via `Idevs.Net.CoreLib.Serilog`**:

```bash
dotnet add package Idevs.Net.CoreLib.Serilog
```

```csharp
app.UseIdevsSerilogLogManager();
```

4. **StaticServiceProvider removed**:

`StaticServiceProvider` was removed in `0.5.0`. Use constructor dependency injection first. For legacy static integration points, use `StaticServiceLocator`.

### From v0.1.x to v0.2.0

#### Autofac Integration

- **Better Performance**: Autofac provides superior dependency resolution performance
- **Advanced Features**: Support for decorators, interceptors, and advanced lifetime scopes
- **Module System**: Organized service registration through modules
- **Attribute-Based Registration**: Automatic service discovery and registration

### From v0.0.x to v0.1.x

1. **Service Registration**: Replace manual service registration with `AddIdevsCorelibServices()`:

```csharp
// Old way
services.AddScoped<IViewPageRenderer, ViewPageRenderer>();
services.AddScoped<IIdevsPdfExporter, IdevsPdfExporter>();
services.AddScoped<IIdevsExcelExporter, IdevsExcelExporter>();

// New way
services.AddIdevsCorelibServices();
```

2. **Chrome Setup**: Add Chrome download to startup:

```csharp
// Add this to Program.cs
ChromeHelper.DownloadChrome();
```

3. **Static Service Provider**: `StaticServiceProvider` was removed in `0.5.0`. Use constructor dependency injection first. For legacy static integration points that cannot receive dependencies through DI, use `StaticServiceLocator`.

```csharp
var app = builder.Build();
app.UseIdevsStaticServiceLocator();
var service = StaticServiceLocator.Resolve<IMyService>();

// Or manual initialization
// StaticServiceLocator.Initialize(app.Services);
```

#### StaticServiceLocator Benefits

- **Legacy Bridge**: Supports static or legacy code while you migrate toward constructor DI
- **Better Error Handling**: More descriptive error messages
- **Scoped Resolution**: Support for creating service scopes
- **Singleton Cache**: Optional caching for services known to be registered as singletons

## Repositories

`Idevs.Net.CoreLib` ships a focused class hierarchy for data access:

- **`SqlServiceBase`** — base for services that need raw SQL access without
  being a typed-row repository. Provides `ISqlConnections`, lazy `Dialect`,
  `SqlQuery()`/`SqlInsert(t)`/`SqlUpdate(t)`/`SqlDelete(t)` factories, and a
  uniform `ExecuteAsync<T>` template that manages connection lifetime and
  composes with an optional `UnitOfWork`.

- **`RepositoryBase<TRow>`** — typed read/list/getby/create on a Serenity
  `IRow`. Methods: `FirstAsync`, `ListAsync`, `GetByAsync<TValue>`,
  `CreateAsync`. `[Obsolete]` sync wrappers for migration.

- **`RepositoryBase<TRow, TKey>`** — adds Id-keyed CRUD on `IIdRow`:
  `GetByIdAsync`, `GetByIdsAsync`, `UpdateAsync`, `DeleteByIdAsync`.

Connection key is configurable via the virtual `ConnectionKey` property or
the `[ConnectionKey("Warehouse")]` attribute.

For caching, see `Idevs.Caching.TwoLevelCacheExtensions` — async wrappers
around Serenity `ITwoLevelCache`.

**Migrating from 0.5.0:** see [docs/migrations/0.6.0-repositorybase.md](docs/migrations/0.6.0-repositorybase.md).

## Cloud Upload Storage

`Idevs.Net.CoreLib` can replace Serenity upload storage with S3-compatible object storage.

```csharp
using Idevs.Extensions;

builder.Services.AddCloudUploadStorage(builder.Configuration);
builder.Services.AddUploadStorage();
```

AWS S3 configuration:

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

Cloudflare R2 configuration:

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

Set `"Provider": "Local"` to keep Serenity's default local upload storage.

## Log Manager

`LogManager` provides a provider-neutral bridge for code paths that cannot receive `ILogger<T>` through dependency injection.

```csharp
using Idevs.Logging;
using Microsoft.Extensions.Logging;

LogManager.SetLoggerFactory(app.Services.GetRequiredService<ILoggerFactory>());
var logger = LogManager.GetLogger<Program>();
```

### Serilog Integration

Serilog support is available from the optional `Idevs.Net.CoreLib.Serilog` package.

```bash
dotnet add package Idevs.Net.CoreLib.Serilog
```

```csharp
using Idevs.Extensions;

app.UseIdevsSerilogLogManager();
```

The core package continues to use `Microsoft.Extensions.Logging` abstractions.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Authors

- [@klomkling](https://www.github.com/klomkling) - Sarawut Phaekuntod

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed history of changes.

---

**Made with ❤️ for the Serenity Framework community**
