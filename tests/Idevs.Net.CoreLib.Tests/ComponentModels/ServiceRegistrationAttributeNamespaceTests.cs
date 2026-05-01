using Idevs.ComponentModels;

namespace Idevs.Net.CoreLib.Tests.ComponentModels;

public class ServiceRegistrationAttributeNamespaceTests
{
    [Fact]
    public void StandardRegistrationAttributes_AreAvailableFromComponentModelsNamespace()
    {
        Assert.Equal("Idevs.ComponentModels", typeof(ScopedAttribute).Namespace);
        Assert.Equal("Idevs.ComponentModels", typeof(SingletonAttribute).Namespace);
        Assert.Equal("Idevs.ComponentModels", typeof(TransientAttribute).Namespace);
    }
}
