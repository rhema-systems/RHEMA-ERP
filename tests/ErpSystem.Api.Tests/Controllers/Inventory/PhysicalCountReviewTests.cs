using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public class PhysicalCountReviewTests
{
    [Fact] public void Defaults_offer_approval_and_investigation_without_automatic_posting()
    {
        var setup = PhysicalCountDecisionPolicy.Read(null);
        setup.Decisions.Select(x => x.Effect).Should().BeEquivalentTo("ApproveAdjustment", "Investigate");
        setup.Revision.Should().HaveLength(64);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("AutoPost")]
    [InlineData("IgnoreVariance")]
    public void Unsupported_effects_are_rejected(string effect)
    {
        var rows = PhysicalCountDecisionPolicy.Read(null).Decisions;
        rows.Add(new("CUSTOM", "Custom", effect));
        var action = () => PhysicalCountDecisionPolicy.Validate(rows);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact] public void Duplicate_codes_or_disabling_all_investigation_choices_is_rejected()
    {
        var rows = PhysicalCountDecisionPolicy.Read(null).Decisions;
        rows.Add(rows[0]);
        ((Action)(() => PhysicalCountDecisionPolicy.Validate(rows))).Should().Throw<InvalidOperationException>();
        rows.RemoveAt(2);
        rows[1] = rows[1] with { IsActive = false };
        ((Action)(() => PhysicalCountDecisionPolicy.Validate(rows))).Should().Throw<InvalidOperationException>();
    }

    [Fact] public void Stale_or_inactive_decisions_are_rejected()
    {
        var setup = PhysicalCountDecisionPolicy.Read(null);
        ((Action)(() => PhysicalCountDecisionPolicy.Resolve(setup, "APPROVE", "old"))).Should().Throw<InvalidOperationException>();
        ((Action)(() => PhysicalCountDecisionPolicy.Resolve(setup, "MISSING", setup.Revision))).Should().Throw<InvalidOperationException>();
        PhysicalCountDecisionPolicy.Resolve(setup, "APPROVE", setup.Revision).Effect.Should().Be("ApproveAdjustment");
    }

    [Theory]
    [InlineData("InProgress", true)]
    [InlineData("UnderReview", true)]
    [InlineData("UnderInvestigation", false)]
    [InlineData("PendingStoresApproval", false)]
    [InlineData("Posted", false)]
    public void Only_the_counter_can_correct_before_submission(string status, bool allowed)
    {
        var actor = Guid.NewGuid();
        var count = new PhysicalCount { CountedById = actor, Status = status };
        Action action = () => PhysicalCountService.EnsureCounterCanEdit(count, actor);
        if (allowed) action.Should().NotThrow(); else action.Should().Throw<InvalidOperationException>();
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, Guid.NewGuid()))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Committee_members_can_record_but_legacy_starter_cannot_bypass_assignment()
    {
        var tenant = Guid.NewGuid(); var starter = Guid.NewGuid(); var member = Guid.NewGuid();
        var count = new PhysicalCount { TenantId = tenant, CountedById = starter, Status = "InProgress" };
        count.Counters.Add(new PhysicalCountCounter { TenantId = tenant, UserId = member, IsActive = true });
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, member))).Should().NotThrow();
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, starter))).Should().Throw<InvalidOperationException>();
        count.Status = "UnderReview";
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, member))).Should().NotThrow();
        count.Status = "PendingStoresApproval";
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, member))).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Removed_or_cross_tenant_committee_members_cannot_record(bool active, bool wrongTenant)
    {
        var tenant = Guid.NewGuid(); var member = Guid.NewGuid();
        var count = new PhysicalCount { TenantId = tenant, CountedById = member, Status = "InProgress" };
        count.Counters.Add(new PhysicalCountCounter { TenantId = wrongTenant ? Guid.NewGuid() : tenant, UserId = member, IsActive = active });
        ((Action)(() => PhysicalCountService.EnsureCounterCanEdit(count, member))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Every_committee_actor_remains_excluded_from_independent_approval()
    {
        var tenant = Guid.NewGuid(); var member = Guid.NewGuid();
        var count = new PhysicalCount { TenantId = tenant, Status = "PendingStoresApproval" };
        count.Counters.Add(new PhysicalCountCounter { TenantId = tenant, UserId = member, IsActive = false });
        var method = typeof(PhysicalCountService).GetMethod("EnsureIndependentActor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var blocked = () => method.Invoke(null, new object[] { count, member, true, true });
        blocked.Should().Throw<System.Reflection.TargetInvocationException>().WithInnerException<InvalidOperationException>();
        var allowed = () => method.Invoke(null, new object[] { count, Guid.NewGuid(), true, true });
        allowed.Should().NotThrow();
    }
}
