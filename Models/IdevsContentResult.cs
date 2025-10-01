using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Idevs.Models;

public enum IdevsContentType
{
    Excel,
    PDF
}

public class IdevsContentResult
{
    /// <summary>
    /// Creates a FileContentResult containing passed data
    /// </summary>
    /// <param name="data">Data containing Pdf bytes</param>
    /// <returns></returns>
    public static FileContentResult Create(byte[] data, IdevsContentType contentType)
    {
        return Create(data, contentType);
    }

    /// <summary>
    /// Creates a FileContentResult containing passed data and a download name
    /// </summary>
    /// <param name="data">Data containing Pdf file bytes</param>
    /// <param name="contentType">Content type</param>
    /// <param name="downloadName">Optional download name</param>
    public static FileContentResult Create(byte[] data, IdevsContentType contentType, string? downloadName)
    {
        var dataType = contentType == IdevsContentType.Excel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "application/octet-stream";
        var result = new FileContentResult(data, dataType)
        {
            FileDownloadName = downloadName ?? GetDownloadName(contentType),
        };
        return result;
    }

    public static IActionResult CreatePdfViewResult(HttpResponse response, byte[] data, string downloadName)
    {
        var fileName = downloadName ?? GetDownloadName(IdevsContentType.PDF);
        var ms = new MemoryStream(data);
        var result = new FileStreamResult(ms, "application/pdf")
        {
            FileDownloadName = fileName
        };
        response.Headers.Clear();
        response.Headers.Append("Content-Disposition", "inline; filename=" + fileName);
        return result;
    }

    public static IdevsContentResponse CreateResponse(byte[] data, IdevsContentType contentType, string? downloadName = null)
    {
        return new IdevsContentResponse
        {
            Content = Convert.ToBase64String(data),
            ContentType = contentType == IdevsContentType.Excel
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "application/octet-stream",
            DownloadName = downloadName ?? GetDownloadName(contentType)
        };
    }

    private static string GetDownloadName(IdevsContentType contentType)
    {
        var ext = contentType == IdevsContentType.Excel ? ".xlsx" : ".pdf";
        return ("report" + DateTime.Now.ToString("yyyyMMddHHmmss",
                           CultureInfo.InvariantCulture) + ext);
    }
}
