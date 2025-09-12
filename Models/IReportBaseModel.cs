namespace Idevs.Models;

/// <summary>
/// Configuration for smart pagination
/// </summary>
public class PaginationConfig
{
    /// <summary>
    /// Capacity of first page (may include special content like supplier details, document info, etc.)
    /// </summary>
    public int FirstPageSize { get; set; }

    /// <summary>
    /// Capacity of regular pages (full content pages with no special headers or footers)
    /// </summary>
    public int RegularPageSize { get; set; }

    /// <summary>
    /// Number of rows to reserve on last page for footer content (summary, signatures, etc.)
    /// </summary>
    public int LastPageReserveRows { get; set; }

    /// <summary>
    /// Enable console logging for pagination analysis
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
/// Result of pagination operation
/// </summary>
public class PaginationResult<T>
{
    /// <summary>
    /// List of pages with their content and metadata
    /// </summary>
    public List<PageData<T>> Pages { get; set; } = new();

    /// <summary>
    /// Total number of pages generated
    /// </summary>
    public int TotalPages => Pages.Count;

    /// <summary>
    /// Total number of items paginated
    /// </summary>
    public int TotalItems { get; set; }
}

/// <summary>
/// Data for a single page
/// </summary>
public class PageData<T>
{
    /// <summary>
    /// Page index (0-based)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Actual items on this page
    /// </summary>
    public List<ItemWithLineNumber<T>> Items { get; set; } = new();

    /// <summary>
    /// Filler rows to maintain consistent page height
    /// </summary>
    public List<FillerRow> FillerRows { get; set; } = new();

    /// <summary>
    /// True if this is the first page (contains special header content)
    /// </summary>
    public bool IsFirst { get; set; }

    /// <summary>
    /// True if this is the last page (contains footer content)
    /// </summary>
    public bool IsLast { get; set; }

    /// <summary>
    /// Starting index of items on this page in the original list
    /// </summary>
    public int PageOffset { get; set; }

    /// <summary>
    /// Total capacity of this page (including filler rows)
    /// </summary>
    public int Capacity { get; set; }
}

/// <summary>
/// Item with its line number for display
/// </summary>
public class ItemWithLineNumber<T>
{
    /// <summary>
    /// Sequential line number (1-based) across all pages
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// The actual item data
    /// </summary>
    public T Item { get; set; } = default!;
}

/// <summary>
/// Represents a filler row for consistent page height
/// </summary>
public class FillerRow
{
    /// <summary>
    /// Indicates this is a filler row (used in templates)
    /// </summary>
    public bool IsFiller => true;
}

/// <summary>
/// Base interface for report models that support pagination
/// </summary>
/// <typeparam name="T">Type of items in the Details collection</typeparam>
public interface IReportBaseModel<T>
{
    /// <summary>
    /// Collection of detail items that will be paginated
    /// </summary>
    IEnumerable<T> Details { get; set; }
}
