using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Repositories;

/// <summary>
/// Runtime extension hook for arbitrary DI registration logic.
/// Implementations are discovered and instantiated by the Idevs source
/// generator; their <see cref="Register"/> method is called from the generated
/// <c>AddIdevsServices</c> method during application startup.
/// </summary>
/// <remarks>
/// Implementing types must be concrete and have a public parameterless
/// constructor. Use this for keyed services, decorators, conditional
/// registration, multi-impl registration, or any logic that doesn't fit a
/// single <c>services.Add{Lifetime}&lt;TService, TImpl&gt;()</c> shape.
/// </remarks>
public interface IIdevsServiceRegistrar
{
    /// <summary>Registers services into the supplied collection.</summary>
    void Register(IServiceCollection services);
}
