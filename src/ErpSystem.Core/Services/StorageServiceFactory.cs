using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpSystem.Core.Services;

/// <summary>
/// Factory for creating file storage service instances based on configuration
/// </summary>
public interface IStorageServiceFactory
{
    /// <summary>
    /// Create the primary storage service based on configuration
    /// </summary>
    IFileStorageService CreatePrimaryStorage();

    /// <summary>
    /// Create a specific storage service by provider type
    /// </summary>
    IFileStorageService CreateStorage(StorageProviderType providerType);

    /// <summary>
    /// Get all available storage providers
    /// </summary>
    IEnumerable<StorageProviderType> GetAvailableProviders();

    /// <summary>
    /// Check if a storage provider is available/configured
    /// </summary>
    bool IsProviderAvailable(StorageProviderType providerType);
}

/// <summary>
/// Implementation of storage service factory
/// </summary>
public class StorageServiceFactory : IStorageServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly StorageProviderOptions _options;
    private readonly ILogger<StorageServiceFactory> _logger;

    public StorageServiceFactory(
        IServiceProvider serviceProvider,
        IOptions<StorageProviderOptions> options,
        ILogger<StorageServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public IFileStorageService CreatePrimaryStorage()
    {
        return CreateStorage(_options.Provider);
    }

    public IFileStorageService CreateStorage(StorageProviderType providerType)
    {
        try
        {
            var service = providerType switch
            {
                StorageProviderType.Local => _serviceProvider.GetRequiredService<LocalFileStorageService>(),
                StorageProviderType.AzureBlob => _serviceProvider.GetRequiredService<AzureBlobStorageService>(),
                StorageProviderType.AwsS3 => CreateAwsS3StorageService(), // Placeholder for future implementation
                StorageProviderType.GoogleCloud => CreateGoogleCloudStorageService(), // Placeholder for future implementation
                _ => throw new NotSupportedException($"Storage provider '{providerType}' is not supported")
            };

            _logger.LogDebug("Created storage service for provider: {ProviderType}", providerType);
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create storage service for provider: {ProviderType}", providerType);
            throw;
        }
    }

    public IEnumerable<StorageProviderType> GetAvailableProviders()
    {
        var availableProviders = new List<StorageProviderType>();

        // Local storage is always available
        availableProviders.Add(StorageProviderType.Local);

        // Check Azure Blob Storage
        if (IsProviderAvailable(StorageProviderType.AzureBlob))
        {
            availableProviders.Add(StorageProviderType.AzureBlob);
        }

        // Check AWS S3 (placeholder)
        if (IsProviderAvailable(StorageProviderType.AwsS3))
        {
            availableProviders.Add(StorageProviderType.AwsS3);
        }

        // Check Google Cloud (placeholder)
        if (IsProviderAvailable(StorageProviderType.GoogleCloud))
        {
            availableProviders.Add(StorageProviderType.GoogleCloud);
        }

        return availableProviders;
    }

    public bool IsProviderAvailable(StorageProviderType providerType)
    {
        try
        {
            return providerType switch
            {
                StorageProviderType.Local => true, // Always available
                StorageProviderType.AzureBlob => IsAzureBlobAvailable(),
                StorageProviderType.AwsS3 => IsAwsS3Available(),
                StorageProviderType.GoogleCloud => IsGoogleCloudAvailable(),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability of storage provider: {ProviderType}", providerType);
            return false;
        }
    }

    #region Private Methods

    private bool IsAzureBlobAvailable()
    {
        return !string.IsNullOrEmpty(_options.Azure.ConnectionString) &&
               !string.IsNullOrEmpty(_options.Azure.ContainerName);
    }

    private bool IsAwsS3Available()
    {
        return !string.IsNullOrEmpty(_options.Aws.AccessKey) &&
               !string.IsNullOrEmpty(_options.Aws.SecretKey) &&
               !string.IsNullOrEmpty(_options.Aws.BucketName);
    }

    private static bool IsGoogleCloudAvailable()
    {
        // Placeholder - implement based on Google Cloud Storage requirements
        return false;
    }

    private IFileStorageService CreateAwsS3StorageService()
    {
        // Placeholder for AWS S3 implementation
        throw new NotImplementedException("AWS S3 storage service is not implemented yet. To add support, create AwsS3StorageService class and implement the interface.");
    }

    private IFileStorageService CreateGoogleCloudStorageService()
    {
        // Placeholder for Google Cloud Storage implementation
        throw new NotImplementedException("Google Cloud Storage service is not implemented yet. To add support, create GoogleCloudStorageService class and implement the interface.");
    }

    #endregion
}

/// <summary>
/// Extension methods for storage service configuration
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>
    /// Add storage services to dependency injection container
    /// </summary>
    public static IServiceCollection AddStorageServices(this IServiceCollection services)
    {
        // Register storage service implementations
        services.AddScoped<LocalFileStorageService>();
        services.AddScoped<AzureBlobStorageService>();

        // Register factory
        services.AddScoped<IStorageServiceFactory, StorageServiceFactory>();

        // Register primary storage service based on configuration
        services.AddScoped<IFileStorageService>(provider =>
        {
            var factory = provider.GetRequiredService<IStorageServiceFactory>();
            return factory.CreatePrimaryStorage();
        });

        return services;
    }

    /// <summary>
    /// Configure storage services with options
    /// </summary>
    public static IServiceCollection ConfigureStorageServices(
        this IServiceCollection services,
        Action<StorageProviderOptions> configure)
    {
        services.Configure(configure);
        return services;
    }
}

/// <summary>
/// Storage service health check extensions
/// </summary>
public static class StorageHealthCheckExtensions
{
    /// <summary>
    /// Add health checks for storage services
    /// </summary>
    public static IServiceCollection AddStorageHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<StorageServiceHealthCheck>("storage_service");

        return services;
    }
}

/// <summary>
/// Health check for storage services
/// </summary>
public class StorageServiceHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IStorageServiceFactory _storageFactory;
    private readonly ILogger<StorageServiceHealthCheck> _logger;

    public StorageServiceHealthCheck(
        IStorageServiceFactory storageFactory,
        ILogger<StorageServiceHealthCheck> logger)
    {
        _storageFactory = storageFactory;
        _logger = logger;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var primaryStorage = _storageFactory.CreatePrimaryStorage();
            var isHealthy = await primaryStorage.IsHealthyAsync();

            if (isHealthy)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Storage service ({primaryStorage.ProviderName}) is healthy");
            }
            else
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                    $"Storage service ({primaryStorage.ProviderName}) is not healthy");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage service health check failed");
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Storage service health check failed", ex);
        }
    }
}
