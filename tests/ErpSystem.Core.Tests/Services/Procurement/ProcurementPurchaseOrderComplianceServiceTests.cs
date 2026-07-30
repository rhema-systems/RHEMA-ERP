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

            var budget = new Mock<IProcurementRequisitionBudgetControlService>();
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
                budget.Object,
                suppliers.Object,
                ghaneps.Object,
                controlEvents.Object,
                notifications.Object,
                NullLogger<ProcurementPurchaseOrderComplianceService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public PurchaseOrder PurchaseOrder { get; }
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
