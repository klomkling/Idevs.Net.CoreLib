using Serenity.Services;

namespace Idevs.Models;

public enum TableTheme
{
    None = 0,
    TableStyleDark1 = 201,
    TableStyleDark2 = 202,
    TableStyleDark3 = 203,
    TableStyleDark4 = 204,
    TableStyleDark5 = 205,
    TableStyleDark6 = 206,
    TableStyleDark7 = 207,
    TableStyleDark8 = 208,
    TableStyleDark9 = 209,
    TableStyleDark10 = 210,
    TableStyleDark11 = 211,
    TableStyleLight1 = 1,
    TableStyleLight2 = 2,
    TableStyleLight3 = 3,
    TableStyleLight4 = 4,
    TableStyleLight5 = 5,
    TableStyleLight6 = 6,
    TableStyleLight7 = 7,
    TableStyleLight8 = 8,
    TableStyleLight9 = 9,
    TableStyleLight10 = 10,
    TableStyleLight11 = 11,
    TableStyleLight12 = 12,
    TableStyleLight13 = 13,
    TableStyleLight14 = 14,
    TableStyleLight15 = 15,
    TableStyleLight16 = 16,
    TableStyleLight17 = 17,
    TableStyleLight18 = 18,
    TableStyleLight19 = 19,
    TableStyleLight20 = 20,
    TableStyleLight21 = 21,
    TableStyleMedium1 = 101,
    TableStyleMedium2 = 102,
    TableStyleMedium3 = 103,
    TableStyleMedium4 = 104,
    TableStyleMedium5 = 105,
    TableStyleMedium6 = 106,
    TableStyleMedium7 = 107,
    TableStyleMedium8 = 108,
    TableStyleMedium9 = 109,
    TableStyleMedium10 = 110,
    TableStyleMedium11 = 111,
    TableStyleMedium12 = 112,
    TableStyleMedium13 = 113,
    TableStyleMedium14 = 114,
    TableStyleMedium15 = 115,
    TableStyleMedium16 = 116,
    TableStyleMedium17 = 117,
    TableStyleMedium18 = 118,
    TableStyleMedium19 = 119,
    TableStyleMedium20 = 120,
    TableStyleMedium21 = 121,
    TableStyleMedium22 = 122,
    TableStyleMedium23 = 123,
    TableStyleMedium24 = 124,
    TableStyleMedium25 = 125,
    TableStyleMedium26 = 126,
    TableStyleMedium27 = 127,
    TableStyleMedium28 = 128
}

public enum AggregateType
{
    LABEL = 0,
    GROUP = 1,
    AVERAGE = 2,
    COUNT = 3,
    SUM = 4
}

public struct AggregateColumn
{
    public string Title { get; set; }
    public string ColumnName { get; set; }
    public AggregateType AggregateType { get; set; }
}

public struct ReportHeader
{
    public string HeaderLine { get; set; }
}

public interface IIdevsExportRequest
{
    string ViewName { get; set; }
    string? CompanyName { get; set; }
    string? ReportName { get; set; }
    string? SelectionRange { get; set; }
    string? ConditionRange { get; set; }
    string? Logo { get; set; }
    PageSize PageSize { get; set; }
    PageMargin Margin { get; set; }
    object? Entity { get; set; }
    TableTheme TableTheme { get; set; }
    IEnumerable<AggregateColumn>? AggregateColumns { get; set; }
}

public class IdevsExportRequest : ListRequest, IIdevsExportRequest
{
    public string ViewName { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? ReportName { get; set; }
    public string? SelectionRange { get; set; }
    public string? ConditionRange { get; set; }
    public string? Logo { get; set; }
    public PageSize PageSize { get; set; } = new PageSize();
    public PageMargin Margin { get; set; } = new PageMargin();
    public object? Entity { get; set; }
    public TableTheme TableTheme { get; set; }
    public IEnumerable<AggregateColumn>? AggregateColumns { get; set; }
}
