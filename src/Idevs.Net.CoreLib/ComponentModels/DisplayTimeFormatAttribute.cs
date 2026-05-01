using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public class DisplayTimeFormatAttribute(bool withSeconds = false)
    : DisplayFormatAttribute(withSeconds ? "HH:mm:ss" : "HH:mm");
