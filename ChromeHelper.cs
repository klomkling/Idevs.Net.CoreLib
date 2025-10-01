using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Playwright;
using PuppeteerSharp;

namespace Idevs;

/// <summary>
/// Usage: Call ChromeHelper.DownloadChrome()
/// before using PuppeteerSharp or Playwright to ensure the Chromium browser is downloaded.
/// The best way is to call on Program.cs Main method.
/// Example:
/// public static void Main(string[] args)
/// {
///     ChromeHelper.DownloadChrome();
///
///     CreateHostBuilder(args).Build().Run();
/// }
/// </summary>
public static class ChromeHelper
{
    private static string BasePath => Path.Combine(AppContext.BaseDirectory, "Idevs", "chromium");
    private static readonly string[] DefaultBrowserArgs =
    [
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage",
        "--disable-web-security",
        "--allow-running-insecure-content"
    ];

    public static bool IsChromeDownloaded()
    {
        var browserPath = GetChromePath();
        return !string.IsNullOrEmpty(browserPath) && File.Exists(browserPath);
    }

    private static bool IsAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        try
        {
            // Use sysctl to detect actual hardware architecture
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "-n hw.optional.arm64",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            return output == "1";
        }
        catch
        {
            // Fallback: check for common ARM64 indicators
            return RuntimeInformation.OSArchitecture == Architecture.Arm64 ||
                   Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "ARM64";
        }
    }

    public static string? GetChromePath()
    {
        var basePath = BasePath;
        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
            return null;
        }

        string? chromiumPath = null;
        if (OperatingSystem.IsWindows())
        {
            basePath = Path.Combine(basePath, "Chrome");
            var directories = Directory.GetDirectories(basePath, "Win*");
            if (directories.Length > 0)
            {
                chromiumPath = Path.Combine(basePath, directories[0], "chrome-win64", "chrome.exe");
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            basePath = Path.Combine(basePath, "Chrome");
            var directories = Directory.GetDirectories(basePath, "Mac*");
            if (directories.Length > 0)
            {
                var chromeFolder = IsAppleSilicon()
                    ? "chrome-mac-arm64"
                    : "chrome-mac-x64";

                chromiumPath = Path.Combine(basePath, directories[0], chromeFolder, "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing");

                // Fallback: if the detected folder doesn't exist, try the other one
                if (!File.Exists(chromiumPath))
                {
                    string alternateChromeFolder = IsAppleSilicon()
                        ? "chrome-mac-x64"
                        : "chrome-mac-arm64";

                    string alternateChromePath = Path.Combine(basePath, directories[0], alternateChromeFolder, "Google Chrome for Testing.app", "Contents", "MacOS", "Google Chrome for Testing");

                    if (File.Exists(alternateChromePath))
                    {
                        chromiumPath = alternateChromePath;
                    }
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            basePath = Path.Combine(basePath, "Chrome");
            var directories = Directory.GetDirectories(basePath, "Linux*");
            if (directories.Length > 0)
            {
                // Detect if we're on ARM64 or x64
                var isArm64 = RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
                var chromeFolder = isArm64 ? "chrome-linux-arm64" : "chrome-linux64";

                chromiumPath = Path.Combine(basePath, directories[0], chromeFolder, "chrome");
            }
        }

        return chromiumPath;
    }

    public static void DownloadChrome()
    {
        if (IsChromeDownloaded()) return;

        var basePath = BasePath;

        // If the Chromium browser is not found, download it
        var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
        {
            Path = basePath
        });

        Task.Run(async () =>
        {
            await browserFetcher.DownloadAsync();
        }).GetAwaiter().GetResult();
    }

    public static string[] GetDefaultBrowserArgs() => DefaultBrowserArgs.ToArray();

    public static BrowserTypeLaunchOptions CreatePlaywrightLaunchOptions(
        bool? headless = null,
        IEnumerable<string>? args = null,
        string? executablePath = null,
        IEnumerable<string>? ignoredDefaultArgs = null)
    {
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = headless ?? true
        };

        var resolvedArgs = args?.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        if (resolvedArgs is { Length: > 0 })
        {
            launchOptions.Args = resolvedArgs;
        }
        else
        {
            launchOptions.Args = GetDefaultBrowserArgs();
        }

        var resolvedIgnored = ignoredDefaultArgs?.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        if (resolvedIgnored is { Length: > 0 })
        {
            launchOptions.IgnoreDefaultArgs = resolvedIgnored;
        }

        var resolvedExecutablePath = string.IsNullOrWhiteSpace(executablePath) ? GetChromePath() : executablePath;
        if (!string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            launchOptions.ExecutablePath = resolvedExecutablePath;
        }

        return launchOptions;
    }
}
