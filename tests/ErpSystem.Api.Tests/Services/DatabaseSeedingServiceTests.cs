using System.Reflection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public partial class DatabaseSeedingServiceTests
{
    [Fact]
    public void FinanceRoleSeeder_ShouldGrantChiefAccountantAssignedPaymentApprovalPermission()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        var chiefAccountantStart = source.IndexOf("[\"Chief Accountant\"] = new[]", StringComparison.Ordinal);
        var managingDirectorStart = source.IndexOf("[\"Managing Director\"] = new[]", chiefAccountantStart, StringComparison.Ordinal);

        chiefAccountantStart.Should().BeGreaterThan(-1);
        managingDirectorStart.Should().BeGreaterThan(chiefAccountantStart);
        source[chiefAccountantStart..managingDirectorStart]
            .Should().Contain("\"Finance.AP.Payments.Approve\"",
                "the payment policy assigns the Chief Accountant an independent approval stage");
    }

    [Fact]
    public void FinanceRoleSeeder_ShouldGrantChiefAccountantControlledReportExportPermission()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        var chiefAccountantStart = source.IndexOf("[\"Chief Accountant\"] = new[]", StringComparison.Ordinal);
        var managingDirectorStart = source.IndexOf("[\"Managing Director\"] = new[]", chiefAccountantStart, StringComparison.Ordinal);

        chiefAccountantStart.Should().BeGreaterThan(-1);
        managingDirectorStart.Should().BeGreaterThan(chiefAccountantStart);
        source[chiefAccountantStart..managingDirectorStart]
            .Should().Contain("\"Finance.Reports.Export\"",
                "Chief Accountants must be able to distribute the controlled reports they review");
    }

    [Fact]
    public void FinanceRoleSeeder_ShouldNotCreateOrImplicitlyGrantTheLegacyDemoAuditorRole()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));

        source.Should().NotContain("\"Finance Auditor\"",
            "the legacy demo role is no longer seeded or assigned implicit Finance permissions; " +
            "this does not assert or change permissions on an existing tenant-owned role");
    }

    [Fact]
    public void FinanceRoleSeeder_ShouldKeepManagingDirectorLimitedToReadAndAssignedApproval()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        var mapping = System.Text.RegularExpressions.Regex.Match(
            source,
            "\\[\"Managing Director\"\\]\\s*=\\s*new\\[\\]\\s*\\{(?<permissions>.*?)\\}",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        mapping.Success.Should().BeTrue("the current executive role must have an explicit narrow permission mapping");
        var permissions = System.Text.RegularExpressions.Regex.Matches(
                mapping.Groups["permissions"].Value, "\"(Finance\\.[^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        permissions.Should().BeEquivalentTo(new[]
        {
            "Finance.Read",
            "Finance.AP.Payments.Approve",
            "Finance.Workflow.Approve",
            "Finance.Workflow.Reject",
            "Finance.Workflow.RequestChanges",
            "Finance.Reports.Run",
            "Finance.Reports.Export"
        }, "executive approval authority must not implicitly grant preparation, posting, reversal or administration");
        permissions.Should().NotContain(new[]
        {
            "Finance.Write",
            "Finance.JournalEntries.Post",
            "Finance.JournalEntries.Reverse",
            "Finance.Workflow.PostAfterApproval"
        });
    }

    [Fact]
    public async Task EnsureFinanceWorkflowsSeededAsync_ShouldCreateMissingAndPreserveExistingPaymentDefinitions()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Finance Workflow Tenant",
            Code = "FIN",
            Status = TenantStatus.Active,
            ContactEmail = "finance-workflow@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());
        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("EnsureFinanceWorkflowsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var bookWorkflowCodes = new[]
        {
            "AccountingBookInitialization", "AccountingBookPeriodLifecycle", "AccountingBookLifecycle"
        };
        var bookDefinitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Where(definition => definition.TenantId == tenant.Id &&
                bookWorkflowCodes.Contains(definition.EntityType.Code))
            .ToListAsync();
        bookDefinitions.Should().HaveCount(3);
        bookDefinitions.Select(definition => definition.EntityType.Code).Should().BeEquivalentTo(bookWorkflowCodes);
        bookDefinitions.Should().OnlyContain(definition => definition.IsActive &&
            definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
            definition.Steps.Count(step => step.StepType == WorkflowStepType.Approval && !step.IsDeleted) == 1);
        bookDefinitions.SelectMany(definition => definition.Steps)
            .Where(step => step.StepType == WorkflowStepType.Approval)
            .Should().OnlyContain(step => step.Name == "Financial Controller Review" &&
                step.Configuration != null && step.Configuration.Contains("Financial Controller"));

        var paymentDefinitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Where(definition =>
                definition.TenantId == tenant.Id &&
                (definition.EntityType.Code == "VendorPayment" ||
                 definition.EntityType.Code == "PaymentBatch" ||
                 definition.EntityType.Code == "VendorInvoiceMatchException"))
            .ToListAsync();
        paymentDefinitions.Should().HaveCount(3);
        paymentDefinitions.Should().OnlyContain(definition =>
            definition.IsActive &&
            definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published &&
            definition.PublishedAt.HasValue &&
            definition.DefinitionKey != Guid.Empty);

        paymentDefinitions.Should().OnlyContain(definition =>
            definition.Steps
                .Where(step => step.StepType == WorkflowStepType.Approval && !step.IsDeleted)
                .OrderBy(step => step.Order)
                .Select(step => step.Name)
                .SequenceEqual(new[]
                {
                    "Finance Manager Approval",
                    "Financial Controller Final Approval"
                }));

        var matchException = paymentDefinitions.Single(definition =>
            definition.EntityType.Code == "VendorInvoiceMatchException");
        matchException.Description.Should().Contain("AP-006");
        matchException.Description.Should().Contain("never allocates or posts payment");

        var vendorPayment = paymentDefinitions.Single(definition => definition.EntityType.Code == "VendorPayment");
        vendorPayment.Steps
            .Single(step => step.StepType == WorkflowStepType.Approval && step.Order == 2)
            .Name = "Accounts Officer Review";

        var paymentBatch = paymentDefinitions.Single(definition => definition.EntityType.Code == "PaymentBatch");
        paymentBatch.IsActive = false;
        paymentBatch.LifecycleStatus = WorkflowDefinitionLifecycleStatus.Draft;
        paymentBatch.PublishedAt = null;
        await context.SaveChangesAsync();

        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        paymentBatch.IsActive.Should().BeFalse();
        paymentBatch.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Draft);
        paymentBatch.PublishedAt.Should().BeNull();

        var activeVendorPaymentDefinitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Where(definition =>
                definition.TenantId == tenant.Id &&
                definition.EntityType.Code == "VendorPayment" &&
                definition.IsActive)
            .ToListAsync();

        activeVendorPaymentDefinitions.Should().ContainSingle();
        activeVendorPaymentDefinitions.Single().Steps
            .Where(step => step.StepType == WorkflowStepType.Approval && !step.IsDeleted)
            .OrderBy(step => step.Order)
            .Select(step => step.Name)
            .Should().Equal("Accounts Officer Review", "Financial Controller Final Approval");
        vendorPayment.IsActive.Should().BeTrue();
        vendorPayment.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Published);
        vendorPayment.RetiredAt.Should().BeNull();
    }

    [Fact]
    public async Task EnsureProjectWorkflowsSeededAsync_ShouldCreateBaselineProjectWorkflowDefinitions()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            ContactEmail = "tenant@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());

        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("EnsureProjectWorkflowsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var definitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Include(definition => definition.Transitions)
            .Where(definition => definition.TenantId == tenant.Id)
            .OrderBy(definition => definition.Name)
            .ToListAsync();

        definitions.Should().HaveCount(3);
        definitions.Select(definition => definition.Name).Should().BeEquivalentTo(new[]
        {
            "Project Approval",
            "Project Budget Revision Approval",
            "Project Closure Approval"
        });

        var projectDefinition = definitions.Single(definition => definition.Name == "Project Approval");
        projectDefinition.Steps.Select(step => step.Name).Should().Contain(new[] { "Draft", "PendingApproval", "Approved" });
        projectDefinition.Transitions.Select(transition => transition.Name).Should().BeEquivalentTo(new[] { "Submit", "Approve" });

        var closureDefinition = definitions.Single(definition => definition.Name == "Project Closure Approval");
        closureDefinition.Steps.Select(step => step.Name).Should().Contain("Closed");

        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        (await context.WorkflowDefinitions.CountAsync(definition => definition.TenantId == tenant.Id)).Should().Be(3);
        (await context.WorkflowEntityTypes.CountAsync(entityType => entityType.TenantId == tenant.Id)).Should().Be(3);
    }

    [Fact]
    public async Task EnsureProjectCatalogDefaultsSeededAsync_ShouldCreateRecommendedCatalogEntries()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Code = "TEST",
            Status = TenantStatus.Active,
            ContactEmail = "tenant@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());

        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("EnsureProjectCatalogDefaultsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var entries = await context.ProjectCatalogEntries
            .Where(entry => entry.TenantId == tenant.Id)
            .ToListAsync();

        entries.Should().NotBeEmpty();
        entries.Should().Contain(entry => entry.CatalogType == "methodologies" && entry.Code == "Hybrid");
        entries.Should().Contain(entry => entry.CatalogType == "billing-types" && entry.Code == "FixedPrice");
        entries.Should().Contain(entry => entry.CatalogType == "resource-roles" && entry.Code == "ProjectManager");

        var initialCount = entries.Count;
        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        (await context.ProjectCatalogEntries.CountAsync(entry => entry.TenantId == tenant.Id)).Should().Be(initialCount);
    }

    [Fact]
    public async Task SeedDefaultTenantModulesAsync_ShouldEnableProjectsForQsAndCivilReports()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Default Test Tenant",
            Code = "DEFAULT",
            Status = TenantStatus.Active,
            ContactEmail = "default@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());
        var seedMethod = typeof(DatabaseSeedingService)
            .GetMethod("SeedDefaultTenantModulesAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);

        var projects = await context.TenantModules.SingleAsync(module =>
            module.TenantId == tenant.Id && module.ModuleName == "Project Management");
        projects.Status.Should().Be(ModuleStatus.Enabled);
        projects.IsDeleted.Should().BeFalse();

        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);
        (await context.TenantModules.CountAsync(module =>
            module.TenantId == tenant.Id && module.ModuleName == "Project Management")).Should().Be(1);
    }

    private static readonly ServiceProvider WorkflowSeedTestServices = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .UseInternalServiceProvider(WorkflowSeedTestServices)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src")) &&
                File.Exists(Path.Combine(current.FullName, "ErpSystem.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new UserManager<ApplicationUser>(
            store.Object,
            null!,
            null!,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!);
    }

    private static RoleManager<ApplicationRole> CreateRoleManager()
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        return new RoleManager<ApplicationRole>(
            store.Object,
            Array.Empty<IRoleValidator<ApplicationRole>>(),
            null!,
            null!,
            null!);
    }

    private static IWebHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Testing");
        return environment.Object;
    }
}
