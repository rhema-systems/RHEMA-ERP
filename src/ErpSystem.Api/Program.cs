using System.Text;
using System.Data;
using ErpSystem.Api.Configuration;
using ErpSystem.Api.Data;
using ErpSystem.Api.Extensions;
using ErpSystem.Api.HealthChecks;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Syncfusion.Licensing;

// Apply pending migrations without starting the web host or running seeders.
// This is the controlled test/deployment database update entry point.
if (args.Length > 0 && args[0] == "apply-migrations")
{
    var migrationCommandOptions = MigrationCommandOptions.Parse(args);

    // Migration-only deployments must keep ASP.NET Core's Production default
    // when ASPNETCORE_ENVIRONMENT is absent. Development is a convenience for
    // explicit seed commands only and could select the wrong database here.
    var tempBuilder = WebApplication.CreateBuilder(args);
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddHttpContextAccessor(); // Required by AuditInterceptor on the audited DbContext.
    tempBuilder.Services.AddErpSystemCliDatabase(
        tempBuilder.Configuration,
        migrationCommandOptions.CommandTimeoutSeconds);
    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        migrationCommandOptions.ApplyAndAssertTo(db.Database);
        Console.WriteLine($"RHEMA_MIGRATION_COMMAND_TIMEOUT_SECONDS={migrationCommandOptions.CommandTimeoutSeconds}");
        await db.Database.MigrateAsync();
    }

    Console.WriteLine("Database migrations completed successfully.");
    return;
}

// Add missing TDC demonstration values to the DEFAULT tenant's existing Finance
// transaction dimensions. This command never assigns defaults or posts accounting data.
if (args.Length > 0 && args[0] == "seed-finance-demo-dimensions")
{
    var dimensionBuilder = CreateSeedBuilder(args);
    dimensionBuilder.Services.AddErpSystemLogging(dimensionBuilder.Configuration);
    dimensionBuilder.Services.AddHttpContextAccessor();
    dimensionBuilder.Services.AddErpSystemDatabase(dimensionBuilder.Configuration);
    var dimensionApp = dimensionBuilder.Build();

    using (var scope = dimensionApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == "DEFAULT" && !item.IsDeleted);
        if (tenant is null)
            throw new InvalidOperationException("FINANCE_DEMO_DIMENSION_TENANT_MISSING: DEFAULT tenant was not found.");

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FinanceDemoDimensionValueSeeder>>();
        await new FinanceDemoDimensionValueSeeder(db, logger).SeedAsync(tenant.Id, DateTime.UtcNow);
    }

    Console.WriteLine("Finance demo transaction dimensions seeded successfully.");
    return;
}

// Converge the DEFAULT tenant's untouched standard Finance books to the executable
// out-of-box baseline. The seeder refuses to overwrite user-touched books or books
// with economic activity, and only fills canonical posting identities that have no
// approved applicability rule.
if (args.Length > 0 && args[0] == "seed-finance-baseline")
{
    var baselineBuilder = CreateSeedBuilder(args);
    baselineBuilder.Services.AddErpSystemLogging(baselineBuilder.Configuration);
    baselineBuilder.Services.AddHttpContextAccessor();
    baselineBuilder.Services.AddErpSystemDatabase(baselineBuilder.Configuration);
    baselineBuilder.Services.AddErpSystemIdentity();
    baselineBuilder.Services.AddDatabaseSeeding();
    var baselineApp = baselineBuilder.Build();

    using (var scope = baselineApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Code == "DEFAULT" && !item.IsDeleted);
        if (tenant is null)
            throw new InvalidOperationException("FINANCE_BASELINE_TENANT_MISSING: DEFAULT tenant was not found.");

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FinanceBaselineProvisioningSeeder>>();
        await new FinanceBaselineProvisioningSeeder(db, logger).SeedAsync(tenant.Id, DateTime.UtcNow);
        var taxLogger = scope.ServiceProvider.GetRequiredService<ILogger<FinanceTaxAccountProvisioningSeeder>>();
        await new FinanceTaxAccountProvisioningSeeder(db, taxLogger).SeedAsync(tenant.Id);

        // Protected statement layouts depend on the canonical books and classification
        // hierarchy established by the Finance baseline. Provision them through this same
        // explicit, idempotent command so hosts that skip broad startup initialization still
        // receive the clone-only reporting standards.
        var statementLayoutLogger = scope.ServiceProvider
            .GetRequiredService<ILogger<FinanceFinancialStatementStandardSeeder>>();
        await new FinanceFinancialStatementStandardSeeder(db, statementLayoutLogger)
            .SeedAsync(tenant.Id, DateTime.UtcNow);

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedFinanceWorkflowDefinitionsAsync();
    }

    Console.WriteLine("Finance executable baseline provisioning completed successfully.");
    return;
}

// Ensure Estate land-bank demonstration parcels exist for every active tenant.
// This is safe to run on a deployed host without enabling broad development seeding.
if (args.Length > 0 && args[0] == "seed-estate-land-bank")
{
    var migrationCommandOptions = MigrationCommandOptions.Parse(args);
    var tempBuilder = CreateSeedBuilder(args);
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddHttpContextAccessor();
    tempBuilder.Services.AddErpSystemCliDatabase(
        tempBuilder.Configuration,
        migrationCommandOptions.CommandTimeoutSeconds);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        migrationCommandOptions.ApplyAndAssertTo(db.Database);
        Console.WriteLine($"RHEMA_MIGRATION_COMMAND_TIMEOUT_SECONDS={migrationCommandOptions.CommandTimeoutSeconds}");
        await db.Database.MigrateAsync();

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedEstateAcquisitionLandBankParcelsAsync();
    }

    Console.WriteLine("Estate acquisition land bank parcel seeding completed successfully.");
    return;
}

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

// Explicit test-only preparation. Never runs during web startup or production deployment.
if (args.Length > 0 && args[0] == "seed-qs-uat")
{
    await ErpSystem.Api.Services.QsUatCommand.RunAsync(CreateSeedBuilder(args));
    return;
}

// Reconcile reusable Procurement, Inventory, Finance and connected QS UAT actors
// and master data. Existing passwords and tenant-owned master records are preserved.
if (args.Length > 0 && args[0] == "seed-operational-uat")
{
    var migrationCommandOptions = MigrationCommandOptions.Parse(args);
    var tempBuilder = CreateSeedBuilder(args);
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddHttpContextAccessor();
    tempBuilder.Services.AddErpSystemCliDatabase(tempBuilder.Configuration, migrationCommandOptions.CommandTimeoutSeconds);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();
    using (var scope = tempApp.Services.CreateScope())
    {
        var result = await scope.ServiceProvider.GetRequiredService<ErpSystem.Api.Services.OperationalUatBaselineSeeder>()
            .SeedAsync();
        Console.WriteLine(
            $"OPERATIONAL_UAT_RESULT|users={result.CreatedUsers}|roles={result.AddedRoleAssignments}|" +
            $"uom={result.CreatedUnitsOfMeasure}|categories={result.CreatedCategories}|" +
            $"warehouses={result.CreatedWarehouses}|locations={result.CreatedLocations}|" +
            $"items={result.CreatedItems}|suppliers={result.CreatedSuppliers}|" +
            $"responsibilities={result.CreatedResponsibilityAssignments}");
    }

    Console.WriteLine("Operational UAT baseline seeding completed successfully.");
    return;
}

// Create the narrowly scoped, role-separated fixture used by the disposable Civil
// Engineering browser acceptance harness. The seeder itself refuses non-test DB names.
if (args.Length > 0 && args[0] == "seed-civil-e2e")
{
    var tempBuilder = CreateSeedBuilder(args);
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();
    tempBuilder.Services.AddScoped<CivilEngineeringConfigurationProfileSeeder>();
    tempBuilder.Services.AddScoped<CivilEngineeringAccessControlSeeder>();
    tempBuilder.Services.AddScoped<ErpSystem.Api.Services.CivilEngineeringE2ETestSeeder>();

    var tempApp = tempBuilder.Build();
    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await db.Database.EnsureCreatedAsync();
        await StampCurrentModelMigrationsAsAppliedAsync(db, logger);

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedTestUsersAsync();
        await scope.ServiceProvider.GetRequiredService<ErpSystem.Api.Services.CivilEngineeringE2ETestSeeder>()
            .SeedAsync();
    }

    Console.WriteLine("Civil Engineering disposable browser fixture seeded successfully.");
    return;
}

// Create role-separated actors and prerequisite reference/source data used by the
// disposable tender browser acceptance harness. Governed lifecycle transitions are
// still performed by the real APIs so each browser transition remains verifiable.
if (args.Length > 0 && args[0] == "seed-tender-e2e")
{
    var tempBuilder = CreateSeedBuilder(args);
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();
    tempBuilder.Services.AddScoped<ErpSystem.Api.Services.TenderLifecycleE2ETestSeeder>();

    var tempApp = tempBuilder.Build();
    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        await StampCurrentModelMigrationsAsAppliedAsync(
            db,
            scope.ServiceProvider.GetRequiredService<ILogger<Program>>());

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedTestUsersAsync();
        await scope.ServiceProvider.GetRequiredService<ProcurementAccessControlSeeder>()
            .SeedAsync();
        await scope.ServiceProvider.GetRequiredService<ErpSystem.Api.Services.TenderLifecycleE2ETestSeeder>()
            .SeedAsync();
    }

    Console.WriteLine("Tender disposable browser prerequisite fixture seeded successfully.");
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
if (args.Length > 0 && args[0] is "seed-db" or "seed-deployment-uat")
{
    var migrationCommandOptions = MigrationCommandOptions.Parse(args);
    var tempBuilder = CreateSeedBuilder(args);
    var includeOperationalUat = args[0] == "seed-deployment-uat";
    if (includeOperationalUat && string.IsNullOrWhiteSpace(tempBuilder.Configuration["UatBootstrap:SharedPassword"]))
        throw new InvalidOperationException("Deployment UAT seeding requires the protected UatBootstrap__SharedPassword setting.");

    // Configure services for seeding
    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemCliDatabase(
        tempBuilder.Configuration,
        migrationCommandOptions.CommandTimeoutSeconds);
    tempBuilder.Services.AddErpSystemIdentity();
    tempBuilder.Services.AddDatabaseSeeding();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        // Apply migrations first so seeding is safe in all environments.
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        migrationCommandOptions.ApplyAndAssertTo(db.Database);
        Console.WriteLine($"RHEMA_MIGRATION_COMMAND_TIMEOUT_SECONDS={migrationCommandOptions.CommandTimeoutSeconds}");
        await db.Database.MigrateAsync();

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedAsync();
        if (includeOperationalUat)
        {
            db.ChangeTracker.Clear();
            await scope.ServiceProvider.GetRequiredService<ErpSystem.Api.Services.OperationalUatBaselineSeeder>().SeedAsync();
        }
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
    tempBuilder.Services.AddHttpContextAccessor();
    tempBuilder.Services.AddScoped<ErpSystem.Core.Interfaces.ICurrentUserProvider, ErpSystem.Api.Services.CurrentUserService>();
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemIdentity();

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

// Gives the organisation an authority hierarchy — a head on every unit and a line manager on every
// employee — so the rules that read reporting lines (FR-HR-080's issuing authority, FR-HR-181's
// grievance ladder, FR-HR-084's responder matrix) have something to resolve against.
//
// ⚠ SEPARATE FROM 'seed-hr-all' ON PURPOSE. That command seeds TDC's REAL organisation structure;
// who heads which unit is fact of the same kind and TDC has not supplied it, so inventing it there
// would put fabricated management lines behind a command that is otherwise trustworthy. This one
// never overwrites an existing head or manager, so running it where the real hierarchy has been
// entered does nothing.
if (args.Length > 0 && args[0] == "seed-hr-org-authority")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        var tenant = await context.Set<ErpSystem.Core.Entities.Tenant>()
            .FirstOrDefaultAsync(t => t.Code == "DEFAULT");
        if (tenant is null)
        {
            Console.WriteLine("❌ DEFAULT tenant not found. Run 'rebuild-db', then 'seed', then 'seed-hr-all'.");
            Environment.ExitCode = 1;
            return;
        }

        var seeder = new ErpSystem.Data.Seeders.HrOrgAuthoritySeeder(
            context, loggerFactory.CreateLogger<ErpSystem.Data.Seeders.HrOrgAuthoritySeeder>());

        // ⚠ Deliberately NOT short-circuited on "every unit has a head". Heads and managers are two
        // passes, and gating the whole command on the first one means a re-run skips the second —
        // which is how a cycle in the manager graph survived its first correction. The seeder is
        // idempotent by never-overwriting, so running it always is safe and reports what it kept.
        await seeder.SeedAsync(tenant.Id);
    }

    Console.WriteLine("✅ Org authority seeded (TEST data — see the warning in the log).");
    return;
}

// Loads a demonstrable HR/SHE dataset: a workforce staffing the establishment, the leave vocabulary
// and holiday calendar, and the nine area seeders that were ported and then deferred.
//
// ⚠ FOR DEMONSTRATION DATABASES ONLY, and separate from 'seed-hr-all' for that reason. That command
// seeds facts — the real organogram, the real positions. Everything this one writes is invented, and
// putting fabricated employees behind the trustworthy command would leave no way to build a clean
// database for anything but a demo.
//
// Prerequisites: 'rebuild-db', 'seed', then 'seed-hr-all' — this populates an establishment, it does
// not create one.
if (args.Length > 0 && args[0] == "seed-hr-demo")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);

    // Identity is needed for the persona logins (password hashing, role membership) — the same
    // registration the plain 'seed' command uses to create the admin user.
    tempBuilder.Services.AddErpSystemIdentity();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        // The seed host builds a reduced service graph, so the email-event catalogues may not be
        // registered. GetServices returns an empty sequence rather than throwing, and the
        // orchestrator treats "no catalogues" as nothing to seed rather than as a failure.
        var emailCatalogs = scope.ServiceProvider
            .GetServices<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog>();

        var orchestrator = new ErpSystem.Data.Seeders.HrDemoSeedOrchestrator(
            context, loggerFactory, emailCatalogs);

        if (!await orchestrator.SeedAsync())
        {
            Console.WriteLine("❌ HR demo seeding could not start — see the log above.");
            Environment.ExitCode = 1;
            return;
        }

        // The logins come AFTER the data: each persona is resolved to a seeded employee by position
        // title, so the workforce has to exist first.
        var personaSeeder = new ErpSystem.Api.Services.TdcDemoPersonaSeeder(
            context,
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ErpSystem.Core.Entities.ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<ErpSystem.Core.Entities.ApplicationRole>>(),
            loggerFactory.CreateLogger<ErpSystem.Api.Services.TdcDemoPersonaSeeder>());

        await personaSeeder.SeedAsync();
    }

    Console.WriteLine("✅ HR demo data seeded. Check the log for any step reported as FAILED.");
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
        var skipMigrations = args.Any(argument =>
            string.Equals(argument, "--skip-migrations", StringComparison.OrdinalIgnoreCase));
        if (!skipMigrations)
        {
            await db.Database.MigrateAsync();
        }

        var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
        await seedingService.SeedWorkflowDefinitionsAsync();
    }

    Console.WriteLine("Workflow definition seeding completed!");
    return;
}

// Seed only the prerequisites needed to exercise supplier onboarding end to end.
// This never creates an applicant, token, payment, registration, or supplier record.
if (args.Length > 0 && args[0] == "seed-supplier-onboarding-e2e")
{
    var tempBuilder = CreateSeedBuilder(args);

    tempBuilder.Services.AddErpSystemLogging(tempBuilder.Configuration);
    tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration);
    tempBuilder.Services.AddDatabaseSeedingFinanceBoundary();
    tempBuilder.Services.AddScoped<ProcurementSupplierOnboardingTestSeeder>();

    var tempApp = tempBuilder.Build();

    using (var scope = tempApp.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<ProcurementSupplierOnboardingTestSeeder>();
        await seeder.SeedAsync();
    }

    Console.WriteLine("Supplier-onboarding E2E prerequisites seeded successfully.");
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
        $"Unknown command '{args[0]}'. Valid commands: seed, seed-civil-e2e, seed-tender-e2e, "
        + "seed-maintenance, seed-maintenance-e2e, seed-db, seed-deployment-uat, seed-operational-uat, seed-qs-uat, seed-workflows, "
        + "seed-supplier-onboarding-e2e, seed-hr-all, seed-hr-org-authority, seed-hr-demo, "
        + "seed-finance-baseline, seed-finance-demo-dimensions, seed-estate-land-bank, rebuild-db, repair-finance-po-schema.");
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
builder.Services.AddSingleton<ErpSystem.Api.Services.IApplicationEnvironmentService,
    ErpSystem.Api.Services.ApplicationEnvironmentService>();
builder.Services.Configure<ErpSystem.Core.DTOs.Procurement.SupplierApplicantAccessOptions>(
    builder.Configuration.GetSection(
        ErpSystem.Core.DTOs.Procurement.SupplierApplicantAccessOptions.SectionName));
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
builder.Services.AddErpSystemRateLimiting(builder.Environment, builder.Configuration);

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

// Disposable browser-assurance hosts exercise synchronous API workflows and should not
// run unrelated schedulers against their short-lived database. Production keeps the
// default enabled value; test launchers must opt out explicitly.
if (!builder.Configuration.GetValue("BackgroundServices:Enabled", true))
{
    builder.Services.RemoveAll<IHostedService>();
}

// Add Quality Certificate Service
builder.Services.AddScoped<ErpSystem.Api.Services.QualityCertificateService>();

// Add Transfer Document Service for Shipment Notes and GRNs
builder.Services.AddScoped<ErpSystem.Api.Services.TransferDocumentService>();

// Add Purchase Receipt (PO GRN) PDF service
builder.Services.AddScoped<ErpSystem.Api.Services.PurchaseOrderReceiptDocumentService>();
builder.Services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptDocumentService>(provider =>
    provider.GetRequiredService<ErpSystem.Api.Services.PurchaseOrderReceiptDocumentService>());
builder.Services.AddScoped<ErpSystem.Api.Services.ProcurementReceiptSourceEvidenceService>();
builder.Services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptSourceEvidenceService>(provider =>
    provider.GetRequiredService<ErpSystem.Api.Services.ProcurementReceiptSourceEvidenceService>());
builder.Services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IProcurementReceiptSourceEvidenceReadinessService>(provider =>
    provider.GetRequiredService<ErpSystem.Api.Services.ProcurementReceiptSourceEvidenceService>());

// Short-lived, disposable assurance hosts can explicitly opt out of unrelated
// schedulers. Production behavior remains unchanged unless this setting is set.
if (!builder.Configuration.GetValue("BackgroundServices:Enabled", true))
{
    builder.Services.RemoveAll<IHostedService>();
}

// Run Estate billing in the local launch profile without enabling every unrelated scheduler.
if (!builder.Configuration.GetValue("BackgroundServices:Enabled", true)
    && builder.Configuration.GetValue("EstateRecurringBilling:Enabled", false))
{
    builder.Services.AddHostedService<ErpSystem.Api.Services.Estate.EstateRecurringBillingBackgroundService>();
}

// Add Award Letter Service for PDF award letter generation
builder.Services.AddScoped<ErpSystem.Core.Interfaces.Procurement.IAwardLetterService, ErpSystem.Api.Services.AwardLetterService>();

// Add Price List Lookup Service for procurement pricing
builder.Services.AddScoped<ErpSystem.Core.Services.Pricing.PriceListLookupService>();

// Local property enquiry verification can dispatch only this feature's queue
// while unrelated schedulers remain disabled.
if (!builder.Configuration.GetValue("BackgroundServices:Enabled", true)
    && builder.Configuration.GetValue("Notifications:PropertyEnquiriesOnly", false))
{
    builder.Services.AddHostedService<ErpSystem.Api.Services.NotificationDispatcherBackgroundService>();
}

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

if (!app.Environment.IsDevelopment())
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

// Personal-data trees that may still exist under the historical public upload root.
// Static files are served BEFORE UseAuthentication/UseAuthorization below, so nothing
// downstream can gate them — this middleware is the only place that can. Every folder
// listed here now has an authorizing download endpoint; the legacy files themselves are
// relocated by the HR legacy-file migration utility.
string[] blockedLegacyUploadPaths =
[
    "/uploads/supplier-registration-evidence",
    "/uploads/cv-uploads",
    "/uploads/candidate-documents",
    "/uploads/candidate-photos",
    "/uploads/leave-attachments",
    "/uploads/pip-attachments",
    "/uploads/staff-discipline",
    "/uploads/movements",
    "/uploads/offers",
];

app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    foreach (var blocked in blockedLegacyUploadPaths)
    {
        if (path.StartsWithSegments(blocked, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    // Belt and braces for every hr-* category, in case the private storage root is ever
    // misconfigured onto the web root. StartsWithSegments compares whole path segments,
    // so a bare prefix like "hr-" needs the raw string check.
    if ((path.Value ?? string.Empty).StartsWith("/uploads/hr-", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

// Enable static file serving for non-sensitive public assets.
app.UseStaticFiles();

app.UseRouting();

// CORS must be after UseRouting and before UseAuthentication
app.UseCors("ErpSystemCorsPolicy");

// Keep framework-generated authentication and method failures structured for
// the source-scoped GHANEPS exchange API without changing other API contracts.
app.UseMiddleware<ProcurementGhanepsProblemDetailsMiddleware>();

app.UseAuthentication();
app.UseMiddleware<JwtBlacklistMiddleware>();
app.UseMiddleware<HrIdentityAccessMiddleware>();

// Rate limiting depends on authenticated user claims for ERP/external users.
// Auth endpoints remain anonymous here, so login/password-reset throttling still applies by IP.
app.UseRateLimiter();

app.UseMiddleware<SupplierApplicantAccessMiddleware>();
app.UseMiddleware<TemporaryPasswordChangeMiddleware>();
app.UseMiddleware<ExternalUserAccessMiddleware>();
// Candidates (self-registered careers accounts on the main scheme) get their own, narrower
// fence — deliberately not folded into the ExternalUser one, whose allowlist carries the
// procurement/projects/estate portals a candidate must never inherit.
app.UseMiddleware<CandidateAccessMiddleware>();
// Consultant-client contacts (invite-only accounts on the main scheme since 2026-08-31) get the
// same treatment: their own sibling fence, narrower still — auth, profile, notifications and the
// client-portal timesheet surface only.
app.UseMiddleware<ConsultantClientAccessMiddleware>();
app.UseAuthorization();

// Keep aggregate diagnostics available to operators, but separate readiness from liveness.
// A SQL/Redis outage should remove this instance from traffic via readiness without causing an
// orchestrator to restart a healthy API process repeatedly via liveness.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/health/shutdown", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("shutdown"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

// Caddy exposes /api/* to browsers and keeps /health* private to the VPS. These
// aliases give authenticated application screens the same sanitized health
// contract without exposing the private loopback routes through the gateway.
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

// API Controllers
app.MapControllers();

// SignalR Hubs
app.MapHub<ErpSystem.Api.Hubs.DashboardHub>("/api/hubs/dashboard");

var skipStartupInitialization = app.Environment.IsEnvironment("Testing")
    || app.Configuration.GetValue<bool>("SkipStartupInitialization");
var databaseConnectionTimeout = TimeSpan.FromSeconds(Math.Max(
    5,
    app.Configuration.GetValue("StartupInitialization:DatabaseConnectionTimeoutSeconds", 30)));
var migrationTimeout = TimeSpan.FromSeconds(Math.Max(
    5,
    app.Configuration.GetValue("StartupInitialization:MigrationTimeoutSeconds", 120)));
var failFastOnDatabaseInitializationError = app.Configuration.GetValue(
    "StartupInitialization:FailFastOnDatabaseInitializationError",
    true);
var seedDevelopmentData = app.Configuration.GetValue("StartupInitialization:SeedDevelopmentData", false);
var allowDevelopmentDataSeedingOutsideDevelopment = app.Configuration.GetValue(
    StartupInitializationPolicy.AllowDevelopmentDataSeedingOutsideDevelopmentKey,
    false);
var developmentDataSeedingPermitted =
    StartupInitializationPolicy.IsDevelopmentDataSeedingPermitted(
        app.Environment.EnvironmentName,
        allowDevelopmentDataSeedingOutsideDevelopment);
var seedWorkflowDefinitions = app.Configuration.GetValue("StartupInitialization:SeedWorkflowDefinitions", false);
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
        app.Logger.LogInformation("Starting TDC procurement security-baseline reconciliation...");
        await ReconcileProcurementSecurityBaselineAsync(app);
        app.Logger.LogInformation("TDC procurement security-baseline reconciliation completed");
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
        app.Logger.LogInformation("Starting critical Finance workflow provisioning...");
        try
        {
            await SeedCriticalFinanceWorkflowDefinitionsAsync(app);
            app.Logger.LogInformation("Critical Finance workflow provisioning completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Critical Finance workflow provisioning failed");
            if (failFastOnDatabaseInitializationError)
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

    if (databaseInitializationSucceeded)
    {
        app.Logger.LogInformation("Starting Estate acquisition land bank parcel seeding...");
        try
        {
            await SeedEstateAcquisitionLandBankParcelsAsync(app);
            app.Logger.LogInformation("Estate acquisition land bank parcel seeding completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Estate acquisition land bank parcel seeding failed");
            if (failFastOnDatabaseInitializationError)
            {
                throw;
            }
        }
    }

    if (databaseInitializationSucceeded)
    {
        app.Logger.LogInformation("Starting baseline Finance close-template seeding...");
        try
        {
            // Close templates are tenant-owned configuration, so migrations cannot cover tenants
            // created later. Startup reconciliation is a missing-only safety net; provisioning is
            // still the primary installation path and custom active templates are preserved.
            await SeedFinanceCloseTemplateBaselineAsync(app);
            app.Logger.LogInformation("Baseline Finance close-template seeding completed");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Baseline Finance close-template seeding failed");
            if (failFastOnDatabaseInitializationError)
            {
                throw;
            }
        }
    }

    // Seed demo/basic data in Development, or on an explicitly opted-in non-production test host.
    if (seedDevelopmentData && developmentDataSeedingPermitted && databaseInitializationSucceeded)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.Logger.LogWarning(
                "Development data seeding is explicitly enabled outside the Development environment. " +
                "This setting is intended only for isolated test servers.");
        }

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
    else if (seedDevelopmentData && developmentDataSeedingPermitted)
    {
        app.Logger.LogWarning(
            "Skipping Development data seeding because database initialization did not complete successfully.");
    }
    else if (seedDevelopmentData)
    {
        app.Logger.LogInformation(
            "Development data seeding was requested but is not permitted in environment {EnvironmentName}. " +
            "Set {OverrideKey}=true only on an isolated test server.",
            app.Environment.EnvironmentName,
            StartupInitializationPolicy.AllowDevelopmentDataSeedingOutsideDevelopmentKey);
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
        var connection = context.Database.GetDbConnection();
        var openedHere = false;
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(testCts.Token);
                openedHere = true;
            }
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException(
                $"Database connection timed out after {databaseConnectionTimeout.TotalSeconds:F0} seconds.",
                ex);
        }
        catch (Exception ex)
        {
            var reason = ex.GetBaseException().Message;
            throw new InvalidOperationException(
                $"Database connection check failed for provider '{providerName}' using '{connectionSummary}'. Startup migrations cannot continue. Reason: {reason}",
                ex);
        }
        finally
        {
            if (openedHere && connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }
    }

    logger.LogInformation("Database connection successful. Running migrations...");

    using var migrationCts = new CancellationTokenSource(migrationTimeout);
    try
    {
        await RepairDevelopmentMigrationHistoryIfNeededAsync(app.Environment, context, logger, migrationCts.Token);
        await context.Database.MigrateAsync(migrationCts.Token);
        await RepairFinanceBaselineSchemaAsync(context, migrationCts.Token);
        await RepairFinanceSettingsSchemaAsync(context, migrationCts.Token);
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

async Task SeedCriticalFinanceWorkflowDefinitionsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
    await seedingService.SeedCriticalFinanceWorkflowDefinitionsAsync();
    var reconciliationService = scope.ServiceProvider.GetRequiredService<
        ErpSystem.Api.Services.Finance.MultiCurrency.ExchangeRateWorkflowReconciliationService>();
    var reconciliation = await reconciliationService.ReconcileAsync();
    app.Logger.LogInformation(
        "Exchange-rate workflow reconciliation completed: {PendingCount} pending, {OrphanCount} orphaned, {RecoveredCount} recovered, {FailedCount} failed, {SkippedWithoutInitiatorCount} skipped without initiator",
        reconciliation.PendingCount,
        reconciliation.OrphanCount,
        reconciliation.RecoveredCount,
        reconciliation.FailedCount,
        reconciliation.SkippedWithoutInitiatorCount);
    var recurringReconciliationService = scope.ServiceProvider.GetRequiredService<
        ErpSystem.Api.Services.Finance.GL.RecurringJournalWorkflowReconciliationService>();
    var recurringReconciliation = await recurringReconciliationService.ReconcileAsync();
    app.Logger.LogInformation(
        "Recurring-journal workflow reconciliation completed: {PendingCount} pending, {OrphanCount} orphaned, {RecoveredCount} recovered, {FailedCount} failed, {SkippedWithoutInitiatorCount} skipped without initiator",
        recurringReconciliation.PendingCount,
        recurringReconciliation.OrphanCount,
        recurringReconciliation.RecoveredCount,
        recurringReconciliation.FailedCount,
        recurringReconciliation.SkippedWithoutInitiatorCount);
}

async Task SeedPaymentTermBaselineAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<PaymentTermBaselineSeeder>();
    await seeder.SeedAllActiveTenantsAsync();
}

async Task SeedEstateAcquisitionLandBankParcelsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
    await seedingService.SeedEstateAcquisitionLandBankParcelsAsync();
}

async Task SeedFinanceCloseTemplateBaselineAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<FinanceCloseTemplateBaselineSeeder>();
    await seeder.SeedAllActiveTenantsAsync();
}

async Task ReconcileProcurementSecurityBaselineAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ProcurementAccessControlSeeder>();
    await seeder.ReconcileIdentityAccessBaselineAsync();
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

    const string disposableDevelopmentBaselineMigration =
        "20260916132000_DisposableDevelopmentCurrentModelBaseline";
    if (pendingMigrations.Contains(disposableDevelopmentBaselineMigration)
        && await TableExistsAsync(context, "AspNetRoles", cancellationToken)
        && await TableExistsAsync(context, "Tenants", cancellationToken)
        && await TableExistsAsync(context, "ProcedureCases", cancellationToken))
    {
        await context.Database.ExecuteSqlRawAsync($"""
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory])
   AND NOT EXISTS (
       SELECT 1
       FROM [dbo].[__EFMigrationsHistory]
       WHERE [MigrationId] = N'{disposableDevelopmentBaselineMigration}')
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'{disposableDevelopmentBaselineMigration}', N'8.0.0');
END
""", cancellationToken);

        logger.LogWarning(
            "Stamped migration {MigrationId} as applied because the existing development database already contains the baseline schema.",
            disposableDevelopmentBaselineMigration);
    }

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

static async Task RepairFinanceBaselineSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
{
    await context.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[dbo].[AccountSegmentStructures]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'IsMandatory') IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM sys.default_constraints
           WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[AccountSegmentStructures]')
             AND [parent_column_id] = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[AccountSegmentStructures]'), N'IsMandatory', 'ColumnId'))
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD CONSTRAINT [DF_AccountSegmentStructures_IsMandatory] DEFAULT CAST(0 AS bit) FOR [IsMandatory];

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'FrozenAtUtc') IS NULL
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [FrozenAtUtc] datetime2 NULL;

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'FrozenByUserId') IS NULL
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [FrozenByUserId] uniqueidentifier NULL;

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'IsSystemDefined') IS NULL
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [IsSystemDefined] bit NOT NULL CONSTRAINT [DF_AccountSegmentStructures_IsSystemDefined] DEFAULT CAST(0 AS bit);

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'LifecycleStatus') IS NULL
    BEGIN
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [LifecycleStatus] int NOT NULL CONSTRAINT [DF_AccountSegmentStructures_LifecycleStatus] DEFAULT 1;
        EXEC(N'UPDATE [dbo].[AccountSegmentStructures]
            SET [LifecycleStatus] = 2
            WHERE [IsActive] = CAST(1 AS bit) AND [IsDeleted] = CAST(0 AS bit);');
    END

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'RetirementReason') IS NULL
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [RetirementReason] nvarchar(500) NULL;

    IF COL_LENGTH(N'[dbo].[AccountSegmentStructures]', N'RowVersion') IS NULL
        ALTER TABLE [dbo].[AccountSegmentStructures] ADD [RowVersion] rowversion NOT NULL;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE [name] = N'CK_AccountSegmentStructures_LifecycleActive'
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[AccountSegmentStructures]'))
        EXEC(N'ALTER TABLE [dbo].[AccountSegmentStructures] ADD CONSTRAINT [CK_AccountSegmentStructures_LifecycleActive]
            CHECK (([LifecycleStatus] IN (2, 3) AND [IsActive] = 1) OR ([LifecycleStatus] IN (1, 4) AND [IsActive] = 0));');
END

IF OBJECT_ID(N'[dbo].[RecurringJournalOccurrences]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'[dbo].[RecurringJournalOccurrences]', N'ReversalAuthorizedAt') IS NULL
        ALTER TABLE [dbo].[RecurringJournalOccurrences] ADD [ReversalAuthorizedAt] datetime2 NULL;

    IF COL_LENGTH(N'[dbo].[RecurringJournalOccurrences]', N'ReversalAuthorizedByUserId') IS NULL
        ALTER TABLE [dbo].[RecurringJournalOccurrences] ADD [ReversalAuthorizedByUserId] uniqueidentifier NULL;

    IF COL_LENGTH(N'[dbo].[RecurringJournalOccurrences]', N'ReversalLastAttemptAt') IS NULL
        ALTER TABLE [dbo].[RecurringJournalOccurrences] ADD [ReversalLastAttemptAt] datetime2 NULL;

    IF COL_LENGTH(N'[dbo].[RecurringJournalOccurrences]', N'ReversalStatus') IS NULL
        ALTER TABLE [dbo].[RecurringJournalOccurrences] ADD [ReversalStatus] int NOT NULL CONSTRAINT [DF_RecurringJournalOccurrences_ReversalStatus] DEFAULT 0;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE [name] = N'IX_RecurringJournalOccurrences_TenantId_ReversalStatus_ReversalDueDate'
          AND [object_id] = OBJECT_ID(N'[dbo].[RecurringJournalOccurrences]'))
        EXEC(N'CREATE INDEX [IX_RecurringJournalOccurrences_TenantId_ReversalStatus_ReversalDueDate]
            ON [dbo].[RecurringJournalOccurrences] ([TenantId], [ReversalStatus], [ReversalDueDate]);');
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
                 [BudgetTrackingEnabled], [Status], [DebitBalance], [CreditBalance], [OpeningBalance], [LastTransactionDate],
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

            -- Phase 5: account-number identity is created only by the Finance segment manifest
            -- and provisioning services. Startup repair must not fabricate DEPT/PROJ placeholders.
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
