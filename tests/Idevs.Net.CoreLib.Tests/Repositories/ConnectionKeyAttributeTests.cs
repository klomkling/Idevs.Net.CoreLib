using System.Reflection;
using Idevs.Repositories;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class ConnectionKeyAttributeTests
{
    [Fact]
    public void Constructor_StoresKey()
    {
        var attr = new ConnectionKeyAttribute("Warehouse");
        Assert.Equal("Warehouse", attr.Key);
    }

    [Fact]
    public void Constructor_NullKey_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ConnectionKeyAttribute(null!));
    }

    [Fact]
    public void Constructor_EmptyKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ConnectionKeyAttribute(""));
    }

    [Fact]
    public void Constructor_WhitespaceKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ConnectionKeyAttribute("   "));
    }

    [Fact]
    public void Attribute_AppliedToClass_IsDiscoverableViaReflection()
    {
        var attr = typeof(SampleAnnotatedClass).GetCustomAttribute<ConnectionKeyAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("Warehouse", attr!.Key);
    }

    [Fact]
    public void Attribute_OnDerivedClass_DiscoverableViaInheritedReflection()
    {
        var attr = typeof(SampleAnnotatedSubclass).GetCustomAttribute<ConnectionKeyAttribute>(inherit: true);
        Assert.NotNull(attr);
        Assert.Equal("Warehouse", attr!.Key);
    }

    [ConnectionKey("Warehouse")]
    private class SampleAnnotatedClass;

    private class SampleAnnotatedSubclass : SampleAnnotatedClass;
}
