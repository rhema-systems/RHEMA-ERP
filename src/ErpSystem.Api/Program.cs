using System.Text;
using ErpSystem.Api.Data;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.Middleware;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
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

// Check for HR module seeding command.
// Seeds HR reference data plus the TDC organisation structure and locations into the DEFAULT tenant.
// Idempotent: every step is skipped when its data is already present, so re-running is always safe.
// Prerequisites: 'rebuild-db' (schema) and 'seed' (DEFAULT tenant + admin user).
if (args.Length > 0 && args[0] == "seed-hr-all")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var orchestrator = new ErpSystem.Data.Seeders.HrSeedOrchestrator(context, loggerFactory);

        if (!await orchestrator.SeedAsync())
        {
            Console.WriteLine("❌ HR seeding did not complete — see the log above.");
            Environment.ExitCode = 1;
            return;
        }
    }

    Console.WriteLine("✅ HR seeding completed!");
    return;
}

// Check for workflow-only seeding command.
if (args.Length > 0 && args[0] == "seed-workflows")
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
        await db.Database.MigrateAsync();

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedWorkflowDefinitionsAsync();
    }

    Console.WriteLine("Workflow definition seeding completed!");
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
        await StampCurrentModelMigrationsAsAppliedAsync(db, logger);
        await seedingService.SeedWithoutMigrationAsync();
    }

    Console.WriteLine("✅ Database rebuild completed!");
    return;
}

if (args.Length > 0 && args[0] == "repair-finance-po-schema")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await RepairFinanceSettingsSchemaAsync(db);
        await RepairAccountingBooksSchemaAsync(db);
        await RepairCustomerPaymentSchemaAsync(db);
        await RepairFinancePurchaseOrderSchemaAsync(db);
    }

    Console.WriteLine("Finance schema repair completed.");
    return;
}

if (args.Length > 0 && args[0] == "post-finance-grv")
{
    Console.Error.WriteLine("The legacy post-finance-grv maintenance command is disabled. Finance GRV posting now runs through the receipt workflow and IFinancePostingEngine.");
    return;
}

if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"Unknown command '{args[0]}'. Valid commands: seed, seed-maintenance, seed-maintenance-e2e, seed-db, seed-workflows, rebuild-db, repair-finance-po-schema.");
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
builder.Services.AddErpSystemFinanceServices();
builder.Services.AddErpSystemJwtAuthentication(builder.Configuration);
builder.Services.AddErpSystemAuthorization();
builder.Services.AddErpSystemApi();
builder.Services.AddErpSystemHealthChecks(builder.Configuration);
builder.Services.AddErpSystemCaching(builder.Configuration);
builder.Services.AddErpSystemWebFarm(builder.Configuration);
builder.Services.AddErpSystemSearch(builder.Configuration);
builder.Services.AddErpSystemLifecycle();
builder.Services.AddErpSystemCors(builder.Configuration);
builder.Services.AddErpSystemRateLimiting();

// Forwarded headers so the app sees the REAL client IP behind a proxy/load balancer — used by rate
// limiting (per-caller partitions) and audit logging. SECURE DEFAULT: trust NO proxies, so the
// X-Forwarded-* headers are ignored (no client-IP spoofing) until an operator lists their proxy
// IPs/networks in config: ForwardedHeaders:KnownProxies (["10.0.0.5", ...]) and/or
// ForwardedHeaders:KnownNetworks (["10.0.0.0/8", ...]). Inert with empty config.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
    // Start from a clean, trust-nothing baseline.
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);
    }
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        var parts = network.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var prefixLength))
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
    }
});
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

// Apply forwarded headers FIRST so every downstream component (rate limiter, logging, audit) sees
// the real client IP. No-op unless trusted proxies/networks are configured (see registration above).
app.UseForwardedHeaders();

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
var seedWorkflowDefinitions = app.Configuration.GetValue("StartupInitialization:SeedWorkflowDefinitions", true);
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

    if (seedWorkflowDefinitions && databaseInitializationSucceeded)
    {
        app.Logger.LogInformation("Starting baseline workflow seeding...");
        try
        {
            await SeedWorkflowDefinitionsAsync(app);
            app.Logger.LogInformation("Baseline workflow seeding completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Baseline workflow seeding failed");
            if (failFastOnDevelopmentSeedError)
            {
                throw;
            }
        }
    }

    if (databaseInitializationSucceeded)
    {
        app.Logger.LogInformation("Starting baseline payment-term seeding...");
        try
        {
            await SeedPaymentTermBaselineAsync(app);
            app.Logger.LogInformation("Baseline payment-term seeding completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Baseline payment-term seeding failed");
            if (failFastOnDatabaseInitializationError)
            {
                throw;
            }
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
        await RepairFinanceSettingsSchemaAsync(context, migrationCts.Token);
        await RepairAccountingBooksSchemaAsync(context, migrationCts.Token);
        await RepairCustomerPaymentSchemaAsync(context, migrationCts.Token);
        await RepairFinancePurchaseOrderSchemaAsync(context, migrationCts.Token);
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

async Task SeedWorkflowDefinitionsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
    await seedingService.SeedWorkflowDefinitionsAsync();
}

async Task SeedPaymentTermBaselineAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<PaymentTermBaselineSeeder>();
    await seeder.SeedAllActiveTenantsAsync();
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

    var guidance =
        "Detected migration history drift in development: schema objects exist while migration history is behind. " +
        "Automatic migration stamping is disabled to avoid masking missing columns. " +
        "Recommended: recreate local dev DB and rerun migrations/seeding.";

    logger.LogError(guidance);
    throw new InvalidOperationException(guidance);
}

static async Task StampCurrentModelMigrationsAsAppliedAsync(
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

    var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString(3) ?? "9.0.0";
    var stampedCount = 0;

    foreach (var migrationId in migrationsAssembly.Migrations.Keys.OrderBy(id => id))
    {
        if (appliedMigrations.Contains(migrationId))
        {
            continue;
        }

        var insertScript = historyRepository.GetInsertScript(new HistoryRow(migrationId, productVersion));
        await context.Database.ExecuteSqlRawAsync(insertScript, cancellationToken);
        appliedMigrations.Add(migrationId);
        stampedCount++;
    }

    logger.LogInformation("Stamped {MigrationCount} EF migrations as applied after current-model database rebuild.", stampedCount);
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

static async Task RepairCustomerPaymentSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
{
    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[CustomerPayment]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[BusinessPartners]', N'U') IS NOT NULL
BEGIN
    DECLARE @legacyCustomerFk sysname;

    SELECT TOP (1) @legacyCustomerFk = fk.[name]
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc
        ON fkc.constraint_object_id = fk.[object_id]
    INNER JOIN sys.columns pc
        ON pc.[object_id] = fkc.parent_object_id
       AND pc.column_id = fkc.parent_column_id
    WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[CustomerPayment]')
      AND fk.referenced_object_id = OBJECT_ID(N'[dbo].[Customers]')
      AND pc.[name] = N'CustomerId';

    IF @legacyCustomerFk IS NOT NULL
    BEGIN
        DECLARE @dropSql nvarchar(max) =
            N'ALTER TABLE [dbo].[CustomerPayment] DROP CONSTRAINT [' + REPLACE(@legacyCustomerFk, N']', N']]') + N']';
        EXEC sp_executesql @dropSql;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE [name] = N'FK_CustomerPayment_BusinessPartners_CustomerId'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[CustomerPayment]')
    )
    BEGIN
        ALTER TABLE [dbo].[CustomerPayment] WITH NOCHECK
            ADD CONSTRAINT [FK_CustomerPayment_BusinessPartners_CustomerId]
            FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[BusinessPartners] ([Id]);
    END;
END
""", cancellationToken);
}

static async Task RepairFinanceSettingsSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
{
    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountAllowedAccountId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinanceSettings] ADD [DiscountReceivedAccountId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'ControlAccountGRVAccrualId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinanceSettings] ADD [ControlAccountGRVAccrualId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'SupplierAdvanceAccountId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinanceSettings] ADD [SupplierAdvanceAccountId] uniqueidentifier NULL;
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'CustomerAdvanceAccountId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinanceSettings] ADD [CustomerAdvanceAccountId] uniqueidentifier NULL;
    END;
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_FinanceSettings_DiscountAllowedAccountId'
              AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
       )
    BEGIN
        CREATE INDEX [IX_FinanceSettings_DiscountAllowedAccountId]
            ON [dbo].[FinanceSettings] ([DiscountAllowedAccountId]);
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_FinanceSettings_DiscountReceivedAccountId'
              AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
       )
    BEGIN
        CREATE INDEX [IX_FinanceSettings_DiscountReceivedAccountId]
            ON [dbo].[FinanceSettings] ([DiscountReceivedAccountId]);
    END;

    IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'ControlAccountGRVAccrualId') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE [name] = N'IX_FinanceSettings_ControlAccountGRVAccrualId'
              AND [object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
       )
    BEGIN
        CREATE INDEX [IX_FinanceSettings_ControlAccountGRVAccrualId]
            ON [dbo].[FinanceSettings] ([ControlAccountGRVAccrualId]);
    END;

    IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountAllowedAccountId') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountAllowedAccountId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
           )
        BEGIN
            ALTER TABLE [dbo].[FinanceSettings]
                ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountAllowedAccountId]
                FOREIGN KEY ([DiscountAllowedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
        END;

        IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'DiscountReceivedAccountId') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_FinanceSettings_Accounts_DiscountReceivedAccountId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
           )
        BEGIN
            ALTER TABLE [dbo].[FinanceSettings]
                ADD CONSTRAINT [FK_FinanceSettings_Accounts_DiscountReceivedAccountId]
                FOREIGN KEY ([DiscountReceivedAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
        END;

        IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'ControlAccountGRVAccrualId') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_FinanceSettings_Accounts_ControlAccountGRVAccrualId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
           )
        BEGIN
            ALTER TABLE [dbo].[FinanceSettings]
                ADD CONSTRAINT [FK_FinanceSettings_Accounts_ControlAccountGRVAccrualId]
                FOREIGN KEY ([ControlAccountGRVAccrualId]) REFERENCES [dbo].[Accounts] ([Id]);
        END;

        IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'SupplierAdvanceAccountId') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_FinanceSettings_Accounts_SupplierAdvanceAccountId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
           )
        BEGIN
            ALTER TABLE [dbo].[FinanceSettings]
                ADD CONSTRAINT [FK_FinanceSettings_Accounts_SupplierAdvanceAccountId]
                FOREIGN KEY ([SupplierAdvanceAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
        END;

        IF COL_LENGTH(N'[dbo].[FinanceSettings]', N'CustomerAdvanceAccountId') IS NOT NULL
           AND NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_FinanceSettings_Accounts_CustomerAdvanceAccountId'
                  AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinanceSettings]')
           )
        BEGIN
            ALTER TABLE [dbo].[FinanceSettings]
                ADD CONSTRAINT [FK_FinanceSettings_Accounts_CustomerAdvanceAccountId]
                FOREIGN KEY ([CustomerAdvanceAccountId]) REFERENCES [dbo].[Accounts] ([Id]);
        END;
    END;
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinanceSettings]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
BEGIN
        DECLARE @DefaultFinanceTenantId uniqueidentifier;

        SELECT TOP (1) @DefaultFinanceTenantId = fs.[TenantId]
        FROM [dbo].[FinanceSettings] fs
        WHERE ISNULL(fs.[IsDeleted], 0) = 0
        ORDER BY fs.[CreatedAt], fs.[Id];

        IF @DefaultFinanceTenantId IS NOT NULL
        BEGIN
            DECLARE @FinanceDefaultAccounts TABLE
            (
                [Id] uniqueidentifier NOT NULL,
                [AccountCode] nvarchar(50) NOT NULL,
                [AccountNumber] nvarchar(100) NOT NULL,
                [AccountName] nvarchar(200) NOT NULL,
                [AccountType] int NOT NULL,
                [AccountCategory] nvarchar(100) NULL,
                [AccountSubCategory] nvarchar(100) NULL,
                [Description] nvarchar(1000) NULL,
                [IsMultiCurrency] bit NOT NULL,
                [AllowDirectPosting] bit NOT NULL,
                [IsControlAccount] bit NOT NULL,
                [BudgetTrackingEnabled] bit NOT NULL
            );

            INSERT INTO @FinanceDefaultAccounts
                ([Id], [AccountCode], [AccountNumber], [AccountName], [AccountType], [AccountCategory], [AccountSubCategory], [Description], [IsMultiCurrency], [AllowDirectPosting], [IsControlAccount], [BudgetTrackingEnabled])
            VALUES
                ('00000005-2110-0000-0000-000000000001', N'2110', N'000-2110-0000', N'GRV Accrual Control', 2, N'Current Liabilities', N'Goods Received Not Invoiced', N'Dedicated control account credited when goods are received before supplier invoicing, then cleared when the AP invoice is posted.', 0, 0, 1, 0),
                ('00000005-4210-0000-0000-000000000001', N'4210', N'000-4210-0000', N'Sales Discounts Allowed', 4, N'Revenue Deductions', N'Contra Revenue', N'Contra-revenue account debited for customer trade and settlement discounts allowed.', 1, 1, 0, 1),
                ('00000005-4910-0000-0000-000000000001', N'4910', N'000-4910-0000', N'Purchase Discounts Received', 4, N'Other Income', N'Supplier Discounts', N'Income account credited for supplier trade and settlement discounts received.', 1, 1, 0, 0);

            -- Backfills accounts introduced after early finance seeds so existing local/UAT databases do not need a rebuild.
            INSERT INTO [dbo].[Accounts]
                ([Id], [AccountCode], [AccountNumber], [AccountName], [AccountType], [AccountCategory], [AccountSubCategory], [Description],
                 [ParentAccountId], [IsSegmented], [CurrencyCode], [IsMultiCurrency], [IsIFRSClassified], [IsBaseClassified], [IsLocalClassified],
                 [IFRSLineItem], [BaseLineItem], [LocalLineItem], [AllowDirectPosting], [IsControlAccount], [RequireDepartmentCode], [RequireProjectCode],
                 [BudgetTrackingEnabled], [Status], [Balance], [DebitBalance], [CreditBalance], [OpeningBalance], [LastTransactionDate],
                 [EstateModuleLinkId], [PayrollModuleLinkId], [ProcurementModuleLinkId], [TaxReportingCategory], [CashFlowClassification], [IsSystemAccount],
                 [InactivatedDate], [InactivationReason], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [CreatedById], [LastModifiedById],
                 [IsDeleted], [DeletedAt], [DeletedBy], [TenantId], [ReferenceNumber], [EffectiveDate], [ExpirationDate], [Metadata], [Tags], [Priority])
            SELECT
                seed.[Id],
                seed.[AccountCode],
                seed.[AccountNumber],
                seed.[AccountName],
                seed.[AccountType],
                seed.[AccountCategory],
                seed.[AccountSubCategory],
                seed.[Description],
                NULL,
                1,
                N'GHS',
                seed.[IsMultiCurrency],
                1,
                1,
                1,
                NULL,
                NULL,
                NULL,
                seed.[AllowDirectPosting],
                seed.[IsControlAccount],
                0,
                0,
                seed.[BudgetTrackingEnabled],
                1,
                0,
                0,
                0,
                0,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                NULL,
                1,
                NULL,
                NULL,
                SYSUTCDATETIME(),
                NULL,
                N'System',
                NULL,
                NULL,
                NULL,
                0,
                NULL,
                NULL,
                @DefaultFinanceTenantId,
                seed.[AccountCode],
                NULL,
                NULL,
                NULL,
                N'finance-default,system',
                5
            FROM @FinanceDefaultAccounts seed
            WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[Accounts] existing
                    WHERE existing.[Id] = seed.[Id]
                )
              AND NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[Accounts] existing
                    WHERE existing.[TenantId] = @DefaultFinanceTenantId
                      AND existing.[AccountCode] = seed.[AccountCode]
                      AND ISNULL(existing.[IsDeleted], 0) = 0
                );

            IF OBJECT_ID(N'[dbo].[AccountSegmentValues]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[AccountSegmentStructures]', N'U') IS NOT NULL
            BEGIN
                ;WITH SeededAccounts AS
                (
                    SELECT a.[Id], a.[TenantId], a.[AccountCode], a.[AccountName]
                    FROM [dbo].[Accounts] a
                    INNER JOIN @FinanceDefaultAccounts seed ON seed.[AccountCode] = a.[AccountCode]
                    WHERE a.[TenantId] = @DefaultFinanceTenantId
                      AND ISNULL(a.[IsDeleted], 0) = 0
                ),
                TargetSegments AS
                (
                    SELECT s.[Id], s.[TenantId], s.[SegmentCode], s.[SegmentPosition], s.[IsNaturalAccount]
                    FROM [dbo].[AccountSegmentStructures] s
                    WHERE s.[TenantId] = @DefaultFinanceTenantId
                      AND ISNULL(s.[IsDeleted], 0) = 0
                      AND (
                            (s.[SegmentCode] = N'DEPT' AND s.[SegmentPosition] = 1)
                         OR (s.[IsNaturalAccount] = 1)
                         OR (s.[SegmentCode] = N'PROJ' AND s.[SegmentPosition] = 3)
                      )
                )
                INSERT INTO [dbo].[AccountSegmentValues]
                    ([Id], [AccountId], [SegmentStructureId], [SegmentValue], [SegmentLookupValueId], [SegmentValueDescription],
                     [SegmentPosition], [IsLocked], [EffectiveDate], [EndDate], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy],
                     [CreatedById], [LastModifiedById], [IsDeleted], [DeletedAt], [DeletedBy], [TenantId])
                SELECT
                    NEWID(),
                    account.[Id],
                    segment.[Id],
                    CASE
                        WHEN segment.[SegmentCode] = N'DEPT' THEN N'000'
                        WHEN segment.[IsNaturalAccount] = 1 THEN account.[AccountCode]
                        WHEN segment.[SegmentCode] = N'PROJ' THEN N'0000'
                    END,
                    lookupValue.[Id],
                    CASE
                        WHEN segment.[IsNaturalAccount] = 1 THEN account.[AccountName]
                        ELSE lookupValue.[Description]
                    END,
                    segment.[SegmentPosition],
                    0,
                    SYSUTCDATETIME(),
                    NULL,
                    SYSUTCDATETIME(),
                    NULL,
                    N'System',
                    NULL,
                    NULL,
                    NULL,
                    0,
                    NULL,
                    NULL,
                    account.[TenantId]
                FROM SeededAccounts account
                INNER JOIN TargetSegments segment ON segment.[TenantId] = account.[TenantId]
                OUTER APPLY
                (
                    SELECT TOP (1) lookup.[Id], lookup.[Description]
                    FROM [dbo].[SegmentLookupValues] lookup
                    WHERE lookup.[TenantId] = account.[TenantId]
                      AND lookup.[SegmentStructureId] = segment.[Id]
                      AND lookup.[SegmentValue] = CASE
                            WHEN segment.[SegmentCode] = N'DEPT' THEN N'000'
                            WHEN segment.[SegmentCode] = N'PROJ' THEN N'0000'
                            ELSE account.[AccountCode]
                          END
                      AND ISNULL(lookup.[IsDeleted], 0) = 0
                ) lookupValue
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[AccountSegmentValues] existing
                    WHERE existing.[AccountId] = account.[Id]
                      AND existing.[SegmentStructureId] = segment.[Id]
                      AND ISNULL(existing.[IsDeleted], 0) = 0
                );
            END;
        END;

        UPDATE fs
            SET [DiscountAllowedAccountId] = account.[Id]
        FROM [dbo].[FinanceSettings] fs
        CROSS APPLY (
            SELECT TOP (1) a.[Id]
            FROM [dbo].[Accounts] a
            WHERE a.[TenantId] = fs.[TenantId]
              AND ISNULL(a.[IsDeleted], 0) = 0
              AND (a.[Id] = '00000005-4210-0000-0000-000000000001' OR a.[AccountCode] = N'4210')
            ORDER BY CASE WHEN a.[Id] = '00000005-4210-0000-0000-000000000001' THEN 0 ELSE 1 END
        ) account
        WHERE fs.[DiscountAllowedAccountId] IS NULL
          AND ISNULL(fs.[IsDeleted], 0) = 0;

        UPDATE fs
            SET [DiscountReceivedAccountId] = account.[Id]
        FROM [dbo].[FinanceSettings] fs
        CROSS APPLY (
            SELECT TOP (1) a.[Id]
            FROM [dbo].[Accounts] a
            WHERE a.[TenantId] = fs.[TenantId]
              AND ISNULL(a.[IsDeleted], 0) = 0
              AND (a.[Id] = '00000005-4910-0000-0000-000000000001' OR a.[AccountCode] = N'4910')
            ORDER BY CASE WHEN a.[Id] = '00000005-4910-0000-0000-000000000001' THEN 0 ELSE 1 END
        ) account
        WHERE fs.[DiscountReceivedAccountId] IS NULL
          AND ISNULL(fs.[IsDeleted], 0) = 0;

        UPDATE fs
            SET [ControlAccountGRVAccrualId] = account.[Id]
        FROM [dbo].[FinanceSettings] fs
        CROSS APPLY (
            SELECT TOP (1) a.[Id]
            FROM [dbo].[Accounts] a
            WHERE a.[TenantId] = fs.[TenantId]
              AND ISNULL(a.[IsDeleted], 0) = 0
              AND (a.[Id] = '00000005-2110-0000-0000-000000000001' OR a.[AccountCode] = N'2110')
            ORDER BY CASE WHEN a.[Id] = '00000005-2110-0000-0000-000000000001' THEN 0 ELSE 1 END
        ) account
        WHERE (fs.[ControlAccountGRVAccrualId] IS NULL
            OR fs.[ControlAccountGRVAccrualId] = '00000005-2100-0000-0000-000000000001')
          AND ISNULL(fs.[IsDeleted], 0) = 0;
END
""", cancellationToken);
}

static async Task RepairFinancePurchaseOrderSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
{
    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinancePurchaseOrders]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FinancePurchaseOrders] (
        [Id] uniqueidentifier NOT NULL,
        [OrderNumber] nvarchar(50) NOT NULL,
        [VendorId] uniqueidentifier NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [ExpectedDeliveryDate] datetime2 NULL,
        [PaymentTermId] uniqueidentifier NULL,
        [Status] int NOT NULL,
        [CurrencyCode] nvarchar(3) NOT NULL,
        [ExchangeRate] decimal(18,4) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_FinancePurchaseOrders_DiscountAmount] DEFAULT 0,
        [TaxGroupId] uniqueidentifier NULL,
        [Remarks] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_FinancePurchaseOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrders_BusinessPartners_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[BusinessPartners] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId] FOREIGN KEY ([PaymentTermId]) REFERENCES [dbo].[PaymentTerms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePurchaseOrders_TaxGroups_TaxGroupId] FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrders_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_FinancePurchaseOrders_TenantId] ON [dbo].[FinancePurchaseOrders] ([TenantId]);
    CREATE INDEX [IX_FinancePurchaseOrders_VendorId] ON [dbo].[FinancePurchaseOrders] ([VendorId]);
    CREATE INDEX [IX_FinancePurchaseOrders_PaymentTermId] ON [dbo].[FinancePurchaseOrders] ([PaymentTermId]);
    CREATE INDEX [IX_FinancePurchaseOrders_TaxGroupId] ON [dbo].[FinancePurchaseOrders] ([TaxGroupId]);
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinancePurchaseOrders]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.FinancePurchaseOrders', N'PaymentTermId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[FinancePurchaseOrders] ADD [PaymentTermId] uniqueidentifier NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_FinancePurchaseOrders_PaymentTermId'
          AND [object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
    )
    BEGIN
        CREATE INDEX [IX_FinancePurchaseOrders_PaymentTermId] ON [dbo].[FinancePurchaseOrders] ([PaymentTermId]);
    END;

    IF OBJECT_ID(N'[dbo].[PaymentTerms]', N'U') IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE [name] = N'FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId'
              AND [parent_object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
       )
    BEGIN
        ALTER TABLE [dbo].[FinancePurchaseOrders]
            ADD CONSTRAINT [FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId]
            FOREIGN KEY ([PaymentTermId]) REFERENCES [dbo].[PaymentTerms] ([Id]) ON DELETE NO ACTION;
    END;
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinancePurchaseOrderItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FinancePurchaseOrderItems] (
        [Id] uniqueidentifier NOT NULL,
        [FinancePurchaseOrderId] uniqueidentifier NOT NULL,
        [LineType] int NOT NULL,
        [InventoryItemId] uniqueidentifier NULL,
        [WarehouseId] uniqueidentifier NULL,
        [GlAccountId] uniqueidentifier NULL,
        [Description] nvarchar(500) NOT NULL,
        [OrderedQuantity] decimal(18,4) NOT NULL,
        [ReceivedQuantity] decimal(18,4) NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [CancelledQuantity] decimal(18,4) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [CurrencyCode] nvarchar(3) NULL,
        [ExchangeRate] decimal(18,4) NOT NULL,
        [TaxCode] nvarchar(50) NULL,
        [TaxRate] decimal(18,4) NOT NULL,
        [TaxAmount] decimal(18,2) NOT NULL,
        [TaxGroupId] uniqueidentifier NULL,
        [DiscountPercentage] decimal(18,4) NOT NULL CONSTRAINT [DF_FinancePurchaseOrderItems_DiscountPercentage] DEFAULT 0,
        [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_FinancePurchaseOrderItems_DiscountAmount] DEFAULT 0,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_FinancePurchaseOrderItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderItems_Accounts_GlAccountId] FOREIGN KEY ([GlAccountId]) REFERENCES [dbo].[Accounts] ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderItems_FinancePurchaseOrders_FinancePurchaseOrderId] FOREIGN KEY ([FinancePurchaseOrderId]) REFERENCES [dbo].[FinancePurchaseOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FinancePurchaseOrderItems_InventoryItems_InventoryItemId] FOREIGN KEY ([InventoryItemId]) REFERENCES [dbo].[InventoryItems] ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderItems_TaxGroups_TaxGroupId] FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_FinancePurchaseOrderItems_FinancePurchaseOrderId] ON [dbo].[FinancePurchaseOrderItems] ([FinancePurchaseOrderId]);
    CREATE INDEX [IX_FinancePurchaseOrderItems_GlAccountId] ON [dbo].[FinancePurchaseOrderItems] ([GlAccountId]);
    CREATE INDEX [IX_FinancePurchaseOrderItems_InventoryItemId] ON [dbo].[FinancePurchaseOrderItems] ([InventoryItemId]);
    CREATE INDEX [IX_FinancePurchaseOrderItems_TaxGroupId] ON [dbo].[FinancePurchaseOrderItems] ([TaxGroupId]);
    CREATE INDEX [IX_FinancePurchaseOrderItems_TenantId] ON [dbo].[FinancePurchaseOrderItems] ([TenantId]);
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinancePurchaseOrderReceipts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FinancePurchaseOrderReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [FinancePurchaseOrderId] uniqueidentifier NOT NULL,
        [ReceiptNumber] nvarchar(50) NOT NULL,
        [ReceiptDate] datetime2 NOT NULL,
        [Remarks] nvarchar(500) NULL,
        [VendorInvoiceId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_FinancePurchaseOrderReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderReceipts_FinancePurchaseOrders_FinancePurchaseOrderId] FOREIGN KEY ([FinancePurchaseOrderId]) REFERENCES [dbo].[FinancePurchaseOrders] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FinancePurchaseOrderReceipts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePurchaseOrderReceipts_VendorInvoice_VendorInvoiceId] FOREIGN KEY ([VendorInvoiceId]) REFERENCES [dbo].[VendorInvoice] ([Id])
    );

    CREATE INDEX [IX_FinancePurchaseOrderReceipts_FinancePurchaseOrderId] ON [dbo].[FinancePurchaseOrderReceipts] ([FinancePurchaseOrderId]);
    CREATE INDEX [IX_FinancePurchaseOrderReceipts_TenantId] ON [dbo].[FinancePurchaseOrderReceipts] ([TenantId]);
    CREATE INDEX [IX_FinancePurchaseOrderReceipts_VendorInvoiceId] ON [dbo].[FinancePurchaseOrderReceipts] ([VendorInvoiceId]);
END
""", cancellationToken);

    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[FinancePurchaseOrderReceiptItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[FinancePurchaseOrderReceiptItems] (
        [Id] uniqueidentifier NOT NULL,
        [FinancePurchaseOrderReceiptId] uniqueidentifier NOT NULL,
        [FinancePurchaseOrderItemId] uniqueidentifier NOT NULL,
        [QuantityReceived] decimal(18,4) NOT NULL,
        [InvoicedQuantity] decimal(18,4) NOT NULL,
        [DiscountPercentage] decimal(18,4) NOT NULL CONSTRAINT [DF_FinancePurchaseOrderReceiptItems_DiscountPercentage] DEFAULT 0,
        [DiscountAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_FinancePurchaseOrderReceiptItems_DiscountAmount] DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_FinancePurchaseOrderReceiptItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderItems_FinancePurchaseOrderItemId] FOREIGN KEY ([FinancePurchaseOrderItemId]) REFERENCES [dbo].[FinancePurchaseOrderItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderReceipts_FinancePurchaseOrderReceiptId] FOREIGN KEY ([FinancePurchaseOrderReceiptId]) REFERENCES [dbo].[FinancePurchaseOrderReceipts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FinancePurchaseOrderReceiptItems_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderItemId] ON [dbo].[FinancePurchaseOrderReceiptItems] ([FinancePurchaseOrderItemId]);
    CREATE INDEX [IX_FinancePurchaseOrderReceiptItems_FinancePurchaseOrderReceiptId] ON [dbo].[FinancePurchaseOrderReceiptItems] ([FinancePurchaseOrderReceiptId]);
    CREATE INDEX [IX_FinancePurchaseOrderReceiptItems_TenantId] ON [dbo].[FinancePurchaseOrderReceiptItems] ([TenantId]);
END
""", cancellationToken);
}

static async Task RepairAccountingBooksSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
{
    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccountingBooks] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Purpose] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [AllowsPosting] bit NOT NULL,
        [IsSystemDefined] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountingBooks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountingBooks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_AccountingBooks_TenantId_Code'
          AND [object_id] = OBJECT_ID(N'[dbo].[AccountingBooks]')
   )
BEGIN
    CREATE UNIQUE INDEX [IX_AccountingBooks_TenantId_Code]
        ON [dbo].[AccountingBooks] ([TenantId], [Code]);
END;

IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccountAccountingBooks] (
        [Id] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [AccountingBookId] uniqueidentifier NOT NULL,
        [IsEnabled] bit NOT NULL,
        [FinancialStatementLineItem] nvarchar(100) NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [CreatedById] uniqueidentifier NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        [LastModifiedById] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_AccountAccountingBooks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccountAccountingBooks_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AccountAccountingBooks_AccountingBooks_AccountingBookId] FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AccountAccountingBooks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_AccountAccountingBooks_AccountId'
          AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
   )
BEGIN
    CREATE INDEX [IX_AccountAccountingBooks_AccountId]
        ON [dbo].[AccountAccountingBooks] ([AccountId]);
END;

IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_AccountAccountingBooks_AccountingBookId'
          AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
   )
BEGIN
    CREATE INDEX [IX_AccountAccountingBooks_AccountingBookId]
        ON [dbo].[AccountAccountingBooks] ([AccountingBookId]);
END;

IF OBJECT_ID(N'[dbo].[AccountAccountingBooks]', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE [name] = N'IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId'
          AND [object_id] = OBJECT_ID(N'[dbo].[AccountAccountingBooks]')
   )
BEGIN
    CREATE UNIQUE INDEX [IX_AccountAccountingBooks_TenantId_AccountId_AccountingBookId]
        ON [dbo].[AccountAccountingBooks] ([TenantId], [AccountId], [AccountingBookId]);
END;
""", cancellationToken);
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

sealed class MaintenanceCurrentUserContext : ErpSystem.Core.Interfaces.ICurrentUserService, ErpSystem.Core.Interfaces.ICurrentUserProvider
{
    private static readonly Guid SystemUserId = Guid.Empty;
    private static readonly string[] SystemRoles = ["SuperAdmin"];
    private readonly Guid _tenantId;

    public MaintenanceCurrentUserContext(Guid tenantId)
    {
        _tenantId = tenantId;
    }

    public string? UserId => SystemUserId.ToString();
    public string? UserName => "system";
    public string? Email => "system@local";
    public Guid? TenantId => _tenantId;
    public Guid? EmployeeId => null;
    public bool IsAuthenticated => true;
    public IEnumerable<string> Roles => SystemRoles;
    public string? IpAddress => null;
    public string? UserAgent => "MaintenanceCommand";
    public bool IsInRole(string role) => HasRole(role);

    Guid ErpSystem.Core.Interfaces.ICurrentUserProvider.UserId => SystemUserId;
    Guid ErpSystem.Core.Interfaces.ICurrentUserProvider.TenantId => _tenantId;
    public string Username => UserName!;
    public string FullName => "System";
    public bool HasRole(string role) => SystemRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public IDictionary<string, string> Claims => new Dictionary<string, string>
    {
        ["tenant_id"] = _tenantId.ToString()
    };
    public bool IsExternalUser => false;
    public string AuthenticationProvider => "MaintenanceCommand";
}

class NoopServiceProxy : System.Reflection.DispatchProxy
{
    protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
        {
            return null;
        }

        var returnType = targetMethod.ReturnType;
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [defaultValue]);
        }

        if (returnType == typeof(ValueTask))
        {
            return ValueTask.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return Activator.CreateInstance(returnType, defaultValue);
        }

        return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
    }

    public static T Create<T>() where T : class
    {
        return System.Reflection.DispatchProxy.Create<T, NoopServiceProxy>();
    }
}

public partial class Program;
