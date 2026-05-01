using Idevs.Extensions;
using Idevs.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serenity.Web;

namespace Idevs.Net.CoreLib.Tests.Extensions;

public sealed class CloudUploadStorageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCloudUploadStorage_WithLocalProvider_DoesNotRegisterUploadStorage()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("CloudUploadStorage:Provider", "Local"));

        services.AddCloudUploadStorage(configuration);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IUploadStorage));
    }

    [Fact]
    public void AddCloudUploadStorage_WithAwsProvider_RegistersCloudUploadStorage()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            ("CloudUploadStorage:Provider", "AWS"),
            ("CloudUploadStorage:BucketName", "docs-bucket"));

        services.AddCloudUploadStorage(configuration);

        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUploadStorage));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CloudUploadStorage>(provider.GetRequiredService<IUploadStorage>());
    }

    [Fact]
    public void AddCloudUploadStorage_WithCloudflareProvider_RegistersCloudUploadStorage()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            ("CloudUploadStorage:Provider", "CloudflareR2"),
            ("CloudUploadStorage:BucketName", "docs-bucket"),
            ("CloudUploadStorage:CloudflareAccountId", "abc123"));

        services.AddCloudUploadStorage(configuration);

        var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUploadStorage));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<CloudUploadStorage>(provider.GetRequiredService<IUploadStorage>());
    }

    [Fact]
    public void AddCloudUploadStorage_WithConfigureAction_BindsOptions()
    {
        var services = new ServiceCollection();

        services.AddCloudUploadStorage(options =>
        {
            options.Provider = CloudUploadStorageOptions.ProviderAws;
            options.BucketName = "docs-bucket";
            options.KeyPrefix = "tenant-a";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CloudUploadStorageOptions>>().Value;

        Assert.Equal("AWS", options.Provider);
        Assert.Equal("docs-bucket", options.BucketName);
        Assert.Equal("tenant-a", options.KeyPrefix);
        Assert.IsType<CloudUploadStorage>(provider.GetRequiredService<IUploadStorage>());
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();
    }
}
