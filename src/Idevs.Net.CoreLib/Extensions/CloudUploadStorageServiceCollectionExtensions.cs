using Idevs.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serenity.Web;

namespace Idevs.Extensions;

public static class CloudUploadStorageServiceCollectionExtensions
{
    public static IServiceCollection AddCloudUploadStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(CloudUploadStorageOptions.SectionName);
        services.Configure<CloudUploadStorageOptions>(section);

        var provider = section.GetValue<string>(nameof(CloudUploadStorageOptions.Provider), CloudUploadStorageOptions.DefaultProvider);
        RegisterCloudStorageWhenNeeded(services, provider);

        return services;
    }

    public static IServiceCollection AddCloudUploadStorage(this IServiceCollection services, Action<CloudUploadStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new CloudUploadStorageOptions();
        configure(options);

        services.Configure(configure);
        RegisterCloudStorageWhenNeeded(services, options.Provider);

        return services;
    }

    private static void RegisterCloudStorageWhenNeeded(IServiceCollection services, string? provider)
    {
        provider = string.IsNullOrWhiteSpace(provider) ? CloudUploadStorageOptions.DefaultProvider : provider.Trim();

        if (string.Equals(provider, CloudUploadStorageOptions.ProviderLocal, StringComparison.OrdinalIgnoreCase))
            return;

        services.AddSingleton<IUploadStorage>(serviceProvider =>
        {
            var cloudOptions = serviceProvider.GetRequiredService<IOptions<CloudUploadStorageOptions>>();
            var uploadSettings = serviceProvider.GetService<IOptions<UploadSettings>>()?.Value ?? new UploadSettings();
            var logger = serviceProvider.GetService<ILogger<CloudUploadStorage>>();

            return new CloudUploadStorage(cloudOptions, uploadSettings, logger);
        });
    }
}
