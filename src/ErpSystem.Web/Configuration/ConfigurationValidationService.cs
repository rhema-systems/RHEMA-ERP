using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ErpSystem.Web.Configuration
{
    public interface IConfigurationValidationService
    {
        Task<ValidationResult> ValidateConfigurationAsync();
        void ValidateOptionsOnStartup<T>(string sectionName) where T : class;
    }

    public class ConfigurationValidationService : IConfigurationValidationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ConfigurationValidationService> _logger;

        public ConfigurationValidationService(IServiceProvider serviceProvider, ILogger<ConfigurationValidationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateConfigurationAsync()
        {
            var validationErrors = new List<string>();

            // Validate all configured options
            await ValidateOptionsSection<ApplicationOptions>(ApplicationOptions.SectionName, validationErrors);
            await ValidateOptionsSection<PerformanceOptions>(PerformanceOptions.SectionName, validationErrors);
            await ValidateOptionsSection<AuthenticationOptions>(AuthenticationOptions.SectionName, validationErrors);
            await ValidateOptionsSection<DataProtectionOptions>(DataProtectionOptions.SectionName, validationErrors);
            await ValidateOptionsSection<SecurityHeaderOptions>(SecurityHeaderOptions.SectionName, validationErrors);
            await ValidateOptionsSection<MonitoringOptions>(MonitoringOptions.SectionName, validationErrors);
            await ValidateOptionsSection<ModuleOptions>(ModuleOptions.SectionName, validationErrors);

            // Custom business logic validations
            await ValidateBusinessLogicAsync(validationErrors);

            if (validationErrors.Any())
            {
                var errorMessage = string.Join(Environment.NewLine, validationErrors);
                _logger.LogCritical("Configuration validation failed:{NewLine}{Errors}", Environment.NewLine, errorMessage);
                return new ValidationResult(false, errorMessage, validationErrors);
            }

            _logger.LogInformation("Configuration validation completed successfully");
            return new ValidationResult(true, "Configuration is valid", new List<string>());
        }

        private async Task ValidateOptionsSection<T>(string sectionName, List<string> errors) where T : class
        {
            try
            {
                var options = _serviceProvider.GetService<IOptions<T>>();
                if (options?.Value == null)
                {
                    errors.Add($"Configuration section '{sectionName}' is missing or invalid");
                    return;
                }

                var validationContext = new ValidationContext(options.Value);
                var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

                if (!Validator.TryValidateObject(options.Value, validationContext, validationResults, true))
                {
                    foreach (var validationResult in validationResults)
                    {
                        var memberNames = validationResult.MemberNames.Any() 
                            ? string.Join(", ", validationResult.MemberNames)
                            : "Unknown";
                        errors.Add($"{sectionName}.{memberNames}: {validationResult.ErrorMessage}");
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                errors.Add($"Error validating section '{sectionName}': {ex.Message}");
                _logger.LogError(ex, "Error validating configuration section {SectionName}", sectionName);
            }
        }

        private async Task ValidateBusinessLogicAsync(List<string> errors)
        {
            try
            {
                // Validate database connection
                await ValidateDatabaseConnectionAsync(errors);

                // Validate LDAP settings if enabled
                await ValidateLdapSettingsAsync(errors);

                // Validate Redis connection if configured
                await ValidateRedisConnectionAsync(errors);

                // Validate required directories exist
                ValidateDirectoriesExist(errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Business logic validation error: {ex.Message}");
                _logger.LogError(ex, "Error during business logic validation");
            }
        }

        private async Task ValidateDatabaseConnectionAsync(List<string> errors)
        {
            try
            {
                var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    errors.Add("Database connection string 'DefaultConnection' is required");
                    return;
                }

                // Try to get database context to validate connection
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetService<ErpSystem.Data.ApplicationDbContext>();
                
                if (dbContext != null)
                {
                    await dbContext.Database.CanConnectAsync();
                    _logger.LogDebug("Database connection validated successfully");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Database connection validation failed: {ex.Message}");
                _logger.LogWarning(ex, "Database connection validation failed");
            }
        }

        private async Task ValidateLdapSettingsAsync(List<string> errors)
        {
            try
            {
                var authOptions = _serviceProvider.GetService<IOptions<AuthenticationOptions>>()?.Value;
                if (authOptions?.LDAP.Enabled == true)
                {
                    if (string.IsNullOrWhiteSpace(authOptions.LDAP.DefaultServer))
                    {
                        errors.Add("LDAP is enabled but DefaultServer is not configured");
                    }

                    if (string.IsNullOrWhiteSpace(authOptions.LDAP.DefaultBaseDn))
                    {
                        errors.Add("LDAP is enabled but DefaultBaseDn is not configured");
                    }

                    _logger.LogDebug("LDAP configuration validated");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                errors.Add($"LDAP validation error: {ex.Message}");
                _logger.LogWarning(ex, "LDAP validation failed");
            }
        }

        private async Task ValidateRedisConnectionAsync(List<string> errors)
        {
            try
            {
                var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
                var redisConnectionString = configuration.GetConnectionString("Redis");

                if (!string.IsNullOrWhiteSpace(redisConnectionString))
                {
                    // Redis is configured, validate it's reachable
                    using var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString);
                    var database = redis.GetDatabase();
                    await database.PingAsync();
                    _logger.LogDebug("Redis connection validated successfully");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Redis connection validation failed: {ex.Message}");
                _logger.LogWarning(ex, "Redis connection validation failed");
            }
        }

        private void ValidateDirectoriesExist(List<string> errors)
        {
            try
            {
                var dataProtectionOptions = _serviceProvider.GetService<IOptions<DataProtectionOptions>>()?.Value;
                if (!string.IsNullOrWhiteSpace(dataProtectionOptions?.KeysPath))
                {
                    var directory = new DirectoryInfo(dataProtectionOptions.KeysPath);
                    if (!directory.Exists)
                    {
                        try
                        {
                            directory.Create();
                            _logger.LogInformation("Created data protection keys directory: {Path}", dataProtectionOptions.KeysPath);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Cannot create data protection keys directory '{dataProtectionOptions.KeysPath}': {ex.Message}");
                        }
                    }
                }

                // Ensure logs directory exists
                var logsDir = new DirectoryInfo("logs");
                if (!logsDir.Exists)
                {
                    logsDir.Create();
                    _logger.LogDebug("Created logs directory");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Directory validation error: {ex.Message}");
                _logger.LogWarning(ex, "Directory validation failed");
            }
        }

        public void ValidateOptionsOnStartup<T>(string sectionName) where T : class
        {
            // This is used for simple validation that throws on startup
            var options = _serviceProvider.GetService<IOptions<T>>();
            if (options?.Value == null)
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' is missing or invalid");
            }

            var validationContext = new ValidationContext(options.Value);
            Validator.ValidateObject(options.Value, validationContext, validateAllProperties: true);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; }
        public string Message { get; }
        public List<string> Errors { get; }

        public ValidationResult(bool isValid, string message, List<string> errors)
        {
            IsValid = isValid;
            Message = message;
            Errors = errors;
        }
    }
}