namespace Idevs.Storage;

public sealed class CloudUploadStorageOptions
{
    public const string SectionName = "CloudUploadStorage";
    public const string ProviderLocal = "Local";
    public const string ProviderAws = "AWS";
    public const string ProviderCloudflareR2 = "CloudflareR2";
    public const string DefaultProvider = ProviderLocal;

    public string Provider { get; set; } = DefaultProvider;
    public string? BucketName { get; set; }
    public string Region { get; set; } = "us-east-1";
    public string? KeyPrefix { get; set; }
    public string? CloudflareAccountId { get; set; }
    public string? AwsProfileName { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? SessionToken { get; set; }
    public string? ServiceUrl { get; set; }
    public bool ForcePathStyle { get; set; }
    public int TemporaryRetentionHours { get; set; } = 24;
}
