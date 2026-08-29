using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderComplianceServiceTests
{
    [Fact]
    public async Task BlockedEnforcementReturnsTenChecksAndRecordsImmutableAudit()
    {
        await using var fixture = new Fixture();

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Submit",
            "tdc0404-blocked");

        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        exception.Which.Code.Should().Be("PO_COMPLIANCE_BLOCKED");
        exception.Which.Readiness.Checks.Should().HaveCount(10);
        exception.Which.Readiness.Checks.Select(item => item.Key).Should().Equal(
            "source",
            "supplier",
            "budget",
            "commitment",
            "evaluation",
            "award",
            "sod",
            "ghaneps",
            "contract",
            "signature");
        exception.Which.Readiness.DecisionKeys.Should().HaveCount(14)
            .And.StartWith("DEC-001")
            .And.EndWith("DEC-014");

        fixture.ControlEvents.Should().ContainSingle();
        var controlEvent = fixture.ControlEvents.Single();
        controlEvent.RuleCode.Should().Be("PO-003");
        controlEvent.RuleVersion.Should().Be("TDC-0404");
        controlEvent.Result.Should().Be(ProcurementControlEventResult.Denied);
        controlEvent.DecisionKeys.Should().HaveCount(14);
        controlEvent.SourceId.Should().Be(fixture.PurchaseOrder.Id);
        fixture.Notifications.Should().ContainSingle(item =>
            item.TopicKey == "procurement.purchase-order.compliance-blocked" &&
            item.EntityId == fixture.PurchaseOrder.Id);
    }

    [Fact]
    public async Task ForeignTenantPurchaseOrderIsRejectedBeforeAudit()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.TenantId = Guid.NewGuid();

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Approve",
            "tdc0404-foreign");

        await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceAuthorizationException>();
        fixture.ControlEvents.Should().BeEmpty();
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public void BuildEvidenceCollapsesDuplicateResolvedReferences()
    {
        var referenceId = Guid.NewGuid();
        var checks = new[]
        {
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "evaluation",
                Label = "Approved evaluation",
                Reference = " EVAL-001 "
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "award",
                Label = "Approved award",
                Reference = "eval-001"
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "source",
                Label = "Approved source",
                ReferenceId = referenceId
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "empty",
                Label = "No evidence"
            }
        };

        var evidence = ProcurementPurchaseOrderComplianceService
            .BuildEvidence(checks);

        evidence.Should().HaveCount(2);
        evidence.Select(item => item.Reference).Should().BeEquivalentTo(
            "EVAL-001",
            referenceId.ToString("D"));
        evidence.Should().OnlyHaveUniqueItems(item => new
        {
            item.ReferenceKind,
            Reference = item.Reference!.ToUpperInvariant()
        });
    }

    [Theory]
    [InlineData(ProcurementCategoryClass.Goods, "GOODS")]
    [InlineData(ProcurementCategoryClass.Works, "WORKS")]
    [InlineData(ProcurementCategoryClass.TechnicalServices, "SERVICES")]
    [InlineData(ProcurementCategoryClass.ConsultancyServices, "SERVICES")]
    [InlineData(ProcurementCategoryClass.GeneralServices, "SERVICES")]
    public void SupplierCategoryCodeUsesTheCanonicalOnboardingClassification(
        ProcurementCategoryClass category,
        string expectedCode)
    {
        ProcurementPurchaseOrderComplianceService.SupplierCategoryCode(category)
            .Should().Be(expectedCode);
    }

    [Fact]
    public void ReleaseOnlyRfqAwardIsRecognizedWithoutFabricatedAdvancedLineage()
    {
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var integrityHash = new string('a', 64);
        var purchaseOrder = new PurchaseOrder
        {
            ProcurementSourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            SourceRequisitionId = requisitionId,
            SourcingReleaseId = releaseId,
            SourcingCaseId = null,
            AwardReadinessDecisionId = null,
            BusinessPartnerId = supplierId,
            SourceIntegrityHash = integrityHash
        };
        var source = new ProcurementPurchaseOrderSourceResolution
        {
            SourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            SourceId = Guid.NewGuid(),
            PurchaseRequisitionId = requisitionId,
            SourcingReleaseId = releaseId,
            SourcingCaseId = Guid.Empty,
            AwardReadinessDecisionId = Guid.Empty,
            BusinessPartnerId = supplierId,
            SourceIntegrityHash = integrityHash
        };

        ProcurementPurchaseOrderComplianceService.IsReleaseOnlyRfqAward(
                purchaseOrder,
                source)
            .Should().BeTrue();
    }

    [Fact]
    public async Task DraftPoPassesCommitmentCheckWhenActualExposureIsBudgetAvailable()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.SourceRequisitionId = Guid.NewGuid();
        fixture.BudgetControl.Setup(item => item.GetDownstreamReadinessAsync(
                fixture.PurchaseOrder.SourceRequisitionId.Value,
                It.IsAny<decimal>(),
                fixture.PurchaseOrder.Currency,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReadinessDto
            {
                RequisitionId = fixture.PurchaseOrder.SourceRequisitionId.Value,
                BudgetId = Guid.NewGuid(),
                BudgetCode = "PB-TEST-001",
                BudgetStatus = "Approved",
                Currency = "GHS",
                RequestedAmount = fixture.PurchaseOrder.TotalAmount,
                AvailableAmount = 500m,
                IsCompliant = true,
                CanReserve = true,
                DecisionCode = "PR_BUDGET_AVAILABLE"
            });

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Submit",
            "trace-draft-budget");
        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        var readiness = exception.Which.Readiness;

        var commitment = readiness.Checks.Single(item => item.Key == "commitment");
        commitment.Passed.Should().BeTrue();
        commitment.Required.Should().BeFalse();
        commitment.Message.Should().Contain("final PO approval");
    }

    [Fact]
    public async Task ApprovedPoRequiresTheFinalApprovalCommitment()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.SourceRequisitionId = Guid.NewGuid();
        fixture.PurchaseOrder.Status = "Approved";
        fixture.BudgetControl.Setup(item => item.GetDownstreamReadinessAsync(
                fixture.PurchaseOrder.SourceRequisitionId.Value,
                It.IsAny<decimal>(),
                fixture.PurchaseOrder.Currency,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReadinessDto
            {
                RequisitionId = fixture.PurchaseOrder.SourceRequisitionId.Value,
                BudgetId = Guid.NewGuid(),
                BudgetCode = "PB-TEST-001",
                BudgetStatus = "Approved",
                Currency = "GHS",
                RequestedAmount = fixture.PurchaseOrder.TotalAmount,
                AvailableAmount = 500m,
                IsCompliant = true,
                CanReserve = true,
                DecisionCode = "PR_BUDGET_AVAILABLE"
            });

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Approve",
            "trace-approved-budget");
        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        var readiness = exception.Which.Readiness;

        var commitment = readiness.Checks.Single(item => item.Key == "commitment");
        commitment.Passed.Should().BeFalse();
        commitment.Required.Should().BeTrue();
        commitment.Message.Should().Contain("no active reservation");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            _context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(_context);

            PurchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrderNumber = "PO-TDC0404-001",
                BusinessPartnerId = Guid.NewGuid(),
                Status = "Draft",
                Currency = "GHS",
                TotalAmount = 100m
            };

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.Username).Returns("officer@tdc.test");
            current.SetupGet(item => item.FullName).Returns("TDC Officer");
            current.SetupGet(item => item.Roles)
                .Returns(["TDC_PROCUREMENT_OFFICER"]);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });

            var sources = new Mock<IProcurementPurchaseOrderSourceService>();
            sources.Setup(item => item.EvaluateCurrentAsync(
                    It.IsAny<PurchaseOrder>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcurementPurchaseOrderSourceValidationException(
                    "PO_SOURCE_REQUIRED",
                    "Approved source lineage is required."));

            BudgetControl = new Mock<IProcurementRequisitionBudgetControlService>();
            var suppliers = new Mock<ISupplierValidationService>();
            suppliers.Setup(item => item.EvaluateEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SupplierValidationResult
                {
                    IsValid = false,
                    ValidationCode = "SUPPLIER_INELIGIBLE",
                    Errors = ["Supplier evidence is incomplete."],
                    BusinessPartnerId = PurchaseOrder.BusinessPartnerId,
                    TenantId = TenantId,
                    PartnerCode = "SUP-001",
                    PartnerName = "Controlled Supplier"
                });

            var ghaneps = new Mock<IProcurementGhanepsExchangeService>();
            var controlEvents = new Mock<IProcurementControlEventService>();
            controlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>(
                    (request, _) => ControlEvents.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());

            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NotificationTopicEvent, CancellationToken>(
                    (request, _) => Notifications.Add(request))
                .Returns(Task.CompletedTask);

            Service = new ProcurementPurchaseOrderComplianceService(
                _unitOfWork,
                current.Object,
                access.Object,
                sources.Object,
                BudgetControl.Object,
                suppliers.Object,
                ghaneps.Object,
                controlEvents.Object,
                notifications.Object,
                NullLogger<ProcurementPurchaseOrderComplianceService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public PurchaseOrder PurchaseOrder { get; }
        public Mock<IProcurementRequisitionBudgetControlService> BudgetControl { get; }
        public ProcurementPurchaseOrderComplianceService Service { get; }
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } = [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
