namespace Idevs.Net.CoreLib.Tests;

public class ChromeHelperTests
{
    [Fact]
    public void GetChromePath_WhenChromiumBaseExistsWithoutChromeDirectory_ReturnsNull()
    {
        var chromiumDirectory = Path.Combine(AppContext.BaseDirectory, "Idevs", "chromium");
        if (Directory.Exists(chromiumDirectory))
        {
            Directory.Delete(chromiumDirectory, recursive: true);
        }

        Assert.Null(ChromeHelper.GetChromePath());

        var exception = Record.Exception(() => ChromeHelper.GetChromePath());

        Assert.Null(exception);
        Assert.Null(ChromeHelper.GetChromePath());
    }
}
