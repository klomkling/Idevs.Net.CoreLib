namespace Idevs.Models;

public enum PageSizes
{
    A4,
    A3,
    Letter,
    Legal,
}

public enum PageOrientations
{
    Portrait,
    Landscape
}

public record PageSize(PageSizes Size = PageSizes.A4, PageOrientations Orientation = PageOrientations.Landscape)
{
    public string PageOrientation => Orientation.ToString().ToLowerInvariant();
    public override string ToString() => $"{Size} {Orientation.ToString().ToLowerInvariant()}";
}
