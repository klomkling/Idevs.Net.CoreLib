using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Idevs.Helpers;

/// <summary>
/// Builder class for creating PdfOptions with common configurations
/// </summary>
public static class PdfOptionsBuilder
{
    /// <summary>
    /// Creates PdfOptions with no default browser headers/footers
    /// Perfect for clean documents without page numbers or URLs
    /// </summary>
    /// <param name="format">Paper f ormat (default: A4)</param>
    /// <param name="margins">Custom margins (default: 0mm all sides)</param>
    /// <returns>PdfOptions configured for clean output</returns>
    public static PdfOptions CreateClean(PaperFormat? format = null, (string top, string bottom, string left, string right)? margins = null)
    {
        var (top, bottom, left, right) = margins ?? ("0mm", "0mm", "0mm", "0mm");
        
        return new PdfOptions
        {
            Format = format ?? PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            DisplayHeaderFooter = false, // This removes default browser headers/footers
            HeaderTemplate = string.Empty,
            FooterTemplate = string.Empty,
            MarginOptions = new MarginOptions
            {
                Top = top,
                Bottom = bottom,
                Left = left,
                Right = right
            },
            OmitBackground = false,
            Scale = 1.0m
        };
    }
    
    /// <summary>
    /// Creates PdfOptions with custom header and footer templates
    /// </summary>
    /// <param name="headerTemplate">HTML template for header</param>
    /// <param name="footerTemplate">HTML template for footer</param>
    /// <param name="format">Paper format (default: A4)</param>
    /// <param name="headerHeight">Header margin height (default: 20mm)</param>
    /// <param name="footerHeight">Footer margin height (default: 20mm)</param>
    /// <returns>PdfOptions configured with custom templates</returns>
    public static PdfOptions CreateWithTemplates(
        string? headerTemplate = null, 
        string? footerTemplate = null,
        PaperFormat? format = null,
        string headerHeight = "20mm",
        string footerHeight = "20mm")
    {
        var hasHeader = !string.IsNullOrEmpty(headerTemplate);
        var hasFooter = !string.IsNullOrEmpty(footerTemplate);
        
        return new PdfOptions
        {
            Format = format ?? PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            DisplayHeaderFooter = hasHeader || hasFooter,
            HeaderTemplate = hasHeader ? headerTemplate : " ",
            FooterTemplate = hasFooter ? footerTemplate : " ",
            MarginOptions = new MarginOptions
            {
                Top = hasHeader ? headerHeight : "0mm",
                Bottom = hasFooter ? footerHeight : "0mm",
                Left = "0mm",
                Right = "0mm"
            },
            OmitBackground = false,
            Scale = 1.0m
        };
    }
    
    /// <summary>
    /// Creates PdfOptions for business documents with standard margins
    /// </summary>
    /// <param name="format">Paper format (default: A4)</param>
    /// <returns>PdfOptions with business-appropriate margins</returns>
    public static PdfOptions CreateBusiness(PaperFormat? format = null)
    {
        return new PdfOptions
        {
            Format = format ?? PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            DisplayHeaderFooter = false, // No default browser headers/footers
            HeaderTemplate = string.Empty,
            FooterTemplate = string.Empty,
            MarginOptions = new MarginOptions
            {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            },
            OmitBackground = false,
            Scale = 1.0m
        };
    }
}
