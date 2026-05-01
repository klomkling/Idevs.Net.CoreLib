using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public class DisplayDateTimeFormatAttribute(bool withSeconds = false)
    : DisplayFormatAttribute(withSeconds ? "dd/MM/yyyy HH:mm:ss" : "dd/MM/yyyy HH:mm");
