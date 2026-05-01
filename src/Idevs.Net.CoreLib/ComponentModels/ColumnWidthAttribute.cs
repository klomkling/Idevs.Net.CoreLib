using Serenity.ComponentModel;

namespace Idevs.ComponentModel;

public class ColumnWidthAttribute : FormWidthAttribute
{
    public ColumnWidthAttribute()
    {
    }

    public ColumnWidthAttribute(string cssClass) : base(cssClass)
    {
    }

    /// <summary>
    /// Gets / sets cols (1..12) in large devices (width >= 1400px, large desktops)
    /// </summary>
    public int ExtraExtraLarge
    {
        get => Get("col-xxl-") ?? 0;
        set => Set("col-xxl-", value);
    }

    /// <summary>
    /// Gets / sets cols (1..12) in large devices (width >= 1200px, large desktops)
    /// </summary>
    public int ExtraLarge
    {
        get => Get("col-xl-") ?? 0;
        set => Set("col-xl-", value);
    }

    private void Set(string prefix, int cols)
    {
        var parts = (Value ?? "").Split(' ');
        var index = Array.FindIndex(parts, x =>
            x.Length > prefix.Length &&
            x.StartsWith(prefix) &&
            x[prefix.Length] >= '0' &&
            x[prefix.Length] <= '9');

        if (index < 0)
        {
            if (cols <= 0)
                return;

            if (!string.IsNullOrEmpty(Value))
                Value += " ";
            Value += prefix + cols;
        }
        else
        {
            if (cols <= 0)
                Value = string.Join(" ", parts.Take(index).Concat(parts.Skip(index + 1)));
            else
            {
                parts[index] = prefix + cols;
                Value = string.Join(" ", parts);
            }
        }
    }

    private int? Get(string prefix)
    {
        var klass = (Value ?? "").Split(' ')
            .FirstOrDefault(x =>
                x.Length > prefix.Length &&
                x.StartsWith(prefix) &&
                x[prefix.Length] >= '0' &&
                x[prefix.Length] <= '9');

        if (klass == null)
            return null;

        if (!int.TryParse(klass[prefix.Length..], out var cols))
            return null;

        return cols;
    }
}
