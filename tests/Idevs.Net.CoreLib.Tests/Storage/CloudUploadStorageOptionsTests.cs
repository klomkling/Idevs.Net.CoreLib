using Idevs.Storage;

namespace Idevs.Net.CoreLib.Tests.Storage;

public sealed class CloudUploadStorageOptionsTests
{
    [Fact]
    public void Defaults_MatchExpectedConfigurationContract()
    {
        var options = new CloudUploadStorageOptions();

        Assert.Equal("CloudUploadStorage", CloudUploadStorageOptions.SectionName);
        Assert.Equal("Local", CloudUploadStorageOptions.ProviderLocal);
        Assert.Equal("AWS", CloudUploadStorageOptions.ProviderAws);
        Assert.Equal("CloudflareR2", CloudUploadStorageOptions.ProviderCloudflareR2);
        Assert.Equal(CloudUploadStorageOptions.ProviderLocal, CloudUploadStorageOptions.DefaultProvider);
        Assert.Equal(CloudUploadStorageOptions.DefaultProvider, options.Provider);
        Assert.Equal("us-east-1", options.Region);
        Assert.Equal(24, options.TemporaryRetentionHours);
        Assert.False(options.ForcePathStyle);
        Assert.Null(options.BucketName);
        Assert.Null(options.KeyPrefix);
        Assert.Null(options.CloudflareAccountId);
        Assert.Null(options.AwsProfileName);
        Assert.Null(options.AccessKey);
        Assert.Null(options.SecretKey);
        Assert.Null(options.SessionToken);
        Assert.Null(options.ServiceUrl);
    }
}
