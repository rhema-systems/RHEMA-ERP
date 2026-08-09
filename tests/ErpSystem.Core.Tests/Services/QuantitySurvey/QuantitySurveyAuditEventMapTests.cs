using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.Audit;
using ErpSystem.Core.Services.QuantitySurvey;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyAuditEventMapTests
{
    private const QuantitySurveyAuditFacet RequiredContext =
        QuantitySurveyAuditFacet.Actor |
        QuantitySurveyAuditFacet.ActorRoles |
        QuantitySurveyAuditFacet.Source |
        QuantitySurveyAuditFacet.CorrelationId;

    [Fact]
    public void Configuration_lifecycle_actions_are_unique_registered_and_classifiable()
    {
        var definitions = QuantitySurveyAuditEventMap.Definitions;

        definitions.Should().HaveCount(38);
        definitions.Select(value => value.Action).Should().OnlyHaveUniqueItems();
        definitions.Should().OnlyContain(value =>
            AuditOperationClassifier.Classify(value.Action) == value.Operation);
        definitions.Should().OnlyContain(value =>
            (value.RequiredFacets & RequiredContext) == RequiredContext);
    }

    [Fact]
    public void Rate_library_lifecycle_actions_are_registered_with_change_and_approval_evidence()
    {
        foreach (var action in new[]
                 {
                     QuantitySurveyAuditEventMap.UpdateRateLibraryItem,
                     QuantitySurveyAuditEventMap.CreateRateDraft,
                     QuantitySurveyAuditEventMap.UpdateRateDraft,
                     QuantitySurveyAuditEventMap.CreateMarketSurveyUpdate,
                     QuantitySurveyAuditEventMap.PromoteHistoricalRate,
                     QuantitySurveyAuditEventMap.CreateRateBuildUp,
                     QuantitySurveyAuditEventMap.PublishRate,
                     QuantitySurveyAuditEventMap.RetireRate
                 })
        {
            var definition = QuantitySurveyAuditEventMap.GetRequired(action);
            definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.Reason);
            definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.AfterValues);
        }

        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.CreateRateLibraryItem)
            .RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.AfterValues);

        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.PublishRate)
            .RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.ApprovalState);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.RetireRate)
            .RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.ApprovalState);
    }

    [Fact]
    public void Tender_boq_intake_and_vetting_actions_require_lineage_evidence_and_review_state()
    {
        foreach (var action in new[]
                 {
                     QuantitySurveyAuditEventMap.StageTenderBoqSubmission,
                     QuantitySurveyAuditEventMap.CommitTenderBoqSubmission,
                     QuantitySurveyAuditEventMap.AcceptTenderBoqSubmission,
                     QuantitySurveyAuditEventMap.RejectTenderBoqSubmission
                 })
        {
            var definition = QuantitySurveyAuditEventMap.GetRequired(action);
            definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.FormulaInputs);
            definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.EvidenceLinks);
        }

        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.StageTenderBoqSubmission)
            .Operation.Should().Be(AuditOperationKind.Create);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.CommitTenderBoqSubmission)
            .Operation.Should().Be(AuditOperationKind.Update);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.AcceptTenderBoqSubmission)
            .Operation.Should().Be(AuditOperationKind.Approve);
        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.RejectTenderBoqSubmission)
            .Operation.Should().Be(AuditOperationKind.Reject);
    }

    [Fact]
    public void Boq_version_creation_requires_snapshot_inputs_and_reason()
    {
        var definition = QuantitySurveyAuditEventMap.GetRequired(
            QuantitySurveyAuditEventMap.CreateBoqVersion);

        definition.Operation.Should().Be(AuditOperationKind.Create);
        definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.AfterValues);
        definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.FormulaInputs);
        definition.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.Reason);
    }

    [Fact]
    public void Boq_approval_and_publication_actions_are_registered_with_approval_state()
    {
        foreach (var action in new[]
                 {
                     QuantitySurveyAuditEventMap.SubmitBoqVersion,
                     QuantitySurveyAuditEventMap.ApproveBoqVersion,
                     QuantitySurveyAuditEventMap.RejectBoqVersion,
                     QuantitySurveyAuditEventMap.RecallBoqVersion,
                     QuantitySurveyAuditEventMap.PublishBoqVersion,
                     QuantitySurveyAuditEventMap.RetireBoqPublication
                 })
        {
            QuantitySurveyAuditEventMap.GetRequired(action).RequiredFacets
                .Should().HaveFlag(QuantitySurveyAuditFacet.ApprovalState);
        }
    }

    [Fact]
    public void Estimate_version_actions_capture_formula_lineage_reason_and_approval_state()
    {
        var create = QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.CreateEstimateVersion);
        create.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.FormulaInputs);
        create.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.EvidenceLinks);
        create.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.Reason);

        foreach (var action in new[]
                 {
                     QuantitySurveyAuditEventMap.SubmitEstimateVersion,
                     QuantitySurveyAuditEventMap.ApproveEstimateVersion,
                     QuantitySurveyAuditEventMap.RejectEstimateVersion,
                     QuantitySurveyAuditEventMap.RetireEstimateVersion
                 })
        {
            QuantitySurveyAuditEventMap.GetRequired(action).RequiredFacets
                .Should().HaveFlag(QuantitySurveyAuditFacet.ApprovalState);
        }
    }

    [Fact]
    public void Decision_and_evidence_events_require_their_control_payloads()
    {
        var save = QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.SaveDecision);
        save.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.FormulaInputs);
        save.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.ApprovalState);
        save.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.BeforeValues);
        save.RequiredFacets.Should().HaveFlag(QuantitySurveyAuditFacet.AfterValues);

        foreach (var action in new[]
                 {
                     QuantitySurveyAuditEventMap.LinkEvidence,
                     QuantitySurveyAuditEventMap.UnlinkEvidence,
                     QuantitySurveyAuditEventMap.ApproveDecision,
                     QuantitySurveyAuditEventMap.RejectDecision
                 })
        {
            QuantitySurveyAuditEventMap.GetRequired(action).RequiredFacets
                .Should().HaveFlag(QuantitySurveyAuditFacet.EvidenceLinks);
        }
    }

    [Fact]
    public void Shared_coverage_contributor_exposes_only_registered_QS_actions()
    {
        var contributor = new QuantitySurveyAuditEventCoverageContributor();
        var definitions = contributor.GetDefinitions();

        definitions.Should().NotBeEmpty();
        definitions.Should().OnlyContain(value => value.Module == "Quantity Survey");
        definitions.SelectMany(value => value.EmittedActions)
            .Should().BeEquivalentTo(QuantitySurveyAuditEventMap.Definitions.Select(value => value.Action));
        definitions.Should().OnlyContain(value =>
            value.EmittedActions.All(action => AuditOperationClassifier.Classify(action) == value.Operation));
        definitions.Select(value => value.Operation).Should().Contain(
        [
            AuditOperationKind.Create,
            AuditOperationKind.Update,
            AuditOperationKind.Approve,
            AuditOperationKind.Reject
        ]);
    }

    [Fact]
    public void Unregistered_action_is_rejected()
    {
        var action = () => QuantitySurveyAuditEventMap.GetRequired("UnmappedAction");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not registered*");
    }
}
