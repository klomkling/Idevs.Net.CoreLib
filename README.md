

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

### 1. Service Registration with Autofac (Recommended)

Idevs.Net.CoreLib now uses Autofac as the preferred dependency injection container. Add the following to your `Program.cs`:

```csharp
using Idevs.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Autofac as the service provider and register Idevs services
builder.UseIdevsAutofac();

// Your other service registrations
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Your middleware configuration
app.UseRouting();
app.MapControllers();

app.Run();
```

### 1.1. Alternative Service Registration (Legacy)

For projects that cannot use Autofac, you can still use the traditional service collection approach:

```csharp
// In ConfigureServices method or Program.cs
builder.Services.AddIdevsCorelibServices();

// Note: RegisterServices() is now obsolete and included in AddIdevsCorelibServices()
```

### 1.2. Advanced Autofac Configuration

For more advanced scenarios, you can customize the Autofac container:

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

Idevs.Net.CoreLib supports both legacy attributes and enhanced standard attributes for service registration:

#### Legacy Attributes (Backward Compatibility)

```csharp
[ScopedRegistration]
public class OrderService : IOrderService
{
    // Your service implementation
}

[SingletonRegiatration]
public class CacheService : ICacheService
{
    // Singleton service
}

[TransientRegistration]
public class EmailService : IEmailService
{
    // Transient service
}
```

#### Standard Attributes (Enhanced Features)

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
| Backward Compatibility | ✅ | ✅ (both work together) |

### Static Service Resolution

For scenarios where dependency injection is not feasible (e.g., static methods, legacy code integration), you can use the StaticServiceLocator:

```csharp
// Initialize in your Program.cs (automatic with Autofac)
var app = builder.Build();
app.UseIdevsStaticServiceLocator(); // Automatically detects Autofac or traditional DI

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
        // Cache singleton services for better performance
        var cachedService = StaticServiceLocator.ResolveSingleton<IMySingletonService>();
    }
}
```

**Important**: Use StaticServiceLocator sparingly and prefer proper dependency injection whenever possible.

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

### From v0.1.x to v0.2.0 (Current)

#### Autofac Integration (Recommended)

1. **Update to Autofac**: Replace service collection registration with Autofac:

```csharp
// Old way (v0.1.x)
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIdevsCorelibServices();

// New way (v0.2.0) - Recommended
var builder = WebApplication.CreateBuilder(args);
builder.UseIdevsAutofac();
```

2. **Legacy Support**: If you cannot use Autofac, the old way still works:

```csharp
// Still supported for backward compatibility
builder.Services.AddIdevsCorelibServices();

// Note: RegisterServices() is now obsolete
// builder.Services.RegisterServices(); // Remove this line
```

#### Benefits of Autofac Integration

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

3. **Static Service Provider**: Migrate to StaticServiceLocator (recommended):

```csharp
// Old way (still works but obsolete)
StaticServiceProvider.Provider = app.ApplicationServices;
var service = StaticServiceProvider.GetService<IMyService>();

// New way (recommended)
var app = builder.Build();
app.UseIdevsStaticServiceLocator(); // Automatic initialization
var service = StaticServiceLocator.Resolve<IMyService>();

// Or manual initialization
// StaticServiceLocator.Initialize(app.Services);
```

#### StaticServiceLocator Benefits

- **Autofac Support**: Works seamlessly with both Autofac and traditional DI
- **Thread Safety**: Improved thread-safe operations
- **Better Error Handling**: More descriptive error messages
- **Scoped Resolution**: Support for creating service scopes
- **Performance**: Caching options for singleton services
- **Backward Compatibility**: Automatic fallback when using StaticServiceProvider

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
