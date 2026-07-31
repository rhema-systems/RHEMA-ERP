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

public sealed class ProcurementReceiptSourceControlServiceTests
{
    [Fact]
    public async Task GovernedCreateReturnsCapacitySnapshotAndRecordsAllowedAudit()
    {
        await using var fixture = new Fixture();

        var snapshot = await fixture.Service.EnforceCreateAsync(
            fixture.PurchaseOrder,
            [new ProcurementReceiptSourceLineRequest
            {
                PurchaseOrderItemId = fixture.PurchaseOrderItem.Id,
                ReceivedQuantity = 6m
            }],
            "PurchaseOrderReceipt",
            Guid.NewGuid(),
            "tdc0501-allowed");

        snapshot.IntegrityHash.Should().HaveLength(64);
        snapshot.Readiness.CanReceive.Should().BeTrue();
        snapshot.Readiness.Code.Should().Be("RCV_SOURCE_READY");
        snapshot.Readiness.DecisionKeys.Should().HaveCount(14)
            .And.StartWith("DEC-001")
            .And.EndWith("DEC-014");
        snapshot.Readiness.Lines.Should().ContainSingle();
        snapshot.Readiness.Lines.Single().Should().Match<ProcurementReceiptSourceLineDto>(
            line => line.OrderedQuantity == 10m &&
                    line.PreviouslyReceiptedQuantity == 0m &&
                    line.ToleranceQuantity == 0.5m &&
                    line.MaximumReceivableQuantity == 10.5m &&
                    line.RemainingQuantity == 10.5m &&
                    line.RequestedQuantity == 6m &&
                    line.Allowed);

        fixture.ControlEvents.Should().ContainSingle(item =>
            item.RuleCode == "RCV-001" &&
            item.RuleVersion == "TDC-0501" &&
            item.Result == ProcurementControlEventResult.Allowed &&
            item.DecisionKeys.Count == 14);
        fixture.Notifications.Should().ContainSingle(item =>
            item.TopicKey == "procurement.receipt-source.allowed" &&
            item.TenantId == fixture.TenantId);
    }

    [Fact]
    public async Task PriorReceiptReducesAuthoritativeRemainingCapacity()
    {
        await using var fixture = new Fixture(priorReceiptQuantity: 7m);

        var readiness = await fixture.Service.GetReadinessAsync(
            fixture.PurchaseOrder.Id,
            "tdc0501-capacity");

        readiness.CanReceive.Should().BeTrue();
        readiness.Lines.Should().ContainSingle();
        readiness.Lines.Single().PreviouslyReceiptedQuantity.Should().Be(7m);
        readiness.Lines.Single().RemainingQuantity.Should().Be(3.5m);
    }

    [Fact]
    public async Task OverCapacityReceiptFailsBeforeAllowedAuditOrNotification()
    {
        await using var fixture = new Fixture(priorReceiptQuantity: 7m);

        var action = () => fixture.Service.EnforceCreateAsync(
            fixture.PurchaseOrder,
            [new ProcurementReceiptSourceLineRequest
            {
                PurchaseOrderItemId = fixture.PurchaseOrderItem.Id,
                ReceivedQuantity = 3.5001m
            }],
            "GoodsReceiptNote",
            Guid.NewGuid(),
            "tdc0501-over-capacity");

        var exception = await action.Should()
            .ThrowAsync<ProcurementReceiptSourceValidationException>();
        exception.Which.Code.Should().Be("RCV_REMAINING_QUANTITY_EXCEEDED");
        exception.Which.Readiness.Should().NotBeNull();
        exception.Which.Readiness!.CanReceive.Should().BeFalse();
        fixture.ControlEvents.Should().BeEmpty();
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ForeignTenantPurchaseOrderIsRejectedBeforeCapabilityCheck()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.TenantId = Guid.NewGuid();

        var action = () => fixture.Service.EnforceCreateAsync(
            fixture.PurchaseOrder,
            [new ProcurementReceiptSourceLineRequest
            {
                PurchaseOrderItemId = fixture.PurchaseOrderItem.Id,
                ReceivedQuantity = 1m
            }],
            "PurchaseOrderReceipt",
            Guid.NewGuid(),
            "tdc0501-foreign-tenant");

        await action.Should()
            .ThrowAsync<ProcurementReceiptSourceAuthorizationException>();
        fixture.Access.Invocations.Should().BeEmpty();
        fixture.ControlEvents.Should().BeEmpty();
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task ExternalApplicantCannotReadInternalReceiptReadiness()
    {
        await using var fixture = new Fixture();
        fixture.IsExternalUser = true;

        var action = () => fixture.Service.GetReadinessAsync(
            fixture.PurchaseOrder.Id,
            "tdc0501-external");

        await action.Should()
            .ThrowAsync<ProcurementReceiptSourceAuthorizationException>();
        fixture.Access.Invocations.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public Fixture(decimal priorReceiptQuantity = 0m)
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
                OrderNumber = "PO-TDC0501-001",
                BusinessPartnerId = Guid.NewGuid(),
                Status = "Approved",
                Currency = "GHS",
                TotalAmount = 100m,
                DeliveryWarehouseId = Guid.NewGuid(),
                TolerancePercent = 5m,
                ProcurementSourceType = ProcurementPurchaseOrderSourceType.Contract,
                ProcurementSourceId = Guid.NewGuid(),
                ProcurementSourceReference = "CONTRACT/TDC0501",
                SourceIntegrityHash = new string('A', 64)
            };
            PurchaseOrderItem = new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseOrderId = PurchaseOrder.Id,
                ItemDescription = "Governed test item",
                OrderedQuantity = 10m,
                RemainingQuantity = 10m,
                UnitOfMeasure = "EA",
                UnitPrice = 10m,
                LineTotal = 100m
            };
            _context.PurchaseOrders.Add(PurchaseOrder);
            _context.PurchaseOrderItems.Add(PurchaseOrderItem);

            if (priorReceiptQuantity > 0)
            {
                var receipt = new PurchaseOrderReceipt
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PurchaseOrderId = PurchaseOrder.Id,
                    ReceiptNumber = "POR-TDC0501-PRIOR",
                    Status = "Received"
                };
                _context.PurchaseOrderReceipts.Add(receipt);
                _context.PurchaseOrderReceiptItems.Add(
                    new PurchaseOrderReceiptItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        ReceiptId = receipt.Id,
                        PurchaseOrderItemId = PurchaseOrderItem.Id,
                        ReceivedQuantity = priorReceiptQuantity,
                        AcceptedQuantity = priorReceiptQuantity,
                        UnitOfMeasure = "EA"
                    });
            }

            _context.SaveChanges();

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser)
                .Returns(() => IsExternalUser);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.Username).Returns("stores@tdc.test");
            current.SetupGet(item => item.FullName).Returns("TDC Stores Officer");
            current.SetupGet(item => item.Roles).Returns(["TDC_STORES_MANAGER"]);

            Access = new Mock<IProcurementAccessControlService>();
            Access.Setup(item => item.EnforceCapabilityAsync(
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
                .ReturnsAsync(new ProcurementPurchaseOrderSourceResolution
                {
                    SourceType = PurchaseOrder.ProcurementSourceType!.Value,
                    SourceId = PurchaseOrder.ProcurementSourceId!.Value,
                    SourceReference = PurchaseOrder.ProcurementSourceReference!,
                    BusinessPartnerId = PurchaseOrder.BusinessPartnerId,
                    CurrencyCode = PurchaseOrder.Currency,
                    SourceIntegrityHash = PurchaseOrder.SourceIntegrityHash!,
                    ValidatedAtUtc = DateTime.UtcNow
                });

            var events = new Mock<IProcurementControlEventService>();
            events.Setup(item => item.RecordAsync(
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

            Service = new ProcurementReceiptSourceControlService(
                _unitOfWork,
                current.Object,
                Access.Object,
                sources.Object,
                events.Object,
                notifications.Object,
                NullLogger<ProcurementReceiptSourceControlService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public bool IsExternalUser { get; set; }
        public PurchaseOrder PurchaseOrder { get; }
        public PurchaseOrderItem PurchaseOrderItem { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public ProcurementReceiptSourceControlService Service { get; }
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } = [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
