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

public sealed class ProcurementRfqControlServiceTests
{
    [Fact]
    public async Task DispatchRequiresCurrentLineageMinimumQualifiedSuppliersAndConfiguredWorkflow()
    {
        await using var fixture = new Fixture();

        await fixture.Service.Invoking(service => service.EnsureDispatchReadyAsync(
                fixture.Rfq.Id, [fixture.Suppliers[0].Id], "trace-minimum"))
            .Should().ThrowAsync<ProcurementRfqControlValidationException>()
            .Where(exception => exception.Code == "RFQ_MINIMUM_SUPPLIERS_NOT_MET");

        fixture.SupplierValidation.Setup(service => service.ValidateForRfqAsync(
                fixture.Suppliers[1].Id, It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
            .ReturnsAsync(SupplierValidationResult.Fail("Supplier registration expired."));
        await fixture.Service.Invoking(service => service.EnsureDispatchReadyAsync(
                fixture.Rfq.Id, [fixture.Suppliers[0].Id, fixture.Suppliers[1].Id], "trace-qualification"))
            .Should().ThrowAsync<ProcurementRfqControlValidationException>()
            .Where(exception => exception.Code == "RFQ_SUPPLIER_QUALIFICATION_FAILED");

        fixture.SupplierValidation.Setup(service => service.ValidateForRfqAsync(
                fixture.Suppliers[1].Id, It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
            .ReturnsAsync(SupplierValidationResult.Success());
        await fixture.Service.EnsureDispatchReadyAsync(
            fixture.Rfq.Id, [fixture.Suppliers[0].Id, fixture.Suppliers[1].Id], "trace-ready");

        fixture.SourcingCases.Verify(service => service.RevalidateSourceEntryAsync(
            fixture.Requisition.Id, fixture.ReleaseId, fixture.CaseId, ProcurementMethodType.RequestForQuotation,
            "RequestForQuotation", fixture.Rfq.Id, fixture.Rfq.RfqNumber, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request => request.Action == "DispatchReadinessPassed" &&
                request.DecisionKeys.Count == 14), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReceiptLogIsServerClassifiedSealedHashedAndDuplicateSafe()
    {
        await using var fixture = new Fixture();
        var onTime = fixture.Rfq.SubmissionDeadline!.Value.AddMinutes(-2);
        var late = fixture.Rfq.SubmissionDeadline.Value.AddSeconds(1);

        var first = await fixture.Service.RecordReceiptAsync(
            fixture.Rfq.Id, fixture.Quotes[0].Id, onTime, "trace-on-time");
        var second = await fixture.Service.RecordReceiptAsync(
            fixture.Rfq.Id, fixture.Quotes[1].Id, late, "trace-late");

        first.Disposition.Should().Be(ProcurementRfqReceiptDisposition.OnTimeAccepted);
        second.Disposition.Should().Be(ProcurementRfqReceiptDisposition.LateRejected);
        first.ReceiptNumber.Should().EndWith("R0001");
        second.ReceiptNumber.Should().EndWith("R0002");
        first.SubmissionSnapshotJson.Should().Contain("tdc.rfq-sealed-quote.v1");
        first.IntegrityHash.Should().HaveLength(64);
        (await fixture.Context.ProcurementRfqReceipts.CountAsync()).Should().Be(2);

        await fixture.Service.Invoking(service => service.RecordReceiptAsync(
                fixture.Rfq.Id, fixture.Quotes[0].Id, onTime, "trace-duplicate"))
            .Should().ThrowAsync<ProcurementRfqControlConflictException>()
            .Where(exception => exception.Code == "RFQ_QUOTE_ALREADY_RECEIVED");
    }

    [Fact]
    public async Task CommercialOptionsRemainHiddenUntilSignedOpeningRegisterIsLocked()
    {
        await using var fixture = new Fixture();
        await fixture.RecordTwoOnTimeReceiptsAsync();

        var sealedControl = await fixture.Service.GetAsync(fixture.Rfq.Id);
        sealedControl.QuotesRemainSealed.Should().BeTrue();
        sealedControl.EvaluationOptions.Should().BeEmpty();
        await fixture.Service.Invoking(service => service.CompleteOpeningAsync(
                fixture.Rfq.Id, fixture.ValidOpeningRequest(), "trace-early"))
            .Should().ThrowAsync<ProcurementRfqControlConflictException>()
            .Where(exception => exception.Code == "RFQ_OPENING_BEFORE_DEADLINE");

        fixture.Rfq.SubmissionDeadline = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Context.SaveChangesAsync();
        var request = fixture.ValidOpeningRequest();
        request.Securities.Add(new ProcurementRfqOpeningSecurityRequest
        {
            ReceiptId = sealedControl.Receipts[0].Id,
            SecurityReference = "BID-SEC-001"
        });
        var register = await fixture.Service.CompleteOpeningAsync(fixture.Rfq.Id, request, "trace-opening");

        register.Participants.Should().HaveCount(2).And.ContainSingle(item => item.IsObserver);
        register.Entries.Should().HaveCount(2);
        register.Entries.Should().ContainSingle(item => item.SecurityReference == "BID-SEC-001");
        register.IntegrityHash.Should().HaveLength(64);
        (await fixture.Service.AreQuotesOpenAsync(fixture.Rfq.Id)).Should().BeTrue();
        var openedControl = await fixture.Service.GetAsync(fixture.Rfq.Id);
        openedControl.QuotesRemainSealed.Should().BeFalse();
        openedControl.EvaluationOptions.Should().HaveCount(4);

        await fixture.Service.Invoking(service => service.CompleteOpeningAsync(
                fixture.Rfq.Id, request, "trace-repeat"))
            .Should().ThrowAsync<ProcurementRfqControlConflictException>()
            .Where(exception => exception.Code == "RFQ_OPENING_ALREADY_COMPLETED");
    }

    [Fact]
    public async Task EvaluationEnforcesCoverageOnTimeQuotesScoresAndEvaluatorSod()
    {
        await using var fixture = new Fixture();
        await fixture.OpenAsync();
        var request = fixture.ValidEvaluationRequest();
        request.Lines.RemoveAt(1);

        await fixture.Service.Invoking(service => service.SaveEvaluationAsync(
                fixture.Rfq.Id, request, "trace-coverage"))
            .Should().ThrowAsync<ProcurementRfqControlValidationException>()
            .Where(exception => exception.Code == "RFQ_EVALUATION_LINE_COVERAGE");

        request = fixture.ValidEvaluationRequest();
        foreach (var line in request.Lines) line.QuoteId = fixture.Quotes[2].Id;
        await fixture.Service.Invoking(service => service.SaveEvaluationAsync(
                fixture.Rfq.Id, request, "trace-late"))
            .Should().ThrowAsync<ProcurementRfqControlValidationException>()
            .Where(exception => exception.Code == "RFQ_LATE_QUOTE_NOT_ELIGIBLE");

        fixture.SodGuard.Setup(service => service.EnforceAsync(
                It.Is<ProcurementSodGuardRequest>(item => item.ControlCode == "SOD-RFQ-EVALUATOR-CONFLICT"),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = false, Message = "Evaluator conflict." });
        await fixture.Service.Invoking(service => service.SaveEvaluationAsync(
                fixture.Rfq.Id, fixture.ValidEvaluationRequest(), "trace-sod"))
            .Should().ThrowAsync<ProcurementRfqControlAuthorizationException>();

        fixture.AllowSod();
        var saved = await fixture.Service.SaveEvaluationAsync(
            fixture.Rfq.Id, fixture.ValidEvaluationRequest(), "trace-save");
        saved.Status.Should().Be(ProcurementRfqEvaluationStatus.Draft);
        saved.Lines.Should().HaveCount(2);
        saved.IntegrityHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task ExactWorkflowApprovalControlsRecommendationAndAwardHandoff()
    {
        await using var fixture = new Fixture();
        await fixture.OpenAsync();
        await fixture.Service.SaveEvaluationAsync(
            fixture.Rfq.Id, fixture.ValidEvaluationRequest(), "trace-save");
        var evaluation = await fixture.Context.ProcurementRfqEvaluations.SingleAsync();
        evaluation.RowVersion = [1, 2, 3, 4];
        await fixture.Context.SaveChangesAsync();

        var submitted = await fixture.Service.SubmitEvaluationAsync(fixture.Rfq.Id,
            new SubmitProcurementRfqEvaluationRequest { RowVersion = Convert.ToBase64String(evaluation.RowVersion) },
            "trace-submit");
        submitted.Status.Should().Be(ProcurementRfqEvaluationStatus.Submitted);
        submitted.WorkflowDefinitionId.Should().Be(fixture.WorkflowDefinitionId);
        submitted.WorkflowInstanceId.Should().Be(fixture.WorkflowInstanceId);

        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "RequestForQuotation", fixture.Rfq.Id, fixture.UserId, "reject", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        await fixture.Service.Invoking(service => service.DecideEvaluationAsync(fixture.Rfq.Id,
                new DecideProcurementRfqEvaluationRequest
                {
                    Action = "Reject", ApprovalReference = "APP-REJECT",
                    RowVersion = submitted.RowVersion
                }, "trace-wrong-reject"))
            .Should().ThrowAsync<ProcurementRfqControlConflictException>()
            .Where(exception => exception.Code == "RFQ_WORKFLOW_REJECTION_NOT_FINAL");

        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "RequestForQuotation", fixture.Rfq.Id, fixture.UserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        var approved = await fixture.Service.DecideEvaluationAsync(fixture.Rfq.Id,
            new DecideProcurementRfqEvaluationRequest
            {
                Action = "Approve", ApprovalReference = "APP-001",
                RowVersion = submitted.RowVersion
            }, "trace-approve");
        approved.Status.Should().Be(ProcurementRfqEvaluationStatus.Approved);
        approved.ApprovalReference.Should().Be("APP-001");

        var handoff = await fixture.Service.GetApprovedAwardAsync(fixture.Rfq.Id, "trace-award");
        handoff.Mode.Should().Be("WinnerTakesAll");
        handoff.QuoteId.Should().Be(fixture.Quotes[0].Id);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly Mock<IProcurementAccessControlService> _accessControl = new();

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            ReleaseId = Guid.NewGuid();
            CaseId = Guid.NewGuid();
            WorkflowDefinitionId = Guid.NewGuid();
            WorkflowInstanceId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Context = new ApplicationDbContext(options);

            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(TenantId);
            _currentUser.SetupGet(item => item.Username).Returns("rfq.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("RFQ Controller");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(["SuperAdmin"]);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => role == "SuperAdmin");

            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RequisitionNumber = "PR-RFQ-001",
                RequestedById = Guid.NewGuid(), RequiredDate = DateTime.UtcNow.AddDays(10),
                Status = "Approved", Currency = "GHS", TotalAmount = 400m
            };
            var rule = new ProcurementPolicyMethodRule
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PolicySetId = Guid.NewGuid(),
                RuleCode = "RFQ-GOODS-001", Name = "Goods RFQ", Category = ProcurementCategoryClass.Goods,
                Method = ProcurementMethodType.RequestForQuotation, IsAllowed = true, IsEnabled = true,
                MinimumQuotationCount = 2, WorkflowDefinitionId = WorkflowDefinitionId,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            var sourcingCase = new ProcurementSourcingCase
            {
                Id = CaseId, TenantId = TenantId, PurchaseRequisitionId = Requisition.Id, SourcingReleaseId = ReleaseId,
                CaseSequence = 1, CaseNumber = "SC-RFQ-001", Category = ProcurementCategoryClass.Goods,
                RecommendedMethod = ProcurementMethodType.RequestForQuotation,
                SelectedMethod = ProcurementMethodType.RequestForQuotation,
                EstimatedValue = 400m, CurrencyCode = "GHS", MethodRuleId = rule.Id,
                MethodRuleCode = rule.RuleCode, Status = ProcurementSourcingCaseStatus.InProgress,
                PolicyCode = "POLICY", PolicyVersion = 1, ThresholdRuleCode = "TH-RFQ",
                AuthorityRouteReference = "AUTH-RFQ", Justification = "Competitive quotation.",
                CreatedByName = "Controller", SourceControlFingerprint = new string('a', 64),
                CaseFingerprint = new string('b', 64), SnapshotJson = "{}", IntegrityHash = new string('c', 64)
            };
            Rfq = new RequestForQuotation
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RfqNumber = "RFQ-2026-001", Title = "Laptop RFQ",
                Status = "Sent", SubmissionDeadline = DateTime.UtcNow.AddHours(1), Currency = "GHS",
                EstimatedValue = 400m, SourcePurchaseRequisitionId = Requisition.Id,
                SourcingReleaseId = ReleaseId, SourcingCaseId = CaseId, CreatedById = Guid.NewGuid()
            };
            Items =
            [
                new RequestForQuotationItem { Id = Guid.NewGuid(), TenantId = TenantId, RfqId = Rfq.Id, LineNumber = 1, Description = "Laptop", Quantity = 2, UnitOfMeasure = "EA" },
                new RequestForQuotationItem { Id = Guid.NewGuid(), TenantId = TenantId, RfqId = Rfq.Id, LineNumber = 2, Description = "Dock", Quantity = 2, UnitOfMeasure = "EA" }
            ];
            Suppliers = Enumerable.Range(1, 3).Select(index => new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerCode = $"SUP-{index:000}",
                PartnerName = $"Supplier {index}", PartnerType = "Supplier"
            }).ToList();
            Quotes = Suppliers.Select((supplier, supplierIndex) =>
            {
                var quote = new RequestForQuotationQuote
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, RfqId = Rfq.Id, BusinessPartnerId = supplier.Id,
                    Status = "Submitted", SubmittedAt = DateTime.UtcNow, SubmittedByUserId = Guid.NewGuid()
                };
                quote.Items = Items.Select((item, itemIndex) => new RequestForQuotationQuoteItem
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, QuoteId = quote.Id, RfqItemId = item.Id,
                    UnitPrice = 100m + supplierIndex * 10m + itemIndex * 5m,
                    LineTotal = (100m + supplierIndex * 10m + itemIndex * 5m) * item.Quantity
                }).ToList();
                return quote;
            }).ToList();
            var invitations = Suppliers.Select(supplier => new RequestForQuotationInvitation
            {
                Id = Guid.NewGuid(), TenantId = TenantId, RfqId = Rfq.Id,
                BusinessPartnerId = supplier.Id, Status = "Invited"
            }).ToList();
            Context.Add(new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active });
            Context.AddRange(Requisition, rule, sourcingCase, Rfq);
            Context.AddRange(Items);
            Context.AddRange(Suppliers);
            Context.AddRange(Quotes);
            Context.AddRange(Quotes.SelectMany(item => item.Items));
            Context.AddRange(invitations);
            Context.SaveChanges();

            _unitOfWork = new UnitOfWork(Context);
            SourcingCases.Setup(service => service.RevalidateSourceEntryAsync(
                    Requisition.Id, ReleaseId, CaseId, ProcurementMethodType.RequestForQuotation,
                    "RequestForQuotation", Rfq.Id, Rfq.RfqNumber, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
                {
                    SourcingCaseId = CaseId, SourcingReleaseId = ReleaseId,
                    SelectedMethod = ProcurementMethodType.RequestForQuotation,
                    MethodRuleId = rule.Id, MethodRuleCode = rule.RuleCode,
                    MinimumQuotationCount = 2, WorkflowDefinitionId = WorkflowDefinitionId,
                    EstimatedValue = Rfq.EstimatedValue, CurrencyCode = Rfq.Currency
                });
            SupplierValidation.Setup(service => service.ValidateForRfqAsync(
                    It.IsAny<Guid>(), It.IsAny<List<Guid>?>(), It.IsAny<decimal?>()))
                .ReturnsAsync(SupplierValidationResult.Success());
            ControlEvents.Setup(service => service.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Workflow.Setup(service => service.StartApprovalWorkflowAsync(
                    "RequestForQuotation", Rfq.Id, WorkflowDefinitionId))
                .ReturnsAsync(new WorkflowExecutionResult
                {
                    Success = true, Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = WorkflowInstanceId
                });
            Workflow.Setup(service => service.CanUserApproveAsync("RequestForQuotation", Rfq.Id, UserId))
                .ReturnsAsync(true);
            AllowSod();
            Service = new ProcurementRfqControlService(
                _unitOfWork, _currentUser.Object, _accessControl.Object, SodGuard.Object,
                ControlEvents.Object, SourcingCases.Object, Workflow.Object, SupplierValidation.Object);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid ReleaseId { get; }
        public Guid CaseId { get; }
        public Guid WorkflowDefinitionId { get; }
        public Guid WorkflowInstanceId { get; }
        public ApplicationDbContext Context { get; }
        public PurchaseRequisition Requisition { get; }
        public RequestForQuotation Rfq { get; }
        public List<RequestForQuotationItem> Items { get; }
        public List<BusinessPartner> Suppliers { get; }
        public List<RequestForQuotationQuote> Quotes { get; }
        public ProcurementRfqControlService Service { get; }
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<IProcurementSodGuardService> SodGuard { get; } = new();
        public Mock<IWorkflowService> Workflow { get; } = new();

        public void AllowSod() => SodGuard.Setup(service => service.EnforceAsync(
                It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true, Message = "Allowed." });

        public async Task RecordTwoOnTimeReceiptsAsync()
        {
            await Service.RecordReceiptAsync(Rfq.Id, Quotes[0].Id, Rfq.SubmissionDeadline!.Value.AddMinutes(-5), "receipt-1");
            await Service.RecordReceiptAsync(Rfq.Id, Quotes[1].Id, Rfq.SubmissionDeadline.Value.AddMinutes(-4), "receipt-2");
        }

        public CompleteProcurementRfqOpeningRequest ValidOpeningRequest() => new()
        {
            EvidenceReference = "OPENING-EVIDENCE-001",
            Participants =
            [
                new ProcurementRfqOpeningParticipantRequest { ParticipantUserId = UserId, ParticipantName = "RFQ Controller", RoleName = "Opening officer", SignatureReference = "SIG-OFFICER" },
                new ProcurementRfqOpeningParticipantRequest { ParticipantName = "Independent Observer", RoleName = "Observer", IsObserver = true, SignatureReference = "SIG-OBSERVER" }
            ]
        };

        public async Task OpenAsync()
        {
            await RecordTwoOnTimeReceiptsAsync();
            await Service.RecordReceiptAsync(Rfq.Id, Quotes[2].Id, Rfq.SubmissionDeadline!.Value.AddMinutes(1), "receipt-late");
            Rfq.SubmissionDeadline = DateTime.UtcNow.AddMinutes(-1);
            await Context.SaveChangesAsync();
            await Service.CompleteOpeningAsync(Rfq.Id, ValidOpeningRequest(), "opening");
        }

        public SaveProcurementRfqEvaluationRequest ValidEvaluationRequest() => new()
        {
            AwardMode = "WinnerTakesAll",
            RecommendationReason = "Supplier 1 has the best evaluated responsive quotation.",
            EvidenceReference = "EVAL-EVIDENCE-001",
            Lines = Items.Select(item => new SaveProcurementRfqEvaluationLineRequest
            {
                RfqItemId = item.Id, QuoteId = Quotes[0].Id,
                TechnicalScore = 85m, CommercialScore = 90m, TotalScore = 88m,
                RecommendationReason = "Best evaluated responsive offer."
            }).ToList()
        };

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
