using System.Collections.Generic;
using Idevs.Net.CoreLib.Generators;
using VerifyXunit;
using Xunit;

namespace Idevs.Net.CoreLib.Generators.Tests.Emission;

public class MsbuildFlagOffTests
{

    [Fact]
    public Task FlagOff_EmitsLegacyScanFallback()
    {
        var source = """
            using Idevs.ComponentModels;
            namespace Demo;
            public interface IFoo { }
            [Scoped] public class Foo : IFoo { }
        """;

        var optionsProvider = new TestOptionsProvider(
            new Dictionary<string, string> { ["build_property.IdevsCoreLibUseSourceGenerator"] = "false" });

        return TestHelpers.VerifyWithOptions<IdevsServiceRegistrationGenerator>(source, optionsProvider);
    }
}
