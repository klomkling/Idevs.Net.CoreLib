using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Idevs.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;
using Serenity.Web;

namespace Idevs.Storage;

public sealed class CloudUploadStorage : IUploadStorage, IDisposable
{
    private const string TemporaryFolder = "temporary";
    private const string HistoryFolder = "history";

    private readonly IAmazonS3 s3Client;
    private readonly ILogger<CloudUploadStorage> logger;
    private readonly bool ownsClient;
    private readonly string provider;
    private readonly string bucketName;
    private readonly string keyPrefix;
    private readonly string rootUrl;
    private readonly TimeSpan temporaryRetention;

    public CloudUploadStorage(
        IOptions<CloudUploadStorageOptions> options,
        UploadSettings uploadSettings,
        ILogger<CloudUploadStorage>? logger = null)
        : this(options, uploadSettings, CreateAmazonS3Client, logger, ownsClient: true)
    {
    }

    internal CloudUploadStorage(
        IOptions<CloudUploadStorageOptions> options,
        UploadSettings uploadSettings,
        IAmazonS3 s3Client,
        ILogger<CloudUploadStorage>? logger = null)
        : this(
            options,
            uploadSettings,
            _ => s3Client ?? throw new ArgumentNullException(nameof(s3Client)),
            logger,
            ownsClient: false)
    {
    }

    private CloudUploadStorage(
        IOptions<CloudUploadStorageOptions> options,
        UploadSettings uploadSettings,
        Func<CloudUploadStorageOptions, IAmazonS3> s3ClientFactory,
        ILogger<CloudUploadStorage>? logger,
        bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(uploadSettings);
        ArgumentNullException.ThrowIfNull(s3ClientFactory);

        this.logger = logger ?? LogManager.TryGetLogger<CloudUploadStorage>();
        this.ownsClient = ownsClient;

        var value = options.Value;
        provider = string.IsNullOrWhiteSpace(value.Provider)
            ? CloudUploadStorageOptions.DefaultProvider
            : value.Provider.Trim();

        if (string.Equals(provider, CloudUploadStorageOptions.ProviderLocal, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CloudUploadStorage cannot be created when Provider is Local.");

        if (!string.Equals(provider, CloudUploadStorageOptions.ProviderAws, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(provider, CloudUploadStorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CloudUploadStorage:Provider must be {CloudUploadStorageOptions.ProviderAws}, {CloudUploadStorageOptions.ProviderCloudflareR2}, or {CloudUploadStorageOptions.ProviderLocal}.");
        }

        (bucketName, var bucketPrefix) = ParseBucketName(value.BucketName);
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new InvalidOperationException("CloudUploadStorage:BucketName is required when Provider is AWS or CloudflareR2.");

        keyPrefix = CombinePrefixes(bucketPrefix, value.KeyPrefix);
        rootUrl = BuildRootUrl(value, provider, bucketName);
        temporaryRetention = TimeSpan.FromHours(value.TemporaryRetentionHours > 0 ? value.TemporaryRetentionHours : 24);
        var client = s3ClientFactory(value);
        ArgumentNullException.ThrowIfNull(client);

        s3Client = client;
    }

    internal string BucketNameForTesting => bucketName;

    internal string KeyPrefixForTesting => keyPrefix;

    internal string GetStorageKeyForTesting(string path)
    {
        return ToStorageKey(path);
    }

    public string GetFileUrl(string path)
    {
        var storageKey = ToStorageKey(path);
        return rootUrl + "/" + EncodePath(storageKey);
    }

    public string ArchiveFile(string path)
    {
        var sourcePath = NormalizePath(path).TrimStart('/');
        return CopyFrom(this, sourcePath, $"{HistoryFolder}/{sourcePath}", OverwriteOption.AutoRename);
    }

    public string CopyFrom(IUploadStorage source, string sourcePath, string targetPath, OverwriteOption overwrite)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var sourceStream = source.OpenFile(sourcePath);
        var finalPath = WriteFile(targetPath, sourceStream, overwrite);

        var sourceMetadata = source.GetFileMetadata(sourcePath);
        if (sourceMetadata.Count > 0)
            SetFileMetadata(finalPath, sourceMetadata, overwriteAll: true);

        return finalPath;
    }

    public void DeleteFile(string path)
    {
        var normalizedPath = NormalizePath(path).TrimStart('/');
        Execute(() => s3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = ToStorageKey(normalizedPath)
        }, CancellationToken.None));
    }

    public bool FileExists(string path)
    {
        return TryGetObjectMetadata(ToStorageKey(path), suppressAuthorizationErrors: true) != null;
    }

    public long GetFileSize(string path)
    {
        var metadata = TryGetObjectMetadata(ToStorageKey(path));
        if (metadata is null)
            throw new FileNotFoundException("File not found", NormalizePath(path).TrimStart('/'));

        return metadata.Headers.ContentLength;
    }

    public string[] GetFiles(string path, string searchPattern)
    {
        var directory = NormalizeDirectory(path);
        var pattern = string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern;
        var objectPrefix = BuildPrefix(directory);
        var files = new List<string>();

        string? continuationToken = null;
        do
        {
            var response = Execute(() => s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = objectPrefix,
                ContinuationToken = continuationToken
            }, CancellationToken.None));

            foreach (var objectItem in response.S3Objects)
            {
                if (objectItem.Key.EndsWith('/'))
                    continue;

                var relativePath = ToRelativePath(objectItem.Key);
                if (!IsImmediateChild(directory, relativePath, out var fileName))
                    continue;

                if (IsMatch(fileName, pattern))
                    files.Add(relativePath);
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (!string.IsNullOrEmpty(continuationToken));

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files.ToArray();
    }

    public IDictionary<string, string> GetFileMetadata(string path)
    {
        var metadata = TryGetObjectMetadata(ToStorageKey(path));
        if (metadata is null)
            throw new FileNotFoundException("File not found", NormalizePath(path).TrimStart('/'));

        return metadata.Metadata.Keys.ToDictionary(key => key, key => metadata.Metadata[key], StringComparer.OrdinalIgnoreCase);
    }

    public void SetFileMetadata(string path, IDictionary<string, string> metadata, bool overwriteAll)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var normalizedPath = NormalizePath(path).TrimStart('/');
        var objectKey = ToStorageKey(normalizedPath);
        var current = TryGetObjectMetadata(objectKey);
        if (current is null)
            throw new FileNotFoundException("File not found", normalizedPath);

        var mergedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!overwriteAll)
        {
            foreach (var key in current.Metadata.Keys)
                mergedMetadata[key] = current.Metadata[key];
        }

        foreach (var item in metadata)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
                mergedMetadata[item.Key] = item.Value ?? string.Empty;
        }

        var request = new CopyObjectRequest
        {
            SourceBucket = bucketName,
            SourceKey = objectKey,
            DestinationBucket = bucketName,
            DestinationKey = objectKey,
            MetadataDirective = S3MetadataDirective.REPLACE,
            ContentType = string.IsNullOrWhiteSpace(current.Headers.ContentType)
                ? GetContentType(normalizedPath)
                : current.Headers.ContentType
        };

        foreach (var item in mergedMetadata)
            request.Metadata[item.Key] = item.Value ?? string.Empty;

        Execute(() => s3Client.CopyObjectAsync(request, CancellationToken.None));
    }

    public Stream OpenFile(string path)
    {
        var normalizedPath = NormalizePath(path).TrimStart('/');
        try
        {
            using var response = Execute(() => s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = ToStorageKey(normalizedPath)
            }, CancellationToken.None));

            var memory = new MemoryStream();
            response.ResponseStream.CopyTo(memory);
            memory.Position = 0;
            return memory;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            throw new FileNotFoundException("File not found", normalizedPath, ex);
        }
    }

    public void PurgeTemporaryFiles()
    {
        var cutoff = DateTime.UtcNow - temporaryRetention;
        var temporaryPrefix = BuildPrefix(TemporaryFolder);
        string? continuationToken = null;

        try
        {
            do
            {
                var response = Execute(() => s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = temporaryPrefix,
                    ContinuationToken = continuationToken
                }, CancellationToken.None));

                var candidates = new List<KeyVersion>();
                foreach (var objectItem in response.S3Objects)
                {
                    if (objectItem.LastModified.HasValue &&
                        objectItem.LastModified.Value.ToUniversalTime() <= cutoff)
                    {
                        candidates.Add(new KeyVersion { Key = objectItem.Key });
                    }
                }

                if (candidates.Count > 0)
                {
                    Execute(() => s3Client.DeleteObjectsAsync(new DeleteObjectsRequest
                    {
                        BucketName = bucketName,
                        Objects = candidates
                    }, CancellationToken.None));
                }

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            } while (!string.IsNullOrEmpty(continuationToken));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            logger.LogWarning(
                ex,
                "PurgeTemporaryFiles skipped because cloud storage list/delete permissions are unavailable for prefix {Prefix}.",
                temporaryPrefix);
        }
    }

    public string WriteFile(string path, Stream source, OverwriteOption overwrite)
    {
        ArgumentNullException.ThrowIfNull(source);

        var normalizedPath = NormalizePath(path).TrimStart('/');

        switch (overwrite)
        {
            case OverwriteOption.Disallowed:
                if (FileExists(normalizedPath))
                    throw new IOException($"A file already exists at '{normalizedPath}'.");
                break;

            case OverwriteOption.AutoRename:
                normalizedPath = UploadPathHelper.FindAvailableName(normalizedPath, FileExists);
                break;

            case OverwriteOption.Overwrite:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(overwrite), overwrite, null);
        }

        Execute(() => s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = ToStorageKey(normalizedPath),
            InputStream = source,
            AutoCloseStream = false,
            ContentType = GetContentType(normalizedPath)
        }, CancellationToken.None));

        return normalizedPath;
    }

    public void Dispose()
    {
        if (ownsClient)
            s3Client.Dispose();
    }

    private static IAmazonS3 CreateAmazonS3Client(CloudUploadStorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle
        };

        var isCloudflareR2 = string.Equals(
            options.Provider,
            CloudUploadStorageOptions.ProviderCloudflareR2,
            StringComparison.OrdinalIgnoreCase);

        if (isCloudflareR2)
        {
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
                config.ServiceURL = options.ServiceUrl.TrimEnd('/');
            else if (!string.IsNullOrWhiteSpace(options.CloudflareAccountId))
                config.ServiceURL = $"https://{options.CloudflareAccountId.Trim()}.r2.cloudflarestorage.com";

            config.ForcePathStyle = true;
            config.AuthenticationRegion = string.IsNullOrWhiteSpace(options.Region) ? "auto" : options.Region.Trim();
        }
        else
        {
            var region = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region.Trim();
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);

            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
                config.ServiceURL = options.ServiceUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(options.AwsProfileName))
        {
            var profileName = options.AwsProfileName.Trim();
            var profileChain = new CredentialProfileStoreChain();
            if (!profileChain.TryGetAWSCredentials(profileName, out var profileCredentials))
                throw new InvalidOperationException($"AWS profile '{profileName}' was not found.");

            return new AmazonS3Client(profileCredentials, config);
        }

        if (!string.IsNullOrWhiteSpace(options.AccessKey) && !string.IsNullOrWhiteSpace(options.SecretKey))
        {
            AWSCredentials credentials = string.IsNullOrWhiteSpace(options.SessionToken)
                ? new BasicAWSCredentials(options.AccessKey.Trim(), options.SecretKey.Trim())
                : new SessionAWSCredentials(options.AccessKey.Trim(), options.SecretKey.Trim(), options.SessionToken.Trim());

            return new AmazonS3Client(credentials, config);
        }

        return new AmazonS3Client(config);
    }

    private static (string BucketName, string KeyPrefix) ParseBucketName(string? bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            return (string.Empty, string.Empty);

        var value = bucketName.Trim();
        if (value.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            value = value[5..];

        value = value.Trim('/');
        var slashIndex = value.IndexOf('/');
        if (slashIndex < 0)
            return (value, string.Empty);

        return (value[..slashIndex], NormalizePrefix(value[(slashIndex + 1)..]));
    }

    private static string CombinePrefixes(string first, string? second)
    {
        var left = NormalizePrefix(first);
        var right = NormalizePrefix(second);

        if (left.Length == 0)
            return right;

        if (right.Length == 0)
            return left;

        return left + "/" + right;
    }

    private static string NormalizePrefix(string? path)
    {
        return NormalizePath(path).Trim('/');
    }

    private static string NormalizePath(string? path)
    {
        var normalizedPath = (path ?? string.Empty)
            .Replace('\\', '/')
            .Trim();

        foreach (var segment in normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
                throw new ArgumentException("CloudUploadStorage paths cannot contain relative path segments.", nameof(path));
        }

        return normalizedPath;
    }

    private static string NormalizeDirectory(string? path)
    {
        return NormalizePath(path).Trim('/');
    }

    private string ToStorageKey(string path)
    {
        var normalizedPath = NormalizePath(path).TrimStart('/');
        if (keyPrefix.Length == 0)
            return normalizedPath;

        if (normalizedPath.Length == 0)
            return keyPrefix;

        return keyPrefix + "/" + normalizedPath;
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    private string BuildPrefix(string directory)
    {
        if (string.IsNullOrEmpty(directory))
            return string.IsNullOrEmpty(keyPrefix) ? string.Empty : keyPrefix + "/";

        return ToStorageKey(directory).TrimEnd('/') + "/";
    }

    private string ToRelativePath(string objectKey)
    {
        if (string.IsNullOrEmpty(keyPrefix))
            return objectKey;

        var prefix = keyPrefix + "/";
        return objectKey.StartsWith(prefix, StringComparison.Ordinal) ? objectKey[prefix.Length..] : objectKey;
    }

    private static bool IsImmediateChild(string directory, string relativePath, out string fileName)
    {
        fileName = relativePath;

        if (string.IsNullOrEmpty(directory))
            return !relativePath.Contains('/');

        var expectedPrefix = directory + "/";
        if (!relativePath.StartsWith(expectedPrefix, StringComparison.Ordinal))
            return false;

        fileName = relativePath[expectedPrefix.Length..];
        return !fileName.Contains('/');
    }

    private static bool IsMatch(string fileName, string searchPattern)
    {
        if (searchPattern == "*")
            return true;

        var regex = "^" + Regex.Escape(searchPattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal) + "$";

        return Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private GetObjectMetadataResponse? TryGetObjectMetadata(string key, bool suppressAuthorizationErrors = false)
    {
        try
        {
            return Execute(() => s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = key
            }, CancellationToken.None));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            return null;
        }
        catch (AmazonS3Exception ex) when (suppressAuthorizationErrors &&
                                           ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return null;
        }
    }

    private static void Execute(Func<Task> action)
    {
        action().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static T Execute<T>(Func<Task<T>> action)
    {
        return action().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private static string BuildRootUrl(CloudUploadStorageOptions options, string provider, string bucketName)
    {
        if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            return options.ServiceUrl.TrimEnd('/') + "/" + bucketName;

        if (string.Equals(provider, CloudUploadStorageOptions.ProviderCloudflareR2, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.CloudflareAccountId))
                throw new InvalidOperationException("CloudUploadStorage:CloudflareAccountId is required when Provider is CloudflareR2.");

            return $"https://{options.CloudflareAccountId.Trim()}.r2.cloudflarestorage.com/{bucketName}";
        }

        var region = string.IsNullOrWhiteSpace(options.Region) ? "us-east-1" : options.Region.Trim();
        return string.Equals(region, "us-east-1", StringComparison.OrdinalIgnoreCase)
            ? $"https://{bucketName}.s3.amazonaws.com"
            : $"https://{bucketName}.s3.{region}.amazonaws.com";
    }

    private static string EncodePath(string path)
    {
        return string.Join("/", path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }
}
