using Serenity;

namespace Idevs.Extensions;

public static class TextLocalizerExtension
{
    public static string Translate(this ITextLocalizer localizer, string moduleName, string key)
    {
        var name = $"Data.{moduleName}.{key}";
        return localizer.TryGet(name) ?? key;
    }

    public static string TranslateText(this string key, string moduleName, ITextLocalizer? localizer) =>
        localizer?.TryGet($"{moduleName}.{key}") ?? key;

    public static string TranslateData(this string key, string moduleName, ITextLocalizer localizer) =>
        localizer.Translate(moduleName, key);
}
