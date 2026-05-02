using Idevs.Net.CoreLib.Generators;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class MarkerDiscoveryTests
{
    static MarkerDiscoveryTests() => VerifySourceGenerators.Initialize();

    [Fact]
    public Task NonGenericScopedMarker()
    {
        var source = """
            using Idevs.Repositories;
            namespace Demo;
            public interface ICustomerRepository { }
            public class CustomerRepository : ICustomerRepository, IScopedService { }
        """;
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }

    [Fact]
    public Task GenericScopedMarker_PinsServiceType()
    {
        var source = """
            using Idevs.Repositories;
            namespace Demo;
            public interface IFoo { }
            public interface IBar { }
            public class CustomerRepository : IFoo, IBar, IScopedService<IFoo> { }
        """;
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }
}
