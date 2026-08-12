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

public sealed class ProcurementExceptionalSourcingControlServiceTests
{
    [Theory]
    [InlineData(ProcurementMethodType.RestrictedTendering, 2)]
    [InlineData(ProcurementMethodType.SingleSource, 1)]
    public async Task PreparationLocksExactMethodExceptionSuppliersAndEvidence(
        ProcurementMethodType method, int supplierCount)
    {
        await using var fixture = new Fixture(method);

        var prepared = await fixture.PrepareAsync();

        prepared.Method.Should().Be(method);
        prepared.Status.Should().Be(ProcurementExceptionalSourcingControlStatus.Prepared);
        prepared.Suppliers.Should().HaveCount(supplierCount);
        prepared.EvidenceChecklist.Should().ContainSingle(item =>
            item.RequirementKey == fixture.EvidenceRule.SharedRequirementKey &&
            item.VerificationReference == "verify://ppa-approval-letter");
        prepared.Milestones.Should().HaveCount(14);
        prepared.IntegrityHash.Should().HaveLength(64);
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request =>
                request.Action == "ExceptionalSourcingPrepared" && request.DecisionKeys.Count == 14),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreparationRejectsMissingEvidenceAndWrongSingleSourceSupplierCount()
    {
        await using var fixture = new Fixture(ProcurementMethodType.SingleSource);
        var request = fixture.PreparationRequest();
        request.EvidenceChecklist.Clear();

        await fixture.Service.Invoking(service => service.PrepareAsync(
                fixture.Tender.Id, request, "missing-evidence"))
            .Should().ThrowAsync<ProcurementExceptionalSourcingValidationException>()
            .Where(exception => exception.Code == "EXCEPTIONAL_EVIDENCE_MISSING");

        request = fixture.PreparationRequest();
        request.BusinessPartnerIds.Add(fixture.Suppliers[1].Id);
        await fixture.Service.Invoking(service => service.PrepareAsync(
                fixture.Tender.Id, request, "two-sole-suppliers"))
            .Should().ThrowAsync<ProcurementExceptionalSourcingValidationException>()
            .Where(exception => exception.Code == "SINGLE_SOURCE_SUPPLIER_COUNT");
    }

    [Theory]
    [InlineData(ProcurementMethodType.RestrictedTendering)]
    [InlineData(ProcurementMethodType.SingleSource)]
    public async Task ExactWorkflowNegotiationAwardContractAcceptanceAndFilingCloseTheRecord(
        ProcurementMethodType method)
    {
        await using var fixture = new Fixture(method);
        var prepared = await fixture.PrepareAsync();
        var submitted = await fixture.Service.SubmitApprovalAsync(fixture.Tender.Id,
            new SubmitProcurementExceptionalApprovalRequest { RowVersion = prepared.RowVersion }, "submit");

        fixture.CurrentUserId = Guid.NewGuid();
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "TenderException", fixture.Tender.Id, fixture.CurrentUserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = fixture.WorkflowInstanceId
            });
        var approved = await fixture.Service.DecideApprovalAsync(fixture.Tender.Id,
            new DecideProcurementExceptionalApprovalRequest
            {
                Action = "Approve", BoardApprovalReference = "BOARD-001",
                PpaApprovalReference = "PPA-001", RowVersion = submitted.RowVersion
            }, "approve");
        approved.Status.Should().Be(ProcurementExceptionalSourcingControlStatus.Approved);
        (await fixture.Context.TenderInvitations.CountAsync()).Should().Be(method == ProcurementMethodType.SingleSource ? 1 : 2);

        var negotiation = await fixture.AddCompletedNegotiationAsync();
        var negotiated = await fixture.Service.RecordNegotiationAsync(fixture.Tender.Id,
            new RecordProcurementExceptionalNegotiationRequest
            {
                NegotiationId = negotiation.Id, PlanReference = "NEG-PLAN-001",
                MinutesEvidenceReference = "evidence://signed-minutes", OutcomeReference = "NEG-OUT-001",
                RowVersion = approved.RowVersion
            }, "negotiation");
        fixture.CurrentUserId = Guid.NewGuid();
        var recommended = await fixture.Service.RecordRecommendationAsync(fixture.Tender.Id,
            new RecordProcurementExceptionalRecommendationRequest
            {
                BidId = fixture.Bids[0].Id, Reason = "Negotiated value and terms are acceptable.",
                EvidenceReference = "evidence://recommendation", RowVersion = negotiated.RowVersion
            }, "recommendation");
        fixture.CurrentUserId = Guid.NewGuid();
        var awarded = await fixture.Service.RecordAwardAsync(fixture.Tender.Id,
            new RecordProcurementTenderAwardRequest
            {
                BidId = fixture.Bids[0].Id, AwardReference = "AWD-001",
                EvidenceReference = "evidence://award", RowVersion = recommended.RowVersion
            }, "award");
        var contracted = await fixture.Service.RecordContractAsync(fixture.Tender.Id,
            new RecordProcurementTenderContractRequest
            {
                ContractReference = "CON-001", EvidenceReference = "evidence://contract",
                RowVersion = awarded.RowVersion
            }, "contract");
        var accepted = await fixture.Service.RecordAcceptanceAsync(fixture.Tender.Id,
            new RecordProcurementTenderAcceptanceRequest
            {
                AcceptanceReference = "ACC-001", EvidenceReference = "evidence://acceptance",
                RowVersion = contracted.RowVersion
            }, "acceptance");
        var filed = await fixture.Service.RecordPostAwardFilingAsync(fixture.Tender.Id,
            new RecordProcurementPostAwardFilingRequest
            {
                FilingReference = "PPA-FILE-001", FilingEvidenceReference = "evidence://filing",
                ExceptionReportReference = "EXR-001", ExceptionReportEvidenceReference = "evidence://exception-report",
                RowVersion = accepted.RowVersion
            }, "filing");

        filed.Status.Should().Be(ProcurementExceptionalSourcingControlStatus.Filed);
        filed.Milestones.Should().OnlyContain(item => item.CompletedAtUtc.HasValue);
        filed.PostAwardFilingReference.Should().Be("PPA-FILE-001");
        filed.ExceptionReportReference.Should().Be("EXR-001");
        fixture.ControlEvents.Verify(service => service.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(request => request.Action == "ExceptionalPostAwardFiled"),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.AwardReadiness.Verify(service => service.EnsureAwardReadyAsync(
            ProcurementAwardReadinessSourceType.ExceptionalSourcing,
            fixture.Tender.Id,
            It.Is<EvaluateProcurementAwardReadinessRequest>(request =>
                request.IdempotencyKey.StartsWith("award-gate:2:") &&
                request.ExpectedRecommendedSubjectIds.SequenceEqual(
                    new[] { fixture.Bids[0].Id }) &&
                request.ExpectedBusinessPartnerIds.SequenceEqual(
                    new[] { fixture.Bids[0].BusinessPartnerId }) &&
                request.ExpectedSourceIntegrityHash == null),
            "award",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnlistedSupplierAndForeignTenantAreRejected()
    {
        await using var fixture = new Fixture();
        var prepared = await fixture.PrepareAsync();
        var submitted = await fixture.Service.SubmitApprovalAsync(fixture.Tender.Id,
            new SubmitProcurementExceptionalApprovalRequest { RowVersion = prepared.RowVersion }, "submit");
        fixture.CurrentUserId = Guid.NewGuid();
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "TenderException", fixture.Tender.Id, fixture.CurrentUserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed, WorkflowInstanceId = fixture.WorkflowInstanceId });
        await fixture.Service.DecideApprovalAsync(fixture.Tender.Id,
            new DecideProcurementExceptionalApprovalRequest
            {
                Action = "Approve", BoardApprovalReference = "BOARD-001",
                PpaApprovalReference = "PPA-001", RowVersion = submitted.RowVersion
            }, "approve");

        await fixture.Service.Invoking(service => service.EnsureBidSupplierAllowedAsync(
                fixture.Tender.Id, fixture.Suppliers[2].Id))
            .Should().ThrowAsync<ProcurementExceptionalSourcingAuthorizationException>();
        fixture.CurrentTenantId = Guid.NewGuid();
        await fixture.Service.Invoking(service => service.GetAsync(fixture.Tender.Id))
            .Should().ThrowAsync<ProcurementExceptionalSourcingNotFoundException>()
            .Where(exception => exception.Code == "EXCEPTIONAL_CONTROL_NOT_FOUND");
    }

    [Fact]
    public async Task PettyPurchaseUsesDec005SingleSupplierWorkflowWithoutPpaFiling()
    {
        await using var fixture = new Fixture(ProcurementMethodType.PettyPurchase);
        var readiness = await fixture.Service.GetReadinessAsync(fixture.Tender.Id);
        readiness.Method.Should().Be(ProcurementMethodType.PettyPurchase);
        readiness.MinimumSupplierCount.Should().Be(1);
        readiness.PpaApprovalRequired.Should().BeFalse();
        readiness.PostAwardFilingRequired.Should().BeFalse();

        var prepared = await fixture.PrepareAsync();
        prepared.Method.Should().Be(ProcurementMethodType.PettyPurchase);
        prepared.Suppliers.Should().ContainSingle();
        var submitted = await fixture.Service.SubmitApprovalAsync(fixture.Tender.Id,
            new SubmitProcurementExceptionalApprovalRequest { RowVersion = prepared.RowVersion }, "petty-submit");
        fixture.CurrentUserId = Guid.NewGuid();
        fixture.Workflow.Setup(service => service.ProcessApprovalStepAsync(
                "TenderException", fixture.Tender.Id, fixture.CurrentUserId, "approve", It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true, Status = WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = fixture.WorkflowInstanceId
            });
        var approved = await fixture.Service.DecideApprovalAsync(fixture.Tender.Id,
            new DecideProcurementExceptionalApprovalRequest
            {
                Action = "Approve", RowVersion = submitted.RowVersion
            }, "petty-approve");
        approved.Status.Should().Be(ProcurementExceptionalSourcingControlStatus.Approved);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly Mock<ICurrentUserProvider> _currentUser = new();

        public Fixture(ProcurementMethodType method = ProcurementMethodType.RestrictedTendering)
        {
            CurrentTenantId = Guid.NewGuid(); CurrentUserId = Guid.NewGuid(); ReleaseId = Guid.NewGuid();
            WorkflowDefinitionId = Guid.NewGuid(); WorkflowInstanceId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
            Context = new ApplicationDbContext(options);
            _currentUser.SetupGet(item => item.UserId).Returns(() => CurrentUserId);
            _currentUser.SetupGet(item => item.TenantId).Returns(() => CurrentTenantId);
            _currentUser.SetupGet(item => item.Username).Returns("exception.controller@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Exception Controller");
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Roles).Returns(["SuperAdmin"]);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => role == "SuperAdmin");

            Requisition = new PurchaseRequisition
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, RequisitionNumber = "PR-EX-001",
                RequestedById = Guid.NewGuid(), RequiredDate = DateTime.UtcNow.AddDays(30),
                Status = "Approved", Currency = "GHS", TotalAmount = 500_000m
            };
            Rule = new ProcurementPolicyMethodRule
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PolicySetId = Guid.NewGuid(),
                RuleCode = method == ProcurementMethodType.SingleSource ? "SS-GOODS-001" :
                    method == ProcurementMethodType.PettyPurchase ? "PETTY-GOODS-001" : "RT-GOODS-001",
                Name = method.ToString(), Category = ProcurementCategoryClass.Goods, Method = method,
                IsAllowed = true, IsEnabled = true, RequiresCompetition = method == ProcurementMethodType.RestrictedTendering,
                MinimumQuotationCount = method == ProcurementMethodType.RestrictedTendering ? 2 : 1,
                WorkflowDefinitionId = WorkflowDefinitionId, EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            ExceptionRule = new ProcurementPolicyExceptionRule
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PolicySetId = Rule.PolicySetId,
                RuleCode = method == ProcurementMethodType.PettyPurchase ? "EXCEPTION-DEC-005" : "EXCEPTION-DEC-006",
                ExceptionName = $"{method} prerequisites",
                ExceptionType = method.ToString(), Category = ProcurementCategoryClass.Goods, Method = method,
                Disposition = ProcurementExceptionDisposition.ApprovalRequired, JustificationRequired = true,
                EvidenceRequired = true, PostAwardFilingRequired = method != ProcurementMethodType.PettyPurchase,
                ApproverRole = method == ProcurementMethodType.PettyPurchase ? "Finance Manager" : "Board and PPA",
                WorkflowDefinitionId = WorkflowDefinitionId,
                SourceDecisionKey = method == ProcurementMethodType.PettyPurchase ? "DEC-005" : "DEC-006", IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            EvidenceRule = new ProcurementPolicyEvidenceRule
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PolicySetId = Rule.PolicySetId,
                RuleCode = method == ProcurementMethodType.PettyPurchase ? "EVID-DEC-005-001" : "EVID-DEC-006-001",
                EvidenceName = method == ProcurementMethodType.PettyPurchase ? "Petty-purchase receipt or quotation" : "PPA approval letter",
                Stage = method == ProcurementMethodType.PettyPurchase ? ProcurementEvidenceStage.Requisition : ProcurementEvidenceStage.Sourcing,
                Method = method,
                SharedRequirementKey = method == ProcurementMethodType.PettyPurchase ? "PROC-DEC-005-001" : "PROC-DEC-006-001",
                IsMandatory = true, RequiresVerification = true,
                SourceDecisionKey = method == ProcurementMethodType.PettyPurchase ? "DEC-005" : "DEC-006", IsEnabled = true,
                EffectiveFrom = DateTime.UtcNow.AddDays(-10)
            };
            Route = new ProcurementRequisitionAuthorityRoute
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PurchaseRequisitionId = Requisition.Id,
                AttemptNumber = 1, RouteReference = "AUTH-BOARD-PPA-001", EvaluationId = Guid.NewGuid(),
                CorrelationId = "route", PolicySetId = Rule.PolicySetId, PolicyKey = Guid.NewGuid(),
                PolicyCode = "TDC-PROC", PolicyName = "TDC Procurement", PolicyVersion = 1,
                SourceConfigurationProfileId = Guid.NewGuid(), Category = ProcurementCategoryClass.Goods,
                Amount = 500_000m, CurrencyCode = "GHS", PolicyDateUtc = DateTime.UtcNow,
                EvaluatedAtUtc = DateTime.UtcNow, WorkflowDefinitionId = WorkflowDefinitionId,
                WorkflowDefinitionKey = Guid.NewGuid(), WorkflowName = "Exceptional sourcing approval",
                WorkflowVersion = 1, WorkflowEntityTypeCode = "TenderException",
                CapturedAtUtc = DateTime.UtcNow, CapturedById = CurrentUserId,
                CapturedByName = "Controller", SnapshotJson = "{}", IntegrityHash = new string('a', 64)
            };
            if (method == ProcurementMethodType.PettyPurchase)
                Route.Steps.Add(Step("AUTH-FINANCE", "Finance Manager", "Finance Manager", 1));
            else
            {
                Route.Steps.Add(Step("AUTH-BOARD", "TDC Board", "Board Secretary", 1));
                Route.Steps.Add(Step("AUTH-PPA", "Public Procurement Authority", "PPA Reviewer", 2));
            }
            Case = new ProcurementSourcingCase
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PurchaseRequisitionId = Requisition.Id,
                SourcingReleaseId = ReleaseId, CaseSequence = 1, CaseNumber = "SC-EX-001",
                Category = ProcurementCategoryClass.Goods, RecommendedMethod = method, SelectedMethod = method,
                MethodSelectionBasis = ProcurementSourcingMethodSelectionBasis.AutomaticRecommendation,
                EstimatedValue = 500_000m, CurrencyCode = "GHS", PolicySetId = Rule.PolicySetId,
                PolicyCode = "TDC-PROC", PolicyVersion = 1, MethodRuleId = Rule.Id,
                MethodRuleCode = Rule.RuleCode, ThresholdRuleId = Guid.NewGuid(), ThresholdRuleCode = "TH-EX",
                AuthorityRouteId = Route.Id, AuthorityRouteReference = Route.RouteReference,
                ApprovedExceptionRuleId = ExceptionRule.Id, Justification = "Exceptional method selected by policy.",
                CreatedByName = "Controller", Status = ProcurementSourcingCaseStatus.InProgress,
                SourceControlFingerprint = new string('b', 64), CaseFingerprint = new string('c', 64),
                SnapshotJson = "{}", IntegrityHash = new string('d', 64), AuthorityRoute = Route
            };
            Tender = new Tender
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId,
                TenderNumber = method == ProcurementMethodType.SingleSource ? "SS-2026-001" :
                    method == ProcurementMethodType.PettyPurchase ? "PETTY-2026-001" : "RT-2026-001",
                Title = "Exceptional infrastructure procurement", TenderType = "RFP", Status = "Approved",
                EstimatedValue = Case.EstimatedValue, Currency = Case.CurrencyCode,
                SubmissionDeadline = DateTime.UtcNow.AddDays(3), SourcePurchaseRequisitionId = Requisition.Id,
                SourcingReleaseId = ReleaseId, SourcingCaseId = Case.Id, CreatedById = Guid.NewGuid()
            };
            Suppliers = Enumerable.Range(1, 3).Select(index => new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, PartnerCode = $"SUP-{index:000}",
                PartnerName = $"Supplier {index}", PartnerType = "Supplier", RegistrationStatus = "Approved",
                ApprovalStatus = "Approved", IsActive = true
            }).ToList();
            Bids = Suppliers.Take(method is ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase ? 1 : 2).Select((supplier, index) => new TenderBid
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, TenderId = Tender.Id,
                BusinessPartnerId = supplier.Id, BusinessPartner = supplier, BidNumber = $"BID-{index + 1:000}",
                Status = "Submitted", TotalBidAmount = 450_000m + index * 10_000m, Currency = "GHS",
                Items = [new TenderBidItem { Id = Guid.NewGuid(), TenantId = CurrentTenantId, TenderItemId = Guid.NewGuid(), OfferedQuantity = 1, UnitPrice = 450_000m, TotalPrice = 450_000m }]
            }).ToList();
            foreach (var bid in Bids) foreach (var item in bid.Items) item.TenderBidId = bid.Id;

            Context.Add(new Tenant { Id = CurrentTenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active });
            Context.AddRange(Requisition, Rule, ExceptionRule, EvidenceRule, Route, Case, Tender);
            Context.AddRange(Route.Steps); Context.AddRange(Suppliers); Context.AddRange(Bids);
            Context.AddRange(Bids.SelectMany(item => item.Items)); Context.SaveChanges();
            _unitOfWork = new UnitOfWork(Context);
            SourcingCases.Setup(service => service.RevalidateSourceEntryAsync(
                    Requisition.Id, ReleaseId, Case.Id, method, "Tender", Tender.Id, Tender.TenderNumber,
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
                {
                    SourcingCaseId = Case.Id, SourcingReleaseId = ReleaseId, SelectedMethod = method,
                    MethodRuleId = Rule.Id, MethodRuleCode = Rule.RuleCode,
                    MinimumQuotationCount = Rule.MinimumQuotationCount, WorkflowDefinitionId = WorkflowDefinitionId,
                    EstimatedValue = Tender.EstimatedValue!.Value, CurrencyCode = Tender.Currency!
                });
            SupplierValidation.Setup(service => service.ValidateForTenderAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<decimal?>()))
                .ReturnsAsync(SupplierValidationResult.Success());
            SupplierValidation.Setup(service => service.EvaluateEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SupplierValidationResult.Success());
            ControlEvents.Setup(service => service.RecordAsync(It.IsAny<ProcurementControlEventWriteRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Workflow.Setup(service => service.StartApprovalWorkflowAsync("TenderException", Tender.Id, WorkflowDefinitionId))
                .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = WorkflowInstanceId });
            Workflow.Setup(service => service.CanUserApproveAsync("TenderException", Tender.Id, It.IsAny<Guid>())).ReturnsAsync(true);
            SodGuard.Setup(service => service.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementSodGuardRequest request, string _, CancellationToken _) => new ProcurementSodGuardDecisionDto
                { Allowed = !request.ProhibitedActorUserIds.Contains(CurrentUserId), Message = "Actors must be separated." });
            Notifications.Setup(service => service.SendTenderPublishedNotificationAsync(Tender.Id, It.IsAny<List<Guid>>(), It.IsAny<List<string>?>()))
                .Returns(Task.CompletedTask);
            TenderDocuments.Setup(service => service.EnsurePublicationReadyAsync(
                    It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(), It.IsAny<DateTime>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementTenderDocumentEffectiveStateDto { Ready = true });
            TenderDocuments.Setup(service => service.EnsureDispatchReadyAsync(
                    It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementTenderDocumentEffectiveStateDto { Ready = true });
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
            Service = new ProcurementExceptionalSourcingControlService(_unitOfWork, _currentUser.Object,
                AccessControl.Object, SodGuard.Object, ControlEvents.Object, SourcingCases.Object,
                Workflow.Object, SupplierValidation.Object, Notifications.Object, TenderDocuments.Object,
                AwardReadiness.Object);
        }

        public Guid CurrentTenantId { get; set; }
        public Guid CurrentUserId { get; set; }
        public Guid ReleaseId { get; }
        public Guid WorkflowDefinitionId { get; }
        public Guid WorkflowInstanceId { get; }
        public ApplicationDbContext Context { get; }
        public PurchaseRequisition Requisition { get; }
        public ProcurementPolicyMethodRule Rule { get; }
        public ProcurementPolicyExceptionRule ExceptionRule { get; }
        public ProcurementPolicyEvidenceRule EvidenceRule { get; }
        public ProcurementRequisitionAuthorityRoute Route { get; }
        public ProcurementSourcingCase Case { get; }
        public Tender Tender { get; }
        public List<BusinessPartner> Suppliers { get; }
        public List<TenderBid> Bids { get; }
        public ProcurementExceptionalSourcingControlService Service { get; }
        public Mock<IProcurementAccessControlService> AccessControl { get; } = new();
        public Mock<IProcurementSodGuardService> SodGuard { get; } = new();
        public Mock<IProcurementTenderDocumentControlService> TenderDocuments { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } = new();
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public Mock<IWorkflowService> Workflow { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<ITenderNotificationService> Notifications { get; } = new();
        public Mock<IProcurementAwardReadinessService> AwardReadiness { get; } = new();

        public PrepareProcurementExceptionalSourcingRequest PreparationRequest() => new()
        {
            Justification = "A necessary statutory exception supported by the attached market evidence.",
            JustificationEvidenceReference = "evidence://justification",
            SupplierSelectionEvidenceReference = "evidence://supplier-selection",
            BusinessPartnerIds = Suppliers.Take(Case.SelectedMethod is ProcurementMethodType.SingleSource or ProcurementMethodType.PettyPurchase ? 1 : 2).Select(item => item.Id).ToList(),
            EvidenceChecklist = [new ProcurementExceptionalEvidenceRequest
            {
                RequirementKey = EvidenceRule.SharedRequirementKey!, EvidenceReference = "evidence://ppa-approval-letter",
                VerificationReference = "verify://ppa-approval-letter"
            }]
        };

        public Task<ProcurementExceptionalSourcingControlDto> PrepareAsync() =>
            Service.PrepareAsync(Tender.Id, PreparationRequest(), "prepare");

        public async Task<TenderNegotiation> AddCompletedNegotiationAsync()
        {
            var negotiation = new TenderNegotiation
            {
                Id = Guid.NewGuid(), TenantId = CurrentTenantId, TenderId = Tender.Id,
                TenderBidId = Bids[0].Id, BusinessPartnerId = Bids[0].BusinessPartnerId,
                Status = "Completed", InvitedDate = DateTime.UtcNow.AddHours(-2),
                CompletedDate = DateTime.UtcNow.AddHours(-1), CompletedById = Guid.NewGuid(),
                OriginalAmount = Bids[0].TotalBidAmount, NegotiatedAmount = Bids[0].TotalBidAmount - 10_000m,
                Currency = "GHS", Notes = "Signed negotiated outcome."
            };
            Context.Add(negotiation); await Context.SaveChangesAsync(); return negotiation;
        }

        private ProcurementRequisitionAuthorityRouteStep Step(string code, string name, string role, int sequence) => new()
        {
            Id = Guid.NewGuid(), TenantId = CurrentTenantId, AuthorityRouteId = Route?.Id ?? Guid.Empty,
            Sequence = sequence, AuthorityRuleId = Guid.NewGuid(), RulePolicySetId = Rule.Id == Guid.Empty ? Guid.NewGuid() : Rule.PolicySetId,
            RulePolicyCode = "TDC-PROC", RulePolicyVersion = 1, RuleCode = code, AuthorityName = name,
            AuthorityRole = role, CurrencyCode = "GHS", LowerBound = 0, LowerInclusive = true, UpperInclusive = true,
            Quorum = 1, WorkflowDefinitionId = WorkflowDefinitionId, WorkflowStepId = Guid.NewGuid(),
            WorkflowStepName = name, WorkflowStepOrder = sequence
        };

        public async ValueTask DisposeAsync() { _unitOfWork.Dispose(); await Context.DisposeAsync(); }
    }
}
