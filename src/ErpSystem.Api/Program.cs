using System.Text;
using ErpSystem.Api.Data;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.Middleware;
using ErpSystem.Data;
using ErpSystem.Web.Middleware;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Syncfusion.Licensing;

// Check for seed command
if (args.Length > 0 && args[0] == "seed")
{
    var tempBuilder = CreateSeedBuilder(args);

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    // Run user seeding through the shared database seeding service so roles/default tenant stay in sync.
    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedTestUsersAsync();
    }

    return;
}

// Check for maintenance workflow seeding command
if (args.Length > 0 && args[0] == "seed-maintenance")
{
    var tempBuilder = CreateSeedBuilder(args);

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);

    var tempApp = tempBuilder.Build();

    // Run maintenance workflow seeding using the new MaintenanceDataSeeder
    using (var scope = tempApp.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ErpSystem.Data.ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ErpSystem.Data.Seeders.MaintenanceDataSeeder>>();
        var seeder = new ErpSystem.Data.Seeders.MaintenanceDataSeeder(context, logger);

        await seeder.SeedAsync();
    }

    Console.WriteLine("Maintenance workflow seeding completed!");
    return;
}

// Check for maintenance E2E test data seeding command
if (args.Length > 0 && args[0] == "seed-maintenance-e2e")
{
    var tempBuilder = CreateSeedBuilder(args);

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddHttpContextAccessor(); // Required for ICurrentUserProvider
    tempBuilder.Services.AddScoped<ErpSystem.Core.Interfaces.ICurrentUserProvider, ErpSystem.Api.Services.CurrentUserService>();
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    // Run E2E maintenance test data seeding
    using (var scope = tempApp.Services.CreateScope())
    {
        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedMaintenanceE2ETestDataAsync();
    }

    Console.WriteLine("✅ Maintenance E2E test data seeding completed!");
    return;
}

// Check for full database seeding command (roles, workflows, modules, etc.)
if (args.Length > 0 && args[0] == "seed-db")
{
    var tempBuilder = CreateSeedBuilder(args);

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        // Apply migrations first so seeding is safe in all environments.
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedAsync();
    }

    Console.WriteLine("✅ Database seeding completed!");
    return;
}

// Check for development database rebuild command.
// This bypasses the current migration chain and recreates the schema directly from the EF model.
if (args.Length > 0 && args[0] == "rebuild-db")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        Console.WriteLine("⚠️  Rebuilding database from the current EF model...");
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await StampAllKnownMigrationsAsAppliedAsync(db, logger);
        await seedingService.SeedWithoutMigrationAsync();
    }

    Console.WriteLine("✅ Database rebuild completed!");
    return;
}

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Testing")
    && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=ErpSystem_TestHost;Trusted_Connection=True;TrustServerCertificate=True",
        ["Database:Provider"] = "SqlServer",
        ["SkipStartupInitialization"] = "true",
        ["JwtSettings:SecretKey"] = "TestingOnlySecretKeyForApiHost1234567890",
        ["JwtSettings:Issuer"] = "ErpSystem.Api.Tests",
        ["JwtSettings:Audience"] = "ErpSystem.Api.Tests.Client"
    });
}

var syncfusionLicenseKey =
    builder.Configuration["Syncfusion:LicenseKey"] ??
    builder.Configuration["SyncfusionLicenseKey"];
if (!string.IsNullOrWhiteSpace(syncfusionLicenseKey))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
}

// Configure host shutdown timeout
builder.Host.ConfigureServices((context, services) =>
{
    services.Configure<HostOptions>(opts =>
    {
        opts.ShutdownTimeout = TimeSpan.FromSeconds(60);
    });
});

// Configure Serilog early
builder.Host.UseSerilog();

// Add all ERP System services using extension methods
builder.Services.AddErpSystemLogging(builder.Configuration);
builder.Services.AddErpSystemDatabase(builder.Configuration);
builder.Services.AddErpSystemIdentity();
builder.Services.AddErpSystemRepositories();
builder.Services.AddErpSystemServices();
builder.Services.AddErpSystemJwtAuthentication(builder.Configuration);
builder.Services.AddErpSystemAuthorization();
builder.Services.AddErpSystemApi();
builder.Services.AddErpSystemHealthChecks(builder.Configuration);
builder.Services.AddErpSystemCaching(builder.Configuration);
builder.Services.AddErpSystemWebFarm(builder.Configuration);
builder.Services.AddErpSystemSearch(builder.Configuration);
builder.Services.AddErpSystemLifecycle();
builder.Services.AddErpSystemCors(builder.Configuration);
builder.Services.AddErpSystemRateLimiting(builder.Environment);
builder.Services.AddErpSystemFileUpload(builder.Configuration);
builder.Services.AddErpSystemSignalR();
builder.Services.AddScoped<ErpSystem.Core.Interfaces.IDistributedLockService, ErpSystem.Api.Services.DistributedLockService>();
builder.Services.AddDevelopmentServices(builder.Environment);

// Add Quality Certificate Service
builder.Services.AddScoped<ErpSystem.Api.Services.QualityCertificateService>();

// Add Transfer Document Service for Shipment Notes and GRNs
builder.Services.AddScoped<ErpSystem.Api.Services.TransferDocumentService>();

// Add Purchase Receipt (PO GRN) PDF service
builder.Services.AddScoped<ErpSystem.Api.Services.PurchaseOrderReceiptDocumentService>();

// Add Award Letter Service for PDF award letter generation
builder.Services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAwardLetterService, ErpSystem.Api.Services.AwardLetterService>();

// Add Price List Lookup Service for procurement pricing
builder.Services.AddScoped<ErpSystem.Core.Services.Pricing.PriceListLookupService>();

var app = builder.Build();

Console.WriteLine("🔧 App built successfully - configuring middleware...");

// Configure the HTTP request pipeline

// Add global exception handling first
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Optional deep HTTP request/response logging. Keep this opt-in because
// capturing full bodies on every request is expensive during normal development.
if (app.Configuration.GetValue("HttpRequestResponseLogging:Enabled", false))
{
    app.UseMiddleware<HttpLoggingMiddleware>();
}

// Add security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
}

// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ERP System API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "ERP System API Documentation";
    c.DefaultModelsExpandDepth(-1); // Hide schemas section by default
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None); // Collapse operations by default
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();

    // Persist authorization
    c.EnablePersistAuthorization();

    // Add custom CSS for better UX
    c.InjectStylesheet("/swagger-ui/custom.css");
});

// Add application lifecycle management
app.UseApplicationLifecycleManagement();

// Add development middleware
app.UseSimpleDevelopmentMiddleware(app.Environment);

// Keep failed and slow requests visible, but avoid flooding development output
// with a line for every successful API call.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, exception) =>
    {
        if (exception is not null || httpContext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        if (httpContext.Response.StatusCode >= 400)
        {
            return LogEventLevel.Warning;
        }

        return elapsed > 1000
            ? LogEventLevel.Information
            : LogEventLevel.Debug;
    };
});

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseResponseCaching();

// Enable static file serving for uploaded files
app.UseStaticFiles();

app.UseRouting();

// CORS must be after UseRouting and before UseAuthentication
app.UseCors("ErpSystemCorsPolicy");

app.UseAuthentication();
app.UseMiddleware<JwtBlacklistMiddleware>();

// Rate limiting depends on authenticated user claims for ERP/external users.
// Auth endpoints remain anonymous here, so login/password-reset throttling still applies by IP.
app.UseRateLimiter();

app.UseMiddleware<ExternalUserAccessMiddleware>();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/shutdown", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("shutdown")
});

// API Controllers
app.MapControllers();

// SignalR Hubs
app.MapHub<ErpSystem.Api.Hubs.DashboardHub>("/api/hubs/dashboard");

var skipStartupInitialization = app.Environment.IsEnvironment("Testing")
    || app.Configuration.GetValue<bool>("SkipStartupInitialization");
var databaseConnectionTimeout = TimeSpan.FromSeconds(Math.Max(
    1,
    app.Configuration.GetValue("StartupInitialization:DatabaseConnectionTimeoutSeconds", 5)));
var migrationTimeout = TimeSpan.FromSeconds(Math.Max(
    5,
    app.Configuration.GetValue("StartupInitialization:MigrationTimeoutSeconds", 120)));
var failFastOnDatabaseInitializationError = app.Configuration.GetValue(
    "StartupInitialization:FailFastOnDatabaseInitializationError",
    true);
var seedDevelopmentData = app.Configuration.GetValue("StartupInitialization:SeedDevelopmentData", true);
var failFastOnDevelopmentSeedError = app.Configuration.GetValue(
    "StartupInitialization:FailFastOnDevelopmentSeedError",
    false);
var databaseInitializationSucceeded = false;

if (!skipStartupInitialization)
{
    // Initialize database and seed data
    app.Logger.LogInformation("Starting database initialization...");
    try
    {
        await InitializeDatabaseAsync(app, databaseConnectionTimeout, migrationTimeout);
        databaseInitializationSucceeded = true;
        app.Logger.LogInformation("Database initialization completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Database initialization failed");
        if (failFastOnDatabaseInitializationError)
        {
            throw;
        }
    }

    // Seed demo/basic data in Development to make local testing easier.
    if (app.Environment.IsDevelopment() && seedDevelopmentData && databaseInitializationSucceeded)
    {
        app.Logger.LogInformation("Starting Development data seeding...");
        try
        {
            await SeedDatabaseAsync(app);
            app.Logger.LogInformation("Development data seeding completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Development data seeding failed");
            if (failFastOnDevelopmentSeedError)
            {
                throw;
            }
        }
    }
    else if (app.Environment.IsDevelopment() && seedDevelopmentData)
    {
        app.Logger.LogWarning(
            "Skipping Development data seeding because database initialization did not complete successfully.");
    }
}
else
{
    app.Logger.LogInformation("Skipping startup database initialization for environment {EnvironmentName}", app.Environment.EnvironmentName);
}

// Workflow automation trigger - comprehensive testing active
// Version: 2.0.0 - Full CI/CD Pipeline Integration
app.Run();

async Task InitializeDatabaseAsync(
    WebApplication app,
    TimeSpan databaseConnectionTimeout,
    TimeSpan migrationTimeout)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogDebug("Testing database connection...");
    var providerName = context.Database.ProviderName ?? "Unknown";
    var connectionSummary = SummarizeConnectionTarget(context.Database.GetConnectionString());

    using (var testCts = new CancellationTokenSource(databaseConnectionTimeout))
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync(testCts.Token);
            if (!canConnect)
            {
                throw new InvalidOperationException(
                    $"Database connection check failed for provider '{providerName}' using '{connectionSummary}'. Startup migrations cannot continue.");
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException(
                $"Database connection timed out after {databaseConnectionTimeout.TotalSeconds:F0} seconds.",
                ex);
        }
    }

    logger.LogInformation("Database connection successful. Running migrations...");

    using var migrationCts = new CancellationTokenSource(migrationTimeout);
    try
    {
        await RepairDevelopmentMigrationHistoryIfNeededAsync(app.Environment, context, logger, migrationCts.Token);
        await context.Database.MigrateAsync(migrationCts.Token);
    }
    catch (OperationCanceledException ex)
    {
        throw new TimeoutException(
            $"Database migration timed out after {migrationTimeout.TotalSeconds:F0} seconds.",
            ex);
    }

    logger.LogInformation("Database migration completed successfully");
}

static WebApplicationBuilder CreateSeedBuilder(string[] args)
{
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    return WebApplication.CreateBuilder(args);
}

async Task SeedDatabaseAsync(WebApplication app)
{
    await app.Services.SeedDatabaseAsync();
}

static async Task RepairDevelopmentMigrationHistoryIfNeededAsync(
    IWebHostEnvironment environment,
    ApplicationDbContext context,
    Microsoft.Extensions.Logging.ILogger logger,
    CancellationToken cancellationToken)
{
    if (!environment.IsDevelopment())
    {
        return;
    }

    var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
    if (!pendingMigrations.Contains("20260311184920_AddProjectMaterialCostLedger"))
    {
        return;
    }

    if (!await TableExistsAsync(context, "ProjectMaterialCostEntries", cancellationToken))
    {
        return;
    }

    logger.LogWarning(
        "Detected a development database with current-model tables but missing migration history. " +
        "Stamping known migrations as applied before running startup migrations.");

    await StampAllKnownMigrationsAsAppliedAsync(context, logger, cancellationToken);
}

static async Task StampAllKnownMigrationsAsAppliedAsync(
    ApplicationDbContext context,
    Microsoft.Extensions.Logging.ILogger logger,
    CancellationToken cancellationToken = default)
{
    var historyRepository = context.GetService<IHistoryRepository>();
    var migrationsAssembly = context.GetService<IMigrationsAssembly>();

    var createHistoryScript = historyRepository.GetCreateIfNotExistsScript();
    if (!string.IsNullOrWhiteSpace(createHistoryScript))
    {
        await context.Database.ExecuteSqlRawAsync(createHistoryScript, cancellationToken);
    }

    var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "9.0.0";

    foreach (var migrationId in migrationsAssembly.Migrations.Keys.OrderBy(id => id))
    {
        if (appliedMigrations.Contains(migrationId))
        {
            continue;
        }

        var insertScript = historyRepository.GetInsertScript(new HistoryRow(migrationId, productVersion));
        await context.Database.ExecuteSqlRawAsync(insertScript, cancellationToken);
        appliedMigrations.Add(migrationId);
    }

    logger.LogInformation("Stamped {MigrationCount} migrations as applied in EF migration history.", appliedMigrations.Count);
}

static async Task<bool> TableExistsAsync(
    ApplicationDbContext context,
    string tableName,
    CancellationToken cancellationToken)
{
    var connection = context.Database.GetDbConnection();
    var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

    if (shouldCloseConnection)
    {
        await connection.OpenAsync(cancellationToken);
    }

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@tableName, 'U') IS NOT NULL THEN 1 ELSE 0 END";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result ?? 0) == 1;
    }
    finally
    {
        if (shouldCloseConnection)
        {
            await connection.CloseAsync();
        }
    }
}

static string SummarizeConnectionTarget(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return "No connection string configured";
    }

    var parts = connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var safeParts = parts
        .Where(part =>
            part.StartsWith("Server=", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    return safeParts.Length == 0
        ? "Configured connection string target unavailable"
        : string.Join("; ", safeParts);
}

public partial class Program;
