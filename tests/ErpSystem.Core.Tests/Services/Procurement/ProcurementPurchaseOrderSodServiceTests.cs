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

public sealed class ProcurementPurchaseOrderSodServiceTests
{
    [Fact]
    public async Task DirectFinalizedOrderDoesNotAdvertiseApprovalButRetainsReceivingIndependence()
    {
        var requester = Guid.NewGuid();
        var creator = Guid.NewGuid();
        await using var fixture = new Fixture(requester, creator, requester);
        fixture.PurchaseOrder.ApprovalRequired = false;

        var readiness = await fixture.Service.EnforceReceiptAsync(fixture.PurchaseOrder, "direct-receipt");

        readiness.ApprovalRequired.Should().BeFalse();
        readiness.CanApprove.Should().BeFalse();
        readiness.CanReceive.Should().BeTrue();
        readiness.Code.Should().Be("PO_SOD_READY");
        readiness.Checks.Single(check => check.Key == "approval").Code.Should().Be("PO_APPROVAL_NOT_REQUIRED");
        fixture.EnforcedRequests.Should().OnlyContain(request =>
            request.ControlCode == ProcurementPurchaseOrderSodRules.ReceiptControl);
    }

    [Fact]
    public async Task DirectFinalizedOrderStillRejectsCreatorReceivingOwnOrder()
    {
        var creator = Guid.NewGuid();
        await using var fixture = new Fixture(Guid.NewGuid(), creator, creator);
        fixture.PurchaseOrder.ApprovalRequired = false;

        var action = () => fixture.Service.EnforceReceiptAsync(fixture.PurchaseOrder, "direct-creator-receipt");

        (await action.Should().ThrowAsync<ProcurementPurchaseOrderSodBlockedException>())
            .Which.Code.Should().Be("PO_SOD_RECEIPT_BLOCKED");
    }

    [Fact]
    public async Task RequesterCannotApproveAndDenialRecordsAllDecisionKeys()
    {
        var requester = Guid.NewGuid();
        await using var fixture = new Fixture(
            requester,
            Guid.NewGuid(),
            requester);

        var action = () => fixture.Service.EnforceApprovalAsync(
            fixture.PurchaseOrder,
            "tdc0405-requester");

        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderSodBlockedException>();
        exception.Which.Code.Should().Be("PO_SOD_APPROVAL_BLOCKED");
        exception.Which.Readiness.CanApprove.Should().BeFalse();
        exception.Which.Readiness.Checks.Should().HaveCount(2);
        fixture.EnforcedRequests.Should().ContainSingle(request =>
            request.ControlCode ==
            ProcurementPurchaseOrderSodRules.ApprovalControl &&
            request.ProhibitedActorUserIds.Contains(requester));
        fixture.ControlEvents.Should().ContainSingle();
        fixture.ControlEvents.Single().RuleCode.Should().Be("PO-004");
        fixture.ControlEvents.Single().RuleVersion.Should().Be("TDC-0405");
        fixture.ControlEvents.Single().DecisionKeys.Should().HaveCount(14);
        fixture.Notifications.Should().ContainSingle(item =>
            item.TopicKey == "procurement.purchase-order.sod-blocked");
    }

    [Fact]
    public async Task IndependentApproverIsAllowed()
    {
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var readiness = await fixture.Service.EnforceApprovalAsync(
            fixture.PurchaseOrder,
            "tdc0405-independent");

        readiness.CanApprove.Should().BeTrue();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Result == ProcurementControlEventResult.Allowed &&
            item.RuleCode == "PO-004");
    }

    [Fact]
    public async Task CreatorCannotApprove()
    {
        var creator = Guid.NewGuid();
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            creator,
            creator);

        var action = () => fixture.Service.EnforceApprovalAsync(
            fixture.PurchaseOrder,
            "tdc0405-creator-approval");

        (await action.Should()
                .ThrowAsync<ProcurementPurchaseOrderSodBlockedException>())
            .Which.Code.Should().Be("PO_SOD_APPROVAL_BLOCKED");
    }

    [Fact]
    public async Task CreatorCannotReceiveButDifferentRequesterMayReceive()
    {
        var requester = Guid.NewGuid();
        var creator = Guid.NewGuid();
        await using var creatorFixture = new Fixture(
            requester,
            creator,
            creator);

        var denied = () => creatorFixture.Service.EnforceReceiptAsync(
            creatorFixture.PurchaseOrder,
            "tdc0405-creator-receipt");
        (await denied.Should()
                .ThrowAsync<ProcurementPurchaseOrderSodBlockedException>())
            .Which.Code.Should().Be("PO_SOD_RECEIPT_BLOCKED");
        creatorFixture.AccessRequests.Should().ContainSingle(request =>
            request.PermissionCode == "procurement.inventory.receive" &&
            request.WarehouseId ==
            creatorFixture.PurchaseOrder.DeliveryWarehouseId);
        creatorFixture.ControlEvents.Should().ContainSingle(item =>
            item.RuleCode == "RCV-004" &&
            item.Result == ProcurementControlEventResult.Denied);

        await using var requesterFixture = new Fixture(
            requester,
            creator,
            requester);
        var allowed = await requesterFixture.Service.EnforceReceiptAsync(
            requesterFixture.PurchaseOrder,
            "tdc0405-requester-receipt");
        allowed.CanReceive.Should().BeTrue();
    }

    [Fact]
    public async Task CreatorCannotApproveReceiptInspectionAndDenialUsesExactAction()
    {
        var creator = Guid.NewGuid();
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            creator,
            creator);

        var action = () => fixture.Service.EnforceReceiptActionAsync(
            fixture.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.ApproveReceiptInspection,
            "tdc0503-inspection-approval");

        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderSodBlockedException>();
        exception.Which.Code.Should().Be("PO_SOD_RECEIPT_BLOCKED");
        exception.Which.Readiness.ReceiptActionCoverage.Should()
            .Equal(ProcurementPurchaseOrderSodRules.ReceiptActionCoverage);
        fixture.AccessRequests.Should().ContainSingle(request =>
            request.PermissionCode == "procurement.inventory.receive" &&
            request.WarehouseId ==
            fixture.PurchaseOrder.DeliveryWarehouseId);
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action ==
                ProcurementPurchaseOrderSodRules.ApproveReceiptInspection &&
            item.RuleCode == "RCV-004" &&
            item.RuleVersion == "TDC-0503" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task IndependentActorMayConfirmReplacementReceipt()
    {
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var readiness = await fixture.Service.EnforceReceiptActionAsync(
            fixture.PurchaseOrder,
            ProcurementPurchaseOrderSodRules.ConfirmReplacementReceipt,
            "tdc0503-replacement");

        readiness.CanReceive.Should().BeTrue();
        readiness.ReceiptActionCoverage.Should().HaveCount(9);
        fixture.AccessRequests.Should().ContainSingle(request =>
            request.PermissionCode == "procurement.inventory.receive" &&
            request.WarehouseId ==
            fixture.PurchaseOrder.DeliveryWarehouseId);
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action ==
                ProcurementPurchaseOrderSodRules.ConfirmReplacementReceipt &&
            item.RuleVersion == "TDC-0503" &&
            item.Result == ProcurementControlEventResult.Allowed);
    }

    [Fact]
    public async Task AutomaticApprovalIsRejectedEvenForIndependentActor()
    {
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());

        var action = () => fixture.Service.RejectApprovalBypassAsync(
            fixture.PurchaseOrder,
            "WorkflowAutoApprove",
            "tdc0405-auto");

        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderSodBlockedException>();
        exception.Which.Code.Should()
            .Be("PO_AUTOMATIC_APPROVAL_PROHIBITED");
        exception.Which.Readiness.CanApprove.Should().BeFalse();
        fixture.ControlEvents.Should().ContainSingle(item =>
            item.Action == "WorkflowAutoApprove" &&
            item.Result == ProcurementControlEventResult.Denied);
    }

    [Fact]
    public async Task ForeignTenantPurchaseOrderIsRejectedBeforeAudit()
    {
        await using var fixture = new Fixture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid());
        fixture.PurchaseOrder.TenantId = Guid.NewGuid();

        var action = () => fixture.Service.EnforceApprovalAsync(
            fixture.PurchaseOrder,
            "tdc0405-foreign");

        await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderSodAuthorizationException>();
        fixture.ControlEvents.Should().BeEmpty();
        fixture.EnforcedRequests.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public Fixture(Guid requesterId, Guid creatorId, Guid currentUserId)
        {
            TenantId = Guid.NewGuid();
            UserId = currentUserId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            _context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(_context);
            PurchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrderNumber = "PO-TDC0405-001",
                BusinessPartnerId = Guid.NewGuid(),
                Status = "Pending Approval",
                RequestedById = requesterId,
                CreatedById = creatorId,
                DeliveryWarehouseId = Guid.NewGuid(),
                Currency = "GHS",
                TotalAmount = 100m
            };

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.Username)
                .Returns("actor@tdc.test");
            current.SetupGet(item => item.FullName).Returns("TDC Actor");
            current.SetupGet(item => item.Roles)
                .Returns(["TDC_HEAD_OF_PROCUREMENT"]);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementAccessCapabilityRequest, string,
                    CancellationToken>((request, _, _) =>
                    AccessRequests.Add(request))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });

            var sod = new Mock<IProcurementSodGuardService>();
            sod.Setup(item => item.CheckAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    ProcurementSodGuardRequest request,
                    string _,
                    CancellationToken _) => Decision(request));
            sod.Setup(item => item.EnforceAsync(
                    It.IsAny<ProcurementSodGuardRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementSodGuardRequest, string, CancellationToken>(
                    (request, _, _) => EnforcedRequests.Add(request))
                .ReturnsAsync((
                    ProcurementSodGuardRequest request,
                    string _,
                    CancellationToken _) => Decision(request));

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

            Service = new ProcurementPurchaseOrderSodService(
                _unitOfWork,
                current.Object,
                access.Object,
                sod.Object,
                controlEvents.Object,
                notifications.Object,
                NullLogger<ProcurementPurchaseOrderSodService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public PurchaseOrder PurchaseOrder { get; }
        public ProcurementPurchaseOrderSodService Service { get; }
        public List<ProcurementSodGuardRequest> EnforcedRequests { get; } = [];
        public List<ProcurementAccessCapabilityRequest> AccessRequests { get; } =
            [];
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } =
            [];
        public List<NotificationTopicEvent> Notifications { get; } = [];

        private ProcurementSodGuardDecisionDto Decision(
            ProcurementSodGuardRequest request)
        {
            var allowed =
                !request.ProhibitedActorUserIds.Contains(UserId);
            return new ProcurementSodGuardDecisionDto
            {
                Allowed = allowed,
                Code = allowed ? "SOD_ALLOWED" : "SOD_CONFLICT",
                Message = allowed
                    ? "The current actor is independent."
                    : "The current actor has a prohibited prior role.",
                ActorUserId = UserId,
                ControlCode = request.ControlCode,
                SourceType = request.SourceType,
                SourceReference = request.SourceReference,
                PolicySetId = Guid.NewGuid(),
                PolicyCode = "TDC-POLICY",
                PolicyVersion = 1,
                RuleId = Guid.NewGuid(),
                RuleCode = request.ControlCode,
                EvaluatedAtUtc = DateTime.UtcNow
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
