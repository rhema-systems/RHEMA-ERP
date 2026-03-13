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
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Check for seed command
if (args.Length > 0 && args[0] == "seed")
{
    var tempBuilder = WebApplication.CreateBuilder(args);

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();

    var tempApp = tempBuilder.Build();

    // Run user seeding
    await ErpSystem.Api.UserSeeder.SeedTestUsersAsync(tempApp.Services);
    return;
}

// Check for maintenance workflow seeding command
if (args.Length > 0 && args[0] == "seed-maintenance")
{
    var tempBuilder = WebApplication.CreateBuilder(args);

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
    var tempBuilder = WebApplication.CreateBuilder(args);

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
    var tempBuilder = WebApplication.CreateBuilder(args);

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

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Testing")
    && string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=ErpSystem_TestHost;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
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

// Add HTTP request/response logging (after exception handling)
app.UseMiddleware<HttpLoggingMiddleware>();

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

// Add Serilog request logging
app.UseSerilogRequestLogging();

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

// Rate limiting should be after CORS but before authentication
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<JwtBlacklistMiddleware>();
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

if (!skipStartupInitialization)
{
    // Initialize database and seed data
    app.Logger.LogInformation("Starting database initialization...");
    try
    {
        await InitializeDatabaseAsync(app);
        app.Logger.LogInformation("Database initialization completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database initialization failed");
    }

    // Seed demo/basic data in Development to make local testing easier.
    if (app.Environment.IsDevelopment())
    {
        app.Logger.LogInformation("Starting Development data seeding...");
        await SeedDatabaseAsync(app);
        app.Logger.LogInformation("Development data seeding completed");
    }
}
else
{
    app.Logger.LogInformation("Skipping startup database initialization for environment {EnvironmentName}", app.Environment.EnvironmentName);
}

// Workflow automation trigger - comprehensive testing active
// Version: 2.0.0 - Full CI/CD Pipeline Integration
app.Run();

async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogDebug("Testing database connection...");

        // Test connection first with a longer timeout
        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var canConnect = await context.Database.CanConnectAsync(testCts.Token);

        if (!canConnect)
        {
            Console.WriteLine("   ❌ Cannot connect to database (Timeout 30s) - Initialization aborted");
            logger.LogError("Cannot connect to database - Initialization aborted");
            return;
        }

        logger.LogInformation("Database connection successful. Running migrations...");

        // ── TEMPORARY HOTFIX: Mark already-applied migrations & add missing columns ──
        // The FixRuntimeSchemaIssues migration was deleted (it tried to drop non-existent FKs).
        // The FixBusinessPartnerSchema columns already exist from an earlier SQL hotfix.
        // Additional customer-specific columns are also missing from the database.
        try
        {
            // 1. Mark migrations as applied
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260212203425_FixRuntimeSchemaIssues')
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260212203425_FixRuntimeSchemaIssues', '8.0.0');
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260215231931_FixBusinessPartnerSchema')
                    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260215231931_FixBusinessPartnerSchema', '8.0.0');
            ");

            // 2. Add ALL missing BusinessPartner customer-specific columns
            await context.Database.ExecuteSqlRawAsync(@"
                IF COL_LENGTH('BusinessPartners','CustomerAccountNumber') IS NULL ALTER TABLE [BusinessPartners] ADD [CustomerAccountNumber] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','CustomerType') IS NULL ALTER TABLE [BusinessPartners] ADD [CustomerType] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','CreditLimit') IS NULL ALTER TABLE [BusinessPartners] ADD [CreditLimit] decimal(18,2) NULL;
                IF COL_LENGTH('BusinessPartners','OutstandingBalance') IS NULL ALTER TABLE [BusinessPartners] ADD [OutstandingBalance] decimal(18,2) NULL;
                IF COL_LENGTH('BusinessPartners','PaymentTerms') IS NULL ALTER TABLE [BusinessPartners] ADD [PaymentTerms] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','Currency') IS NULL ALTER TABLE [BusinessPartners] ADD [Currency] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','DefaultDiscount') IS NULL ALTER TABLE [BusinessPartners] ADD [DefaultDiscount] decimal(5,2) NULL;
                IF COL_LENGTH('BusinessPartners','PriceList') IS NULL ALTER TABLE [BusinessPartners] ADD [PriceList] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','SalesRepresentativeId') IS NULL ALTER TABLE [BusinessPartners] ADD [SalesRepresentativeId] uniqueidentifier NULL;
                IF COL_LENGTH('BusinessPartners','SalesTerritory') IS NULL ALTER TABLE [BusinessPartners] ADD [SalesTerritory] nvarchar(100) NULL;
                IF COL_LENGTH('BusinessPartners','IsTaxExempt') IS NULL ALTER TABLE [BusinessPartners] ADD [IsTaxExempt] bit NOT NULL DEFAULT 0;
                IF COL_LENGTH('BusinessPartners','TaxExemptionNumber') IS NULL ALTER TABLE [BusinessPartners] ADD [TaxExemptionNumber] nvarchar(100) NULL;
                IF COL_LENGTH('BusinessPartners','TaxExemptionExpiry') IS NULL ALTER TABLE [BusinessPartners] ADD [TaxExemptionExpiry] datetime2 NULL;
                IF COL_LENGTH('BusinessPartners','PreferredShippingMethod') IS NULL ALTER TABLE [BusinessPartners] ADD [PreferredShippingMethod] nvarchar(100) NULL;
                IF COL_LENGTH('BusinessPartners','DeliveryInstructions') IS NULL ALTER TABLE [BusinessPartners] ADD [DeliveryInstructions] nvarchar(500) NULL;
                IF COL_LENGTH('BusinessPartners','CustomerSince') IS NULL ALTER TABLE [BusinessPartners] ADD [CustomerSince] datetime2 NULL;
                IF COL_LENGTH('BusinessPartners','LastPurchaseDate') IS NULL ALTER TABLE [BusinessPartners] ADD [LastPurchaseDate] datetime2 NULL;
                IF COL_LENGTH('BusinessPartners','TotalLifetimePurchases') IS NULL ALTER TABLE [BusinessPartners] ADD [TotalLifetimePurchases] decimal(18,2) NULL;
                IF COL_LENGTH('BusinessPartners','AverageOrderValue') IS NULL ALTER TABLE [BusinessPartners] ADD [AverageOrderValue] decimal(18,2) NULL;
                IF COL_LENGTH('BusinessPartners','LoyaltyTier') IS NULL ALTER TABLE [BusinessPartners] ADD [LoyaltyTier] nvarchar(50) NULL;
                IF COL_LENGTH('BusinessPartners','LoyaltyPoints') IS NULL ALTER TABLE [BusinessPartners] ADD [LoyaltyPoints] int NULL;
                IF COL_LENGTH('BusinessPartners','IsOnCreditHold') IS NULL ALTER TABLE [BusinessPartners] ADD [IsOnCreditHold] bit NOT NULL DEFAULT 0;
                IF COL_LENGTH('BusinessPartners','CreditHoldReason') IS NULL ALTER TABLE [BusinessPartners] ADD [CreditHoldReason] nvarchar(500) NULL;
                IF COL_LENGTH('BusinessPartners','CreditHoldDate') IS NULL ALTER TABLE [BusinessPartners] ADD [CreditHoldDate] datetime2 NULL;
                IF COL_LENGTH('BusinessPartners','ParentId') IS NULL ALTER TABLE [BusinessPartners] ADD [ParentId] uniqueidentifier NULL;
                IF COL_LENGTH('BusinessPartners','UserId') IS NULL ALTER TABLE [BusinessPartners] ADD [UserId] uniqueidentifier NULL;
            ");
            Console.WriteLine("   ✅ Migration history records patched & missing columns added");
        }
        catch (Exception patchEx)
        {
            Console.WriteLine($"   ⚠️ Schema hotfix warning: {patchEx.Message}");
        }
        // ── END HOTFIX ──

        // Add timeout to prevent hanging
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await context.Database.MigrateAsync(cts.Token);

        logger.LogInformation("Database migration completed successfully");
    }
    catch (OperationCanceledException ex)
    {
        logger.LogError(ex, "Database operation timed out. Check if SQL Server is running and reachable.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database");
    }
}

async Task SeedDatabaseAsync(WebApplication app)
{
    try
    {
        await app.Services.SeedDatabaseAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}

public partial class Program;
