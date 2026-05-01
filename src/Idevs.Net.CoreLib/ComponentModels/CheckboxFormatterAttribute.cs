using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public partial class CheckboxFormatterAttribute() : CustomFormatterAttribute(Key)
{
    private const string Key = "Idevs.CheckboxFormatter";

    public string CssClass
    {
        get => GetOption<string>("cssClass") ?? string.Empty;
        set => SetOption("cssClass", value);
    }

    public string TrueText
    {
        get => GetOption<string>("trueText") ?? string.Empty;
        set => SetOption("trueText", value);
    }

    public string FalseText
    {
        get => GetOption<string>("falseText") ?? string.Empty;
        set => SetOption("falseText", value);
    }

    public string TrueValueIcon
    {
        get => GetOption<string>("trueValueIcon") ?? string.Empty;
        set => SetOption("trueValueIcon", value);
    }

    public string FalseValueIcon
    {
        get => GetOption<string>("falseValueIcon") ?? string.Empty;
        set => SetOption("falseValueIcon", value);
    }
}