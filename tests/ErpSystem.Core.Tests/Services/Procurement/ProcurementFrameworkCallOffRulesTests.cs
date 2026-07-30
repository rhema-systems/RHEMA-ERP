using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkCallOffRulesTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CommercialRevalidationRunsOnlyForApproval(
        bool approved,
        bool expected)
    {
        ProcurementFrameworkCallOffCommercialRules
            .RequiresCommercialRevalidationOnDecision(approved)
            .Should().Be(expected);
    }

    [Fact]
    public void LineTotalUsesThePersistedFourDecimalQuantity()
    {
        var normalized =
            ProcurementFrameworkCallOffCommercialRules.NormalizeLine(
                1.23456m,
                1000m);

        normalized.Quantity.Should().Be(1.2346m);
        normalized.LineTotal.Should().Be(1234.60m);
    }

    [Fact]
    public void RevisionCannotReserveMoreThanAgreementFamilyBalance()
    {
        var rejected =
            ProcurementFrameworkCallOffCommercialRules.EvaluateFamilyCapacity(
                1000m,
                600m,
                500m);
        var allowed =
            ProcurementFrameworkCallOffCommercialRules.EvaluateFamilyCapacity(
                1000m,
                600m,
                400m);

        rejected.CommittedAmount.Should().Be(600m);
        rejected.AvailableAmount.Should().Be(400m);
        rejected.CanReserve.Should().BeFalse();
        allowed.CanReserve.Should().BeTrue();
    }

    [Fact]
    public void NewerEffectiveRevisionSupersedesAnOverlappingPublishedRevision()
    {
        var v1 = Revision(
            version: 1,
            effectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = Revision(
            version: 2,
            effectiveFromUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var beforeReplacement =
            new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var afterReplacement =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        ProcurementFrameworkCallOffCommercialRules
            .SelectCurrentEffectiveRevisions([v1, v2], beforeReplacement)
            .Should().ContainSingle()
            .Which.AgreementId.Should().Be(v1.AgreementId);
        ProcurementFrameworkCallOffCommercialRules
            .SelectCurrentEffectiveRevisions([v1, v2], afterReplacement)
            .Should().ContainSingle()
            .Which.AgreementId.Should().Be(v2.AgreementId);
        ProcurementFrameworkCallOffCommercialRules.IsCurrentEffectiveRevision(
                v1,
                [v1, v2],
                afterReplacement)
            .Should().BeFalse();
    }

    [Fact]
    public void SummaryCountsOneCeilingAndAllMovementsPerAgreementFamily()
    {
        var atUtc = new DateTime(
            2026,
            8,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var v1 = Revision(
            version: 1,
            effectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = Revision(
            version: 2,
            effectiveFromUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var movements = new[]
        {
            Movement(
                v1.AgreementId,
                ProcurementFrameworkBalanceMovementType.Commitment,
                600m),
            Movement(
                v1.AgreementId,
                ProcurementFrameworkBalanceMovementType.Issue,
                250m)
        };

        var summary = ProcurementFrameworkCallOffCommercialRules
            .SummarizeFamilies([v1, v2], movements, atUtc)
            .Should().ContainSingle().Which;

        summary.CurrentAgreementId.Should().Be(v2.AgreementId);
        summary.CeilingAmount.Should().Be(1000m);
        summary.CommittedAmount.Should().Be(600m);
        summary.IssuedAmount.Should().Be(250m);
        summary.AvailableAmount.Should().Be(400m);
    }

    [Fact]
    public void HistoryRowsUseTheSameFamilyBalanceAcrossAllRevisions()
    {
        var atUtc = new DateTime(
            2026,
            8,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var v1 = Revision(
            version: 1,
            effectiveFromUtc: new DateTime(
                2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = Revision(
            version: 2,
            effectiveFromUtc: new DateTime(
                2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var summaries = ProcurementFrameworkCallOffCommercialRules
            .SummarizeRevisionFamilies(
                [v1, v2],
                [
                    Movement(
                        v1.AgreementId,
                        ProcurementFrameworkBalanceMovementType.Commitment,
                        600m),
                    Movement(
                        v2.AgreementId,
                        ProcurementFrameworkBalanceMovementType.Commitment,
                        400m)
                ],
                atUtc);

        summaries[v1.AgreementId].CommittedAmount.Should().Be(1000m);
        summaries[v1.AgreementId].AvailableAmount.Should().Be(0m);
        summaries[v2.AgreementId].CommittedAmount.Should().Be(1000m);
        summaries[v2.AgreementId].AvailableAmount.Should().Be(0m);
    }

    [Fact]
    public void ExpiryAlertUsesTheCurrentRevisionAndFamilyWideCapacity()
    {
        var atUtc = new DateTime(
            2026,
            12,
            15,
            0,
            0,
            0,
            DateTimeKind.Utc);
        var v1 = Revision(
            version: 1,
            effectiveFromUtc: new DateTime(
                2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var v2 = Revision(
            version: 2,
            effectiveFromUtc: new DateTime(
                2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var summary = ProcurementFrameworkCallOffCommercialRules
            .SummarizeFamilies(
                [v1, v2],
                [
                    Movement(
                        v1.AgreementId,
                        ProcurementFrameworkBalanceMovementType.Commitment,
                        900m),
                    Movement(
                        v2.AgreementId,
                        ProcurementFrameworkBalanceMovementType.Commitment,
                        100m)
                ],
                atUtc)
            .Should().ContainSingle().Which;

        ProcurementFrameworkCallOffCommercialRules.ShouldEmitExpiryAlert(
                summary,
                v2.AgreementId,
                null,
                atUtc,
                30)
            .Should().BeFalse(
                "the agreement family has no remaining capacity");
        ProcurementFrameworkCallOffCommercialRules.ShouldEmitExpiryAlert(
                summary with { AvailableAmount = 100m },
                v1.AgreementId,
                null,
                atUtc,
                30)
            .Should().BeFalse(
                "only the current effective revision may emit the family alert");
        ProcurementFrameworkCallOffCommercialRules.ShouldEmitExpiryAlert(
                summary with { AvailableAmount = 100m },
                v2.AgreementId,
                null,
                atUtc,
                30)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(ProcurementFrameworkCallOffStatus.Draft, "submit")]
    [InlineData(ProcurementFrameworkCallOffStatus.Draft, "cancel")]
    [InlineData(ProcurementFrameworkCallOffStatus.PendingApproval, "approve")]
    [InlineData(ProcurementFrameworkCallOffStatus.PendingApproval, "reject")]
    [InlineData(ProcurementFrameworkCallOffStatus.Approved, "issue")]
    [InlineData(ProcurementFrameworkCallOffStatus.Approved, "cancel")]
    public void AllowedActionsAreExplicitAndServerOwned(
        ProcurementFrameworkCallOffStatus status,
        string expectedAction)
    {
        var callOff = new ProcurementFrameworkCallOff { Status = status };

        ProcurementFrameworkCallOffRules.AllowedActions(callOff)
            .Should().Contain(expectedAction);
    }

    [Theory]
    [InlineData(ProcurementFrameworkCallOffStatus.Issued)]
    [InlineData(ProcurementFrameworkCallOffStatus.Rejected)]
    [InlineData(ProcurementFrameworkCallOffStatus.Cancelled)]
    public void TerminalOutcomesExposeNoMutationActions(
        ProcurementFrameworkCallOffStatus status)
    {
        var callOff = new ProcurementFrameworkCallOff { Status = status };

        ProcurementFrameworkCallOffRules.AllowedActions(callOff)
            .Should().BeEmpty();
    }

    private static ProcurementFrameworkCallOffCommercialRules
        .AgreementRevisionState Revision(
            int version,
            DateTime effectiveFromUtc) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            version,
            "GHS",
            1000m,
            effectiveFromUtc,
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            true);

    private static ProcurementFrameworkCallOffCommercialRules.MovementState
        Movement(
            Guid agreementId,
            ProcurementFrameworkBalanceMovementType movementType,
            decimal amount) =>
        new(agreementId, movementType, amount);
}
