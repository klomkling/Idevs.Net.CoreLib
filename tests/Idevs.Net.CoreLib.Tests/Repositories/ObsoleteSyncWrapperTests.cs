using System.Reflection;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class ObsoleteSyncWrapperTests
{
    [Theory]
    [InlineData(typeof(Idevs.Repositories.RepositoryBase<TestSampleRow>), "First")]
    [InlineData(typeof(Idevs.Repositories.RepositoryBase<TestSampleRow>), "List")]
    [InlineData(typeof(Idevs.Repositories.RepositoryBase<TestSampleRow>), "GetBy")]
    [InlineData(typeof(Idevs.Repositories.RepositoryBase<TestSampleRow>), "Create")]
    public void SyncWrapper_HasObsoleteAttribute(Type baseType, string methodName)
    {
        var method = baseType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ObsoleteAttribute>());
    }
}
