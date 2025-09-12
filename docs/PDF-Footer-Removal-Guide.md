# PDF Footer Removal Guide

This guide explains how to remove default browser footers (like "Page 1 of 2" or URL) from PDF generation using IdevsPdfExporter.

## Problem

When generating PDFs with PuppeteerSharp, browsers often add default headers/footers showing:
- Page numbers ("Page 1 of 2")
- URL/File path
- Date/Time stamps
- Browser-generated content

## Solution

The updated `IdevsPdfExporter` (v0.2.7+) now provides better control over PDF generation to eliminate unwanted default footers.

## Methods to Remove Default Footers

### 1. **Automatic Footer Removal (Default Behavior)**

The standard methods now automatically remove default footers when no custom header/footer is provided:

```csharp
// This will generate PDF WITHOUT default browser footers
var response = await pdfExporter.CreateResponseAsync<MyModel, MyDetail>(
    model, 
    "template.hbs"
    // No header/footer = No default browser header/footer
);
```

**Key Changes:**
- `DisplayHeaderFooter = false` when no templates provided
- `MarginOptions` set to `0mm` when no custom templates
- `HeaderTemplate` and `FooterTemplate` set to empty strings

### 2. **Using PdfOptionsBuilder for Complete Control**

Use the new `PdfOptionsBuilder` helper class for precise control:

```csharp
using Idevs.Helpers;

// Option A: Completely clean PDF (no headers, no footers, no margins)
var cleanOptions = PdfOptionsBuilder.CreateClean();
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, cleanOptions);

// Option B: Business document with standard margins but no default footers
var businessOptions = PdfOptionsBuilder.CreateBusiness();
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, businessOptions);

// Option C: Custom margins but no default footers
var customOptions = PdfOptionsBuilder.CreateClean(
    format: PaperFormat.A4,
    margins: ("5mm", "5mm", "10mm", "10mm") // top, bottom, left, right
);
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, customOptions);
```

### 3. **Custom PdfOptions for Advanced Control**

Create your own `PdfOptions` for maximum flexibility:

```csharp
var customOptions = new PdfOptions
{
    Format = PaperFormat.A4,
    PrintBackground = true,
    PreferCSSPageSize = true,
    DisplayHeaderFooter = false,  // ← KEY: This removes default headers/footers
    HeaderTemplate = string.Empty,
    FooterTemplate = string.Empty,
    MarginOptions = new MarginOptions
    {
        Top = "0mm",     // No top margin = no header space
        Bottom = "0mm",  // No bottom margin = no footer space
        Left = "5mm",
        Right = "5mm"
    },
    Scale = 1.0m
};

var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, customOptions);
```

## Key Properties for Footer Control

| Property | Value | Effect |
|----------|-------|---------|
| `DisplayHeaderFooter` | `false` | **Primary control** - removes all default browser headers/footers |
| `HeaderTemplate` | `string.Empty` | Ensures no header content |
| `FooterTemplate` | `string.Empty` | Ensures no footer content |
| `MarginOptions.Top` | `"0mm"` | Eliminates header space |
| `MarginOptions.Bottom` | `"0mm"` | Eliminates footer space |

## Migration from Previous Versions

### Before (v0.2.6 and earlier)
```csharp
// Old way - might show default footers
var response = await pdfExporter.CreateResponseAsync(
    templatePath, 
    model
);
```

### After (v0.2.7+)
```csharp
// New way - automatically removes default footers
var response = await pdfExporter.CreateResponseAsync<MyModel, MyDetail>(
    model,
    templatePath
);

// Or with explicit clean options
var cleanOptions = PdfOptionsBuilder.CreateClean();
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, cleanOptions);
```

## Troubleshooting

### Still seeing footers?

1. **Check if you're passing header/footer templates:**
   ```csharp
   // This might still show default footers if templates are invalid
   var response = await pdfExporter.CreateResponseAsync<MyModel, MyDetail>(
       model, templatePath, 
       headerPath,  // If this file doesn't exist or is empty
       footerPath   // Default browser footer might appear
   );
   ```

2. **Ensure you're using the updated methods:**
   ```csharp
   // Old method - may not have footer removal
   var bytes = await pdfExporter.CompileTemplateAsync(templatePath, model);
   
   // New method - has footer removal
   var response = await pdfExporter.CreateResponseAsync<MyModel, MyDetail>(
       model, templatePath);
   ```

3. **Use explicit clean options:**
   ```csharp
   var cleanOptions = PdfOptionsBuilder.CreateClean();
   var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, cleanOptions);
   ```

## CSS-based Footer Control

You can also control footers through CSS in your HTML templates:

```css
@page {
    margin: 0;  /* Remove all margins */
}

/* Or specific margins */
@page {
    margin-top: 0;
    margin-bottom: 0;
    margin-left: 10mm;
    margin-right: 10mm;
}

/* Hide any print-specific elements */
@media print {
    .no-print {
        display: none;
    }
}
```

## Examples

### Clean Business Document
```csharp
// Perfect for invoices, purchase orders, reports
var options = PdfOptionsBuilder.CreateBusiness(); // 10mm margins, no default footers
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, options);
```

### Edge-to-Edge Design
```csharp
// Perfect for certificates, flyers, artistic layouts
var options = PdfOptionsBuilder.CreateClean(); // 0mm margins, no footers
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, options);
```

### Custom Layout
```csharp
// Perfect for specific business requirements
var options = PdfOptionsBuilder.CreateClean(
    format: PaperFormat.Letter,
    margins: ("15mm", "10mm", "20mm", "20mm")
);
var bytes = await pdfExporter.ExportByteArrayAsync(htmlContent, options);
```

## Result

Following this guide, your PDFs will be generated **without** any default browser footers, giving you complete control over the document appearance.