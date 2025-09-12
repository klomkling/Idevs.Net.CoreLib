# SmartPagination Utility

A flexible pagination utility for documents with different page capacities, designed for PDF/document generation where first page, regular pages, and last page have different content requirements.

## Features

- ✅ **Different Page Capacities**: First page (with header content), regular pages (full content), last page (with footer/signatures)
- ✅ **Smart Distribution**: Optimally distributes items across pages to maximize space utilization
- ✅ **Always Includes Footer**: Ensures summary/signature pages are always created
- ✅ **Filler Row Support**: Maintains consistent page heights with automatic filler rows
- ✅ **Comprehensive Logging**: Detailed console output for debugging pagination logic
- ✅ **Generic Type Support**: Works with any item type
- ✅ **Template Compatible**: Outputs data structures compatible with Handlebars templates

## Quick Start

### Basic Usage

```csharp
using Idevs.Net.CoreLib.Utilities;

// Simple method (backward compatible)
var paginatedData = SmartPagination.CreatePaginatedData(
    items: myOrderItems,
    firstPageSize: 25,      // First page capacity (includes supplier details)
    regularPageSize: 29,    // Regular page capacity (full content)
    lastPageReserveRows: 9  // Rows reserved for summary/signatures
);
```

### Advanced Usage with Configuration

```csharp
var config = new SmartPagination.PaginationConfig
{
    FirstPageSize = 25,          // First page with special header content
    RegularPageSize = 29,        // Regular pages with full content
    LastPageReserveRows = 9,     // Space for footer/signatures
    EnableLogging = true         // Console logging for debugging
};

var result = SmartPagination.CreatePages(orderItems, config);

// Access detailed information
Console.WriteLine($"Generated {result.TotalPages} pages for {result.TotalItems} items");

foreach (var page in result.Pages)
{
    Console.WriteLine($"Page {page.Index + 1}: {page.Items.Count} items, IsFirst: {page.IsFirst}, IsLast: {page.IsLast}");
}
```

## Use Cases

### Purchase Orders / Invoices
```csharp
// Purchase Order with supplier details on first page
var poPagination = SmartPagination.CreatePaginatedData(
    poItems, 
    firstPageSize: 25,   // Space for supplier detail box
    regularPageSize: 29, // Full item rows
    lastPageReserveRows: 9  // Summary totals + signatures
);
```

### Reports with Headers/Footers
```csharp
// Financial report with summary
var reportPagination = SmartPagination.CreatePaginatedData(
    reportLines,
    firstPageSize: 20,   // Executive summary
    regularPageSize: 25, // Detail lines  
    lastPageReserveRows: 8  // Charts + analysis
);
```

### Multi-page Forms
```csharp
// Application with terms on last page
var formPagination = SmartPagination.CreatePaginatedData(
    formFields,
    firstPageSize: 15,   // Header info + instructions
    regularPageSize: 20, // Form fields
    lastPageReserveRows: 10  // Terms + signatures
);
```

## Template Integration

The output is designed to work seamlessly with Handlebars templates:

### Handlebars Template Example
```handlebars
{{#each Pagination.pages}}
<div class="page">
    {{#if isFirst}}
    <!-- Special header content only on first page -->
    <div class="supplier-details">...</div>
    {{/if}}
    
    <table class="items-table">
        {{#each items}}
        <tr>
            <td>{{LineNumber}}</td>
            <td>{{Item.Description}}</td>
            <td>{{Item.Amount}}</td>
        </tr>
        {{/each}}
        
        {{#each fillerRows}}
        <tr class="filler">
            <td>&nbsp;</td>
            <td>&nbsp;</td>
            <td>&nbsp;</td>
        </tr>
        {{/each}}
    </table>
    
    {{#if isLast}}
    <!-- Summary and signatures only on last page -->
    <div class="summary">...</div>
    <div class="signatures">...</div>
    {{/if}}
</div>
{{/each}}
```

## API Reference

### SmartPagination.CreatePaginatedData<T>

Creates paginated data compatible with existing templates.

**Parameters:**
- `items: List<T>` - Items to paginate
- `firstPageSize: int` - Capacity of first page
- `regularPageSize: int` - Capacity of regular pages  
- `lastPageReserveRows: int` - Rows reserved on last page
- `enableLogging: bool` - Enable console logging (default: true)

**Returns:** Anonymous object with `pages` array

### SmartPagination.CreatePages<T>

Advanced method returning strongly-typed results.

**Parameters:**
- `items: List<T>` - Items to paginate
- `config: PaginationConfig` - Configuration object

**Returns:** `PaginationResult<T>` with detailed page information

### PaginationConfig Properties

- `FirstPageSize: int` - First page capacity
- `RegularPageSize: int` - Regular page capacity
- `LastPageReserveRows: int` - Reserved rows on last page
- `EnableLogging: bool` - Console logging toggle

### PageData<T> Properties

- `Index: int` - Page index (0-based)
- `Items: List<ItemWithLineNumber<T>>` - Items with line numbers
- `FillerRows: List<FillerRow>` - Filler rows for consistent height
- `IsFirst: bool` - First page flag
- `IsLast: bool` - Last page flag  
- `PageOffset: int` - Starting index in original list
- `Capacity: int` - Total page capacity

## Console Output Example

```
=== Smart Pagination Analysis ===
Total Items: 35
First Page Capacity: 25 rows (includes special content)
Regular Page Capacity: 29 rows per page
Reserved Rows: 9 (for footer content)
Last Page Capacity: 20 items max
================================================

✓ MULTI-PAGE SOLUTION:
   - 35 items require multiple pages
   - First page: 25 items (+ special content)
   - Regular pages: 29 items each
   - Last page: max 20 items (+ footer)

✓ DISTRIBUTION PLAN:
   - First page: 25 items (+ special content)
   - 1 regular page(s): 10 items
   - Last page: 0 items (+ footer)
   - ✓ Last page: 0 items + footer

================================================
✓ PAGINATION COMPLETE: 2 page(s) generated
================================================
```

## Migration from Local Methods

If you have existing pagination code, you can easily migrate:

### Before (Local Method)
```csharp
var paginatedData = CreateSmartPaginatedData(items, 25, 29, 9);
```

### After (Core Library)
```csharp
var paginatedData = SmartPagination.CreatePaginatedData(items, 25, 29, 9);
```

The output format is identical, so no template changes are required!

## Best Practices

1. **Choose Appropriate Capacities**: Consider the actual content that will appear on each page type
2. **Reserve Adequate Space**: Ensure `lastPageReserveRows` accounts for all footer content
3. **Test with Various Data Sizes**: Verify pagination works with 1 item, edge cases, and large datasets
4. **Enable Logging During Development**: Use console output to understand pagination decisions
5. **Use Type Safety**: Prefer the strongly-typed `CreatePages<T>` method for new development

## Troubleshooting

### Items Don't Fit on Single Page
- Increase `firstPageSize` or reduce `lastPageReserveRows`
- Check if special content is taking more space than expected

### Summary/Signatures Not Appearing  
- Verify `lastPageReserveRows` is sufficient for your footer content
- Check template uses `{{#if isLast}}` condition properly

### Page Heights Inconsistent
- Ensure your CSS accounts for filler rows
- Verify `capacity` values match your page layout design

### Performance Issues
- Disable logging in production: `enableLogging: false`
- Consider caching pagination results for repeated operations