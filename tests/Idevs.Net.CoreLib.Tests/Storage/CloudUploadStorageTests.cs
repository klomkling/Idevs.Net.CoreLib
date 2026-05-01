using Amazon.S3;
using Amazon.S3.Model;
using Idevs.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using Serenity.Web;

namespace Idevs.Net.CoreLib.Tests.Storage;

public sealed class CloudUploadStorageTests
{
    [Fact]
    public void Constructor_CombinesS3BucketPrefixAndConfiguredKeyPrefix()
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "s3://documents/root",
            KeyPrefix = "/tenant-a/uploads/"
        });

        Assert.Equal("documents", storage.BucketNameForTesting);
        Assert.Equal("root/tenant-a/uploads", storage.KeyPrefixForTesting);
        Assert.Equal("root/tenant-a/uploads/invoices/2026/file.pdf", storage.GetStorageKeyForTesting("invoices/2026/file.pdf"));
    }

    [Fact]
    public void Constructor_CombinesBucketPathAndConfiguredKeyPrefix()
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "documents/root",
            KeyPrefix = "tenant-a"
        });

        Assert.Equal("documents", storage.BucketNameForTesting);
        Assert.Equal("root/tenant-a", storage.KeyPrefixForTesting);
    }

    [Fact]
    public void Constructor_RejectsMissingBucketForCloudProvider()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws
        }));

        Assert.Equal("CloudUploadStorage:BucketName is required when Provider is AWS or CloudflareR2.", exception.Message);
    }

    [Fact]
    public void Constructor_RejectsUnsupportedProvider()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateStorage(new CloudUploadStorageOptions
        {
            Provider = "AWZ",
            BucketName = "docs-bucket"
        }));

        Assert.Equal("CloudUploadStorage:Provider must be AWS, CloudflareR2, or Local.", exception.Message);
    }

    [Fact]
    public void GetFileUrl_UsesVirtualHostUrlAndEncodesSegments()
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket",
            Region = "ap-southeast-1",
            KeyPrefix = "tenant-a"
        });

        var url = storage.GetFileUrl("folder name/report 1.pdf");

        Assert.Equal("https://docs-bucket.s3.ap-southeast-1.amazonaws.com/tenant-a/folder%20name/report%201.pdf", url);
    }

    [Fact]
    public void GetFileUrl_UsesConfiguredServiceUrlForCloudflareR2()
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderCloudflareR2,
            BucketName = "docs-bucket",
            CloudflareAccountId = "abc123",
            KeyPrefix = "tenant-a"
        });

        var url = storage.GetFileUrl("folder/report.pdf");

        Assert.Equal("https://abc123.r2.cloudflarestorage.com/docs-bucket/tenant-a/folder/report.pdf", url);
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("./secret.txt")]
    [InlineData("folder\\..\\secret.txt")]
    [InlineData("folder/./secret.txt")]
    public void GetFileUrl_RejectsRelativePathSegments(string path)
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket",
            KeyPrefix = "tenant-a"
        });

        var exception = Assert.Throws<ArgumentException>(() => storage.GetFileUrl(path));

        Assert.Equal("path", exception.ParamName);
        Assert.StartsWith("CloudUploadStorage paths cannot contain relative path segments.", exception.Message);
    }

    [Fact]
    public void GetFileUrl_DoesNotTreatEncodedDotsAsRelativeSegments()
    {
        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket",
            KeyPrefix = "tenant-a"
        });

        var url = storage.GetFileUrl("%2e%2e/report.pdf");

        Assert.Equal("https://docs-bucket.s3.amazonaws.com/tenant-a/%252e%252e/report.pdf", url);
    }

    [Fact]
    public void WriteFile_PutsObjectWithBucketKeyAndContentType()
    {
        PutObjectRequest? capturedRequest = null;
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client
            .PutObjectAsync(Arg.Do<PutObjectRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket",
            KeyPrefix = "tenant-a"
        }, s3Client);

        using var stream = new MemoryStream("hello"u8.ToArray());

        var result = storage.WriteFile("reports/file.txt", stream, OverwriteOption.Overwrite);

        Assert.Equal("reports/file.txt", result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("docs-bucket", capturedRequest.BucketName);
        Assert.Equal("tenant-a/reports/file.txt", capturedRequest.Key);
        Assert.Equal("text/plain", capturedRequest.ContentType);
    }

    [Fact]
    public void FileExists_ReturnsFalseWhenObjectDoesNotExist()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client
            .GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GetObjectMetadataResponse>>(_ => throw new AmazonS3Exception("missing")
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket"
        }, s3Client);

        Assert.False(storage.FileExists("missing.txt"));
    }

    [Fact]
    public void GetFileSize_ReturnsContentLength()
    {
        var response = new GetObjectMetadataResponse();
        response.Headers.ContentLength = 123;

        var s3Client = Substitute.For<IAmazonS3>();
        s3Client
            .GetObjectMetadataAsync(Arg.Any<GetObjectMetadataRequest>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket"
        }, s3Client);

        Assert.Equal(123, storage.GetFileSize("file.txt"));
    }

    [Fact]
    public void CopyFrom_ReadsFromSourceStorageAndWritesTarget()
    {
        PutObjectRequest? capturedRequest = null;
        var s3Client = Substitute.For<IAmazonS3>();
        s3Client
            .PutObjectAsync(Arg.Do<PutObjectRequest>(request => capturedRequest = request), Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        var source = Substitute.For<IUploadStorage>();
        source.OpenFile("source.txt").Returns(new MemoryStream("copy"u8.ToArray()));
        source.GetFileMetadata("source.txt").Returns(new Dictionary<string, string>());

        var storage = CreateStorage(new CloudUploadStorageOptions
        {
            Provider = CloudUploadStorageOptions.ProviderAws,
            BucketName = "docs-bucket"
        }, s3Client);

        var result = storage.CopyFrom(source, "source.txt", "target.txt", OverwriteOption.Overwrite);

        Assert.Equal("target.txt", result);
        Assert.NotNull(capturedRequest);
        Assert.Equal("target.txt", capturedRequest.Key);
    }

    private static CloudUploadStorage CreateStorage(CloudUploadStorageOptions options)
    {
        return CreateStorage(options, Substitute.For<IAmazonS3>());
    }

    private static CloudUploadStorage CreateStorage(CloudUploadStorageOptions options, IAmazonS3 s3Client)
    {
        var uploadSettings = new UploadSettings();

        return new CloudUploadStorage(
            Options.Create(options),
            uploadSettings,
            s3Client,
            NullLogger<CloudUploadStorage>.Instance);
    }
}
