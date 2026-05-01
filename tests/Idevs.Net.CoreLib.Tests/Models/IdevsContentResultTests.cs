using Idevs.Models;
using Microsoft.AspNetCore.Mvc;

namespace Idevs.Net.CoreLib.Tests.Models;

public class IdevsContentResultTests
{
    [Fact]
    public void Create_WithPdfContentType_ReturnsPdfFileResult()
    {
        var data = new byte[] { 1, 2, 3 };

        var result = IdevsContentResult.Create(data, IdevsContentType.PDF, "report.pdf");

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("report.pdf", result.FileDownloadName);
        Assert.Same(data, result.FileContents);
    }

    [Fact]
    public void CreateResponse_WithPdfContentType_ReturnsPdfContentType()
    {
        var data = new byte[] { 4, 5, 6 };

        var response = IdevsContentResult.CreateResponse(data, IdevsContentType.PDF, "report.pdf");

        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal("report.pdf", response.DownloadName);
        Assert.Equal(Convert.ToBase64String(data), response.Content);
    }
}
