using System.Reflection;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class ObsoleteSyncWrapperTests
{
    [Theory]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow>), "First")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow>), "List")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow>), "GetBy")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow>), "Create")]
    public void SyncWrapper_HasObsoleteAttribute(Type baseType, string methodName)
    {
        var method = baseType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Theory]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow, int>), "GetById")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow, int>), "GetByIds")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow, int>), "Update")]
    [InlineData(typeof(Idevs.Repositories.RowRepositoryBase<TestSampleRow, int>), "DeleteById")]
    public void TKeySyncWrapper_HasObsoleteAttribute(Type baseType, string methodName)
    {
        var method = baseType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName && !m.IsGenericMethod);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ObsoleteAttribute>());
    }
}
