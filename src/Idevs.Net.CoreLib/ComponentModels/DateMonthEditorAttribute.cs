using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public partial class DateMonthEditorAttribute() : CustomEditorAttribute(Key)
{
    private const string Key = "DateMonthEditor";

    public string Display
    {
        get => GetOption<string>("display") ?? "2-digit";
        set => SetOption("display", value);
    }

    public string Locale
    {
        get => GetOption<string>("locale") ?? "en";
        set => SetOption("locale", value);
    }
}
