using System.Collections.Concurrent;
using System.Reflection;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Per-row-type lookup for the <see cref="RowVersionAttribute"/> field,
/// cached forever after first resolution. Internal — consumers go
/// through <see cref="RepositoryBase{TRow,TKey}"/> overloads.
/// </summary>
/// <remarks>
/// Reflection runs at most once per <c>TRow</c>. After that every
/// guarded UPDATE pays a single <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// lookup — no per-call attribute scan. Validation throws on misuse
/// (multiple attributes, wrong property type, missing field, missing
/// Updatable flag) at the first lookup, so configuration bugs surface
/// immediately rather than silently disabling the guard.
/// </remarks>
internal static class RowVersionMetadata
{
    private static readonly ConcurrentDictionary<Type, Field?> _cache = new();

    /// <summary>
    /// Return the <see cref="Field"/> on <paramref name="row"/>'s type
    /// flagged with <see cref="RowVersionAttribute"/>, or <c>null</c>
    /// when the type is not opted in.
    /// </summary>
    public static Field? Find(IRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return _cache.GetOrAdd(row.GetType(), t => Resolve(t, row));
    }

    private static Field? Resolve(Type rowType, IRow rowInstance)
    {
        var props = rowType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<RowVersionAttribute>(inherit: true) is not null)
            .ToArray();

        if (props.Length == 0)
            return null;

        if (props.Length > 1)
        {
            var names = string.Join(", ", props.Select(p => p.Name));
            throw new InvalidOperationException(
                $"Row type '{rowType.FullName}' has {props.Length} [RowVersion] properties ({names}); " +
                "expected at most one. Optimistic concurrency requires a single version column per row.");
        }

        var prop = props[0];

        if (prop.PropertyType != typeof(long?))
            throw new InvalidOperationException(
                $"[RowVersion] property '{rowType.FullName}.{prop.Name}' must be of type 'long?'; " +
                $"got '{prop.PropertyType}'. Use 'public long? {prop.Name} {{ get; set; }}' " +
                "with a matching Int64Field in the row's RowFields class.");

        var field = rowInstance.GetFields().FirstOrDefault(f =>
            string.Equals(f.PropertyName, prop.Name, StringComparison.Ordinal));

        if (field is null)
            throw new InvalidOperationException(
                $"[RowVersion] property '{rowType.FullName}.{prop.Name}' has no matching " +
                $"Field in RowFields. Add 'public Int64Field {prop.Name};' to the RowFields class.");

        if ((field.Flags & FieldFlags.Updatable) != FieldFlags.Updatable)
            throw new InvalidOperationException(
                $"[RowVersion] field '{rowType.FullName}.{prop.Name}' is not Updatable. " +
                "The library increments this field on every guarded UPDATE; clearing the " +
                "Updatable flag would prevent that. Remove the [SetFieldFlags] that clears it.");

        return field;
    }
}
