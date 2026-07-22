using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowApprovalPolicyMatcherTests
{
    [Fact]
    public void Select_prefers_priority_then_specificity()
    {
        var context = Context(amount: 150_000m, category: "Goods");
        var general = Policy("GENERAL", priority: 10);
        var goods = Policy("GOODS", priority: 10, category: "Goods");
        var lowerPriority = Policy("LOW", priority: 5, category: "Goods");

        var selected = WorkflowApprovalPolicyMatcher.Select(
            new[] { general, lowerPriority, goods },
            context);

        selected.Should().BeSameAs(goods);
    }

    [Fact]
    public void Select_enforces_amount_location_legal_entity_and_currency_scope()
    {
        var locationId = Guid.NewGuid();
        var legalEntityId = Guid.NewGuid();
        var matching = Policy("MATCH", 20, "Works");
        matching.LocationId = locationId;
        matching.LegalEntityId = legalEntityId;
        matching.MinimumAmount = 100_000m;
        matching.MaximumAmount = 200_000m;
        matching.CurrencyCode = "GHS";
        var context = Context(150_000m, "Works", locationId, legalEntityId, "GHS");

        WorkflowApprovalPolicyMatcher.Select(new[] { matching }, context).Should().BeSameAs(matching);
        WorkflowApprovalPolicyMatcher.Select(
            new[] { matching },
            context with { Amount = 250_000m }).Should().BeNull();
    }

    private static WorkflowApprovalPolicyContext Context(
        decimal amount,
        string category,
        Guid? locationId = null,
        Guid? legalEntityId = null,
        string currency = "GHS")
        => new(Guid.NewGuid(), "PurchaseOrder", DateTime.UtcNow, "Procurement", category,
            locationId, legalEntityId, amount, currency);

    private static WorkflowApprovalPolicySet Policy(string code, int priority, string? category = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            EntityType = "PurchaseOrder",
            Module = "Procurement",
            Category = category,
            Priority = priority,
            ApprovalConfiguration = "{}"
        };

}
