using Idevs.Net.CoreLib.Generators;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class DeterministicOrderingTests
{
    static DeterministicOrderingTests() => VerifySourceGenerators.Initialize();

    [Fact]
    public Task RegistrationsAreSortedAlphabetically()
    {
        var source = """
            using Idevs.ComponentModels;
            namespace Demo;
            public interface IZetaRepository { }
            [Scoped]
            public class ZetaRepository : IZetaRepository { }
            public interface IAlphaRepository { }
            [Scoped]
            public class AlphaRepository : IAlphaRepository { }
            public interface IMidRepository { }
            [Scoped]
            public class MidRepository : IMidRepository { }
        """;
        return TestHelpers.Verify<IdevsServiceRegistrationGenerator>(source);
    }
}
