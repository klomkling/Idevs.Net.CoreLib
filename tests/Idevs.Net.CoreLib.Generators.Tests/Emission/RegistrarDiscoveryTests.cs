using Idevs.Net.CoreLib.Generators;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class RegistrarDiscoveryTests
{

    [Fact]
    public Task SingleRegistrar_EmitsConstructAndRegisterCall()
    {
        var source = """
            using Idevs.Repositories;
            using Microsoft.Extensions.DependencyInjection;
            namespace Demo;
            public class MyRegistrar : IIdevsServiceRegistrar
            {
                public void Register(IServiceCollection services)
                {
                    services.AddSingleton<object>();
                }
            }
        """;
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }
}
