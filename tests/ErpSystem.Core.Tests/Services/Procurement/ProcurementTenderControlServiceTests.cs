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

    [Fact]
    public async Task ExactWorkflowApprovalControlsAwardContractAndBidderAcceptance()
    {
        await using var fixture = new Fixture();
        var financial = await fixture.EvaluateAsync();

        var submitted = await fixture.Service.SubmitApprovalAsync(
            fixture.Tender.Id,
            new SubmitProcurementTenderApprovalRequest { RowVersion = financial.RowVersion },
            "submit-award");
        submitted.Status.Should().Be(ProcurementTenderControlStatus.PendingApproval);
        submitted.WorkflowInstanceId.Should().Be(fixture.WorkflowInstanceId);

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
    public async Task TenantIsolationHidesAnotherTenantsTenderControl()
    {
        await using var fixture = new Fixture();
        await fixture.PublishAsync();
        fixture.CurrentTenantId = Guid.NewGuid();

        await fixture.Service.Invoking(service => service.GetAsync(fixture.Tender.Id))
            .Should().ThrowAsync<ProcurementTenderControlNotFoundException>()
            .Where(exception => exception.Code == "TENDER_CONTROL_NOT_FOUND");
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
            _currentUser.SetupGet(item => item.Roles).Returns(["SuperAdmin"]);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>()))
                .Returns((string role) => role == "SuperAdmin");

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
                RuleCode = method == ProcurementMethodType.InternationalCompetitiveTendering
                    ? "ICT-GOODS-001" : "NCT-GOODS-001",
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
                TenderNumber = method == ProcurementMethodType.InternationalCompetitiveTendering
                    ? "ICT-2026-001" : "NCT-2026-001",
                Title = "Network infrastructure", TenderType = "ITB", Status = "Approved",
                EstimatedValue = Case.EstimatedValue, Currency = Case.CurrencyCode,
                SourcePurchaseRequisitionId = Requisition.Id, SourcingReleaseId = ReleaseId,
                SourcingCaseId = Case.Id, CreatedById = Guid.NewGuid()
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
            Service = new ProcurementTenderControlService(
                _unitOfWork, _currentUser.Object, AccessControl.Object, SodGuard.Object,
                ControlEvents.Object, SourcingCases.Object, Workflow.Object,
                SupplierValidation.Object);
        }

        public Guid CurrentTenantId { get; set; }
        public Guid CurrentUserId { get; set; }
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

        public async Task<ProcurementTenderControlDto> PublishAsync(decimal documentFee = 0m)
        {
            var deadline = DateTime.UtcNow.AddHours(2);
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
