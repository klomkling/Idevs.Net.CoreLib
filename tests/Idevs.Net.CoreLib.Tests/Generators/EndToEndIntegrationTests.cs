using Idevs.ComponentModels;
using Idevs.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Net.CoreLib.Tests.Generators;

public class EndToEndIntegrationTests
{
    // Path 1: attribute-based discovery
    public interface IExampleViaAttribute;

    [Scoped]
    public class ExampleViaAttribute : IExampleViaAttribute;

    // Path 2: marker interface discovery
    public interface IExampleViaMarker;

    public class ExampleViaMarker : IExampleViaMarker, IScopedService;

    // Path 3: registrar discovery
    public interface IExtraRegistrarService;

    public class ExtraRegistrarServiceImpl : IExtraRegistrarService;

    public class ExampleRegistrar : IIdevsServiceRegistrar
    {
        public void Register(IServiceCollection services)
        {
            services.AddScoped<IExtraRegistrarService, ExtraRegistrarServiceImpl>();
        }
    }

    [Fact]
    public void GeneratedAddIdevsServices_RegistersAllPaths()
    {
        var services = new ServiceCollection();

        Generated.IdevsServiceRegistrations.AddIdevsServices(services);

        Assert.Contains(services, d => d.ServiceType == typeof(IExampleViaAttribute));
        Assert.Contains(services, d => d.ServiceType == typeof(IExampleViaMarker));
        Assert.Contains(services, d => d.ServiceType == typeof(IExtraRegistrarService));
    }
}
