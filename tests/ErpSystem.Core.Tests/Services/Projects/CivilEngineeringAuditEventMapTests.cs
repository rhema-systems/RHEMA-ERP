using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.Audit;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringAuditEventMapTests
{
    private const CivilEngineeringAuditFacet MandatoryPayload =
        CivilEngineeringAuditFacet.Tenant |
        CivilEngineeringAuditFacet.Actor |
        CivilEngineeringAuditFacet.ActorRoles |
        CivilEngineeringAuditFacet.Source |
        CivilEngineeringAuditFacet.BeforeValues |
        CivilEngineeringAuditFacet.AfterValues |
        CivilEngineeringAuditFacet.FileVersionEvidence |
        CivilEngineeringAuditFacet.ApprovalState |
        CivilEngineeringAuditFacet.CorrelationId;

    [Fact]
    public void Known_civil_actions_are_unique_classifiable_and_require_the_complete_lineage_payload()
    {
        var definitions = CivilEngineeringAuditEventMap.Definitions;

        definitions.Should().HaveCountGreaterThanOrEqualTo(110);
        definitions.Select(value => value.Action).Should().OnlyHaveUniqueItems();
        definitions.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value.SourceType));
        definitions.Should().OnlyContain(value =>
            AuditOperationClassifier.Classify(value.Action) == value.Operation);
        definitions.Should().OnlyContain(value =>
            (value.RequiredFacets & MandatoryPayload) == MandatoryPayload);
    }

    [Fact]
    public void Map_covers_configuration_design_supervision_maintenance_permitting_tasks_and_handover()
    {
        var representativeActions = new[]
        {
            CivilEngineeringAuditEventMap.ApproveDecision,
            CivilEngineeringAuditEventMap.ApproveEngineeringCase,
            CivilEngineeringAuditEventMap.ApproveDesignPackage,
            CivilEngineeringAuditEventMap.PublishEngineeringFileVersion,
            CivilEngineeringAuditEventMap.IssueSiteInstruction,
            CivilEngineeringAuditEventMap.AcknowledgeSiteInstruction,
            CivilEngineeringAuditEventMap.ApproveEngineeringTestReport,
            CivilEngineeringAuditEventMap.SubmitIpcEngineeringCheck,
            CivilEngineeringAuditEventMap.ApproveIpcEndorsement,
            CivilEngineeringAuditEventMap.ApproveRemediationScope,
            CivilEngineeringAuditEventMap.LinkMaintenanceWorkOrder,
            CivilEngineeringAuditEventMap.ApproveDevelopmentDecision,
            CivilEngineeringAuditEventMap.OverrideCivilTaskUrgency,
            CivilEngineeringAuditEventMap.ApproveCivilTaskCompletion,
            CivilEngineeringAuditEventMap.ApproveCivilVariationImpact,
            CivilEngineeringAuditEventMap.ApproveCivilDefectClosure,
            CivilEngineeringAuditEventMap.CloseCivilInspection,
            CivilEngineeringAuditEventMap.ApproveCivilCompletionCertificate,
            CivilEngineeringAuditEventMap.ApproveCivilHandover,
            CivilEngineeringAuditEventMap.LinkCivilAssetHistory,
            CivilEngineeringAuditEventMap.ApproveCivilMigrationBatch
        };

        representativeActions.Select(CivilEngineeringAuditEventMap.GetRequired)
            .Should().OnlyContain(value => value.RequiredFacets.HasFlag(CivilEngineeringAuditFacet.Source));
    }

    [Fact]
    public void Shared_coverage_contributor_exposes_the_complete_civil_map_without_claiming_finance_posting()
    {
        var definitions = new CivilEngineeringAuditEventCoverageContributor().GetDefinitions();

        definitions.Should().NotBeEmpty();
        definitions.Should().OnlyContain(value => value.Module == "Civil Engineering");
        definitions.SelectMany(value => value.EmittedActions)
            .Should().BeEquivalentTo(CivilEngineeringAuditEventMap.Definitions.Select(value => value.Action));
        definitions.Should().OnlyContain(value =>
            value.EmittedActions.All(action => AuditOperationClassifier.Classify(action) == value.Operation));
        definitions.Select(value => value.Operation).Should().Contain(
        [
            AuditOperationKind.Create,
            AuditOperationKind.Update,
            AuditOperationKind.Approve,
            AuditOperationKind.Reject,
            AuditOperationKind.Override,
            AuditOperationKind.Dispatch,
            AuditOperationKind.Receive
        ]);
        definitions.Select(value => value.Operation).Should().NotContain(
        [
            AuditOperationKind.Post,
            AuditOperationKind.Reverse
        ]);
    }

    [Fact]
    public void Approval_rejection_and_controlled_amendment_events_require_reasons()
    {
        foreach (var action in new[]
                 {
                     CivilEngineeringAuditEventMap.UpdateEngineeringCase,
                     CivilEngineeringAuditEventMap.ApproveDesign,
                     CivilEngineeringAuditEventMap.RejectDesign,
                     CivilEngineeringAuditEventMap.CloseCivilInspection,
                     CivilEngineeringAuditEventMap.ApproveCivilWorkClosure,
                     CivilEngineeringAuditEventMap.RejectDevelopmentDecision,
                     CivilEngineeringAuditEventMap.OverrideCivilTaskUrgency
                 })
        {
            CivilEngineeringAuditEventMap.GetRequired(action).RequiredFacets
                .Should().HaveFlag(CivilEngineeringAuditFacet.Reason);
        }
    }

    [Fact]
    public void Unregistered_action_is_rejected()
    {
        var action = () => CivilEngineeringAuditEventMap.GetRequired("UnmappedCivilAction");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not registered in the shared event map*");
    }
}
