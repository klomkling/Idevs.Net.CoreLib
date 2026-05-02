// These tests intentionally exercise the obsolete AddIdevsCorelibServices()
// + the legacy [ScopedRegistration] attribute paths to prove they still work
// through 0.7.x. CS0618 (obsolete) and IDEVSGEN010 (legacy attribute) are
// expected and suppressed in the test csproj's <NoWarn>.
using Idevs.ComponentModel;
using Idevs.ComponentModels;
using Idevs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Idevs.Net.CoreLib.Tests.Extensions;

public class ServiceExtensionsTests
{
    public interface IStandardScopedSample;
    public interface IStandardSingletonSample;
    public interface IStandardTransientSample;
    public interface ILegacyScopedSample;

    [Scoped]
    public class StandardScopedSample : IStandardScopedSample;

    [Singleton]
    public class StandardSingletonSample : IStandardSingletonSample;

    [Transient]
    public class StandardTransientSample : IStandardTransientSample;

    [Scoped(ServiceType = typeof(IExplicitContract))]
    public class StandardScopedWithExplicitServiceType : IExplicitContract;

    public interface IExplicitContract;

    [Singleton(AllowSelfRegistration = true)]
    public class StandardSelfRegistered;

#pragma warning disable CS0618 // Test legacy path on purpose
    [ScopedRegistration]
    public class LegacyScopedSample : ILegacyScopedSample;

    [ScopedRegistration]
    public class LegacyScopedWithoutInterface;
#pragma warning restore CS0618

    [Fact]
    public void AddIdevsCorelibServices_RegistersStandardAttributedTypes_WithExpectedLifetimes()
    {
        var services = new ServiceCollection().AddIdevsCorelibServices();

        var scoped = services.SingleOrDefault(d => d.ServiceType == typeof(IStandardScopedSample));
        var singleton = services.SingleOrDefault(d => d.ServiceType == typeof(IStandardSingletonSample));
        var transient = services.SingleOrDefault(d => d.ServiceType == typeof(IStandardTransientSample));

        Assert.NotNull(scoped);
        Assert.Equal(ServiceLifetime.Scoped, scoped!.Lifetime);
        Assert.Equal(typeof(StandardScopedSample), scoped.ImplementationType);

        Assert.NotNull(singleton);
        Assert.Equal(ServiceLifetime.Singleton, singleton!.Lifetime);
        Assert.Equal(typeof(StandardSingletonSample), singleton.ImplementationType);

        Assert.NotNull(transient);
        Assert.Equal(ServiceLifetime.Transient, transient!.Lifetime);
        Assert.Equal(typeof(StandardTransientSample), transient.ImplementationType);
    }

    [Fact]
    public void AddIdevsCorelibServices_HonorsExplicitServiceTypeOnAttribute()
    {
        var services = new ServiceCollection().AddIdevsCorelibServices();

        var descriptor = services.SingleOrDefault(d =>
            d.ServiceType == typeof(IExplicitContract) &&
            d.ImplementationType == typeof(StandardScopedWithExplicitServiceType));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
    }

    [Fact]
    public void AddIdevsCorelibServices_RegistersSelfWhenAllowSelfRegistrationIsTrue()
    {
        var services = new ServiceCollection().AddIdevsCorelibServices();

        var descriptor = services.SingleOrDefault(d =>
            d.ServiceType == typeof(StandardSelfRegistered) &&
            d.ImplementationType == typeof(StandardSelfRegistered));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor!.Lifetime);
    }

    [Fact]
    public void AddIdevsCorelibServices_RegistersLegacyAttributedTypes_WhenInterfaceFollowsConvention()
    {
        var services = new ServiceCollection().AddIdevsCorelibServices();

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILegacyScopedSample));

        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Scoped, descriptor!.Lifetime);
        Assert.Equal(typeof(LegacyScopedSample), descriptor.ImplementationType);
    }

    [Fact]
    public void AddIdevsCorelibServices_SkipsLegacyAttributedTypeWithoutConventionalInterface()
    {
        var services = new ServiceCollection().AddIdevsCorelibServices();

        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(LegacyScopedWithoutInterface));
    }

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

    [Fact]
    public void AddIdevsCorelibLegacyScan_RegistersAttributedServices()
    {
        var services = new ServiceCollection().AddIdevsCorelibLegacyScan();

        Assert.Contains(services, d => d.ServiceType == typeof(IStandardScopedSample));
    }
}
