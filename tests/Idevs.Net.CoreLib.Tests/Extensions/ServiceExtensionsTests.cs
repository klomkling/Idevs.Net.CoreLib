using Idevs.ComponentModels;
using Idevs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Net.CoreLib.Tests.Extensions;

public class ServiceExtensionsTests
{
    [Fact]
    public void StandardAttributes_ImplementIServiceRegistrationAttribute()
    {
        Assert.True(typeof(IServiceRegistrationAttribute).IsAssignableFrom(typeof(ScopedAttribute)));
        Assert.True(typeof(IServiceRegistrationAttribute).IsAssignableFrom(typeof(SingletonAttribute)));
        Assert.True(typeof(IServiceRegistrationAttribute).IsAssignableFrom(typeof(TransientAttribute)));
    }

    [Fact]
    public void StandardAttributes_ExposeExpectedLifetimeViaInterface()
    {
        Assert.Equal(ServiceLifetime.Scoped, ((IServiceRegistrationAttribute)new ScopedAttribute()).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, ((IServiceRegistrationAttribute)new SingletonAttribute()).Lifetime);
        Assert.Equal(ServiceLifetime.Transient, ((IServiceRegistrationAttribute)new TransientAttribute()).Lifetime);
    }

    [Fact]
    public void AddIdevsCorelibCore_RegistersHandCodedServices()
    {
        var services = new ServiceCollection().AddIdevsCorelibCore();

        Assert.Contains(services, d => d.ServiceType == typeof(IViewPageRenderer));
        Assert.Contains(services, d => d.ServiceType == typeof(IIdevsExcelExporter));
        Assert.Contains(services, d => d.ServiceType == typeof(IIdevsPdfExporter));
    }
}
