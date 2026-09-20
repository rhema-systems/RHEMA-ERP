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
}
