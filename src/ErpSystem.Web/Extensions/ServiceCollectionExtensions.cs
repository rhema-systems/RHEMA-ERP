using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;
using Serilog;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Web.Services;
using ErpSystem.Web.Middleware;
using ErpSystem.Web.HealthChecks;
using ErpSystem.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ErpSystem.Web.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddErpSystemDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("ErpSystem.Data");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                }));

            return services;
        }

        public static IServiceCollection AddErpSystemIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredUniqueChars = 1;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = true;

                // Sign in settings
                options.SignIn.RequireConfirmedEmail = false;
                options.SignIn.RequireConfirmedPhoneNumber = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            return services;
        }

        public static IServiceCollection AddErpSystemRepositories(this IServiceCollection services)
        {
            // Generic repository
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // Specific repositories
            services.AddScoped<ITenantRepository, TenantRepository>();

            return services;
        }

        public static IServiceCollection AddErpSystemServices(this IServiceCollection services)
        {
            // Core services
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();
            services.AddScoped<ISearchService, SearchService>();
            
            // Communication services
            services.AddScoped<IEmailService, SimpleEmailService>();

            // Add more services as modules are developed
            // services.AddScoped<IFinanceService, FinanceService>();
            // services.AddScoped<IHRService, HRService>();
            // services.AddScoped<IInventoryService, InventoryService>();
            // services.AddScoped<ISalesService, SalesService>();

            return services;
        }

        public static IServiceCollection AddErpSystemAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Custom fallback policy that allows anonymous access to login/auth pages
                options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAssertion(context =>
                    {
                        var path = context.Resource as Microsoft.AspNetCore.Http.DefaultHttpContext;
                        var requestPath = path?.Request.Path.Value?.ToLowerInvariant();
                        
                        // Allow anonymous access to auth-related paths
                        if (requestPath != null && (
                            requestPath.StartsWith("/account/") ||
                            requestPath.StartsWith("/api/account/") ||
                            requestPath == "/" ||
                            requestPath.StartsWith("/authdebug") ||
                            requestPath.StartsWith("/testauth") ||
                            requestPath.StartsWith("/loginsuccess") ||
                            requestPath.StartsWith("/_blazor") ||
                            requestPath.StartsWith("/_framework") ||
                            requestPath.StartsWith("/css/") ||
                            requestPath.StartsWith("/js/") ||
                            requestPath.StartsWith("/health") ||
                            requestPath.StartsWith("/favicon")
                        ))
                        {
                            return true; // Allow anonymous access
                        }
                        
                        // For all other paths, require authentication
                        return context.User.Identity?.IsAuthenticated == true;
                    })
                    .Build();
                
                // Role-based policies
                options.AddPolicy("SuperAdmin", policy => 
                    policy.RequireRole("SuperAdmin"));
                
                options.AddPolicy("TenantAdmin", policy => 
                    policy.RequireRole("TenantAdmin", "SuperAdmin"));
                
                options.AddPolicy("Manager", policy => 
                    policy.RequireRole("Manager", "TenantAdmin", "SuperAdmin"));
                
                options.AddPolicy("Employee", policy => 
                    policy.RequireRole("Employee", "Manager", "TenantAdmin", "SuperAdmin"));

                // Module-based policies
                options.AddPolicy("Finance", policy =>
                    policy.RequireClaim("module", "Finance"));
                
                options.AddPolicy("HR", policy =>
                    policy.RequireClaim("module", "HR"));
                
                options.AddPolicy("Sales", policy =>
                    policy.RequireClaim("module", "Sales"));
                
                options.AddPolicy("Inventory", policy =>
                    policy.RequireClaim("module", "Inventory"));
                
                options.AddPolicy("Procurement", policy =>
                    policy.RequireClaim("module", "Procurement"));
                
                options.AddPolicy("Marketing", policy =>
                    policy.RequireClaim("module", "Marketing"));
            });

            return services;
        }

        public static IServiceCollection AddErpSystemBlazor(this IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor(options =>
            {
                options.DetailedErrors = true;
                options.MaxBufferedUnacknowledgedRenderBatches = 10;
            })
            .AddHubOptions(options =>
            {
                // Configure SignalR hub to access HttpContext for authentication
                options.EnableDetailedErrors = true;
            });
            
            services.AddControllers(); // Add API controller support

            // Add HttpContextAccessor for authentication state provider
            services.AddHttpContextAccessor();

            // Add custom revalidating authentication state provider for Blazor Server
            services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, 
                ErpSystem.Web.Services.CustomRevalidatingAuthenticationStateProvider>();

            // Add Syncfusion Blazor
            services.AddSyncfusionBlazor();
            
            return services;
        }

        public static IServiceCollection AddErpSystemSession(this IServiceCollection services)
        {
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            return services;
        }

        public static IServiceCollection AddErpSystemLogging(this IServiceCollection services, IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .CreateLogger();

            services.AddSerilog();

            return services;
        }

        public static IServiceCollection AddErpSystemHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var redisConnectionString = configuration.GetConnectionString("Redis");
            
            var healthChecksBuilder = services.AddHealthChecks()
                .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application is running"))
                .AddCheck("startup", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application started successfully"));

            // Add database health check
            if (!string.IsNullOrEmpty(connectionString))
            {
                healthChecksBuilder.AddDbContextCheck<ErpSystem.Data.ApplicationDbContext>("database", 
                    tags: new[] { "db", "sql", "ready" });
            }

            // Add Redis health check if configured
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                // TODO: Add Redis health check when package is available
                // healthChecksBuilder.AddRedis(redisConnectionString, "redis", tags: new[] { "cache", "redis", "ready" });
                healthChecksBuilder.AddCheck("redis", () => 
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis configured"));
            }

            // Add memory health check
            healthChecksBuilder.AddCheck("memory", () =>
            {
                var gc = GC.GetTotalMemory(false);
                var workingSet = Environment.WorkingSet;
                
                if (workingSet > 1024 * 1024 * 1024) // > 1GB
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"High memory usage: {workingSet / 1024 / 1024} MB");
                }
                
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                    $"Memory usage: {workingSet / 1024 / 1024} MB");
            }, tags: new[] { "memory", "performance" });

            // Add disk space health check
            healthChecksBuilder.AddCheck("disk_space", () =>
            {
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(Directory.GetCurrentDirectory())!);
                    var freeSpaceGB = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
                    
                    if (freeSpaceGB < 1) // Less than 1GB free
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                            $"Low disk space: {freeSpaceGB} GB remaining");
                    }
                    
                    if (freeSpaceGB < 5) // Less than 5GB free
                    {
                        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                            $"Low disk space: {freeSpaceGB} GB remaining");
                    }
                    
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                        $"Disk space: {freeSpaceGB} GB available");
                }
                catch (Exception ex)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                        $"Could not check disk space: {ex.Message}");
                }
            }, tags: new[] { "storage", "disk" });

            // Add application-specific health checks
            healthChecksBuilder.AddCheck("application", () => 
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Application is healthy"), 
                tags: new[] { "application", "custom" });
            
            // Add shutdown health check
            healthChecksBuilder.AddShutdownHealthCheck();

            return services;
    }

    public static IServiceCollection AddDevelopmentServices(this IServiceCollection services, IWebHostEnvironment environment)
    {
        // Always register these services, but they will behave differently based on environment
        services.AddSingleton<IFeatureService, SimpleFeatureService>();
        services.AddScoped<IDevInfoService, DevInfoService>();
        
        // Add database seeding service
        services.AddDatabaseSeeding();

        return services;
    }

    public static IServiceCollection AddErpSystemLifecycle(this IServiceCollection services)
    {
        // Graceful shutdown and resource cleanup
        services.AddSingleton<IResourceCleanupService, ResourceCleanupService>();
        services.AddSingleton<IGracefulShutdownService, GracefulShutdownService>();
        services.AddHostedService<GracefulShutdownService>(serviceProvider => 
            (GracefulShutdownService)serviceProvider.GetRequiredService<IGracefulShutdownService>());

        return services;
    }

    public static IApplicationBuilder UseSimpleDevelopmentMiddleware(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // Add development-specific middleware
            app.UseMiddleware<SimpleDevMiddleware>();
        }
        
        // Security headers middleware for all environments
        app.UseMiddleware<DevSecurityHeadersMiddleware>();

        return app;
    }

        public static IServiceCollection AddErpSystemCaching(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMemoryCache();
            
            // Check if Redis is configured
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = "ErpSystem";
                });
            }
            else
            {
                services.AddDistributedMemoryCache();
            }
            
            // Add response caching for full page caching
            services.AddResponseCaching(options =>
            {
                options.MaximumBodySize = 64 * 1024; // 64KB
                options.SizeLimit = 50 * 1024 * 1024; // 50MB
            });
            
            return services;
        }

        public static IServiceCollection AddErpSystemWebFarm(this IServiceCollection services, IConfiguration configuration)
        {
            var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
            var redisConnectionString = configuration.GetConnectionString("Redis");
            
            var dataProtectionBuilder = services.AddDataProtection();
                
            if (!string.IsNullOrEmpty(dataProtectionKeysPath))
            {
                // TODO: Add Microsoft.AspNetCore.DataProtection.Extensions package
                // dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
            }
            else if (!string.IsNullOrEmpty(redisConnectionString))
            {
                // TODO: Store data protection keys in Redis for web farm scenarios
                // Requires Microsoft.AspNetCore.DataProtection.StackExchangeRedis package
                // dataProtectionBuilder.PersistKeysToStackExchangeRedis(
                //     StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString),
                //     "DataProtection-Keys");
            }
            
            return services;
        }

        public static IServiceCollection AddErpSystemSearch(this IServiceCollection services, IConfiguration configuration)
        {
            // Add search capabilities (can be extended with Elasticsearch later)
            services.AddScoped<ISearchService, SearchService>();
            
            var elasticSearchUrl = configuration["Search:ElasticsearchUrl"];
            if (!string.IsNullOrEmpty(elasticSearchUrl))
            {
                // TODO: Add Elasticsearch configuration when implementing advanced search
                // services.AddElasticsearch(elasticSearchUrl);
            }
            
            return services;
        }

        public static IServiceCollection AddErpSystemConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure and validate all options
            services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));
            services.Configure<PerformanceOptions>(configuration.GetSection(PerformanceOptions.SectionName));
            services.Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationOptions.SectionName));
            services.Configure<DataProtectionOptions>(configuration.GetSection(DataProtectionOptions.SectionName));
            services.Configure<SecurityHeaderOptions>(configuration.GetSection(SecurityHeaderOptions.SectionName));
            services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
            services.Configure<ModuleOptions>(configuration.GetSection(ModuleOptions.SectionName));

            // Add validation service
            services.AddSingleton<IConfigurationValidationService, ConfigurationValidationService>();

            // Add options validation
            services.AddOptions<ApplicationOptions>()
                .BindConfiguration(ApplicationOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<PerformanceOptions>()
                .BindConfiguration(PerformanceOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<AuthenticationOptions>()
                .BindConfiguration(AuthenticationOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<DataProtectionOptions>()
                .BindConfiguration(DataProtectionOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<SecurityHeaderOptions>()
                .BindConfiguration(SecurityHeaderOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<MonitoringOptions>()
                .BindConfiguration(MonitoringOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<ModuleOptions>()
                .BindConfiguration(ModuleOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }
    }
}
