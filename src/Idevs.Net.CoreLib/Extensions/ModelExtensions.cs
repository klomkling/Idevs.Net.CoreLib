namespace Idevs.Extensions;

public static class ModelExtensions
{
    public static T TrimModel<T>(this T model, bool emptyStringToNull = false)
    {
        if (model is null)
            return model;

        foreach (var property in model.GetType().GetProperties().Where(x => x.PropertyType == typeof(string)))
        {
            if (property.Name == "Table")
                continue;

            var value = (property.GetValue(model) as string)?.Trim();
            property.SetValue(model, string.IsNullOrEmpty(value) && emptyStringToNull ? null : value);
        }

        return model;
    }

    public static IEnumerable<T> TrimModel<T>(IEnumerable<T> collection, bool emptyStringToNull = false)
    {
        return collection.Select(model => model.TrimModel(emptyStringToNull));
    }
}