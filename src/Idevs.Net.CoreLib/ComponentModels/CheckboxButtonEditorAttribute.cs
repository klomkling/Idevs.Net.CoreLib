using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public partial class CheckboxButtonEditorAttribute() : CustomEditorAttribute(Key)
{
    private const string Key = "CheckboxButtonEditor";

    public string EnumKey
    {
        get => GetOption<string>("enumKey") ?? string.Empty;
        set => SetOption("enumKey", value);
    }

    public object EnumType
    {
        get => GetOption<object>("enumType") ?? string.Empty;
        set => SetOption("enumType", value);
    }

    public string LookupKey
    {
        get => GetOption<string>("lookupKey") ?? string.Empty;
        set => SetOption("lookupKey", value);
    }

    public bool IsStringId
    {
        get => GetOption<bool>("isStringId");
        set => SetOption("isStringId", value);
    }
}