namespace Idevs.Repositories;

/// <summary>
/// Marks a class derived from <see cref="SqlServiceBase"/> with the connection key
/// to use when opening connections. The virtual <see cref="SqlServiceBase.ConnectionKey"/>
/// property override on a derived class wins over this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class ConnectionKeyAttribute : Attribute
{
    public ConnectionKeyAttribute(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
    }

    public string Key { get; }
}
