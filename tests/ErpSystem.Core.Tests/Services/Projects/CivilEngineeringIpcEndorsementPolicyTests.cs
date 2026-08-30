using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Services.Projects;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Projects;

public sealed class CivilEngineeringIpcEndorsementPolicyTests
{
    [Fact]
    public void Endorsement_requires_current_published_evidence_when_the_frozen_policy_requires_it()
    {
        var request = new ReviewCivilEngineeringIpcEndorsementRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Endorse = true, Notes = "Engineering measurement and technical evidence checked."
        };
        CivilEngineeringIpcEndorsementPolicy.ValidateReview(request, true)
            .Should().Contain(value => value.Contains("evidence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Endorsement_can_proceed_without_evidence_when_the_frozen_policy_does_not_require_it()
    {
        var request = new ReviewCivilEngineeringIpcEndorsementRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Endorse = true,
            Notes = "Engineering measurement and technical evidence checked."
        };

        CivilEngineeringIpcEndorsementPolicy.ValidateReview(request, false).Should().BeEmpty();
    }

    [Fact]
    public void Return_requires_notes_but_never_accepts_endorsement_evidence()
    {
        var request = new ReviewCivilEngineeringIpcEndorsementRequest
        {
            ClientRequestId = Guid.NewGuid(), RowVersion = "AQID", Endorse = false,
            Notes = "Correct the measured quantities before resubmission.", EvidenceDocumentRecordId = Guid.NewGuid(), EvidenceDocumentVersionId = Guid.NewGuid()
        };
        CivilEngineeringIpcEndorsementPolicy.ValidateReview(request, true)
            .Should().Contain(value => value.Contains("must not", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Qs_workflow_cannot_advance_while_the_latest_civil_review_is_pending_or_returned()
    {
        var pending = new ProjectCivilIpcEndorsement { Status = CivilEngineeringIpcEndorsementStatuses.AwaitingProjectEngineerReview };
        var returned = new ProjectCivilIpcEndorsement { Status = CivilEngineeringIpcEndorsementStatuses.ReturnedToProjectsCoordinator };
        Action pendingAction = () => CivilEngineeringIpcEndorsementPolicy.RequireCanAdvance(pending, true);
        Action returnedAction = () => CivilEngineeringIpcEndorsementPolicy.RequireCanAdvance(returned, true);
        pendingAction.Should().Throw<InvalidOperationException>().WithMessage("*awaiting*");
        returnedAction.Should().Throw<InvalidOperationException>().WithMessage("*returned*");
    }

    [Fact]
    public void Endorsed_review_allows_qs_workflow_to_continue_but_locks_certificate_amendment()
    {
        var endorsed = new ProjectCivilIpcEndorsement { Status = CivilEngineeringIpcEndorsementStatuses.Endorsed };
        Action action = () => CivilEngineeringIpcEndorsementPolicy.RequireCanAdvance(endorsed, true);
        action.Should().NotThrow();
        CivilEngineeringIpcEndorsementPolicy.IsAmendmentBlocked(endorsed).Should().BeTrue();
    }
}
