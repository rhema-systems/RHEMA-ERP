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

    [Fact]
    public void Select_chooses_combined_high_value_cash_control_over_each_broader_payment_policy()
    {
        // TDC payment controls intentionally overlap: base, method-specific, amount-specific, and
        // the combined high-value cash rule. Priority must select the combined rule so neither the
        // cash-custody evidence nor executive authority is lost at the intersection.
        var context = new WorkflowApprovalPolicyContext(
            Guid.NewGuid(), "Vendor Payment", DateTime.UtcNow, "Finance", "Cash",
            Amount: 125_000m, CurrencyCode: "GHS");
        var standard = FinancePaymentPolicy("TDC-AP-PAYMENT-BASE", 100);
        var cash = FinancePaymentPolicy("TDC-AP-PAYMENT-CASH", 200, category: "Cash");
        var high = FinancePaymentPolicy("TDC-AP-PAYMENT-HIGH", 300, minimum: 100_000m);
        var highCash = FinancePaymentPolicy("TDC-AP-PAYMENT-HIGH-CASH", 400, "Cash", 100_000m);

        var selected = WorkflowApprovalPolicyMatcher.Select(
            new[] { standard, cash, high, highCash },
            context);

        selected.Should().BeSameAs(highCash);
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

    private static WorkflowApprovalPolicySet FinancePaymentPolicy(
        string code,
        int priority,
        string? category = null,
        decimal? minimum = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            EntityType = "Vendor Payment",
            Module = "Finance",
            Category = category,
            MinimumAmount = minimum,
            CurrencyCode = "GHS",
            Priority = priority,
            ApprovalConfiguration = "{}"
        };

}
