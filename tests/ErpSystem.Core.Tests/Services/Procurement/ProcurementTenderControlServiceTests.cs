using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementTenderControlServiceTests
{
    [Theory]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.InternationalCompetitiveTendering)]
    public async Task PublicationLocksExactNctOrIctLineageAndAllDecisionKeys(ProcurementMethodType method)
    {
        await using var fixture = new Fixture(method);

        var published = await fixture.PublishAsync();

        published.Method.Should().Be(method);
        published.MethodRuleCode.Should().Be(fixture.Rule.RuleCode);
        published.AuthorityRouteReference.Should().Be(fixture.Route.RouteReference);
        published.Status.Should().Be(ProcurementTenderControlStatus.Advertised);
        published.TenderDocumentReference.Should().Be("TDC-CONTROLLED/v4");
        Guid.TryParse(published.TenderDocumentVersion, out _).Should().BeTrue();
        published.IntegrityHash.Should().HaveLength(64);
        fixture.SourcingCases.Verify(service => service.RevalidateSourceEntryAsync(
            fixture.Requisition.Id, fixture.ReleaseId, fixture.Case.Id, method,
            "Tender", fixture.Tender.Id, fixture.Tender.TenderNumber,
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "TenderAdvertised" && request.DecisionKeys.Count == 14),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublicationRejectsFeeThatContradictsExactControlledRegister()
    {
        await using var fixture = new Fixture();

        var action = () => fixture.PublishAsync(documentFee: 0m, controlledFee: 25m);

        await action.Should().ThrowAsync<ProcurementTenderControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_FEE_MISMATCH");
        (await fixture.Context.ProcurementTenderControls.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PaidDocumentIssueSealedReceiptAndPublicOpeningEnforceStatutoryHardStops()
    {
        await using var fixture = new Fixture();
        await fixture.PublishAsync(documentFee: 250m);

        await fixture.Service.Invoking(service => service.IssueDocumentAsync(
                fixture.Tender.Id, fixture.DocumentIssue(fixture.Suppliers[0], amountPaid: 0m), "fee-mismatch"))
            .Should().ThrowAsync<ProcurementTenderControlValidationException>()
            .Where(exception => exception.Code == "TENDER_DOCUMENT_FEE_MISMATCH");

        await fixture.IssueDocumentsAsync(amountPaid: 250m);
        var deadline = fixture.Tender.SubmissionDeadline!.Value;
        (await fixture.Service.RecordSubmissionAsync(
            fixture.Bids[0], deadline.AddMinutes(-2), "receipt-on-time"))
            .Should().Be(ProcurementTenderSubmissionDisposition.OnTimeAccepted);
        (await fixture.Service.RecordSubmissionAsync(
            fixture.Bids[1], deadline.AddSeconds(1), "receipt-late"))
            .Should().Be(ProcurementTenderSubmissionDisposition.LateRejected);

        var sealedControl = await fixture.Service.GetAsync(fixture.Tender.Id);
        sealedControl.SubmissionReceipts.Should().ContainSingle(item =>
            item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted);
        sealedControl.SubmissionReceipts.Should().ContainSingle(item =>
            item.Disposition == ProcurementTenderSubmissionDisposition.LateRejected);
        sealedControl.SubmissionReceipts.Should().OnlyContain(item =>
            item.TenderBidId == Guid.Empty &&
            item.BusinessPartnerId == Guid.Empty &&
            item.BusinessPartnerName == "Sealed bidder");
        sealedControl.Milestones.Should().HaveCount(14);
        sealedControl.Milestones.Select(item => item.Code).Should()
            .Equal(Enumerable.Range(1, 14).Select(item => $"DEC-{item:000}"));

        await fixture.Service.Invoking(service => service.CompleteOpeningAsync(
                fixture.Tender.Id, fixture.OpeningRequest(), "opening-too-early"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == "TENDER_OPENING_BEFORE_DEADLINE");

        await fixture.AddSecondOnTimeReceiptAsync();
        await fixture.MoveDeadlineToPastAsync();
        var opened = await fixture.Service.CompleteOpeningAsync(
            fixture.Tender.Id, fixture.OpeningRequest(), "opening-signed");

        opened.Status.Should().Be(ProcurementTenderControlStatus.Opened);
        opened.SubmissionReceipts.Where(item =>
            item.Disposition == ProcurementTenderSubmissionDisposition.OnTimeAccepted)
            .Should().OnlyContain(item => item.OpenedAtUtc.HasValue);
        opened.SubmissionReceipts.Should().OnlyContain(item =>
            item.TenderBidId != Guid.Empty &&
            item.BusinessPartnerId != Guid.Empty &&
            item.BusinessPartnerName.StartsWith("Supplier "));
    }

    [Fact]
    public async Task ManualPaymentPendingVerificationBlocksFormalOpeningAndIsAudited()
    {
        await using var fixture = new Fixture();
        await fixture.PublishAsync();
        await fixture.IssueDocumentsAsync();
        var fee = new TenderFee
        {
            Id = Guid.NewGuid(), TenantId = fixture.CurrentTenantId,
            TenderId = fixture.Tender.Id, FeeType = "SubmissionFee",
            Amount = 100m, Currency = "GHS", PaymentMethod = "BankTransfer",
            IsMandatory = true
        };
        fixture.Context.Add(fee);
        fixture.Context.Add(new TenderPayment
        {
            Id = Guid.NewGuid(), TenantId = fixture.CurrentTenantId,
            TenderFeeId = fee.Id, BusinessPartnerId = fixture.Bids[0].BusinessPartnerId,
            PaymentReference = "BANK-PENDING-001", TransactionId = "BANK-PENDING-001",
            PaymentProof = "BANK-PENDING-001", Amount = fee.Amount, Currency = fee.Currency,
            PaymentMethod = fee.PaymentMethod, Status = "Pending", PaymentDate = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();
        await fixture.Service.RecordSubmissionAsync(
            fixture.Bids[0], fixture.Tender.SubmissionDeadline!.Value.AddMinutes(-3), "pending-payment");
        await fixture.AddSecondOnTimeReceiptAsync();
        await fixture.MoveDeadlineToPastAsync();

        var action = () => fixture.Service.CompleteOpeningAsync(
            fixture.Tender.Id, fixture.OpeningRequest(), "opening-payment-pending");

        await action.Should().ThrowAsync<ProcurementTenderControlValidationException>()
            .Where(exception => exception.Code == "TENDER_BID_PAYMENT_VERIFICATION_PENDING");
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "TenderOpeningPaymentAdmissionDenied" &&
                request.Result == ProcurementControlEventResult.Denied),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Bids[0].Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task FinancialEvaluatorMustBeSeparatedAndRowVersionMustMatch()
    {
        await using var fixture = new Fixture();
        await fixture.OpenAsync();
        var control = await fixture.Context.ProcurementTenderControls.SingleAsync();
        control.RowVersion = [1, 2, 3, 4];
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.Invoking(service => service.SaveTechnicalEvaluationAsync(
                fixture.Tender.Id, fixture.TechnicalRequest("BA=="), "stale-version"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == "TENDER_VERSION_CONFLICT");

        var technical = await fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id, fixture.TechnicalRequest("AQIDBA=="), "technical");
        await fixture.Service.Invoking(service => service.SaveFinancialEvaluationAsync(
                fixture.Tender.Id, fixture.FinancialRequest(technical.RowVersion), "same-evaluator"))
            .Should().ThrowAsync<ProcurementTenderControlAuthorizationException>();

        fixture.CurrentUserId = Guid.NewGuid();
        var financial = await fixture.Service.SaveFinancialEvaluationAsync(
            fixture.Tender.Id, fixture.FinancialRequest(technical.RowVersion), "financial");

        financial.Status.Should().Be(ProcurementTenderControlStatus.FinancialEvaluated);
        financial.RecommendedBidId.Should().Be(fixture.Bids[0].Id);
        fixture.SodGuard.Verify(service => service.EnforceAsync(
            It.Is<ProcurementSodGuardRequest>(request =>
                request.ControlCode == "SOD-TENDER-TECHNICAL-FINANCIAL-EVALUATOR"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(ProcurementMethodType.QualityBasedSelection, true)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection, true)]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering, false)]
    public async Task TenderWithoutControlUsesSourcingCaseMethodForFinancialConcealment(
        ProcurementMethodType method,
        bool expected)
    {
        await using var fixture = new Fixture(method);

        var concealed = await fixture.Service.ShouldConcealFinancialProposalAsync(
            fixture.Tender.Id,
            fixture.Bids[0].Id);

        concealed.Should().Be(expected);
    }

    [Fact]
    public async Task QbsAllowsFinancialReviewOnlyForUniqueHighestTechnicalBid()
    {
        await using var fixture = new Fixture(ProcurementMethodType.QualityBasedSelection);
        await fixture.OpenAsync();
        var opened = await fixture.Service.GetAsync(fixture.Tender.Id);
        opened.SubmissionReceipts.Should().OnlyContain(item => item.BidAmount == 0m && item.Currency == string.Empty,
            "QBS financial proposals must remain sealed during technical opening");
        (await fixture.Service.ShouldConcealFinancialProposalAsync(fixture.Tender.Id, fixture.Bids[0].Id)).Should().BeTrue();
        var technical = await fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id, fixture.TechnicalRequest(opened.RowVersion), "qbs-technical");
        fixture.CurrentUserId = Guid.NewGuid();

        var invalid = fixture.FinancialRequest(technical.RowVersion);
        await fixture.Service.Invoking(service => service.SaveFinancialEvaluationAsync(
                fixture.Tender.Id, invalid, "qbs-invalid"))
            .Should().ThrowAsync<ProcurementTenderControlValidationException>()
            .Where(exception => exception.Code == "QBS_RECOMMENDATION_INVALID");

        var selected = invalid.Scores.Single(item => item.BidId == fixture.Bids[0].Id);
        invalid.Scores = [selected];
        invalid.RecommendedBidId = fixture.Bids[0].Id;
        var result = await fixture.Service.SaveFinancialEvaluationAsync(
            fixture.Tender.Id, invalid, "qbs-valid");

        result.RecommendedBidId.Should().Be(fixture.Bids[0].Id);
        result.Status.Should().Be(ProcurementTenderControlStatus.FinancialEvaluated);
        result.SubmissionReceipts.Single(item => item.TenderBidId == fixture.Bids[0].Id).BidAmount.Should().BePositive();
        result.SubmissionReceipts.Where(item => item.TenderBidId != fixture.Bids[0].Id)
            .Should().OnlyContain(item => item.BidAmount == 0m && item.Currency == string.Empty,
                "QBS may open only the uniquely highest-ranked technical proposal");
        (await fixture.Service.ShouldConcealFinancialProposalAsync(fixture.Tender.Id, fixture.Bids[0].Id)).Should().BeFalse();
        (await fixture.Service.ShouldConcealFinancialProposalAsync(fixture.Tender.Id, fixture.Bids[2].Id)).Should().BeTrue();
    }

    [Fact]
    public async Task QcbsRejectsClientRecommendationThatIsNotServerCalculatedWinner()
    {
        await using var fixture = new Fixture(ProcurementMethodType.QualityAndCostBasedSelection);
        await fixture.OpenAsync();
        var opened = await fixture.Service.GetAsync(fixture.Tender.Id);
        opened.SubmissionReceipts.Should().OnlyContain(item => item.BidAmount == 0m && item.Currency == string.Empty,
            "QCBS financial proposals must remain sealed during technical opening");
        var technical = await fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id, fixture.TechnicalRequest(opened.RowVersion), "qcbs-technical");
        fixture.CurrentUserId = Guid.NewGuid();
        var request = fixture.FinancialRequest(technical.RowVersion);
        request.RecommendedBidId = fixture.Bids[2].Id;

        await fixture.Service.Invoking(service => service.SaveFinancialEvaluationAsync(
                fixture.Tender.Id, request, "qcbs-wrong-winner"))
            .Should().ThrowAsync<ProcurementTenderControlValidationException>()
            .Where(exception => exception.Code == "QCBS_RECOMMENDATION_INVALID");

        request.RecommendedBidId = fixture.Bids[0].Id;
        var result = await fixture.Service.SaveFinancialEvaluationAsync(
            fixture.Tender.Id, request, "qcbs-calculated-winner");
        result.RecommendedBidId.Should().Be(fixture.Bids[0].Id);
        result.SubmissionReceipts.Should().OnlyContain(item => item.BidAmount > 0m && item.Currency == "GHS",
            "QCBS opens financial proposals only after technical qualification");
        (await fixture.Service.ShouldConcealFinancialProposalAsync(fixture.Tender.Id, fixture.Bids[0].Id)).Should().BeFalse();
        (await fixture.Service.ShouldConcealFinancialProposalAsync(fixture.Tender.Id, fixture.Bids[2].Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ExactWorkflowApprovalControlsAwardContractAndBidderAcceptance()
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();
        var recommendationMakerId = fixture.CurrentUserId;

        var submitted = await fixture.Service.SubmitApprovalAsync(
            fixture.Tender.Id,
            new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
            "submit-award");
        submitted.Status.Should().Be(ProcurementTenderControlStatus.PendingApproval);
        submitted.WorkflowInstanceId.Should().Be(fixture.WorkflowInstanceId);

        fixture.CurrentUserId = Guid.NewGuid();
        var awardApproverId = fixture.CurrentUserId;
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "TenderAward", fixture.Tender.Id, fixture.CurrentUserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = fixture.WorkflowInstanceId
            });
        var approved = await fixture.Service.DecideApprovalAsync(
            fixture.Tender.Id,
            new DecideProcurementTenderApprovalRequest
            {
                Action = "Approve",
                AuthorityApprovalReference = "ENTITY-TENDER-COMMITTEE-001",
                PpaApprovalReference = "PPA-001",
                RowVersion = submitted.RowVersion
            }, "approve-award");
        var awarded = await fixture.Service.RecordAwardAsync(
            fixture.Tender.Id,
            new RecordProcurementTenderAwardRequest
            {
                BidId = fixture.Bids[0].Id,
                AwardReference = "AWD-001",
                EvidenceReference = "evidence://award-001",
                RowVersion = approved.RowVersion
            }, "record-award");
        var realAward = await fixture.Context.TenderAwards.SingleAsync(item =>
            item.TenantId == fixture.CurrentTenantId &&
            item.TenderId == fixture.Tender.Id &&
            !item.IsDeleted);
        realAward.TenderBidId.Should().Be(fixture.Bids[0].Id);
        realAward.BusinessPartnerId.Should().Be(fixture.Bids[0].BusinessPartnerId);
        realAward.OriginalBidAmount.Should().Be(fixture.Bids[0].TotalBidAmount);
        realAward.AwardedAmount.Should().Be(fixture.Bids[0].TotalBidAmount);
        realAward.Currency.Should().Be(fixture.Bids[0].Currency);
        realAward.Status.Should().Be("Awarded");
        realAward.CreatedById.Should().Be(recommendationMakerId);
        realAward.AwardedById.Should().Be(awardApproverId);
        realAward.CreatedById.Should().NotBe(awardApproverId);
        var contracted = await fixture.Service.RecordContractAsync(
            fixture.Tender.Id,
            new RecordProcurementTenderContractRequest
            {
                ContractReference = "CON-001",
                EvidenceReference = "evidence://contract-001",
                RowVersion = awarded.RowVersion
            }, "record-contract");
        var accepted = await fixture.Service.RecordAcceptanceAsync(
            fixture.Tender.Id,
            new RecordProcurementTenderAcceptanceRequest
            {
                AcceptanceReference = "ACC-001",
                EvidenceReference = "evidence://acceptance-001",
                RowVersion = contracted.RowVersion
            }, "record-acceptance");

        accepted.Status.Should().Be(ProcurementTenderControlStatus.Accepted);
        accepted.AwardBidId.Should().Be(fixture.Bids[0].Id);
        accepted.ContractReference.Should().Be("CON-001");
        accepted.BidderAcceptanceReference.Should().Be("ACC-001");
        accepted.Milestones.Should().HaveCount(14);
        accepted.Milestones.Should().OnlyContain(item => item.CompletedAtUtc.HasValue);
        fixture.AwardReadiness.Verify(service => service.EnsureAwardReadyAsync(
            ProcurementAwardReadinessSourceType.Tender,
            fixture.Tender.Id,
            It.Is<EvaluateProcurementAwardReadinessRequest>(request =>
                request.IdempotencyKey.StartsWith("award-gate:1:") &&
                request.ExpectedRecommendedSubjectIds.SequenceEqual(
                    new[] { fixture.Bids[0].Id }) &&
                request.ExpectedBusinessPartnerIds.SequenceEqual(
                    new[] { fixture.Bids[0].BusinessPartnerId }) &&
                request.ExpectedSourceIntegrityHash == null),
            "record-award",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ControlledAwardRetryWithConsumedRowVersionCannotDuplicateRealAward()
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();
        var submitted = await fixture.Service.SubmitApprovalAsync(
            fixture.Tender.Id,
            new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
            "submit-award-retry");

        fixture.CurrentUserId = Guid.NewGuid();
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "TenderAward", fixture.Tender.Id, fixture.CurrentUserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = fixture.WorkflowInstanceId
            });
        var approved = await fixture.Service.DecideApprovalAsync(
            fixture.Tender.Id,
            new DecideProcurementTenderApprovalRequest
            {
                Action = "Approve",
                AuthorityApprovalReference = "ENTITY-TENDER-COMMITTEE-RETRY",
                PpaApprovalReference = "PPA-RETRY",
                RowVersion = submitted.RowVersion
            }, "approve-award-retry");
        var request = new RecordProcurementTenderAwardRequest
        {
            BidId = fixture.Bids[0].Id,
            AwardReference = "AWD-RETRY-001",
            EvidenceReference = "evidence://award-retry-001",
            RowVersion = approved.RowVersion
        };

        await fixture.Service.RecordAwardAsync(
            fixture.Tender.Id, request, "record-award-first");
        await fixture.Service.Invoking(service => service.RecordAwardAsync(
                fixture.Tender.Id, request, "record-award-retry"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == "TENDER_AWARD_NOT_READY");

        (await fixture.Context.TenderAwards.CountAsync(item =>
            item.TenantId == fixture.CurrentTenantId &&
            item.TenderId == fixture.Tender.Id &&
            !item.IsDeleted)).Should().Be(1);
    }

    [Fact]
    public async Task FailedWorkflowStartDoesNotLeaveRecommendationPending()
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();
        fixture.Workflow.Setup(service => service.StartApprovalWorkflowAsync(
                "TenderAward", fixture.Tender.Id, fixture.WorkflowDefinitionId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = false,
                Status = WorkflowInstanceStatus.Failed,
                Message = "Workflow start failed."
            });

        await fixture.Service.Invoking(service => service.SubmitApprovalAsync(
                fixture.Tender.Id,
                new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
                "failed-workflow"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == "TENDER_AWARD_WORKFLOW_START_FAILED");

        fixture.Context.ChangeTracker.Clear();
        var control = await fixture.Context.ProcurementTenderControls.SingleAsync();
        control.Status.Should().Be(ProcurementTenderControlStatus.FinancialEvaluated);
        control.WorkflowInstanceId.Should().BeNull();
        control.SubmittedForApprovalAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AwardRecommendationSubmissionUsesTenderApprovalCapability()
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();
        fixture.PlatformAdministrator = false;
        fixture.AccessControl.Setup(service => service.EnforceCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request =>
                    request.PermissionCode == "procurement.tender.approve" &&
                    request.SourceType == "Tender" &&
                    request.SourceReference == fixture.Tender.TenderNumber),
                "head-submit-award",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = true,
                PermissionCode = "procurement.tender.approve",
                ActorUserId = fixture.CurrentUserId,
                TenantId = fixture.CurrentTenantId
            });

        var submitted = await fixture.Service.SubmitApprovalAsync(
            fixture.Tender.Id,
            new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
            "head-submit-award");

        submitted.Status.Should().Be(ProcurementTenderControlStatus.PendingApproval);
        fixture.AccessControl.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.tender.approve"),
            "head-submit-award",
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.AccessControl.Verify(service => service.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.tender.evaluate"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TenantIsolationHidesAnotherTenantsTenderControl()
    {
        await using var fixture = new Fixture();
        await fixture.PublishAsync();
        fixture.CurrentTenantId = Guid.NewGuid();

        await fixture.Service.Invoking(service => service.GetAsync(fixture.Tender.Id))
            .Should().ThrowAsync<ProcurementTenderControlNotFoundException>()
            .Where(exception => exception.Code == "TENDER_CONTROL_NOT_FOUND");
    }

    [Theory]
    [InlineData("No active evaluation committee control exists.", false)]
    [InlineData("The current actor is not appointed to this committee.", true)]
    public async Task StatutoryEvaluationFailsClosedForMissingCommitteeOrNonmember(
        string blockedReason,
        bool authorizationFailure)
    {
        await using var fixture = new Fixture();
        await fixture.OpenAsync();
        fixture.EvaluationCommittee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
                ProcurementEvaluationPhase.Technical, "ProcurementTenderControl", fixture.Tender.Id,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementEvaluationScorerEligibilityDto
            {
                Allowed = false,
                BlockedReasons = [blockedReason]
            });
        var control = await fixture.Context.ProcurementTenderControls.SingleAsync();
        control.RowVersion = [1, 2, 3, 4];
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id,
            fixture.TechnicalRequest(Convert.ToBase64String(control.RowVersion)),
            "blocked-scorer");

        if (authorizationFailure)
            await action.Should().ThrowAsync<ProcurementTenderControlAuthorizationException>();
        else
            await action.Should().ThrowAsync<ProcurementTenderControlConflictException>()
                .Where(exception => exception.Code == "EVALUATION_SCORER_INELIGIBLE");
        (await fixture.Context.ProcurementTenderControls.SingleAsync()).Status
            .Should().Be(ProcurementTenderControlStatus.Opened);
    }

    [Fact]
    public async Task ApprovedRecallReplacesExactProjectionButTechnicalRecallClosesAfterFinancialProgression()
    {
        await using var fixture = new Fixture();
        await fixture.OpenAsync();
        var control = await fixture.Context.ProcurementTenderControls.SingleAsync();
        control.RowVersion = [1, 2, 3, 4];
        await fixture.Context.SaveChangesAsync();
        var first = await fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id,
            fixture.TechnicalRequest(Convert.ToBase64String(control.RowVersion)),
            "technical-attempt-1");
        fixture.EvaluationCommittee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                ProcurementEvaluationSourceType.Tender, fixture.Tender.Id,
                ProcurementEvaluationPhase.Technical, "ProcurementTenderControl", fixture.Tender.Id,
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.EligibleScorer(
                ProcurementEvaluationPhase.Technical, authorizedAttempt: 2));

        var replaced = await fixture.Service.SaveTechnicalEvaluationAsync(
            fixture.Tender.Id,
            fixture.TechnicalRequest(first.RowVersion),
            "technical-attempt-2");
        replaced.Status.Should().Be(ProcurementTenderControlStatus.TechnicalEvaluated);

        var technicalUserId = fixture.CurrentUserId;
        fixture.CurrentUserId = Guid.NewGuid();
        var financial = await fixture.Service.SaveFinancialEvaluationAsync(
            fixture.Tender.Id,
            fixture.FinancialRequest(replaced.RowVersion),
            "financial-attempt-1");
        fixture.CurrentUserId = technicalUserId;

        await fixture.Service.Invoking(service => service.SaveTechnicalEvaluationAsync(
                fixture.Tender.Id,
                fixture.TechnicalRequest(financial.RowVersion),
                "late-technical-recall"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == "TENDER_TECHNICAL_RECALL_WINDOW_CLOSED");
    }

    [Theory]
    [InlineData(true, false, "EVALUATION_SCORE_RECALL_UNRESOLVED")]
    [InlineData(false, true, "EVALUATION_SCORE_PROJECTION_MISMATCH")]
    public async Task ApprovalRejectsUnresolvedApprovedRecallOrProjectionMismatch(
        bool approvedRecall,
        bool tamperSnapshot,
        string expectedCode)
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();
        fixture.IncludeApprovedFinancialRecall = approvedRecall;
        fixture.TamperFinancialCommitteeSnapshot = tamperSnapshot;

        await fixture.Service.Invoking(service => service.SubmitApprovalAsync(
                fixture.Tender.Id,
                new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
                "blocked-approval"))
            .Should().ThrowAsync<ProcurementTenderControlConflictException>()
            .Where(exception => exception.Code == expectedCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();

        public Fixture(ProcurementMethodType method = ProcurementMethodType.NationalCompetitiveTendering)
        {
            CurrentTenantId = Guid.NewGuid();
            CurrentUserId = Guid.NewGuid();
            ReleaseId = Guid.NewGuid();
            WorkflowDefinitionId = Guid.NewGuid();
            WorkflowInstanceId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);

            _currentUser.SetupGet(item => item.UserId).Returns(() => CurrentUserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => CurrentTenantId);
            _currentUser.SetupGet(item => item.Username).Returns("tender.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Tender Controller");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(() =>
                PlatformAdministrator ? ["SuperAdmin"] : ["TDC_HEAD_OF_PROCUREMENT"]);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => PlatformAdministrator && role == "SuperAdmin");

            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                RequisitionNumber = "PR-NCT-001", RequestedById = Guid.NewGuid(),
                RequiredDate = DateTime.UtcNow.AddDays(40), Status = "Approved",
                Currency = "GHS", TotalAmount = 2_000_000m
            };
            Rule = new ProcurementPolicyMethodRule
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PolicySetId = Guid.NewGuid(),
                RuleCode = method switch
                {
                    ProcurementMethodType.InternationalCompetitiveTendering => "ICT-GOODS-001",
                    ProcurementMethodType.QualityBasedSelection => "QBS-GOODS-001",
                    ProcurementMethodType.QualityAndCostBasedSelection => "QCBS-GOODS-001",
                    _ => "NCT-GOODS-001"
                },
                Name = "Competitive tendering", Category = ProcurementCategoryClass.Goods,
                Method = method, IsAllowed = true, IsEnabled = true,
                MinimumQuotationCount = 2, WorkflowDefinitionId = WorkflowDefinitionId,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            Route = new ProcurementRequisitionAuthorityRoute
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                PurchaseRequisitionId = Requisition.Id, AttemptNumber = 1,
                RouteReference = "AUTH-PPA-001", EvaluationId = Guid.NewGuid(),
                CorrelationId = "route-correlation", PolicySetId = Rule.PolicySetId,
                PolicyKey = Guid.NewGuid(), PolicyCode = "TDC-PROC", PolicyName = "TDC Procurement",
                PolicyVersion = 1, SourceConfigurationProfileId = Guid.NewGuid(),
                Category = ProcurementCategoryClass.Goods, Amount = 2_000_000m,
                CurrencyCode = "GHS", PolicyDateUtc = DateTime.UtcNow,
                EvaluatedAtUtc = DateTime.UtcNow, WorkflowDefinitionId = WorkflowDefinitionId,
                WorkflowDefinitionKey = Guid.NewGuid(), WorkflowName = "Tender award approval",
                WorkflowVersion = 1, WorkflowEntityTypeCode = "TenderAward",
                CapturedAtUtc = DateTime.UtcNow, CapturedById = CurrentUserId,
                CapturedByName = "Tender Controller", SnapshotJson = "{}",
                IntegrityHash = new string('a', 64)
            };
            Route.Steps.Add(new ProcurementRequisitionAuthorityRouteStep
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, AuthorityRouteId = Route.Id,
                Sequence = 1, AuthorityRuleId = Guid.NewGuid(), RulePolicySetId = Rule.PolicySetId,
                RulePolicyCode = "TDC-PROC", RulePolicyVersion = 1, RuleCode = "AUTH-PPA",
                AuthorityName = "Public Procurement Authority", AuthorityRole = "PPA reviewer",
                CurrencyCode = "GHS", LowerBound = 0m, LowerInclusive = true,
                UpperInclusive = true, Quorum = 1, WorkflowDefinitionId = WorkflowDefinitionId,
                WorkflowStepId = Guid.NewGuid(), WorkflowStepName = "PPA review", WorkflowStepOrder = 1
            });
            Case = new ProcurementSourcingCase
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                PurchaseRequisitionId = Requisition.Id, SourcingReleaseId = ReleaseId,
                CaseSequence = 1, CaseNumber = "SC-NCT-001",
                Category = ProcurementCategoryClass.Goods,
                RecommendedMethod = method, SelectedMethod = method,
                EstimatedValue = 2_000_000m, CurrencyCode = "GHS",
                PolicySetId = Rule.PolicySetId, PolicyCode = "TDC-PROC", PolicyVersion = 1,
                MethodRuleId = Rule.Id, MethodRuleCode = Rule.RuleCode,
                ThresholdRuleId = Guid.NewGuid(), ThresholdRuleCode = "TH-NCT",
                AuthorityRouteId = Route.Id, AuthorityRouteReference = Route.RouteReference,
                Justification = "Statutory competitive tendering.", CreatedByName = "Controller",
                Status = ProcurementSourcingCaseStatus.InProgress,
                SourceControlFingerprint = new string('b', 64),
                CaseFingerprint = new string('c', 64), SnapshotJson = "{}",
                IntegrityHash = new string('d', 64), AuthorityRoute = Route
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                TenderNumber = $"{method}-2026-001",
                Title = "Network infrastructure", TenderType = "ITB", Status = "Approved",
                EstimatedValue = Case.EstimatedValue, Currency = Case.CurrencyCode,
                SourcePurchaseRequisitionId = Requisition.Id, SourcingReleaseId = ReleaseId,
                SourcingCaseId = Case.Id, CreatedById = Guid.NewGuid(),
                UseQCBSEvaluation = method == ProcurementMethodType.QualityAndCostBasedSelection,
                MinimumTechnicalScore = 80m, TechnicalWeight = 60m, FinancialWeight = 40m
            };
            Suppliers = Enumerable.Range(1, 3).Select(index => new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                PartnerCode = $"SUP-{index:000}", PartnerName = $"Supplier {index}",
                PartnerType = "Supplier"
            }).ToList();
            Bids = Suppliers.Select((supplier, index) => new TenderBid
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, TenderId = Tender.Id,
                BusinessPartnerId = supplier.Id, BusinessPartner = supplier,
                BidNumber = $"BID-{index + 1:000}", Status = "Submitted",
                TotalBidAmount = 1_500_000m + index * 50_000m, Currency = "GHS",
                Items =
                [
                    new TenderBidItem
                    {
                        Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                        TenderItemId = Guid.NewGuid(), OfferedQuantity = 1,
                        UnitPrice = 1_500_000m + index * 50_000m,
                        TotalPrice = 1_500_000m + index * 50_000m
                    }
                ]
            }).ToList();
            foreach (var bid in Bids)
            {
                foreach (var item in bid.Items) item.TenderBidId = bid.Id;
            }

            Context.Add(new Tenant
            {
                Id = CurrentTenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active
            });
            Context.AddRange(Requisition, Rule, Route, Case, Tender);
            Context.AddRange(Route.Steps);
            Context.AddRange(Suppliers);
            Context.AddRange(Bids);
            Context.AddRange(Bids.SelectMany(item => item.Items));
            Context.SaveChanges();

            _unitOfWork = new UnitOfWork(Context);
            SourcingCases.Setup(service => service.RevalidateSourceEntryAsync(
                    Requisition.Id, ReleaseId, Case.Id, method, "Tender", Tender.Id,
                    Tender.TenderNumber, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
                {
                    SourcingCaseId = Case.Id, SourcingReleaseId = ReleaseId,
                    SelectedMethod = method, MethodRuleId = Rule.Id,
                    MethodRuleCode = Rule.RuleCode, MinimumQuotationCount = 2,
                    WorkflowDefinitionId = WorkflowDefinitionId,
                    EstimatedValue = Tender.EstimatedValue!.Value,
                    CurrencyCode = Tender.Currency!
                });
            SupplierValidation.Setup(service => service.ValidateForTenderAsync(
                    It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<decimal?>()))
                .ReturnsAsync(SupplierValidationResult.Success());
            SupplierValidation.Setup(service => service.EvaluateEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SupplierValidationResult.Success());
            ControlEvents.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Workflow.Setup(service => service.StartApprovalWorkflowAsync(
                    "TenderAward", Tender.Id, WorkflowDefinitionId))
                .ReturnsAsync(new WorkflowExecutionResult
                {
                    Success = true, Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = WorkflowInstanceId
                });
            Workflow.Setup(service => service.CanUserApproveAsync(
                    "TenderAward", Tender.Id, It.IsAny<Guid>()))
                .ReturnsAsync(true);
            SodGuard.Setup(service => service.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementSodGuardRequest request, string correlation, CancellationToken token) =>
                    new ProcurementSodGuardDecisionDto
                    {
                        Allowed = !request.ProhibitedActorUserIds.Contains(CurrentUserId),
                        Message = "Actors must be separated."
                    });
            TenderDocuments.Setup(service => service.IssueTenderCompatibilityAsync(
                    It.IsAny<Guid>(), It.IsAny<IssueProcurementTenderDocumentRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, IssueProcurementTenderDocumentRequest request, string _, CancellationToken _) =>
                    new ProcurementTenderDocumentIssuanceDto
                    {
                        Id = Guid.NewGuid(),
                        BusinessPartnerId = request.BusinessPartnerId,
                        RecipientName = request.RecipientName,
                        AmountPaid = request.AmountPaid,
                        PaymentReference = request.PaymentReference,
                        ReceiptNumber = request.IssueReceiptNumber,
                        IssuedAtUtc = DateTime.UtcNow,
                        IssuedByUserId = CurrentUserId,
                        EvidenceReference = request.EvidenceReference,
                        IntegrityHash = new string('a', 64)
                    });
            EvaluationCommittee.Setup(service => service.EnsureScoreSubjectEligibleAsync(
                    ProcurementEvaluationSourceType.Tender, Tender.Id,
                    It.IsAny<ProcurementEvaluationPhase>(), "ProcurementTenderControl", Tender.Id,
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementEvaluationSourceType _, Guid _, ProcurementEvaluationPhase phase,
                    string _, Guid _, string _, CancellationToken _) => EligibleScorer(phase));
            EvaluationCommittee.Setup(service => service.GetAsync(
                    ProcurementEvaluationSourceType.Tender, Tender.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CommitteeDetail());
            EvaluationCommittee.Setup(service => service.LockScoreSheetAsync(
                    It.IsAny<LockProcurementEvaluationScoreSheetRequest>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LockProcurementEvaluationScoreSheetRequest request, string _, CancellationToken _) =>
                    new ProcurementEvaluationScoreSheetDto
                    {
                        Id = Guid.NewGuid(), MeetingId = request.MeetingId,
                        AppointmentId = request.AppointmentId, Phase = request.Phase,
                        ScoreSubjectType = request.ScoreSubjectType, ScoreSubjectId = request.ScoreSubjectId,
                        Attempt = 1, Status = ProcurementEvaluationScoreSheetStatus.Locked
                    });
            AwardReadiness.Setup(service => service.EnsureAwardReadyAsync(
                    It.IsAny<ProcurementAwardReadinessSourceType>(),
                    It.IsAny<Guid>(),
                    It.IsAny<EvaluateProcurementAwardReadinessRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAwardReadinessSourceType sourceType, Guid sourceId,
                    EvaluateProcurementAwardReadinessRequest _, string correlationId, CancellationToken _) =>
                    new ProcurementAwardReadinessDto
                    {
                        Id = Guid.NewGuid(),
                        SourceType = sourceType,
                        SourceId = sourceId,
                        CorrelationId = correlationId,
                        Status = ProcurementAwardReadinessDecisionStatus.Ready,
                        IsCurrent = true
                    });
            Service = new ProcurementTenderControlService(
                _unitOfWork, _currentUser.Object, AccessControl.Object, SodGuard.Object,
                ControlEvents.Object, SourcingCases.Object, Workflow.Object,
                SupplierValidation.Object, TenderDocuments.Object, EvaluationCommittee.Object,
                AwardReadiness.Object);
        }

        public Guid CurrentTenantId { get; set; }
        public Guid CurrentUserId { get; set; }
        public bool PlatformAdministrator { get; set; } = true;
        public Guid ReleaseId { get; }
        public Guid WorkflowDefinitionId { get; }
        public Guid WorkflowInstanceId { get; }
        public ApplicationDbContext Context { get; }
        public PurchaseRequisition Requisition { get; }
        public ProcurementPolicyMethodRule Rule { get; }
        public ProcurementRequisitionAuthorityRoute Route { get; }
        public ProcurementSourcingCase Case { get; }
        public Tender Tender { get; }
        public List<BusinessPartner> Suppliers { get; }
        public List<TenderBid> Bids { get; }
        public ProcurementTenderControlService Service { get; }
        public Mock<IProcurementAccessControlService> AccessControl { get; } = new();
        public Mock<IProcurementSodGuardService> SodGuard { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public Mock<IWorkflowService> Workflow { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<IProcurementTenderDocumentControlService> TenderDocuments { get; } = new();
        public Mock<IProcurementEvaluationCommitteeControlService> EvaluationCommittee { get; } = new();
        public Mock<IProcurementAwardReadinessService> AwardReadiness { get; } = new();
        public bool IncludeApprovedFinancialRecall { get; set; }
        public bool TamperFinancialCommitteeSnapshot { get; set; }
        private Guid CommitteeId { get; } = Guid.NewGuid();
        private Guid AppointmentId { get; } = Guid.NewGuid();
        private Guid TechnicalMeetingId { get; } = Guid.NewGuid();
        private Guid FinancialMeetingId { get; } = Guid.NewGuid();
        private Guid TechnicalScoreSheetId { get; } = Guid.NewGuid();
        private Guid FinancialScoreSheetId { get; } = Guid.NewGuid();

        public ProcurementEvaluationScorerEligibilityDto EligibleScorer(
            ProcurementEvaluationPhase phase,
            int authorizedAttempt = 1) => new()
        {
            Allowed = true,
            SourceType = ProcurementEvaluationSourceType.Tender,
            SourceId = Tender.Id,
            Phase = phase,
            ActorUserId = CurrentUserId,
            CommitteeControlId = CommitteeId,
            AppointmentId = AppointmentId,
            MeetingId = phase == ProcurementEvaluationPhase.Technical
                ? TechnicalMeetingId : FinancialMeetingId,
            AuthorizedAttempt = authorizedAttempt
        };

        private ProcurementEvaluationCommitteeDto CommitteeDetail()
        {
            var control = Context.ProcurementTenderControls.Local
                .FirstOrDefault(item => !item.IsDeleted);
            return new ProcurementEvaluationCommitteeDto
            {
            Id = CommitteeId,
            SourceType = ProcurementEvaluationSourceType.Tender,
            SourceId = Tender.Id,
            Status = ProcurementEvaluationCommitteeControlStatus.Active,
            CompositionReady = true,
            QuorumMet = true,
            RowVersion = "AQ==",
            Members =
            [
                new ProcurementEvaluationAppointmentDto
                {
                    Id = AppointmentId, UserId = CurrentUserId, EligibleToScore = true,
                    RowVersion = "AQ=="
                }
            ],
            Meetings =
            [
                new ProcurementEvaluationMeetingDto
                {
                    Id = TechnicalMeetingId, Phase = ProcurementEvaluationPhase.Technical,
                    Status = ProcurementEvaluationMeetingStatus.QuorumConfirmed,
                    QuorumMet = true, RowVersion = "AQ=="
                },
                new ProcurementEvaluationMeetingDto
                {
                    Id = FinancialMeetingId, Phase = ProcurementEvaluationPhase.Financial,
                    Status = ProcurementEvaluationMeetingStatus.QuorumConfirmed,
                    QuorumMet = true, RowVersion = "AQ=="
                }
            ],
            ScoreSheets =
            [
                ScoreSheet(
                    ProcurementEvaluationPhase.Technical,
                    TechnicalMeetingId,
                    TechnicalScoreSheetId,
                    control?.TechnicalEvaluationSnapshotJson ?? "{}"),
                ScoreSheet(
                    ProcurementEvaluationPhase.Financial,
                    FinancialMeetingId,
                    FinancialScoreSheetId,
                    TamperFinancialCommitteeSnapshot
                        ? """{"tampered":true}"""
                        : control?.FinancialEvaluationSnapshotJson ?? "{}")
            ],
            Recalls = IncludeApprovedFinancialRecall
                ?
                [
                    new ProcurementEvaluationScoreRecallDto
                    {
                        Id = Guid.NewGuid(),
                        ScoreSheetId = FinancialScoreSheetId,
                        Status = ProcurementEvaluationScoreRecallStatus.Approved
                    }
                ]
                : []
            };
        }

        private ProcurementEvaluationScoreSheetDto ScoreSheet(
            ProcurementEvaluationPhase phase,
            Guid meetingId,
            Guid scoreSheetId,
            string scoreSnapshotJson) => new()
        {
            Id = scoreSheetId, AppointmentId = AppointmentId, MeetingId = meetingId,
            Phase = phase, ScoreSubjectType = "ProcurementTenderControl",
            ScoreSubjectId = Tender.Id, Attempt = 1,
            Status = ProcurementEvaluationScoreSheetStatus.Locked,
            ScoreSnapshotJson = scoreSnapshotJson,
            SubmittedAtUtc = DateTime.UtcNow
        };

        public async Task<ProcurementTenderControlDto> PublishAsync(
            decimal documentFee = 0m,
            decimal? controlledFee = null)
        {
            var deadline = DateTime.UtcNow.AddHours(2);
            var templateVersionId = Guid.NewGuid();
            TenderDocuments.Setup(service => service.EnsurePublicationReadyAsync(
                    ProcurementTenderDocumentSourceType.Tender, Tender.Id, It.IsAny<DateTime>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementTenderDocumentEffectiveStateDto
                {
                    RegisterId = Guid.NewGuid(),
                    SourceType = ProcurementTenderDocumentSourceType.Tender,
                    SourceId = Tender.Id,
                    EffectiveTemplateVersionId = templateVersionId,
                    EffectiveTemplateReference = "TDC-CONTROLLED/v4",
                    EffectiveSubmissionDeadlineUtc = deadline,
                    EffectiveBidValidityUntilUtc = deadline.AddDays(30),
                    Ready = true
                });
            TenderDocuments.Setup(service => service.GetRegisterAsync(
                    ProcurementTenderDocumentSourceType.Tender, Tender.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementTenderDocumentRegisterDto
                {
                    SourceType = ProcurementTenderDocumentSourceType.Tender,
                    SourceId = Tender.Id,
                    FeeMode = (controlledFee ?? documentFee) > 0
                        ? ProcurementTenderDocumentFeeMode.Paid
                        : ProcurementTenderDocumentFeeMode.Free,
                    FeeAmount = controlledFee ?? documentFee,
                    CurrencyCode = "GHS"
                });
            var published = await Service.PublishAsync(Tender.Id, new PublishProcurementTenderRequest
            {
                AdvertisementReference = "ADVERT-001",
                PublicationChannel = "National newspaper and entity portal",
                TenderDocumentReference = "TDOC-001",
                TenderDocumentVersion = "1.0",
                DocumentFee = documentFee,
                AdvertisementEvidenceReference = "evidence://advert-001",
                SubmissionDeadlineUtc = deadline,
                OpeningScheduledAtUtc = deadline.AddMinutes(30)
            }, "publish");
            await Context.Entry(Tender).ReloadAsync();
            return published;
        }

        public IssueProcurementTenderDocumentRequest DocumentIssue(
            BusinessPartner supplier, decimal amountPaid = 0m) => new()
        {
            BusinessPartnerId = supplier.Id,
            RecipientName = supplier.PartnerName,
            RecipientEmail = $"{supplier.PartnerCode.ToLowerInvariant()}@example.test",
            AmountPaid = amountPaid,
            PaymentReference = amountPaid > 0 ? $"PAY-{supplier.PartnerCode}" : null,
            IssueReceiptNumber = $"ISSUE-{supplier.PartnerCode}",
            EvidenceReference = $"evidence://issue-{supplier.PartnerCode}"
        };

        public async Task IssueDocumentsAsync(decimal amountPaid = 0m)
        {
            foreach (var supplier in Suppliers)
                await Service.IssueDocumentAsync(
                    Tender.Id, DocumentIssue(supplier, amountPaid), $"issue-{supplier.PartnerCode}");
        }

        public CompleteProcurementTenderOpeningRequest OpeningRequest() => new()
        {
            EvidenceReference = "evidence://opening-001",
            Participants =
            [
                new ProcurementTenderOpeningParticipantRequest
                {
                    UserId = CurrentUserId, Name = "Tender Controller",
                    Role = "Opening officer", SignatureReference = "SIG-OFFICER"
                },
                new ProcurementTenderOpeningParticipantRequest
                {
                    Name = "Independent Observer", Role = "Observer", IsObserver = true,
                    SignatureReference = "SIG-OBSERVER"
                }
            ]
        };

        public async Task AddSecondOnTimeReceiptAsync()
        {
            if (!await Context.ProcurementTenderSubmissionReceipts.AnyAsync(item =>
                    item.TenderBidId == Bids[2].Id))
            {
                await Service.RecordSubmissionAsync(
                    Bids[2], Tender.SubmissionDeadline!.Value.AddMinutes(-1), "receipt-second");
            }
        }

        public async Task MoveDeadlineToPastAsync()
        {
            var control = await Context.ProcurementTenderControls.SingleAsync();
            control.SubmissionDeadlineUtc = DateTime.UtcNow.AddMinutes(-2);
            Tender.SubmissionDeadline = control.SubmissionDeadlineUtc;
            await Context.SaveChangesAsync();
        }

        public async Task OpenAsync()
        {
            await PublishAsync();
            await IssueDocumentsAsync();
            await Service.RecordSubmissionAsync(
                Bids[0], Tender.SubmissionDeadline!.Value.AddMinutes(-3), "receipt-first");
            await AddSecondOnTimeReceiptAsync();
            await MoveDeadlineToPastAsync();
            await Service.CompleteOpeningAsync(Tender.Id, OpeningRequest(), "opening");
        }

        public SaveProcurementTenderTechnicalEvaluationRequest TechnicalRequest(string rowVersion) => new()
        {
            EvidenceReference = "evidence://technical-001",
            RowVersion = rowVersion,
            Scores =
            [
                new() { BidId = Bids[0].Id, Score = 90m, Qualified = true, Reason = "Responsive." },
                new() { BidId = Bids[2].Id, Score = 80m, Qualified = true, Reason = "Responsive." }
            ]
        };

        public SaveProcurementTenderFinancialEvaluationRequest FinancialRequest(string rowVersion) => new()
        {
            EvidenceReference = "evidence://financial-001",
            RowVersion = rowVersion,
            RecommendedBidId = Bids[0].Id,
            RecommendationReason = "Lowest evaluated responsive bid.",
            Scores =
            [
                new() { BidId = Bids[0].Id, Score = 100m, EvaluatedAmount = Bids[0].TotalBidAmount },
                new() { BidId = Bids[2].Id, Score = 95m, EvaluatedAmount = Bids[2].TotalBidAmount }
            ]
        };

        public async Task<ProcurementTenderControlDto> EvaluateAsync()
        {
            await OpenAsync();
            var opened = await Service.GetAsync(Tender.Id);
            var technical = await Service.SaveTechnicalEvaluationAsync(
                Tender.Id, TechnicalRequest(opened.RowVersion), "technical");
            CurrentUserId = Guid.NewGuid();
            return await Service.SaveFinancialEvaluationAsync(
                Tender.Id, FinancialRequest(technical.RowVersion), "financial");
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
