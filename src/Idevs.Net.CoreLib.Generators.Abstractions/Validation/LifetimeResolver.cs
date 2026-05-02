namespace Idevs.Generators.Abstractions.Validation;

/// <summary>Outcome of cross-checking attribute-derived and marker-derived lifetimes.</summary>
public enum LifetimeConflict
{
    /// <summary>No conflict; resolution succeeded.</summary>
    None,
    /// <summary>Attribute and marker specify the same lifetime — emit IDEVSGEN004 warning.</summary>
    Redundant,
    /// <summary>Attribute and marker specify different lifetimes — emit IDEVSGEN003 error.</summary>
    Disagreement,
    /// <summary>Neither attribute nor marker specified a lifetime; cannot register.</summary>
    NoneSpecified
}

/// <summary>
/// Cross-checks the lifetime declared by registration attributes against the
/// lifetime implied by marker interfaces, applying the precedence:
/// Attribute &gt; generic marker &gt; non-generic marker.
/// </summary>
public static class LifetimeResolver
{
    public static (Lifetime? Lifetime, LifetimeConflict? Conflict) Resolve(
        Lifetime? attributeLifetime,
        Lifetime? markerLifetime)
    {
        if (attributeLifetime.HasValue && markerLifetime.HasValue)
        {
            return attributeLifetime == markerLifetime
                ? (attributeLifetime, LifetimeConflict.Redundant)
                : (attributeLifetime, LifetimeConflict.Disagreement);
        }

        if (attributeLifetime.HasValue) return (attributeLifetime, null);
        if (markerLifetime.HasValue) return (markerLifetime, null);
        return (null, LifetimeConflict.NoneSpecified);
    }
}
