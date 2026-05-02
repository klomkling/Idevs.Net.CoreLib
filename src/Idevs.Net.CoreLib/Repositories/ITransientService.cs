namespace Idevs.Repositories;

/// <summary>Marker for transient-lifetime registration.</summary>
public interface ITransientService;

/// <summary>Generic marker pinning the service type for transient registration.</summary>
public interface ITransientService<TService> : ITransientService;
