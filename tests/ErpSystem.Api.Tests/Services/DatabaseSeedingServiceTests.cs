using System.Reflection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
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
    public async Task SeedCriticalFinanceWorkflowDefinitionsAsync_ShouldProvisionExchangeRateWithoutOptionalWorkflowFlag()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Critical Finance Workflow Tenant",
            Code = "CFW",
            Status = TenantStatus.Active,
            ContactEmail = "critical-finance-workflow@test.local",
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

        await service.SeedCriticalFinanceWorkflowDefinitionsAsync();

        var definition = await context.WorkflowDefinitions
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .SingleAsync(item => item.TenantId == tenant.Id && item.EntityType.Code == "ExchangeRate");
        definition.IsActive.Should().BeTrue();
        definition.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Published);
        definition.Steps
            .Where(step => step.StepType == WorkflowStepType.Approval && !step.IsDeleted)
            .OrderBy(step => step.Order)
            .Select(step => step.Name)
            .Should().Equal(
                "Accounts Officer Review",
                "Finance Manager Approval",
                "Financial Controller Final Approval");
        (await context.WorkflowDefinitions.CountAsync(item => item.TenantId == tenant.Id))
            .Should().Be(1);
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

        var exchangeRateDefinition = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .SingleAsync(definition => definition.TenantId == tenant.Id &&
                definition.EntityType.Code == "ExchangeRate");
        exchangeRateDefinition.IsActive.Should().BeTrue();
        exchangeRateDefinition.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Published);
        exchangeRateDefinition.PublishedAt.Should().NotBeNull();
        exchangeRateDefinition.Steps
            .Where(step => step.StepType == WorkflowStepType.Approval && !step.IsDeleted)
            .OrderBy(step => step.Order)
            .Select(step => step.Name)
            .Should().Equal(
                "Accounts Officer Review",
                "Finance Manager Approval",
                "Financial Controller Final Approval");

        var bookWorkflowCodes = new[]
        {
            "AccountingBookInitialization", "AccountingBookPeriodLifecycle", "AccountingBookLifecycle",
            "DeltaAdjustmentJournal"
        };
        var bookDefinitions = await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .Include(definition => definition.Steps)
            .Where(definition => definition.TenantId == tenant.Id &&
                bookWorkflowCodes.Contains(definition.EntityType.Code))
            .ToListAsync();
        bookDefinitions.Should().HaveCount(4);
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

        (await context.WorkflowDefinitions
            .Include(definition => definition.EntityType)
            .CountAsync(definition => definition.TenantId == tenant.Id &&
                definition.EntityType.Code == "ExchangeRate")).Should().Be(1);

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
    public async Task SeedEstateAcquisitionLandBankParcelsAsync_ShouldCreateMissingParcelsAndSkipExistingRows()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Default Estate Tenant",
            Code = "DEFAULT",
            Status = TenantStatus.Active,
            ContactEmail = "estate@test.local",
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

        await service.SeedEstateAcquisitionLandBankParcelsAsync();

        (await context.LandAcquisitions.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.EstateManagedAssets.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.EstateLandDemarcations.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(28);
        (await context.CadastralSurveys.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.OwnershipHistories.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.NegotiationOffers.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.LandAssets.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.EstateLandDemarcations.CountAsync(item =>
            item.TenantId == tenant.Id && item.EstateManagedAsset.AssetCode == "TDC-PORTAL-LAND-003")).Should().Be(4);

        var firstAsset = await context.EstateManagedAssets.SingleAsync(item =>
            item.TenantId == tenant.Id && item.AssetCode == "TDC-PORTAL-LAND-001");
        firstAsset.Name = "Tenant edited land bank parcel";
        firstAsset.ExternalSalePrice = 999m;
        await context.SaveChangesAsync();

        await service.SeedEstateAcquisitionLandBankParcelsAsync();

        (await context.LandAcquisitions.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.EstateManagedAssets.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
        (await context.EstateLandDemarcations.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(28);

        var preservedAsset = await context.EstateManagedAssets.SingleAsync(item =>
            item.TenantId == tenant.Id && item.AssetCode == "TDC-PORTAL-LAND-001");
        preservedAsset.Name.Should().Be("Tenant edited land bank parcel");
        preservedAsset.ExternalSalePrice.Should().Be(999m);
        preservedAsset.Status.Should().Be(EstateManagedAssetStatus.LandBank);
        preservedAsset.SourceType.Should().Be(EstateManagedAssetSourceType.LandAcquisition);
    }

    [Fact]
    public async Task SeedEstateAcquisitionLandBankParcelsAsync_ShouldSeedEveryActiveTenant()
    {
        await using var context = CreateContext();
        var tenants = new[]
        {
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Default Estate Tenant",
                Code = "DEFAULT",
                Status = TenantStatus.Active,
                ContactEmail = "default-estate@test.local",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            },
            new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Live Estate Tenant",
                Code = "LIVE",
                Status = TenantStatus.Active,
                ContactEmail = "live-estate@test.local",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Tests"
            }
        };
        context.Tenants.AddRange(tenants);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());

        await service.SeedEstateAcquisitionLandBankParcelsAsync();

        foreach (var tenant in tenants)
        {
            (await context.EstateManagedAssets.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
            (await context.LandAcquisitions.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(12);
            (await context.EstateLandDemarcations.CountAsync(item => item.TenantId == tenant.Id)).Should().Be(28);
        }
    }

    [Fact]
    public async Task PortalPropertyRequestSeeder_ShouldCreateSalesHandoffRequestsForExternalPortal()
    {
        await using var context = CreateContext();
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Default Portal Tenant",
            Code = "DEFAULT",
            Status = TenantStatus.Active,
            ContactEmail = "portal@test.local",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var external = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = "external",
            NormalizedUserName = "EXTERNAL",
            Email = "external@default.com",
            NormalizedEmail = "EXTERNAL@DEFAULT.COM",
            FirstName = "External",
            LastName = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        context.Tenants.Add(tenant);
        context.Users.Add(external);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(
            context,
            CreateUserManager(),
            CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance,
            CreateEnvironment());
        await service.SeedEstateAcquisitionLandBankParcelsAsync();

        var seedMethod = typeof(DatabaseSeedingService).GetMethod(
            "EnsurePortalPropertyRequestExamplesSeededAsync",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        seedMethod.Should().NotBeNull();

        await (Task)seedMethod!.Invoke(service, null)!;
        await (Task)seedMethod.Invoke(service, null)!;

        var customer = await context.BusinessPartners.SingleAsync(item =>
            item.TenantId == tenant.Id && item.PartnerCode == "CUST-PORTAL-DEMO");
        customer.UserId.Should().Be(external.Id);
        customer.CustomerAccountNumber.Should().Be("CUS-PORTAL-001");
        (await context.BusinessPartnerUsers.CountAsync(item =>
            item.TenantId == tenant.Id
            && item.BusinessPartnerId == customer.Id
            && item.UserId == external.Id
            && item.IsActive)).Should().Be(1);

        var cases = await context.ProcedureCases
            .Include(item => item.Fields)
            .Where(item => item.TenantId == tenant.Id
                && item.ReferenceNumber != null
                && item.ReferenceNumber.StartsWith("PORTAL-SALES-HANDOFF-"))
            .OrderBy(item => item.ReferenceNumber)
            .ToListAsync();
        cases.Should().HaveCount(3);
        cases.Should().OnlyContain(item =>
            item.Module == "PropertyManagement"
            && item.EntityType == "EstatePropertyManagementListingApplication"
            && item.SourceDepartment == "Sales - Estate Enquiry"
            && item.CurrentStageName == "Estate intake review");
        cases.SelectMany(item => item.Fields)
            .Where(field => field.Key == "requestType")
            .Select(field => field.Value)
            .Should().BeEquivalentTo("Purchase enquiry", "Lease enquiry", "Rent enquiry");
        cases.Should().OnlyContain(item => item.Fields.Any(field =>
            field.Key == "sourceReference" && field.Value == customer.Id.ToString()));
        cases.Should().OnlyContain(item => item.Fields.Any(field =>
            field.Key == "applicationStatus" && field.Value == "Submitted from Sales"));
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

    [Fact]
    public async Task PropertyManagementListingWorkflowSeeder_ShouldPublishOnceForEachActiveTenant()
    {
        await using var context = CreateContext();
        var tenants = new[]
        {
            new Tenant { Id = Guid.NewGuid(), Name = "Estate One", Code = "EST1", Status = TenantStatus.Active,
                ContactEmail = "estate-one@test.local", CreatedAt = DateTime.UtcNow, CreatedBy = "Tests" },
            new Tenant { Id = Guid.NewGuid(), Name = "Estate Two", Code = "EST2", Status = TenantStatus.Active,
                ContactEmail = "estate-two@test.local", CreatedAt = DateTime.UtcNow, CreatedBy = "Tests" }
        };
        context.Tenants.AddRange(tenants);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(context, CreateUserManager(), CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance, CreateEnvironment());
        await service.SeedPropertyManagementListingWorkflowAsync();
        await service.SeedPropertyManagementListingWorkflowAsync();

        var definitions = await context.WorkflowDefinitions
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .Where(item => item.EntityType.Code == "EstatePropertyManagementListingApplication")
            .ToListAsync();
        definitions.Should().HaveCount(tenants.Length);
        definitions.Select(item => item.TenantId).Should().BeEquivalentTo(tenants.Select(item => item.Id));
        definitions.Should().OnlyContain(item => item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
            && item.PublishedAt.HasValue
            && item.Steps.Count == 5);
        definitions.Should().OnlyContain(item => item.Steps.OrderBy(step => step.Order)
            .Select(step => step.Name)
            .SequenceEqual(new[]
            {
                "Intake and validate property request", "Commercial and availability review",
                "Management decision", "Approved transaction handoff",
                "Customer update and close"
            }));
    }

    [Fact]
    public async Task LegalProcedureWorkflowSeeder_ShouldCoverEveryCatalogProcedure()
    {
        await using var context = CreateContext();
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Legal Tenant", Code = "LEGAL",
            Status = TenantStatus.Active, ContactEmail = "legal@test.local",
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests" };
        context.Tenants.Add(tenant);
        await context.SaveChangesAsync();

        var service = new DatabaseSeedingService(context, CreateUserManager(), CreateRoleManager(),
            NullLogger<DatabaseSeedingService>.Instance, CreateEnvironment());
        var seedMethod = typeof(DatabaseSeedingService).GetMethod(
            "EnsureLegalProcedureWorkflowsSeededAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        seedMethod.Should().NotBeNull();
        await ((Task)seedMethod!.Invoke(service, null)!).ConfigureAwait(false);
        await ((Task)seedMethod.Invoke(service, null)!).ConfigureAwait(false);

        var catalog = new ErpSystem.Core.Services.Legal.LegalProcedureCatalogService();
        var definitions = await context.WorkflowDefinitions
            .Include(item => item.EntityType)
            .Include(item => item.Steps)
            .Where(item => item.TenantId == tenant.Id)
            .ToListAsync();
        definitions.Should().HaveCount(catalog.GetProcedures().Count);
        definitions.Select(item => item.EntityType.Code).Should().BeEquivalentTo(
            catalog.GetProcedures().Select(item => item.EntityType));
        definitions.Should().OnlyContain(item => item.IsActive
            && item.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
            && item.Steps.Count > 0);
        foreach (var definition in definitions)
        {
            definition.Steps.OrderBy(step => step.Order).Select(step => step.Name).Should().Equal(
                catalog.GetProcedureWorkspace(definition.EntityType.Code)!.Stages.Select(stage => stage.Name));
        }
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
