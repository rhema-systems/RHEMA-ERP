using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Data.Seeders;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public partial class DatabaseSeedingServiceTests
{
    [Theory]
    [InlineData("EnsureFinanceWorkflowsSeededAsync", "VendorPayment")]
    [InlineData("EnsureFinanceWorkflowsSeededAsync", "PaymentBatch")]
    [InlineData("EnsureProjectWorkflowsSeededAsync", null)]
    [InlineData("EnsureProcurementOperationalWorkflowsSeededAsync", null)]
    [InlineData("EnsureEstateSopWorkflowsSeededAsync", null)]
    [InlineData("EnsureLegalProcedureWorkflowsSeededAsync", null)]
    [InlineData("EnsureEhcWorkflowSeededAsync", null)]
    public async Task StartupWorkflowSeeder_RetainsRetiredCustomDefinitionAndItsSteps(string helper, string? code)
    {
        await using var db = CreateContext();
        var tenant = await AddWorkflowSeedTenantAsync(db);
        var service = NewWorkflowSeedService(db);
        await InvokeWorkflowSeedAsync(service, helper);
        var definitions = await db.WorkflowDefinitions.Include(item => item.EntityType).Include(item => item.Steps)
            .Where(item => item.TenantId == tenant.Id).ToListAsync();
        definitions.Should().NotBeEmpty("the missing baseline must still be created");
        var chosen = code == null ? definitions.First() : definitions.Single(item => item.EntityType.Code == code);
        chosen.IsActive = false;
        chosen.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired;
        chosen.RetiredAt = DateTime.UtcNow.AddDays(-1);
        chosen.RetiredById = Guid.NewGuid();
        chosen.UpdatedBy = "Tenant administrator";
        chosen.Description = "Retired deliberately; do not re-publish on restart.";
        var step = chosen.Steps.First();
        step.Name = "Tenant custom stage";
        step.Configuration = "{\"tenantDecision\":\"preserve\"}";
        step.RequiredRole = "Tenant reviewer";
        await db.SaveChangesAsync();
        var before = await CaptureWorkflowSeedStateAsync(db, tenant.Id);

        await InvokeWorkflowSeedAsync(service, helper);

        (await CaptureWorkflowSeedStateAsync(db, tenant.Id)).Should().Be(before,
            "restart must not reactivate, unretire, replace, reroute or rewrite a tenant-owned definition");
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("InactivePublished")]
    [InlineData("Deleted")]
    [InlineData("InactiveEntity")]
    public async Task StartupFinanceSeeder_PreservesEveryExplicitNoActiveChoice(string choice)
    {
        await using var db = CreateContext();
        var tenant = await AddWorkflowSeedTenantAsync(db);
        var service = NewWorkflowSeedService(db);
        await InvokeWorkflowSeedAsync(service, "EnsureFinanceWorkflowsSeededAsync");
        var definition = await db.WorkflowDefinitions.Include(item => item.EntityType).SingleAsync(item =>
            item.TenantId == tenant.Id && item.EntityType.Code == "VendorPayment");
        definition.IsActive = choice == "InactiveEntity";
        if (choice == "Draft") definition.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft;
        if (choice == "Deleted") { definition.IsDeleted = true; definition.DeletedAt = DateTime.UtcNow; }
        if (choice == "InactiveEntity") definition.EntityType.IsActive = false;
        await db.SaveChangesAsync();
        var before = await CaptureWorkflowSeedStateAsync(db, tenant.Id);

        await InvokeWorkflowSeedAsync(service, "EnsureFinanceWorkflowsSeededAsync");

        (await CaptureWorkflowSeedStateAsync(db, tenant.Id)).Should().Be(before);
    }

    [Fact]
    public async Task StartupVendorPaymentLegacyHelper_DoesNotRepublishOrRewriteAnExistingLegacyRoute()
    {
        await using var db = CreateContext();
        var tenant = await AddWorkflowSeedTenantAsync(db);
        var entity = new WorkflowEntityType
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "VendorPayment", Name = "Vendor Payment", IsActive = true
        };
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(), DefinitionKey = Guid.NewGuid(), TenantId = tenant.Id, EntityTypeId = entity.Id,
            EntityType = entity, Name = "Vendor Payment Approval", IsActive = false,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired, RetiredAt = DateTime.UtcNow.AddDays(-4),
            RetiredById = Guid.NewGuid(), Configuration = "{\"reason\":\"disabled by administrator\"}"
        };
        db.AddRange(entity, definition);
        await db.SaveChangesAsync();
        var before = await CaptureWorkflowSeedStateAsync(db, tenant.Id);

        await InvokeWorkflowSeedAsync(NewWorkflowSeedService(db), "EnsureVendorPaymentControlWorkflowSeededAsync", tenant.Id);

        (await CaptureWorkflowSeedStateAsync(db, tenant.Id)).Should().Be(before);
    }

    [Fact]
    public async Task StartupWorkflowSeedPreservation_IsTenantScoped()
    {
        await using var db = CreateContext();
        var first = await AddWorkflowSeedTenantAsync(db);
        var service = NewWorkflowSeedService(db);
        await InvokeWorkflowSeedAsync(service, "EnsureProjectWorkflowsSeededAsync");
        var original = await CaptureWorkflowSeedStateAsync(db, first.Id);
        var second = await AddWorkflowSeedTenantAsync(db);

        await InvokeWorkflowSeedAsync(service, "EnsureProjectWorkflowsSeededAsync");

        (await CaptureWorkflowSeedStateAsync(db, first.Id)).Should().Be(original);
        (await db.WorkflowDefinitions.CountAsync(item => item.TenantId == second.Id)).Should().Be(3);
    }

    [Fact]
    public void RoutineWorkflowStartup_DoesNotInvokeTheExplicitUatPublicationRepair()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        var start = source.IndexOf("public async Task SeedWorkflowDefinitionsAsync()", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task EnsureFinancePermissionAssignmentsAsync()", start, StringComparison.Ordinal);
        source[start..end].Should().NotContain("EnsurePublishedPurchaseOrderApprovalWorkflowForUatAsync")
            .And.NotContain("_procurementAccessControlSeeder.SeedAsync");
    }

    [Theory]
    [InlineData("SeedWorkflowDefinitions")]
    [InlineData("SeedDevelopmentData")]
    public void RoutineStartup_RequiresExplicitSeedingOptIn_ButSupportsTrueConfiguration(string setting)
    {
        var api = Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api");
        var program = File.ReadAllText(Path.Combine(api, "Program.cs"));
        program.Should().Contain($"app.Configuration.GetValue(\"StartupInitialization:{setting}\", false)");
        foreach (var path in Directory.GetFiles(api, "appsettings*.json", SearchOption.TopDirectoryOnly))
        {
            using var configuration = JsonDocument.Parse(File.ReadAllText(path),
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            if (configuration.RootElement.TryGetProperty("StartupInitialization", out var startup) &&
                startup.TryGetProperty(setting, out var configured))
                configured.GetBoolean().Should().BeFalse();
        }
        var explicitConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [$"StartupInitialization:{setting}"] = "true" }).Build();
        explicitConfiguration.GetValue($"StartupInitialization:{setting}", false).Should().BeTrue();
        program.Should().Contain("if (seedWorkflowDefinitions && databaseInitializationSucceeded)");
        program.Should().Contain("await context.Database.MigrateAsync(migrationCts.Token)",
            "database migrations remain enabled independently of workflow/demo seeding");
        program.Should().Contain("await seedingService.SeedWorkflowDefinitionsAsync();",
            "the explicit seed-workflows command remains available");
    }

    private static DatabaseSeedingService NewWorkflowSeedService(ApplicationDbContext db) => new(
        db, CreateUserManager(), CreateRoleManager(), NullLogger<DatabaseSeedingService>.Instance, CreateEnvironment());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DevelopmentProcurementSeeder_PreservesExistingDraftOrDeletedWorkflow(bool deleted)
    {
        await using var db = CreateContext();
        var tenant = await AddWorkflowSeedTenantAsync(db);
        var template = ProcurementAccessControlRegistry.Workflows.First();
        var entity = new WorkflowEntityType { Id = Guid.NewGuid(), TenantId = tenant.Id,
            Code = template.EntityTypeCode, Name = template.EntityTypeName, IsActive = true };
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(), DefinitionKey = Guid.NewGuid(), TenantId = tenant.Id, EntityTypeId = entity.Id,
            EntityType = entity, Name = template.Name, IsActive = false, IsDeleted = deleted,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft, Configuration = "{\"tenantChoice\":true}"
        };
        definition.Steps.Add(new WorkflowStep { Id = Guid.NewGuid(), TenantId = tenant.Id,
            WorkflowDefinitionId = definition.Id, Name = "Approval", StepType = WorkflowStepType.Approval,
            RequiredRole = "Tenant-defined reviewer", Configuration = "{\"custom\":true}", Order = 1 });
        db.AddRange(entity, definition);
        await db.SaveChangesAsync();
        var seeder = new ProcurementAccessControlSeeder(db, NullLogger<ProcurementAccessControlSeeder>.Instance);

        await seeder.SeedTenantAsync(tenant.Id, preserveExistingWorkflows: true);

        definition.IsActive.Should().BeFalse();
        definition.IsDeleted.Should().Be(deleted);
        definition.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Draft);
        definition.Configuration.Should().Be("{\"tenantChoice\":true}");
        definition.Steps.Single().RequiredRole.Should().Be("Tenant-defined reviewer");
        definition.Steps.Single().Configuration.Should().Be("{\"custom\":true}");
        (await db.WorkflowDefinitions.IgnoreQueryFilters().CountAsync(item => item.TenantId == tenant.Id && item.EntityTypeId == entity.Id))
            .Should().Be(1, "a retained deleted workflow must not be silently replaced");
    }

    private static async Task<Tenant> AddWorkflowSeedTenantAsync(ApplicationDbContext db)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Workflow state preservation", Code = Guid.NewGuid().ToString("N"),
            ContactEmail = "workflow-state@test.local", Status = TenantStatus.Active };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static Task InvokeWorkflowSeedAsync(DatabaseSeedingService service, string helper, params object[] args)
    {
        var method = typeof(DatabaseSeedingService).GetMethod(helper, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Workflow seed helper was not found: " + helper);
        return (Task)method.Invoke(service, args)!;
    }

    private static async Task<string> CaptureWorkflowSeedStateAsync(ApplicationDbContext db, Guid tenantId)
    {
        var definitions = await db.WorkflowDefinitions.IgnoreQueryFilters().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.EntityTypeId, item.Name, item.IsActive, item.IsDeleted,
                item.LifecycleStatus, item.PublishedAt, item.PublishedById, item.RetiredAt, item.RetiredById,
                item.Version, item.Description, item.Configuration, item.UpdatedAt, item.UpdatedBy }).ToListAsync();
        var steps = await db.WorkflowSteps.IgnoreQueryFilters().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.WorkflowDefinitionId, item.Name, item.Configuration,
                item.RequiredRole, item.IsDeleted, item.StepType, item.Order, item.UpdatedAt, item.UpdatedBy }).ToListAsync();
        var entities = await db.WorkflowEntityTypes.IgnoreQueryFilters().AsNoTracking().Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.Id).Select(item => new { item.Id, item.Name, item.Code, item.IsActive, item.IsDeleted, item.UpdatedAt }).ToListAsync();
        return JsonSerializer.Serialize(new { definitions, steps, entities });
    }
}
