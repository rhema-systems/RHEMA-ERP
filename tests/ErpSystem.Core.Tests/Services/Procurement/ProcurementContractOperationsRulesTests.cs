using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractOperationsRulesTests
{
    private static readonly DateTime Now =
        new(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DelayedMilestoneProducesControlledNonPostingPenaltyPrompt()
    {
        var input = Input(
            penaltyClause: "Penalty applies after approved delay review.",
            milestones:
            [
                new(
                    Guid.NewGuid(), "Design approval", "InProgress",
                    Now.AddDays(-12), 1000m)
            ]);

        var prompt = ProcurementContractOperationsRules.Evaluate(input, Now)
            .Single(item => item.Type == "MilestoneDelay");

        prompt.Severity.Should().Be("High");
        prompt.DaysOverdue.Should().Be(12);
        prompt.RequiresIndependentApproval.Should().BeTrue();
        prompt.AmountAutoPosted.Should().BeFalse();
        prompt.EstimatedPenaltyAmount.Should().BeNull();
    }

    [Fact]
    public void MissingPenaltyClauseFailsClosedWithoutInventingAnAmount()
    {
        var input = Input(
            penaltyClause: null,
            deliveries:
            [
                new(
                    Guid.NewGuid(), "PO-0408", "Sent",
                    Now.AddDays(-2), 200m)
            ]);

        var prompts =
            ProcurementContractOperationsRules.Evaluate(input, Now);

        prompts.Should().Contain(item =>
            item.Type == "PenaltyConfiguration" &&
            item.Severity == "High" &&
            item.EstimatedPenaltyAmount == null &&
            !item.AmountAutoPosted);
    }

    [Fact]
    public void OverduePaymentUsesOutstandingAmountButNeverPostsIt()
    {
        var input = Input(invoices:
        [
            new(
                Guid.NewGuid(), "INV-0408", "Approved",
                Now.AddDays(-35), 450m)
        ]);

        var prompt = ProcurementContractOperationsRules.Evaluate(input, Now)
            .Single(item => item.Type == "PaymentOverdue");

        prompt.Severity.Should().Be("Critical");
        prompt.Message.Should().Contain("450.00");
        prompt.AmountAutoPosted.Should().BeFalse();
    }

    [Theory]
    [InlineData(-2, "Expired", "Critical")]
    [InlineData(10, "Due", "Critical")]
    [InlineData(25, "Due", "High")]
    [InlineData(60, "Upcoming", "Medium")]
    public void OperationalExpiryCreatesExplainableRenewalPrompt(
        int days,
        string renewalStatus,
        string severity)
    {
        var input = Input(endDate: Now.AddDays(days));

        var prompt = ProcurementContractOperationsRules.Evaluate(input, Now)
            .Single(item => item.Type is "Renewal" or "ExpiredContract");

        ProcurementContractOperationsRules.RenewalStatus(
            input.EndDate, input.ContractStatus, Now)
            .Should().Be(renewalStatus);
        prompt.Severity.Should().Be(severity);
    }

    [Fact]
    public void CompletedContractWithHeldRetentionRequiresEvidenceReview()
    {
        var input = Input(
            status: "Completed",
            retentionHeld: 500m,
            retentionReleased: 100m);

        var prompt = ProcurementContractOperationsRules.Evaluate(input, Now)
            .Single(item => item.Type == "RetentionReview");

        prompt.Message.Should().Contain("400.00");
        prompt.RequiresIndependentApproval.Should().BeTrue();
    }

    [Fact]
    public void GovernedPerformanceRiskAndCurrencyBreachesAreVisible()
    {
        var input = Input(
            performanceScore: 45m,
            performanceTarget: 70m,
            performanceBreached: true,
            riskScore: 40m,
            riskBreached: true,
            currencyConflict: true);

        var prompts =
            ProcurementContractOperationsRules.Evaluate(input, Now);

        prompts.Select(item => item.Type).Should().Contain(
            ["SlaKpiBreach", "SupplierRisk", "CurrencyMismatch"]);
        ProcurementContractOperationsRules.OverallRisk(prompts)
            .Should().Be("Critical");
    }

    [Fact]
    public void CompletedAndCancelledSourcesDoNotCreateFalseDelayPrompts()
    {
        var input = Input(
            milestones:
            [
                new(
                    Guid.NewGuid(), "Accepted", "Completed",
                    Now.AddDays(-20), 100m)
            ],
            deliveries:
            [
                new(
                    Guid.NewGuid(), "PO-CANCELLED", "Cancelled",
                    Now.AddDays(-20), 100m)
            ],
            invoices:
            [
                new(
                    Guid.NewGuid(), "INV-VOID", "Voided",
                    Now.AddDays(-20), 100m)
            ]);

        ProcurementContractOperationsRules.Evaluate(input, Now)
            .Should().BeEmpty();
    }

    private static ProcurementContractOperationsRuleInput Input(
        string status = "Active",
        DateTime? endDate = null,
        string? penaltyClause = "Controlled clause",
        decimal retentionHeld = 0m,
        decimal retentionReleased = 0m,
        decimal? performanceScore = null,
        decimal? performanceTarget = null,
        bool performanceBreached = false,
        decimal? riskScore = null,
        bool riskBreached = false,
        bool currencyConflict = false,
        IReadOnlyList<ProcurementContractOperationsMilestoneInput>? milestones =
            null,
        IReadOnlyList<ProcurementContractOperationsDeliveryInput>? deliveries =
            null,
        IReadOnlyList<ProcurementContractOperationsInvoiceInput>? invoices =
            null) => new(
            Guid.NewGuid(),
            "CON-0408",
            status,
            10000m,
            "GHS",
            endDate,
            penaltyClause,
            retentionHeld,
            retentionReleased,
            performanceScore,
            performanceTarget,
            performanceBreached,
            riskScore,
            riskBreached,
            currencyConflict,
            milestones ?? [],
            deliveries ?? [],
            invoices ?? []);
}
