namespace Idevs.Models;

public record PageMargin(string MarginTop = "3.2cm", string MarginLeft = "1cm", string MarginBottom = "1cm", string MarginRight = "1cm")
{
    public override string ToString()
    {
        return MarginLeft == MarginRight && MarginRight == MarginTop && MarginTop == MarginBottom
            ? MarginLeft
            : MarginLeft == MarginRight && MarginTop == MarginBottom
                ? $"{MarginTop} {MarginLeft}"
                : $"{MarginTop} {MarginLeft} {MarginBottom} {MarginRight}";
    }
}
