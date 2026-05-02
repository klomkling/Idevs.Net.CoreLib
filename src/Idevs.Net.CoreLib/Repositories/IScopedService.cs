namespace Idevs.Repositories;

/// <summary>
/// Marker interface — implementing this interface registers the class as
/// <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/>
/// when the Idevs source generator runs.
/// </summary>
public interface IScopedService;

/// <summary>
/// Generic marker — pins the registration's service type to <typeparamref name="TService"/>.
/// </summary>
public interface IScopedService<TService> : IScopedService;
