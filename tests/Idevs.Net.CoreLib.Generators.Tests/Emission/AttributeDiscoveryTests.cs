using Idevs.Net.CoreLib.Generators;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class AttributeDiscoveryTests
{

    [Fact]
    public Task SingleScopedAttributeOnConventionalType()
    {
        var source = """
            using Idevs.ComponentModels;
            namespace Demo;
            public interface ICustomerRepository { }
            [Scoped]
            public class CustomerRepository : ICustomerRepository { }
        """;
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }
}
