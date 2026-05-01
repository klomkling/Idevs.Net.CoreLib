using System.ComponentModel;

namespace Idevs.Extensions;

public static class EnumExtensions
{
    public static string GetDescription<T>(this T enumValue)
        where T : struct, IConvertible
    {
        if (!typeof(T).IsEnum)
            return string.Empty;

        var description = enumValue.ToString();
        if (string.IsNullOrEmpty(description))
            return string.Empty;

        var fieldInfo = enumValue.GetType().GetField(description);
        if (fieldInfo == null) return description;

        var attrs = fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), true);
        if (attrs.Length > 0)
        {
            description = ((DescriptionAttribute)attrs[0]).Description;
        }

        return description;
    }
}
