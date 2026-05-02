using Idevs.Generators.Abstractions.Validation;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Abstractions.Tests.Validation;

public class LifetimeResolverTests
{
    [Fact]
    public void Resolve_AttributeOnly_ReturnsAttributeLifetime()
    {
        var (lifetime, conflict) = LifetimeResolver.Resolve(attributeLifetime: Lifetime.Scoped, markerLifetime: null);
        Assert.Equal(Lifetime.Scoped, lifetime);
        Assert.Null(conflict);
    }

    [Fact]
    public void Resolve_MarkerOnly_ReturnsMarkerLifetime()
    {
        var (lifetime, conflict) = LifetimeResolver.Resolve(attributeLifetime: null, markerLifetime: Lifetime.Singleton);
        Assert.Equal(Lifetime.Singleton, lifetime);
        Assert.Null(conflict);
    }

    [Fact]
    public void Resolve_BothAgree_ReturnsLifetimeWithRedundancyWarning()
    {
        var (lifetime, conflict) = LifetimeResolver.Resolve(attributeLifetime: Lifetime.Scoped, markerLifetime: Lifetime.Scoped);
        Assert.Equal(Lifetime.Scoped, lifetime);
        Assert.Equal(LifetimeConflict.Redundant, conflict);
    }

    [Fact]
    public void Resolve_BothDisagree_ReturnsConflict()
    {
        var (_, conflict) = LifetimeResolver.Resolve(attributeLifetime: Lifetime.Scoped, markerLifetime: Lifetime.Singleton);
        Assert.Equal(LifetimeConflict.Disagreement, conflict);
    }

    [Fact]
    public void Resolve_Neither_ReturnsConflictNone()
    {
        var (_, conflict) = LifetimeResolver.Resolve(attributeLifetime: null, markerLifetime: null);
        Assert.Equal(LifetimeConflict.NoneSpecified, conflict);
    }
}
