using Idevs.Models;

namespace Idevs;

/// <summary>
/// Smart pagination utility for documents with different page capacities
/// Supports first page (with special content), regular pages, and last page (with footer content)
/// </summary>
public static class SmartPagination
{
    /// <summary>
    /// Creates smart paginated data with different capacities for first, regular, and last pages
    /// </summary>
    /// <typeparam name="T">Type of items to paginate</typeparam>
    /// <param name="items">List of items to paginate</param>
    /// <param name="config">Pagination configuration</param>
    /// <returns>Pagination result with page data and metadata</returns>
    public static PaginationResult<T> CreatePages<T>(List<T> items, PaginationConfig config)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (config == null) throw new ArgumentNullException(nameof(config));

        var result = new PaginationResult<T> { TotalItems = items.Count };
        var lastPageCapacity = config.RegularPageSize - config.LastPageReserveRows;

        if (config.EnableLogging)
        {
            LogPaginationAnalysis(items.Count, config, lastPageCapacity);
        }

        // Check if single page is sufficient
        var singlePageCapacity = config.FirstPageSize - config.LastPageReserveRows;

        if (items.Count <= singlePageCapacity)
        {
            CreateSinglePage(result, items, singlePageCapacity, config.EnableLogging);
        }
        else
        {
            CreateMultiplePages(result, items, config, lastPageCapacity);
        }

        if (config.EnableLogging)
        {
            Console.WriteLine($"================================================");
            Console.WriteLine($"✓ PAGINATION COMPLETE: {result.TotalPages} page(s) generated");
            Console.WriteLine($"================================================");
        }

        return result;
    }

    /// <summary>
    /// Creates paginated data using the old method signature for backward compatibility
    /// </summary>
    /// <typeparam name="T">Type of items to paginate</typeparam>
    /// <param name="items">List of items to paginate</param>
    /// <param name="firstPageSize">Capacity of first page</param>
    /// <param name="regularPageSize">Capacity of regular pages</param>
    /// <param name="lastPageReserveRows">Rows reserved on last page</param>
    /// <param name="enableLogging">Enable console logging</param>
    /// <returns>Anonymous object compatible with existing templates</returns>
    public static PaginationResult<T> CreatePaginatedData<T>(List<T> items, int firstPageSize, int regularPageSize, int lastPageReserveRows, bool enableLogging = true)
    {
        var config = new PaginationConfig
        {
            FirstPageSize = firstPageSize,
            RegularPageSize = regularPageSize,
            LastPageReserveRows = lastPageReserveRows,
            EnableLogging = enableLogging
        };

        return CreatePages(items, config);
    }

    private static void LogPaginationAnalysis(int totalItems, PaginationConfig config, int lastPageCapacity)
    {
        Console.WriteLine($"=== Smart Pagination Analysis ===");
        Console.WriteLine($"Total Items: {totalItems}");
        Console.WriteLine($"First Page Capacity: {config.FirstPageSize} rows (includes special content)");
        Console.WriteLine($"Regular Page Capacity: {config.RegularPageSize} rows per page");
        Console.WriteLine($"Reserved Rows: {config.LastPageReserveRows} (for footer content)");
        Console.WriteLine($"Last Page Capacity: {lastPageCapacity} items max");
        Console.WriteLine($"================================================");
    }

    private static void CreateSinglePage<T>(PaginationResult<T> result, List<T> items, int capacity, bool enableLogging)
    {
        if (enableLogging)
        {
            Console.WriteLine($"✓ SINGLE PAGE SOLUTION:");
            Console.WriteLine($"   - {items.Count} items fit (capacity: {capacity})");
            Console.WriteLine($"   - {capacity - items.Count} filler rows added");
            Console.WriteLine($"   - Special content + footer included");
        }

        var page = new PageData<T>
        {
            Index = 0,
            IsFirst = true,
            IsLast = true,
            PageOffset = 0,
            Capacity = capacity
        };

        // Add items with line numbers
        for (var i = 0; i < items.Count; i++)
        {
            page.Items.Add(new ItemWithLineNumber<T>
            {
                LineNumber = i + 1,
                Item = items[i]
            });
        }

        // Add filler rows
        for (var i = 0; i < capacity - items.Count; i++)
        {
            page.FillerRows.Add(new FillerRow());
        }

        result.Pages.Add(page);
    }

    private static void CreateMultiplePages<T>(PaginationResult<T> result, List<T> items, PaginationConfig config, int lastPageCapacity)
    {
        if (config.EnableLogging)
        {
            Console.WriteLine($"✓ MULTI-PAGE SOLUTION:");
            Console.WriteLine($"   - {items.Count} items require multiple pages");
            Console.WriteLine($"   - First page: {config.FirstPageSize} items (+ special content)");
            Console.WriteLine($"   - Regular pages: {config.RegularPageSize} items each");
            Console.WriteLine($"   - Last page: max {lastPageCapacity} items (+ footer)");
        }

        var currentIndex = 0;
        var pageIndex = 0;

        // Create first page
        var firstPageItems = Math.Min(config.FirstPageSize, items.Count);
        var firstPage = CreatePage(items.Take(firstPageItems).ToList(), pageIndex, currentIndex, config.FirstPageSize, true, false);
        result.Pages.Add(firstPage);

        currentIndex += firstPageItems;
        pageIndex++;

        // Calculate distribution for remaining items
        var remainingItems = items.Count - currentIndex;
        var regularPagesNeeded = 0;
        var itemsOnRegularPages = 0;
        var itemsOnLastPage = remainingItems;

        if (remainingItems > lastPageCapacity)
        {
            var itemsNeedingRegularPages = remainingItems - lastPageCapacity;
            regularPagesNeeded = (int)Math.Ceiling((double)itemsNeedingRegularPages / config.RegularPageSize);
            itemsOnRegularPages = Math.Min(regularPagesNeeded * config.RegularPageSize, itemsNeedingRegularPages);
            itemsOnLastPage = remainingItems - itemsOnRegularPages;
        }

        if (config.EnableLogging)
        {
            Console.WriteLine($"✓ DISTRIBUTION PLAN:");
            Console.WriteLine($"   - First page: {firstPageItems} items (+ special content)");
            Console.WriteLine($"   - {regularPagesNeeded} regular page(s): {itemsOnRegularPages} items");
            Console.WriteLine($"   - Last page: {itemsOnLastPage} items (+ footer)");
        }

        // Create regular pages
        for (int i = 0; i < regularPagesNeeded; i++)
        {
            var pageItems = items.Skip(currentIndex).Take(config.RegularPageSize).ToList();
            var regularPage = CreatePage(pageItems, pageIndex, currentIndex, config.RegularPageSize, false, false);
            result.Pages.Add(regularPage);

            currentIndex += pageItems.Count;
            pageIndex++;
        }

        // Create last page
        var lastPageItems = items.Skip(currentIndex).ToList();
        var lastPage = CreatePage(lastPageItems, pageIndex, currentIndex, lastPageCapacity, false, true);
        result.Pages.Add(lastPage);

        if (config.EnableLogging)
        {
            Console.WriteLine($"   - ✓ Last page: {lastPageItems.Count} items + footer");
        }
    }

    private static PageData<T> CreatePage<T>(List<T> items, int pageIndex, int startIndex, int capacity, bool isFirst, bool isLast)
    {
        var page = new PageData<T>
        {
            Index = pageIndex,
            IsFirst = isFirst,
            IsLast = isLast,
            PageOffset = startIndex,
            Capacity = capacity
        };

        // Add items with line numbers
        for (var i = 0; i < items.Count; i++)
        {
            page.Items.Add(new ItemWithLineNumber<T>
            {
                LineNumber = startIndex + i + 1,
                Item = items[i]
            });
        }

        // Add filler rows
        for (var i = 0; i < capacity - items.Count; i++)
        {
            page.FillerRows.Add(new FillerRow());
        }

        return page;
    }
}
