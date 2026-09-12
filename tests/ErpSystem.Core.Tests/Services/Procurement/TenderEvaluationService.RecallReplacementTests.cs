using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed partial class TenderEvaluationServiceCommitteeTests
{
    [Fact]
    public async Task ApprovedOwnRecallCreatesAndLocksNewProjectionWithoutRewritingOriginal()
    {
        var fixture = new Fixture();
        var bid = fixture.Bids[0];
        bid.Status = "Evaluated";
        var original = fixture.Evaluation(bid, fixture.Evaluator, "Submitted");
        original.IsRecommended = true;
        original.SubmittedDate = DateTime.UtcNow.AddDays(-1);
        var originalDate = original.SubmittedDate;
        var eligibility = AuthorizeReplacement(fixture);
        TenderEvaluation? replacement = null;
        fixture.Evaluations.Setup(item => item.GetByBidIdAsync(bid.Id)).ReturnsAsync([original]);
        fixture.Evaluations.Setup(item => item.CreateAsync(It.IsAny<TenderEvaluation>()))
            .Callback<TenderEvaluation>(item => replacement = item);

        var draft = await fixture.Service.CreateEvaluationAsync(new CreateEvaluationDto
        {
            TenderBidId = bid.Id, TechnicalScore = 90, IsRecommended = true,
            Recommendation = "Revalidated the supplier's current registration and approved evidence."
        });

        draft.Status.Should().Be("Draft");
        replacement.Should().NotBeNull();
        replacement!.Id.Should().NotBe(original.Id);
        bid.Status.Should().Be("Evaluated", "an approved recall is not a generic bid reopen");
        // GetByIdAsync eagerly loads these references in the real repository.
        replacement.TenderEvaluator = fixture.Evaluator;
        replacement.TenderBid = bid;
        fixture.Evaluations.Setup(item => item.GetByIdAsync(replacement.Id)).ReturnsAsync(replacement);
        fixture.Evaluators.Setup(item => item.GetByIdAsync(fixture.Evaluator.Id))
            .ReturnsAsync(fixture.Evaluator);
        fixture.UnitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());
        fixture.Committee.Setup(item => item.GetAsync(
                ProcurementEvaluationSourceType.Tender, fixture.TenderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationCommitteeDto
            {
                Id = eligibility.CommitteeControlId!.Value,
                Members = [new ProcurementEvaluationAppointmentDto { Id = eligibility.AppointmentId!.Value }],
                Meetings = [new ProcurementEvaluationMeetingDto { Id = eligibility.MeetingId!.Value }]
            });

        var submitted = await fixture.Service.SubmitEvaluationAsync(replacement.Id, new SubmitEvaluationDto
        {
            ConfirmSubmission = true, SignatureReference = "SIG-REVALIDATED",
            EvidenceReference = "SUPPLIER-REVALIDATION-001", IdempotencyKey = "replacement-lock"
        });

        submitted.Status.Should().Be("Submitted");
        submitted.SubmittedDate.Should().BeAfter(originalDate!.Value);
        original.Status.Should().Be("Submitted");
        original.SubmittedDate.Should().Be(originalDate);
        fixture.Committee.Verify(item => item.LockScoreSheetAsync(
            It.Is<LockProcurementEvaluationScoreSheetRequest>(request =>
                request.ScoreSubjectId == bid.Id && request.ScoreSubjectType == "TenderEvaluation" &&
                request.Phase == ProcurementEvaluationPhase.Combined &&
                request.ScoreSnapshotJson.Contains(replacement.Id.ToString()) &&
                request.ScoreSnapshotJson.Contains(fixture.UserId.ToString()) &&
                request.SignatureReference == "SIG-REVALIDATED"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Evaluations.Verify(item => item.UpdateAsync(
            It.Is<TenderEvaluation>(evaluation => evaluation.Id == original.Id)), Times.Never);
        fixture.BidsRepository.Verify(item => item.UpdateAsync(It.IsAny<TenderBid>()), Times.Never);
    }

    [Theory]
    [InlineData("no-recall")]
    [InlineData("pending-or-rejected")]
    [InlineData("consumed")]
    [InlineData("other-actor")]
    [InlineData("other-source")]
    [InlineData("other-phase")]
    public async Task EvaluatedBidRequiresOwnExactUnconsumedRecall(string variant)
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Evaluated";
        var eligibility = AuthorizeReplacement(fixture);
        switch (variant)
        {
            case "no-recall": eligibility.AuthorizedAttempt = 1; break;
            case "pending-or-rejected": eligibility.Allowed = false; break;
            case "consumed": eligibility.Allowed = false; eligibility.AuthorizedAttempt = 2; break;
            case "other-actor": eligibility.ActorUserId = Guid.NewGuid(); break;
            case "other-source": eligibility.SourceId = Guid.NewGuid(); break;
            case "other-phase": eligibility.Phase = ProcurementEvaluationPhase.Financial; break;
        }

        await fixture.Service.Invoking(service => service.CreateEvaluationAsync(
                new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_REPLACEMENT_RECALL_REQUIRED");

        fixture.Evaluations.Verify(item => item.CreateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
        fixture.UnitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Awarded", false)]
    [InlineData("Cancelled", false)]
    [InlineData("Evaluated", true)]
    public async Task PreviouslyApprovedRecallCannotReopenFinalTender(string status, bool awardDate)
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Evaluated";
        AuthorizeReplacement(fixture);
        fixture.Tenders.Setup(item => item.GetByIdAsync(fixture.TenderId)).ReturnsAsync(new Tender
        {
            Id = fixture.TenderId, TenantId = fixture.TenantId, Status = status,
            AwardDate = awardDate ? DateTime.UtcNow.AddDays(-1) : null
        });

        await fixture.Service.Invoking(service => service.CreateEvaluationAsync(
                new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_SCORE_RECALL_DOWNSTREAM_FINAL");
        fixture.Evaluations.Verify(item => item.CreateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
    }

    [Theory]
    [InlineData("bid-tenant")]
    [InlineData("tender-tenant")]
    [InlineData("deleted-bid")]
    [InlineData("deleted-tender")]
    public async Task ApprovedRecallCannotCrossTenantOrDeletedSource(string variant)
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Evaluated";
        AuthorizeReplacement(fixture);
        var tender = new Tender { Id = fixture.TenderId, TenantId = fixture.TenantId, Status = "Evaluated" };
        switch (variant)
        {
            case "bid-tenant": fixture.Bids[0].TenantId = Guid.NewGuid(); break;
            case "tender-tenant": tender.TenantId = Guid.NewGuid(); break;
            case "deleted-bid": fixture.Bids[0].IsDeleted = true; break;
            case "deleted-tender": tender.IsDeleted = true; break;
        }
        fixture.Tenders.Setup(item => item.GetByIdAsync(fixture.TenderId)).ReturnsAsync(tender);

        await fixture.Service.Invoking(service => service.CreateEvaluationAsync(
                new CreateEvaluationDto { TenderBidId = fixture.Bids[0].Id }))
            .Should().ThrowAsync<ProcurementEvaluationCommitteeAuthorizationException>();
        fixture.Evaluations.Verify(item => item.CreateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
    }

    [Theory]
    [InlineData("Submitted", "update")]
    [InlineData("Approved", "update")]
    [InlineData("Submitted", "submit")]
    [InlineData("Approved", "submit")]
    public async Task ApprovedRecallNeverMakesOriginalProjectionMutable(string status, string stage)
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Evaluated";
        AuthorizeReplacement(fixture);
        var original = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, status);
        fixture.Evaluations.Setup(item => item.GetByIdAsync(original.Id)).ReturnsAsync(original);
        Func<Task> action = stage == "update"
            ? () => fixture.Service.UpdateEvaluationAsync(original.Id, new UpdateEvaluationDto { TechnicalScore = 1 })
            : () => fixture.Service.SubmitEvaluationAsync(original.Id, new SubmitEvaluationDto { ConfirmSubmission = true });

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("Only a draft evaluation*");
        fixture.Evaluations.Verify(item => item.UpdateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
        fixture.Committee.Verify(item => item.LockScoreSheetAsync(
            It.IsAny<LockProcurementEvaluationScoreSheetRequest>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("submit")]
    public async Task ReplacementDraftRechecksRecallBeforeAnyUpdateOrSubmission(string stage)
    {
        var fixture = new Fixture();
        fixture.Bids[0].Status = "Evaluated";
        var draft = fixture.Evaluation(fixture.Bids[0], fixture.Evaluator, "Draft");
        fixture.Evaluations.Setup(item => item.GetByIdAsync(draft.Id)).ReturnsAsync(draft);
        // The default subject decision permits only attempt 1: no approved recall.
        Func<Task> action = stage == "update"
            ? () => fixture.Service.UpdateEvaluationAsync(draft.Id, new UpdateEvaluationDto { TechnicalScore = 1 })
            : () => fixture.Service.SubmitEvaluationAsync(draft.Id, new SubmitEvaluationDto { ConfirmSubmission = true });

        await action.Should().ThrowAsync<ProcurementEvaluationCommitteeConflictException>()
            .Where(exception => exception.Code == "EVALUATION_REPLACEMENT_RECALL_REQUIRED");
        fixture.Evaluations.Verify(item => item.UpdateAsync(It.IsAny<TenderEvaluation>()), Times.Never);
    }

    private static ProcurementEvaluationScorerEligibilityDto AuthorizeReplacement(Fixture fixture)
    {
        var eligibility = new ProcurementEvaluationScorerEligibilityDto
        {
            Allowed = true, AuthorizedAttempt = 2, ActorUserId = fixture.UserId,
            SourceType = ProcurementEvaluationSourceType.Tender, SourceId = fixture.TenderId,
            Phase = ProcurementEvaluationPhase.Combined, CommitteeControlId = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(), MeetingId = Guid.NewGuid()
        };
        fixture.Committee.Setup(item => item.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender, fixture.TenderId,
                ProcurementEvaluationPhase.Combined, "TenderEvaluation", fixture.Bids[0].Id,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(eligibility);
        return eligibility;
    }
}
