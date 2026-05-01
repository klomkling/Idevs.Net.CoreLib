using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public partial class LookupFormatterAttribute() : CustomFormatterAttribute(Key)
{
    private const string Key = "Idevs.LookupFormatter";

    public string LookupKey
    {
        get => GetOption<string>("lookupKey") ?? string.Empty;
        set => SetOption("lookupKey", value);
    }
}