using System.Globalization;
using System.Reflection;
using Ardalis.GuardClauses;
using Idevs.Models;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Idevs;

/// <summary>
/// Provides PDF export functionality using Puppeteer Sharp for HTML-to-PDF conversion
/// </summary>
public interface IIdevsPdfExporter
{
    /// <summary>
    /// Exports HTML content to PDF format synchronously
    /// </summary>
    /// <param name="html">HTML content to convert to PDF</param>
    /// <param name="header">HTML template for page header</param>
    /// <param name="footer">HTML template for page footer</param>
    /// <returns>PDF file as a byte array</returns>
    byte[] ExportByteArray(string html,
        string? header = null,
        string? footer = null) =>
        Task.Run(async () => await ExportByteArrayAsync(html, header, footer)).GetAwaiter().GetResult();

    /// <summary>
    /// Exports HTML content to PDF format asynchronously
    /// </summary>
    /// <param name="html">HTML content to convert to PDF</param>
    /// <param name="header">HTML template for page header</param>
    /// <param name="footer">HTML template for page footer</param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Task containing a PDF file as a byte array</returns>
    Task<byte[]> ExportByteArrayAsync(
        string html,
        string? header = null,
        string? footer = null,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a response containing the PDF file for download
    /// </summary>
    /// <param name="html"></param>
    /// <param name="header"></param>
    /// <param name="footer"></param>
    /// <param name="downloadName"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IdevsContentResponse> CreateResponseAsync(string html,
        string? header = null,
        string? footer = null,
        string? downloadName = null,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of PDF export functionality using Puppeteer Sharp
/// </summary>
public class IdevsPdfExporter : IIdevsPdfExporter, IAsyncDisposable
{
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private readonly LaunchOptions _launchOptions;
    private IBrowser? _browser;
    private bool _disposed;

    public IdevsPdfExporter()
    {
        var chromePath = ChromeHelper.GetChromePath();
        _launchOptions = new LaunchOptions
        {
            Headless = true,
            IgnoredDefaultArgs = ["--disable-extensions"],
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-web-security",
                "--allow-running-insecure-content"
            ],
            ExecutablePath = chromePath
        };
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(IdevsPdfExporter));
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportByteArrayAsync(
        string html,
        string? header = null,
        string? footer = null,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureNotDisposed();
        Guard.Against.NullOrEmpty(html, nameof(html));
        var pdfOptions = BuildPdfOptions(header, footer, options);
        return await GeneratePdfAsync(html, pdfOptions, cancellationToken);
    }
    
    /// <summary>
    /// Exports HTML content to PDF format with custom PdfOptions
    /// </summary>
    /// <param name="html">HTML content to convert to PDF</param>
    /// <param name="pdfOptions">Custom PDF generation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task containing a PDF file as a byte array</returns>
    public async Task<byte[]> ExportByteArrayAsync(
        string html,
        PdfOptions pdfOptions,
        CancellationToken cancellationToken = default
    )
    {
        EnsureNotDisposed();
        Guard.Against.NullOrEmpty(html, nameof(html));
        Guard.Against.Null(pdfOptions, nameof(pdfOptions));
        var safeOptions = ClonePdfOptions(pdfOptions);
        return await GeneratePdfAsync(html, safeOptions, cancellationToken);
    }

    public async Task<IdevsContentResponse> CreateResponseAsync(
        string html,
        string? header = null,
        string? footer = null,
        string? downloadName = null,
        PdfOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        EnsureNotDisposed();
        var bytes = await ExportByteArrayAsync(html, header, footer, options, cancellationToken);
        return new IdevsContentResponse
        {
            Content = Convert.ToBase64String(bytes),
            ContentType = "application/pdf",
            DownloadName = downloadName ??
                           "report" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".pdf"
        };
    }

    private async Task<byte[]> GeneratePdfAsync(
        string html,
        PdfOptions pdfOptions,
        CancellationToken cancellationToken = default
    )
    {
        Guard.Against.NullOrEmpty(html, nameof(html));
        Guard.Against.Null(pdfOptions, nameof(pdfOptions));

        cancellationToken.ThrowIfCancellationRequested();

        var browser = await GetBrowserAsync(cancellationToken).ConfigureAwait(false);
        await using var page = await browser.NewPageAsync().ConfigureAwait(false);
        await page.SetContentAsync(html, new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] })
            .ConfigureAwait(false);

        var pdfData = await page.PdfDataAsync(pdfOptions).ConfigureAwait(false);

        if (pdfData == null || pdfData.Length == 0)
        {
            throw new InvalidOperationException("PDF generation failed - no data returned");
        }

        return pdfData;
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var current = Volatile.Read(ref _browser);
        if (IsBrowserUsable(current))
        {
            return current!;
        }

        await _browserLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            current = _browser;
            if (IsBrowserUsable(current))
            {
                return current!;
            }

            EnsureNotDisposed();
            var browser = await Puppeteer.LaunchAsync(_launchOptions).ConfigureAwait(false);
            if (browser == null)
            {
                throw new InvalidOperationException("Failed to initialize browser instance");
            }

            browser.Disconnected += BrowserOnDisconnected;
            _browser = browser;
            return browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    private static bool IsBrowserUsable(IBrowser? browser) => browser is not null && !browser.IsClosed;

    private void BrowserOnDisconnected(object? sender, EventArgs e)
    {
        if (sender is not IBrowser browser)
        {
            return;
        }

        browser.Disconnected -= BrowserOnDisconnected;
        Interlocked.CompareExchange(ref _browser, null, browser);
    }

    private static PdfOptions BuildPdfOptions(string? header, string? footer, PdfOptions? options)
    {
        var hasHeader = !string.IsNullOrEmpty(header);
        var hasFooter = !string.IsNullOrEmpty(footer);

        if (options is null)
        {
            return new PdfOptions
            {
                PreferCSSPageSize = true,
                PrintBackground = true,
                HeaderTemplate = hasHeader ? header! : ".",
                FooterTemplate = hasFooter ? footer! : ".",
                DisplayHeaderFooter = hasHeader || hasFooter,
                MarginOptions = new MarginOptions
                {
                    Top = hasHeader ? "20mm" : "0mm",
                    Bottom = hasFooter ? "20mm" : "0mm",
                    Left = "0mm",
                    Right = "0mm"
                },
                OmitBackground = false,
                Scale = 1.0m,
                Format = PaperFormat.A4
            };
        }

        var effective = ClonePdfOptions(options);

        if (hasHeader)
        {
            effective.HeaderTemplate = header!;
        }
        else if (string.IsNullOrEmpty(effective.HeaderTemplate))
        {
            effective.HeaderTemplate = ".";
        }

        if (hasFooter)
        {
            effective.FooterTemplate = footer!;
        }
        else if (string.IsNullOrEmpty(effective.FooterTemplate))
        {
            effective.FooterTemplate = ".";
        }

        effective.DisplayHeaderFooter = effective.DisplayHeaderFooter || hasHeader || hasFooter;
        effective.MarginOptions ??= new MarginOptions();

        if (effective.MarginOptions.Top is not string topValue || string.IsNullOrWhiteSpace(topValue))
        {
            effective.MarginOptions.Top = hasHeader ? "20mm" : "0mm";
        }

        if (effective.MarginOptions.Bottom is not string bottomValue || string.IsNullOrWhiteSpace(bottomValue))
        {
            effective.MarginOptions.Bottom = hasFooter ? "20mm" : "0mm";
        }

        if (effective.MarginOptions.Left is not string leftValue || string.IsNullOrWhiteSpace(leftValue))
        {
            effective.MarginOptions.Left = "0mm";
        }

        if (effective.MarginOptions.Right is not string rightValue || string.IsNullOrWhiteSpace(rightValue))
        {
            effective.MarginOptions.Right = "0mm";
        }

        return effective;
    }

    private static PdfOptions ClonePdfOptions(PdfOptions source)
    {
        var clone = new PdfOptions();
        var properties = typeof(PdfOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            if (property.PropertyType == typeof(MarginOptions))
            {
                if (property.GetValue(source) is MarginOptions margin)
                {
                    property.SetValue(clone, new MarginOptions
                    {
                        Top = margin.Top,
                        Bottom = margin.Bottom,
                        Left = margin.Left,
                        Right = margin.Right
                    });
                }

                continue;
            }

            property.SetValue(clone, property.GetValue(source));
        }

        return clone;
    }


    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _browserLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_browser != null)
            {
                var browser = _browser;
                _browser = null;
                browser.Disconnected -= BrowserOnDisconnected;

                try
                {
                    if (!browser.IsClosed)
                    {
                        await browser.CloseAsync().ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Ignore shutdown exceptions to avoid masking dispose failures
                }
                finally
                {
                    await browser.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _browserLock.Release();
            _browserLock.Dispose();
        }
    }

}
