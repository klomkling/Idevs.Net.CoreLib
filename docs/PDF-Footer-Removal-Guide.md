# PDF Footer Removal Guide

This guide explains how to remove default browser headers and footers (such as “Page 1 of 2”, URLs, or timestamps) when generating PDFs with `IdevsPdfExporter`.

## Background

Puppeteer- or Chrome-based PDF generation adds browser headers/footers unless explicit settings say otherwise. Starting with **Idevs.Net.CoreLib 0.3.0**, the PDF exporter no longer performs template compilation; instead, you pass it pre-rendered HTML plus optional header/footer fragments. The exporter now defaults to clean output when you omit custom header/footer content.

## Quick Start: Clean Output

```csharp
var html = await viewRenderer.RenderViewAsync("Reports/Order", model);
var bytes = await pdfExporter.ExportByteArrayAsync(html);
```

With no header/footer arguments, the exporter internally sets `DisplayHeaderFooter = false` and zero margins, so Chrome does not inject its own footer.

If you need an `IdevsContentResponse` for direct HTTP responses:

```csharp
var response = await pdfExporter.CreateResponseAsync(html);
```

## Custom Header/Footer Still Without Chrome Footers

Provide your header/footer markup and let the exporter build sane defaults:

```csharp
var header = "<div class='report-header'>Order Report</div>";
var footer = "<div class='report-footer'>Page <span class='pageNumber'></span></div>";

var bytes = await pdfExporter.ExportByteArrayAsync(html, header, footer);
```

Under the hood the exporter enables `DisplayHeaderFooter` and applies `20mm` top/bottom margins when content is supplied, keeping Chrome’s defaults disabled.

## Fine-Grained Control with PdfOptionsBuilder

Use `PdfOptionsBuilder` for common scenarios while preserving footer suppression:

```csharp
using Idevs.Helpers;
using PuppeteerSharp.Media;

var clean = PdfOptionsBuilder.CreateClean();
var business = PdfOptionsBuilder.CreateBusiness();
var custom = PdfOptionsBuilder.CreateClean(
    format: PaperFormat.A4,
    margins: ("5mm", "5mm", "10mm", "10mm")
);

var bytes = await pdfExporter.ExportByteArrayAsync(html, custom);
```

Even when you pass `PdfOptions`, the exporter clones the instance and fills in safe defaults (blank templates, zero margins) for any missing values.

## DIY PdfOptions

You can still create options manually:

```csharp
var options = new PdfOptions
{
    Format = PaperFormat.A4,
    PrintBackground = true,
    PreferCSSPageSize = true,
    DisplayHeaderFooter = false,
    HeaderTemplate = string.Empty,
    FooterTemplate = string.Empty,
    MarginOptions = new MarginOptions
    {
        Top = "0mm",
        Bottom = "0mm",
        Left = "5mm",
        Right = "5mm"
    },
    Scale = 1.0m
};

var bytes = await pdfExporter.ExportByteArrayAsync(html, options);
```

## Migration from Template-Based APIs

| Before 0.3.0 | After 0.3.0 |
|--------------|-------------|
| `CreateResponseAsync<MyModel, TDetail>(model, templatePath, ...)` | Render HTML yourself (e.g., Razor) and call `CreateResponseAsync(html, ...)`. |
| `CompileTemplateAsync(templatePath, model)` | Use your preferred templating engine outside the exporter, then pass the resulting HTML. |

### Example Migration

```csharp
// Old
var response = await pdfExporter.CreateResponseAsync(model, "Templates/Report.hbs");

// New
var html = await viewRenderer.RenderViewAsync("Reports/Report", model);
var response = await pdfExporter.CreateResponseAsync(html);
```

## Troubleshooting

- **Still seeing Chrome footers?** Ensure you are not forcing `DisplayHeaderFooter = true` without supplying templates, and that your margins are `0mm` when you want completely clean output.
- **Need per-page content?** Provide HTML fragments for `header` and `footer`; Chrome placeholders such as `<span class='pageNumber'></span>` still work.
- **Want additional styling?** Use CSS `@page` rules and print media queries in your HTML to control margins and visibility.

## Helpful CSS Snippets

```css
@page {
    margin: 0;      /* Edge-to-edge */
}

@media print {
    .no-print {
        display: none;
    }
}
```

These settings complement the exporter’s defaults to keep output free of unwanted browser-generated footers.
