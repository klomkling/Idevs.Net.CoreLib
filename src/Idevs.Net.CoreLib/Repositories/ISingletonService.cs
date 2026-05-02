namespace Idevs.Repositories;

/// <summary>Marker for singleton-lifetime registration.</summary>
public interface ISingletonService { }

/// <summary>Generic marker pinning the service type for singleton registration.</summary>
public interface ISingletonService<TService> : ISingletonService { }
