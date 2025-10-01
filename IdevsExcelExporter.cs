using System.Collections;
using Ardalis.GuardClauses;
using ClosedXML.Excel;
using FastMember;
using Idevs.Models;
using Serenity.Data;
using Serenity.Reporting;

namespace Idevs;

/// <summary>
/// Provides Excel export functionality for Serenity Framework applications
/// </summary>
public interface IIdevsExcelExporter
{
    /// <summary>
    /// Exports data to Excel format using predefined columns
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="columns">Column definitions for the export</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Export(IEnumerable data, IEnumerable<ReportColumn> columns);

    /// <summary>
    /// Exports data to Excel format using column type reflection
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="columnsType">Type containing column definitions</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Export(IEnumerable data, Type columnsType);

    /// <summary>
    /// Exports data to Excel format with specific column selection
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="columnsType">Type containing column definitions</param>
    /// <param name="exportColumns">Names of columns to include in export</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Export(IEnumerable data, Type columnsType, IEnumerable<string> exportColumns);

    /// <summary>
    /// Exports data to Excel format with custom report headers
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="columnsType">Type containing column definitions</param>
    /// <param name="reportHeaders">Headers to include at the top of the report</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Export(IEnumerable data, Type columnsType, IEnumerable<ReportHeader> reportHeaders);

    /// <summary>
    /// Exports data to Excel format with column selection and report headers
    /// </summary>
    /// <param name="data">The data to export</param>
    /// <param name="columnsType">Type containing column definitions</param>
    /// <param name="exportColumns">Names of columns to include in export</param>
    /// <param name="reportHeaders">Headers to include at the top of the report</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Export(
        IEnumerable data,
        Type columnsType,
        IEnumerable<string> exportColumns,
        IEnumerable<ReportHeader>? reportHeaders
    );

    /// <summary>
    /// Generates Excel file from structured data with full control over formatting
    /// </summary>
    /// <param name="rows">Data rows to export</param>
    /// <param name="columns">Column definitions</param>
    /// <param name="reportHeaders">Optional report headers</param>
    /// <param name="sheetName">Name of the Excel sheet</param>
    /// <returns>Excel file as byte array</returns>
    byte[] Generate(
        IList rows,
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<ReportHeader>? reportHeaders,
        string sheetName = "Page1"
    );
}

public class IdevsExcelExporter(IServiceProvider serviceProvider) : IIdevsExcelExporter
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <inheritdoc />
    public byte[] Export(IEnumerable data, IEnumerable<ReportColumn> columns)
    {
        Guard.Against.Null(data, nameof(data));
        Guard.Against.Null(columns, nameof(columns));
        
        var report = new TabularDataReport(data, columns);
        return Render(report, null);
    }

    /// <inheritdoc />
    public byte[] Export(IEnumerable data, Type columnsType)
    {
        Guard.Against.Null(data, nameof(data));
        Guard.Against.Null(columnsType, nameof(columnsType));
        
        var report = new TabularDataReport(data, columnsType, _serviceProvider);
        return Render(report, null);
    }

    /// <inheritdoc />
    public byte[] Export(IEnumerable data, Type columnsType, IEnumerable<string> exportColumns)
    {
        Guard.Against.Null(data, nameof(data));
        Guard.Against.Null(columnsType, nameof(columnsType));
        Guard.Against.Null(exportColumns, nameof(exportColumns));
        
        var report = new TabularDataReport(data, columnsType, exportColumns, _serviceProvider);
        return Render(report, null);
    }

    /// <inheritdoc />
    public byte[] Export(
        IEnumerable data,
        Type columnsType,
        IEnumerable<ReportHeader> reportHeaders
    )
    {
        Guard.Against.Null(data, nameof(data));
        Guard.Against.Null(columnsType, nameof(columnsType));
        Guard.Against.Null(reportHeaders, nameof(reportHeaders));
        
        var report = new TabularDataReport(data, columnsType, _serviceProvider);
        return Render(report, reportHeaders);
    }

    /// <inheritdoc />
    public byte[] Export(
        IEnumerable data,
        Type columnsType,
        IEnumerable<string> exportColumns,
        IEnumerable<ReportHeader>? reportHeaders
    )
    {
        Guard.Against.Null(data, nameof(data));
        Guard.Against.Null(columnsType, nameof(columnsType));
        Guard.Against.Null(exportColumns, nameof(exportColumns));
        
        var report = new TabularDataReport(data, columnsType, exportColumns, _serviceProvider);
        return Render(report, reportHeaders);
    }

    private byte[] Render(
        IDataOnlyReport report,
        IEnumerable<ReportHeader>? headers
    )
    {
        var columns = report.GetColumnList();

        var input = report.GetData();
        var list = (input as IEnumerable) ?? new List<object> { input };
        var data = list.Cast<object?>().ToList();

        return Generate(data, columns, headers);
    }

    private static readonly Type[] DateTimeTypes = new[]
    {
        typeof(DateTime),
        typeof(DateTime?),
        typeof(TimeSpan),
        typeof(TimeSpan?)
    };

    private static readonly Type[] NumberTypes = new[]
    {
        typeof(short),
        typeof(short?),
        typeof(int),
        typeof(int?),
        typeof(long),
        typeof(long?),
        typeof(float),
        typeof(float?),
        typeof(decimal),
        typeof(decimal?)
    };

    private static string FixFormatSpecifier(string format, Type dataType)
    {
        if (string.IsNullOrEmpty(format))
        {
            return format;
        }

        if (
            format.Contains('f', StringComparison.Ordinal)
            && Array.IndexOf(DateTimeTypes, dataType) >= 0
        )
        {
            return format.Replace('f', '0');
        }

        if (
            !format.StartsWith("n", StringComparison.OrdinalIgnoreCase)
            || Array.IndexOf(NumberTypes, dataType) < 0
        )
        {
            return format;
        }

        if (
            format.Contains('%', StringComparison.OrdinalIgnoreCase)
            && Array.IndexOf(NumberTypes, dataType) >= 0
        )
        {
            return format.Replace(" ", "");
        }

        if (int.TryParse(format.ToLower().Replace("n", string.Empty), out var n) == false)
        {
            n = 0;
        }

        return n == 0 ? "#,##0" : "#,##0.".PadRight(n + 6, '0');
    }

    /// <inheritdoc />
    public byte[] Generate(
        IList rows,
        IReadOnlyList<ReportColumn> columns,
        IEnumerable<ReportHeader>? reportHeaders,
        string sheetName = "Sheet1"
    )
    {
        Guard.Against.Null(rows, nameof(rows));
        Guard.Against.Null(columns, nameof(columns));
        Guard.Against.NullOrWhiteSpace(sheetName, nameof(sheetName));
        
        if (columns.Count == 0)
            throw new ArgumentException("At least one column must be provided.", nameof(columns));
        
        using var workbook = CreateWorkbook(sheetName);
        var worksheet = workbook.Worksheets.First();
        
        var startRow = CalculateStartRow(reportHeaders);
        CreateTableHeader(worksheet, columns, startRow);
        
        var dataList = ExtractDataFromRows(rows, columns);
        
        if (rows.Count > 0)
        {
            CreateTable(worksheet, dataList, columns, startRow);
        }
        
        ApplyColumnFormatting(worksheet, columns);
        worksheet.Columns().AdjustToContents();
        
        AddReportHeaders(worksheet, reportHeaders);
        
        return SaveWorkbookToByteArray(workbook);
    }
    
    /// <summary>
    /// Creates and configures a new Excel workbook
    /// </summary>
    private static XLWorkbook CreateWorkbook(string sheetName)
    {
        var workbook = new XLWorkbook();
        workbook.Style.Font.FontName = "Carlito";
        workbook.Worksheets.Add(sheetName);
        return workbook;
    }
    
    /// <summary>
    /// Calculates the starting row for the data table based on report headers
    /// </summary>
    private static int CalculateStartRow(IEnumerable<ReportHeader>? reportHeaders)
    {
        var startRow = reportHeaders?.Count(x => !string.IsNullOrEmpty(x.HeaderLine)) + 1 ?? 0;
        return startRow + 1; // Add 1 for the table header row
    }
    
    /// <summary>
    /// Extracts and processes data from rows into a structured format
    /// </summary>
    private static List<object[]> ExtractDataFromRows(IList rows, IReadOnlyList<ReportColumn> columns)
    {
        var dataList = new List<object[]>();
        Field[]? fields = null;
        TypeAccessor? accessor = null;
        bool[]? invalidProperty = null;
        var colCount = columns.Count;
        
        foreach (var obj in rows)
        {
            var data = new object?[colCount];
            var row = obj as IRow;
            
            // Initialize field mappings for IRow objects
            if (row != null)
            {
                fields ??= InitializeFieldMappings(columns, row);
            }
            // Initialize type accessor for regular objects
            else if (obj != null && obj is not IDictionary and not IDictionary<string, object>)
            {
                if (accessor == null)
                {
                    (accessor, invalidProperty) = InitializeTypeAccessor(obj, columns);
                }
            }
            
            // Extract data for each column
            for (var c = 0; c < colCount; c++)
            {
                data[c] = ExtractColumnValue(obj, row, columns[c], c, fields, accessor, invalidProperty);
            }
            
            dataList.Add(data!);
        }
        
        return dataList;
    }
    
    /// <summary>
    /// Initializes field mappings for IRow objects
    /// </summary>
    private static Field[] InitializeFieldMappings(IReadOnlyList<ReportColumn> columns, IRow row)
    {
        var fields = new Field[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var columnName = columns[i].Name;
            fields[i] = row.FindFieldByPropertyName(columnName) ?? row.FindField(columnName);
        }
        return fields;
    }
    
    /// <summary>
    /// Initializes type accessor for regular objects
    /// </summary>
    private static (TypeAccessor accessor, bool[] invalidProperty) InitializeTypeAccessor(object obj, IReadOnlyList<ReportColumn> columns)
    {
        var accessor = TypeAccessor.Create(obj.GetType());
        var invalidProperty = new bool[columns.Count];
        
        for (var c = 0; c < columns.Count; c++)
        {
            try
            {
                _ = accessor[obj, columns[c].Name];
            }
            catch
            {
                invalidProperty[c] = true;
            }
        }
        
        return (accessor, invalidProperty);
    }
    
    /// <summary>
    /// Extracts value for a specific column from the given object
    /// </summary>
    private static object? ExtractColumnValue(
        object? obj, 
        IRow? row, 
        ReportColumn column, 
        int columnIndex,
        Field[]? fields, 
        TypeAccessor? accessor, 
        bool[]? invalidProperty)
    {
        if (row != null)
        {
            var field = fields?[columnIndex];
            return field?.AsObject(row);
        }
        
        if (obj == null) return null;
        
        return obj switch
        {
            IDictionary<string, object> stringDict => 
                stringDict.TryGetValue(column.Name, out var value) ? value : null,
            IDictionary dict => 
                dict.Contains(column.Name) ? dict[column.Name] ?? string.Empty : null,
            _ when accessor != null && invalidProperty != null && !invalidProperty[columnIndex] => 
                accessor[obj, column.Name],
            _ => null
        };
    }
    
    /// <summary>
    /// Applies formatting to worksheet columns based on column definitions
    /// </summary>
    private static void ApplyColumnFormatting(IXLWorksheet worksheet, IReadOnlyList<ReportColumn> columns)
    {
        for (var i = 1; i <= columns.Count; i++)
        {
            var column = columns[i - 1];
            if (!string.IsNullOrEmpty(column.Format))
            {
                worksheet.Column(i).Style.NumberFormat.Format = FixFormatSpecifier(
                    column.Format,
                    column.DataType
                );
            }
        }
    }
    
    /// <summary>
    /// Adds report headers to the worksheet
    /// </summary>
    private static void AddReportHeaders(IXLWorksheet worksheet, IEnumerable<ReportHeader>? reportHeaders)
    {
        if (reportHeaders == null) return;
        
        var startRow = 1;
        foreach (var header in reportHeaders)
        {
            if (!string.IsNullOrEmpty(header.HeaderLine))
            {
                worksheet.Cell(startRow++, 1).Value = header.HeaderLine;
            }
        }
    }
    
    /// <summary>
    /// Saves workbook to byte array
    /// </summary>
    private static byte[] SaveWorkbookToByteArray(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void CreateTableHeader(
        IXLWorksheet worksheet,
        IReadOnlyList<ReportColumn> columns,
        int startRow
    )
    {
        for (var col = 0; col < columns.Count; col++)
        {
            worksheet.Cell(startRow, col + 1).Value = columns[col].Title ?? columns[col].Name;
        }
    }

    private static void CreateTable(
        IXLWorksheet worksheet,
        ICollection dataList,
        IReadOnlyCollection<ReportColumn> columns,
        int startRow
    )
    {
        var endRow = startRow + dataList.Count;
        worksheet.Cell(startRow + 1, 1).InsertData(dataList);
        var range = worksheet.Range(startRow, 1, endRow, columns.Count);

        // create the actual table
        var table = range.CreateTable();

        // apply style
        const string theme = nameof(TableTheme.TableStyleMedium2);
        table.Theme = XLTableTheme.FromName(theme);
    }
}
