using Serenity;

namespace Idevs.Extensions;

public static class TextLocalizerExtension
{
    public static string Translate(this ITextLocalizer localizer, string moduleName, string key)
    {
        var name = $"Data.{moduleName}.{key}";
        return localizer.TryGet(name) ?? key;
    }

    extension(string key)
    {
        public string TranslateText(string moduleName, ITextLocalizer? localizer) =>
            localizer?.TryGet($"{moduleName}.{key}") ?? key;

        public string TranslateData(string moduleName, ITextLocalizer localizer) =>
            localizer.Translate(moduleName, key);
    }
}
