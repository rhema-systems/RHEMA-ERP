using System.Reflection;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSupplierOnboardingTestSeederPolicyTests
{
    [Fact]
    public async Task ExistingUnrelatedEffectivePolicyWithoutOptionalSodRulesIsSelectedUnchanged()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var profile = NewProfile(tenantId);
        var policy = NewEffectivePolicy(tenantId, profile.Id, "TDC-F05B-NCT", version: 5, isDefault: true);
        policy.Description = "Published tenant-owned policy";
        policy.ChangeSummary = "Approved by the business";
        context.ProcurementPolicySets.Add(policy);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var seeder = new ProcurementSupplierOnboardingTestSeeder(
            context,
            NullLogger<ProcurementSupplierOnboardingTestSeeder>.Instance);

        var selected = await SelectEffectivePolicyAsync(seeder, tenantId, profile, Guid.NewGuid());

        selected.Id.Should().Be(policy.Id);
        selected.Code.Should().Be("TDC-F05B-NCT");
        selected.Version.Should().Be(5);
        selected.LifecycleStatus.Should().Be(ProcurementPolicyLifecycleStatus.Published);
        selected.IsDefault.Should().BeTrue();
        selected.Description.Should().Be("Published tenant-owned policy");
        selected.ChangeSummary.Should().Be("Approved by the business");
        selected.SodRules.Should().BeEmpty();
        context.ChangeTracker.Entries<ProcurementPolicySet>()
            .Single(item => item.Entity.Id == policy.Id)
            .State.Should().Be(EntityState.Unchanged);
        context.ChangeTracker.Entries<ProcurementPolicySodRule>().Should().BeEmpty();

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var persisted = await context.ProcurementPolicySets
            .Include(item => item.SodRules)
            .SingleAsync(item => item.Id == policy.Id);
        persisted.Description.Should().Be("Published tenant-owned policy");
        persisted.ChangeSummary.Should().Be("Approved by the business");
        persisted.SodRules.Should().BeEmpty();
    }

    [Fact]
    public async Task AmbiguousEffectivePolicySelectionStillFailsWithoutMutation()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var profile = NewProfile(tenantId);
        var first = NewEffectivePolicy(tenantId, profile.Id, "POLICY-A", version: 1, isDefault: false);
        var second = NewEffectivePolicy(tenantId, profile.Id, "POLICY-B", version: 1, isDefault: false);
        context.ProcurementPolicySets.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var seeder = new ProcurementSupplierOnboardingTestSeeder(
            context,
            NullLogger<ProcurementSupplierOnboardingTestSeeder>.Instance);

        var act = () => SelectEffectivePolicyAsync(seeder, tenantId, profile, Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ambiguous effective procurement-policy selection*");
        context.ChangeTracker.Entries<ProcurementPolicySet>()
            .Should().OnlyContain(item => item.State == EntityState.Unchanged);
        context.ChangeTracker.Entries<ProcurementPolicySodRule>().Should().BeEmpty();
    }

    [Fact]
    public async Task DedicatedSeedPolicyStillReceivesRecommendedSodControls()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var profile = NewProfile(tenantId);
        var policy = NewEffectivePolicy(
            tenantId,
            profile.Id,
            ProcurementSupplierOnboardingTestSeeder.PolicyCode,
            version: 1,
            isDefault: true);
        context.ProcurementPolicySets.Add(policy);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var seeder = new ProcurementSupplierOnboardingTestSeeder(
            context,
            NullLogger<ProcurementSupplierOnboardingTestSeeder>.Instance);

        var selected = await SelectEffectivePolicyAsync(seeder, tenantId, profile, Guid.NewGuid());

        selected.Id.Should().Be(policy.Id);
        selected.SodRules.Should().HaveCount(ProcurementSodRequiredControlRegistry.Definitions.Count);
        selected.SodRules.Should().OnlyContain(rule =>
            rule.Enforcement == ProcurementSodEnforcement.HardStop && rule.IsEnabled);
        selected.SodRules.Select(rule => rule.RuleCode).Should().BeEquivalentTo(
            ProcurementSodRequiredControlRegistry.Definitions.Select(definition => definition.Code));
    }

    private static ApplicationDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"supplier-onboarding-policy-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, tenantId);
    }

    private static ProcurementConfigurationProfile NewProfile(Guid tenantId) => new()
    {
        TenantId = tenantId,
        ProfileCode = ProcurementSupplierOnboardingTestSeeder.ProfileCode,
        Name = "Supplier onboarding test profile",
        Version = 1,
        LifecycleStatus = ProcurementConfigurationProfileStatus.Published,
        EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        IsDefault = true,
        CreatedBy = "Tests"
    };

    private static ProcurementPolicySet NewEffectivePolicy(
        Guid tenantId,
        Guid sourceProfileId,
        string code,
        int version,
        bool isDefault) => new()
    {
        TenantId = tenantId,
        Code = code,
        Name = $"{code} policy",
        Version = version,
        LifecycleStatus = ProcurementPolicyLifecycleStatus.Published,
        ScopeType = ProcurementPolicyScopeType.TenantBaseline,
        SourceConfigurationProfileId = sourceProfileId,
        DefaultCurrencyCode = "GHS",
        EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        IsDefault = isDefault,
        PublishedAt = DateTime.UtcNow.AddHours(-1),
        CreatedBy = "Tests"
    };

    private static async Task<ProcurementPolicySet> SelectEffectivePolicyAsync(
        ProcurementSupplierOnboardingTestSeeder seeder,
        Guid tenantId,
        ProcurementConfigurationProfile profile,
        Guid actorUserId)
    {
        var method = typeof(ProcurementSupplierOnboardingTestSeeder).GetMethod(
            "EnsureEffectiveProcurementPolicyAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("the focused test exercises the seeder policy-selection contract");

        var invocation = method!.Invoke(
            seeder,
            new object[] { tenantId, profile, actorUserId, CancellationToken.None });
        invocation.Should().BeAssignableTo<Task<ProcurementPolicySet>>();
        return await (Task<ProcurementPolicySet>)invocation!;
    }
}
