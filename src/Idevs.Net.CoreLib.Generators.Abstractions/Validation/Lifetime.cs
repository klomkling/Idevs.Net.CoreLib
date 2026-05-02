namespace Idevs.Generators.Abstractions.Validation;

/// <summary>
/// Represents the DI lifetime for a registered service.
/// </summary>
public enum Lifetime
{
    Scoped = 0,
    Singleton = 1,
    Transient = 2,
}
