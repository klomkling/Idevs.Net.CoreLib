using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public partial class ZeroDisplayFormatterAttribute() : CustomFormatterAttribute(Key)
{
    private const string Key = "Idevs.ZeroDisplayFormatter";

    public string DisplayText
    {
        get => GetOption<string>("displayText") ?? string.Empty;
        set => SetOption("displayText", value);
    }
}
