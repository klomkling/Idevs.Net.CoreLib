using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public class DisplayNumberFormatAttribute(int scale = 2)
    : DisplayFormatAttribute($"N{scale}");