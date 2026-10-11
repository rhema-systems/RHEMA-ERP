using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public sealed class ProcurementUatPolicySetSeederTests
{
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddEntityFrameworkInMemoryDatabase()
        .BuildServiceProvider();

    [Fact]
    public async Task SeedTwice_CreatesExactPublishedBandsOnce()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        db.Tenants.Add(tenant);
        AddRoles(db);
        AddWorkflowEntityTypes(db, tenant.Id);
        await db.SaveChangesAsync();

        var seeder = new ProcurementUatPolicySetSeeder(db);
        var actorId = Guid.NewGuid();
        var first = await seeder.SeedTenantAsync(tenant.Id, actorId);
        var second = await seeder.SeedTenantAsync(tenant.Id, actorId);

        first.Should().Be(new ProcurementUatPolicySeedResult(2, 5));
        second.Should().Be(new ProcurementUatPolicySeedResult(0, 0));
        (await db.ProcurementPolicySets.CountAsync(value => value.TenantId == tenant.Id)).Should().Be(2);
        (await db.WorkflowDefinitions.CountAsync(value => value.TenantId == tenant.Id &&
            value.CreatedBy == ProcurementUatPolicySetSeeder.SeedActor)).Should().Be(5);

        var works = await LoadPolicyAsync(db, tenant.Id, ProcurementUatPolicySetSeeder.WorksPolicyCode);
        AssertPublishedPolicy(works);
        works.Name.Should().Be("UAT Works Tender GHS 50,000-100,000");
        works.CategoryRules.Should().ContainSingle().Which.Category.Should().Be(ProcurementCategoryClass.Works);
        works.CategoryRules.Single().ServiceClass.Should().Be("Measured construction works");
        var worksMethod = works.MethodRules.Should().ContainSingle().Which;
        worksMethod.Method.Should().Be(ProcurementMethodType.NationalCompetitiveTendering);
        worksMethod.MinimumQuotationCount.Should().Be(3);
        (await WorkflowEntityCodeAsync(db, worksMethod.WorkflowDefinitionId)).Should().Be("TENDER_AWARD");
        AssertThreshold(
            works.ThresholdRules.Should().ContainSingle().Which,
            ProcurementCategoryClass.Works,
            ProcurementMethodType.NationalCompetitiveTendering,
            50_000m,
            100_000m);
        works.EvidenceRules.Select(value => value.Stage).Should().BeEquivalentTo(new[]
        {
            ProcurementEvidenceStage.Sourcing,
            ProcurementEvidenceStage.Evaluation,
            ProcurementEvidenceStage.Award,
            ProcurementEvidenceStage.Contract
        });

        var cleaning = await LoadPolicyAsync(db, tenant.Id, ProcurementUatPolicySetSeeder.GeneralServicesPolicyCode);
        AssertPublishedPolicy(cleaning);
        cleaning.Name.Should().Be("UAT Cleaning Services RFQ GHS 1,000-50,000");
        cleaning.CategoryRules.Should().ContainSingle().Which.Category.Should().Be(ProcurementCategoryClass.GeneralServices);
        cleaning.CategoryRules.Single().ServiceClass.Should().Be("Cleaning services");
        var cleaningMethod = cleaning.MethodRules.Should().ContainSingle().Which;
        cleaningMethod.Method.Should().Be(ProcurementMethodType.RequestForQuotation);
        cleaningMethod.RequiresCompetition.Should().BeTrue();
        cleaningMethod.MinimumQuotationCount.Should().Be(3);
        (await WorkflowEntityCodeAsync(db, cleaningMethod.WorkflowDefinitionId)).Should().Be("TENDER_EVALUATION");
        AssertThreshold(
            cleaning.ThresholdRules.Should().ContainSingle().Which,
            ProcurementCategoryClass.GeneralServices,
            ProcurementMethodType.RequestForQuotation,
            1_000m,
            50_000m);
        cleaning.EvidenceRules.Select(value => value.Stage).Should().BeEquivalentTo(new[]
        {
            ProcurementEvidenceStage.Sourcing,
            ProcurementEvidenceStage.Evaluation,
            ProcurementEvidenceStage.Award,
            ProcurementEvidenceStage.Contract
        });
    }

    [Fact]
    public async Task ExistingReservedPolicy_IsPreservedWhileMissingPolicyIsAdded()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        db.Tenants.Add(tenant);
        AddRoles(db);
        AddWorkflowEntityTypes(db, tenant.Id);
        var source = new ProcurementConfigurationProfile
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, ProfileKey = Guid.NewGuid(),
            ProfileCode = "TENANT-SOURCE", Name = "Tenant-owned source", Version = 1,
            LifecycleStatus = ProcurementConfigurationProfileStatus.Draft,
            EffectiveFrom = DateTime.UtcNow.Date, IsDefault = false,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tenant administrator"
        };
        var existing = new ProcurementPolicySet
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, PolicyKey = Guid.NewGuid(),
            Code = ProcurementUatPolicySetSeeder.WorksPolicyCode,
            Name = "Tenant-adjusted Works policy", Description = "Do not replace this draft.",
            Version = 1, LifecycleStatus = ProcurementPolicyLifecycleStatus.Draft,
            ScopeType = ProcurementPolicyScopeType.TenantBaseline,
            SourceConfigurationProfileId = source.Id, SourceConfigurationProfile = source,
            DefaultCurrencyCode = "USD", EffectiveFrom = DateTime.UtcNow.Date,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tenant administrator"
        };
        db.ProcurementPolicySets.Add(existing);
        await db.SaveChangesAsync();

        var result = await new ProcurementUatPolicySetSeeder(db)
            .SeedTenantAsync(tenant.Id, Guid.NewGuid());

        result.CreatedPolicies.Should().Be(1);
        existing.Name.Should().Be("Tenant-adjusted Works policy");
        existing.Description.Should().Be("Do not replace this draft.");
        existing.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Draft);
        existing.DefaultCurrencyCode.Should().Be("USD");
        existing.CategoryRules.Should().BeEmpty();
        (await db.ProcurementPolicySets.CountAsync(value =>
            value.TenantId == tenant.Id && value.Code == ProcurementUatPolicySetSeeder.WorksPolicyCode))
            .Should().Be(1);
        (await db.ProcurementPolicySets.CountAsync(value =>
            value.TenantId == tenant.Id && value.Code == ProcurementUatPolicySetSeeder.GeneralServicesPolicyCode))
            .Should().Be(1);
    }

    [Fact]
    public async Task RequisitionSelection_UsesExactCategoryCurrencyAndInclusiveBand()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        db.Tenants.Add(tenant);
        AddRoles(db);
        AddWorkflowEntityTypes(db, tenant.Id);
        await db.SaveChangesAsync();
        await new ProcurementUatPolicySetSeeder(db).SeedTenantAsync(tenant.Id, Guid.NewGuid());

        using var unit = new UnitOfWork(db);
        var selector = new ProcurementRequisitionPolicySelectionService(unit);

        foreach (var amount in new[] { 50_000m, 100_000m })
        {
            var options = await selector.GetEligibleAsync(
                tenant.Id, ProcurementCategoryClass.Works, amount, "ghs", DateTime.UtcNow);
            var option = options.Should().ContainSingle().Which;
            option.PolicyCode.Should().Be(ProcurementUatPolicySetSeeder.WorksPolicyCode);
            option.Method.Should().Be(ProcurementMethodType.NationalCompetitiveTendering);
        }

        foreach (var amount in new[] { 1_000m, 50_000m })
        {
            var options = await selector.GetEligibleAsync(
                tenant.Id, ProcurementCategoryClass.GeneralServices, amount, "GHS", DateTime.UtcNow);
            var option = options.Should().ContainSingle().Which;
            option.PolicyCode.Should().Be(ProcurementUatPolicySetSeeder.GeneralServicesPolicyCode);
            option.Method.Should().Be(ProcurementMethodType.RequestForQuotation);
        }

        (await selector.GetEligibleAsync(
            tenant.Id, ProcurementCategoryClass.Works, 49_999.99m, "GHS", DateTime.UtcNow))
            .Should().BeEmpty();
        (await selector.GetEligibleAsync(
            tenant.Id, ProcurementCategoryClass.Works, 100_000.01m, "GHS", DateTime.UtcNow))
            .Should().BeEmpty();
        (await selector.GetEligibleAsync(
            tenant.Id, ProcurementCategoryClass.GeneralServices, 999.99m, "GHS", DateTime.UtcNow))
            .Should().BeEmpty();
        (await selector.GetEligibleAsync(
            tenant.Id, ProcurementCategoryClass.GeneralServices, 50_000.01m, "GHS", DateTime.UtcNow))
            .Should().BeEmpty();

        var works = await selector.ResolveAsync(
            tenant.Id, null, ProcurementCategoryClass.Works, 100_000m, "GHS", DateTime.UtcNow);
        works!.Code.Should().Be(ProcurementUatPolicySetSeeder.WorksPolicyCode);
        var cleaning = await selector.ResolveAsync(
            tenant.Id, null, ProcurementCategoryClass.GeneralServices, 50_000m, "GHS", DateTime.UtcNow);
        cleaning!.Code.Should().Be(ProcurementUatPolicySetSeeder.GeneralServicesPolicyCode);
    }

    [Fact]
    public async Task RequisitionSelection_RequiresExactChoiceWhenMultiplePoliciesMatch()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        db.Tenants.Add(tenant);
        AddRoles(db);
        AddWorkflowEntityTypes(db, tenant.Id);
        await db.SaveChangesAsync();
        await new ProcurementUatPolicySetSeeder(db).SeedTenantAsync(tenant.Id, Guid.NewGuid());

        var works = await LoadPolicyAsync(db, tenant.Id, ProcurementUatPolicySetSeeder.WorksPolicyCode);
        var overlap = new ProcurementPolicySet
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, PolicyKey = Guid.NewGuid(),
            Code = "TENANT-WORKS-OVERLAP", Name = "Tenant overlapping Works policy", Version = 1,
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
            ScopeType = ProcurementPolicyScopeType.TenantBaseline,
            SourceConfigurationProfileId = works.SourceConfigurationProfileId,
            DefaultCurrencyCode = "GHS", EffectiveFrom = DateTime.UtcNow.Date,
            PublishedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = "Tenant administrator"
        };
        overlap.CategoryRules.Add(new ProcurementPolicyCategoryRule
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Category = ProcurementCategoryClass.Works,
            ServiceClass = "Measured construction works", RuleCode = "TENANT-WORKS-CATEGORY", Name = "Works",
            IsEnabled = true, EffectiveFrom = overlap.EffectiveFrom, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        });
        overlap.MethodRules.Add(new ProcurementPolicyMethodRule
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Category = ProcurementCategoryClass.Works,
            ServiceClass = "Measured construction works", Method = ProcurementMethodType.NationalCompetitiveTendering,
            RuleCode = "TENANT-WORKS-METHOD", Name = "NCT", IsAllowed = true, IsEnabled = true,
            EffectiveFrom = overlap.EffectiveFrom, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        });
        overlap.ThresholdRules.Add(new ProcurementPolicyThresholdRule
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Category = ProcurementCategoryClass.Works,
            ServiceClass = "Measured construction works", Method = ProcurementMethodType.NationalCompetitiveTendering,
            CurrencyCode = "GHS", LowerBound = 50_000m, UpperBound = 100_000m,
            LowerInclusive = true, UpperInclusive = true, RuleCode = "TENANT-WORKS-BAND", Name = "Overlapping band",
            IsEnabled = true, EffectiveFrom = overlap.EffectiveFrom, CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        });
        db.ProcurementPolicySets.Add(overlap);
        await db.SaveChangesAsync();

        using var unit = new UnitOfWork(db);
        var selector = new ProcurementRequisitionPolicySelectionService(unit);
        var options = await selector.GetEligibleAsync(
            tenant.Id, ProcurementCategoryClass.Works, 75_000m, "GHS", DateTime.UtcNow);
        options.Should().HaveCount(2);

        var action = () => selector.ResolveAsync(
            tenant.Id, null, ProcurementCategoryClass.Works, 75_000m, "GHS", DateTime.UtcNow);
        var failure = await action.Should().ThrowAsync<ProcurementRequisitionPolicySelectionException>();
        failure.Which.Code.Should().Be("PR_POLICY_SELECTION_REQUIRED");

        var selected = await selector.ResolveAsync(
            tenant.Id, works.Id, ProcurementCategoryClass.Works, 75_000m, "GHS", DateTime.UtcNow);
        selected!.Id.Should().Be(works.Id);
    }

    [Fact]
    public async Task RequisitionSelection_PreservesSinglePublishedPolicyFallback()
    {
        await using var db = CreateContext();
        var tenant = NewTenant();
        db.Tenants.Add(tenant);
        var policy = new ProcurementPolicySet
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, PolicyKey = Guid.NewGuid(),
            Code = "LEGACY-ONLY", Name = "Legacy only policy", Version = 1,
            LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
            ScopeType = ProcurementPolicyScopeType.TenantBaseline,
            SourceConfigurationProfileId = Guid.NewGuid(), DefaultCurrencyCode = "GHS",
            EffectiveFrom = DateTime.UtcNow.Date, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
        };
        db.ProcurementPolicySets.Add(policy);
        await db.SaveChangesAsync();

        using var unit = new UnitOfWork(db);
        var selected = await new ProcurementRequisitionPolicySelectionService(unit).ResolveAsync(
            tenant.Id, null, ProcurementCategoryClass.Goods, 25m, "GHS", DateTime.UtcNow);

        selected!.Id.Should().Be(policy.Id);
    }

    private static void AssertPublishedPolicy(ProcurementPolicySet policy)
    {
        policy.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        policy.DefaultCurrencyCode.Should().Be("GHS");
        policy.IsDefault.Should().BeFalse("the user must explicitly select one of two category-specific UAT policies");
        policy.PublishedAt.Should().NotBeNull();
        policy.AuthorityRules.Should().ContainSingle();
        policy.SodRules.Should().ContainSingle();
    }

    private static void AssertThreshold(
        ProcurementPolicyThresholdRule threshold,
        ProcurementCategoryClass category,
        ProcurementMethodType method,
        decimal lower,
        decimal upper)
    {
        threshold.Category.Should().Be(category);
        threshold.Method.Should().Be(method);
        threshold.CurrencyCode.Should().Be("GHS");
        threshold.LowerBound.Should().Be(lower);
        threshold.UpperBound.Should().Be(upper);
        threshold.LowerInclusive.Should().BeTrue();
        threshold.UpperInclusive.Should().BeTrue();
    }

    private static async Task<string> WorkflowEntityCodeAsync(ApplicationDbContext db, Guid? definitionId)
    {
        definitionId.Should().HaveValue();
        var entityTypeId = await db.WorkflowDefinitions
            .Where(value => value.Id == definitionId!.Value)
            .Select(value => value.EntityTypeId)
            .SingleAsync();
        return await db.WorkflowEntityTypes
            .Where(value => value.Id == entityTypeId)
            .Select(value => value.Code)
            .SingleAsync();
    }

    private static Task<ProcurementPolicySet> LoadPolicyAsync(
        ApplicationDbContext db,
        Guid tenantId,
        string code) => db.ProcurementPolicySets
        .Include(value => value.CategoryRules)
        .Include(value => value.MethodRules)
        .Include(value => value.ThresholdRules)
        .Include(value => value.AuthorityRules)
        .Include(value => value.EvidenceRules)
        .Include(value => value.SodRules)
        .SingleAsync(value => value.TenantId == tenantId && value.Code == code);

    private static void AddRoles(ApplicationDbContext db)
    {
        foreach (var name in new[] { "TDC_PROCUREMENT_OFFICER", "TDC_MANAGING_DIRECTOR" })
        {
            db.Roles.Add(new ApplicationRole(name)
            {
                Id = Guid.NewGuid(), NormalizedName = name, IsSystemRole = true,
                CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
            });
        }
    }

    private static void AddWorkflowEntityTypes(ApplicationDbContext db, Guid tenantId)
    {
        var requiredCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TDC_PURCHASE_REQUISITION",
            "TDC_TENDER_APPROVAL",
            "TDC_TENDER_EVALUATION",
            "TDC_TENDER_AWARD",
            "TDC_CONTRACT"
        };
        foreach (var template in ProcurementAccessControlRegistry.Workflows.Where(value => requiredCodes.Contains(value.Code)))
        {
            db.WorkflowEntityTypes.Add(new WorkflowEntityType
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = template.EntityTypeCode,
                Name = template.EntityTypeName, IsActive = true, DisplayOrder = 500,
                CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
            });
        }
    }

    private static Tenant NewTenant() => new()
    {
        Id = Guid.NewGuid(), Code = "DEFAULT", Name = "Operational UAT",
        ContactEmail = "uat@example.invalid", Status = TenantStatus.Active,
        CreatedAt = DateTime.UtcNow, CreatedBy = "Tests"
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"procurement-uat-policies-{Guid.NewGuid():N}")
            .UseInternalServiceProvider(Provider)
            .Options;
        return new ApplicationDbContext(options);
    }
}
