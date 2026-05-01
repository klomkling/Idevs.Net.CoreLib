using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public class DisplayPercentageAttribute(int scale = 2)
    : DisplayFormatAttribute($"N{scale} %");
