namespace ErpSystem.Web.Services
{
    public interface IFeatureService
    {
        bool IsEnabled(string featureName);
        T GetValue<T>(string featureName, T defaultValue = default);
        Dictionary<string, object> GetAllFeatures();
    }

    public class SimpleFeatureService : IFeatureService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<SimpleFeatureService> _logger;
        private readonly Dictionary<string, object> _features;

        public SimpleFeatureService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<SimpleFeatureService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
            _features = LoadFeatures();
        }

        public bool IsEnabled(string featureName)
        {
            return GetValue<bool>(featureName, false);
        }

        public T GetValue<T>(string featureName, T defaultValue = default)
        {
            if (_features.TryGetValue(featureName, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert feature value for {FeatureName}", featureName);
                }
            }

            return defaultValue;
        }

        public Dictionary<string, object> GetAllFeatures()
        {
            return new Dictionary<string, object>(_features);
        }

        private Dictionary<string, object> LoadFeatures()
        {
            var features = new Dictionary<string, object>();

            try
            {
                // Load from configuration
                var featureSection = _configuration.GetSection("Features");
                if (featureSection.Exists())
                {
                    foreach (var child in featureSection.GetChildren())
                    {
                        features[child.Key] = child.Value ?? string.Empty;
                    }
                }

                // Set development defaults
                if (_environment.IsDevelopment())
                {
                    features.TryAdd("EnableDetailedLogging", true);
                    features.TryAdd("EnableSwaggerUI", true);
                    features.TryAdd("EnableDatabaseSeeding", true);
                    features.TryAdd("EnableDebugInfo", true);
                    features.TryAdd("EnablePerformanceMetrics", true);
                }
                else
                {
                    features.TryAdd("EnableDetailedLogging", false);
                    features.TryAdd("EnableSwaggerUI", false);
                    features.TryAdd("EnableDatabaseSeeding", false);
                    features.TryAdd("EnableDebugInfo", false);
                    features.TryAdd("EnablePerformanceMetrics", false);
                }

                _logger.LogInformation("Loaded {Count} feature flags", features.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load feature flags");
            }

            return features;
        }
    }

    public interface IDevInfoService
    {
        Task<object> GetSystemInfoAsync();
        Task<object> GetHealthInfoAsync();
        Task<object> GetEnvironmentInfoAsync();
    }

    public class DevInfoService : IDevInfoService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DevInfoService> _logger;

        public DevInfoService(IWebHostEnvironment environment, ILogger<DevInfoService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<object> GetSystemInfoAsync()
        {
            try
            {
                return new
                {
                    Environment = _environment.EnvironmentName,
                    ApplicationName = _environment.ApplicationName,
                    ContentRoot = _environment.ContentRootPath,
                    WebRoot = _environment.WebRootPath,
                    MachineName = Environment.MachineName,
                    OSVersion = Environment.OSVersion.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    WorkingSet = Environment.WorkingSet,
                    RuntimeVersion = Environment.Version.ToString(),
                    IsDevelopment = _environment.IsDevelopment(),
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system info");
                return new { Error = ex.Message };
            }
        }

        public async Task<object> GetHealthInfoAsync()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                
                return new
                {
                    Status = "Healthy",
                    Uptime = DateTime.UtcNow.Subtract(process.StartTime),
                    Environment = _environment.EnvironmentName,
                    Process = new
                    {
                        Id = process.Id,
                        Name = process.ProcessName,
                        StartTime = process.StartTime,
                        WorkingSet = process.WorkingSet64,
                        ThreadCount = process.Threads.Count
                    },
                    Memory = new
                    {
                        WorkingSet = Environment.WorkingSet,
                        GcTotalMemory = GC.GetTotalMemory(false),
                        Gen0Collections = GC.CollectionCount(0),
                        Gen1Collections = GC.CollectionCount(1),
                        Gen2Collections = GC.CollectionCount(2)
                    },
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get health info");
                return new { Error = ex.Message };
            }
        }

        public async Task<object> GetEnvironmentInfoAsync()
        {
            try
            {
                return new
                {
                    Environment = _environment.EnvironmentName,
                    ContentRoot = _environment.ContentRootPath,
                    WebRoot = _environment.WebRootPath,
                    IsDevelopment = _environment.IsDevelopment(),
                    IsProduction = _environment.IsProduction(),
                    IsStaging = _environment.IsStaging(),
                    Variables = new
                    {
                        ASPNETCORE_ENVIRONMENT = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                        DOTNET_ENVIRONMENT = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                    },
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get environment info");
                return new { Error = ex.Message };
            }
        }
    }
}