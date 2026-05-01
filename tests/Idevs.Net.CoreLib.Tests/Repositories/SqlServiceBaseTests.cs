using Idevs.Repositories;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class SqlServiceBaseTests
{
    private static ISqlConnections Conns() => Substitute.For<ISqlConnections>();

    private sealed class DefaultSubject(ISqlConnections c) : SqlServiceBase(c);

    [Idevs.Repositories.ConnectionKey("Warehouse")]
    private sealed class AttributedSubject(ISqlConnections c) : SqlServiceBase(c);

    private sealed class OverrideSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        protected override string ConnectionKey => "Reports";
    }

    [Idevs.Repositories.ConnectionKey("Warehouse")]
    private sealed class AttributedAndOverrideSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        protected override string ConnectionKey => "Reports";
    }

    [Fact]
    public void Ctor_NullConnections_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultSubject(null!));
    }

    [Fact]
    public void ConnectionKey_DefaultsToDefault_WhenNoAttributeOrOverride()
    {
        var subject = new DefaultSubject(Conns());
        Assert.Equal("Default", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_ReadsFromAttribute_WhenPresent()
    {
        var subject = new AttributedSubject(Conns());
        Assert.Equal("Warehouse", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_VirtualOverride_WinsOverAttribute()
    {
        var subject = new AttributedAndOverrideSubject(Conns());
        Assert.Equal("Reports", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_VirtualOverride_WinsWithoutAttribute()
    {
        var subject = new OverrideSubject(Conns());
        Assert.Equal("Reports", InvokeProtectedConnectionKey(subject));
    }

    private static string InvokeProtectedConnectionKey(SqlServiceBase subject)
    {
        var prop = typeof(SqlServiceBase).GetProperty(
            "ConnectionKey",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(prop);
        var value = prop.GetValue(subject);
        Assert.NotNull(value);
        return (string)value;
    }
}
