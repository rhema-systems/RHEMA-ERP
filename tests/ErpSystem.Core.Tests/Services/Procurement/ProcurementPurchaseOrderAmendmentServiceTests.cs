using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentServiceTests
{
    [Fact]
    public async Task CreatesImmutableQuantityAndCommitmentDiffFromCurrentPo()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request(quantity: 3m);

        var result = await fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            request,
            "corr-create");

        result.Status.Should()
            .Be(ProcurementPurchaseOrderAmendmentStatus.Draft);
        result.BaseRevisionNumber.Should().Be(1);
        result.ProposedRevisionNumber.Should().Be(2);
        result.BeforeTotalAmount.Should().Be(20m);
        result.ProposedTotalAmount.Should().Be(30m);
        result.CommitmentDelta.Should().Be(10m);
        result.Diffs.Should().Contain(item => item.Path == "items");
        fixture.Context.ProcurementPurchaseOrderAmendments
            .Should().ContainSingle(item =>
                item.TenantId == fixture.TenantId &&
                item.PurchaseOrderId == fixture.PurchaseOrder.Id &&
                item.BeforeIntegrityHash.Length == 64 &&
                item.ProposedIntegrityHash.Length == 64 &&
                item.DiffIntegrityHash.Length == 64);
        fixture.ControlEvents.Verify(item => item.RecordAsync(
                It.Is<ProcurementControlEventWriteRequest>(write =>
                    write.RuleCode == "PO-005" &&
                    write.RuleVersion == "TDC-0406" &&
                    write.DecisionKeys.Count == 14),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IdempotentRetryReturnsSameDraftWithoutDuplicateAudit()
    {
        await using var fixture = new Fixture();
        var request = fixture.Request(quantity: 3m);

        var first = await fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            request,
            "corr-first");
        var second = await fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            request,
            "corr-second");

        second.Id.Should().Be(first.Id);
        fixture.Context.ProcurementPurchaseOrderAmendments.Count()
            .Should().Be(1);
        fixture.ControlEvents.Verify(item => item.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CompletesReapprovalCommitmentRedispatchAndSupplierAcknowledgement()
    {
        await using var fixture = new Fixture();
        fixture.IsAdministrator = false;

        var created = await fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            fixture.Request(quantity: 3m),
            "corr-lifecycle-create");
        var submitted = await fixture.Service.SubmitAsync(
            created.Id,
            new ProcurementPurchaseOrderAmendmentLifecycleRequest
            {
                Comment = "Submit the documented quantity revision.",
                RowVersion = created.RowVersion,
                EvidenceReference = "DMS-PO-AMD-SUBMIT"
            },
            "corr-lifecycle-submit");

        submitted.Status.Should().Be(
            ProcurementPurchaseOrderAmendmentStatus.PendingApproval);
        fixture.ActorUserId = fixture.ApproverUserId;
        var applied = await fixture.Service.DecideAsync(
            submitted.Id,
            new DecideProcurementPurchaseOrderAmendmentRequest
            {
                Approved = true,
                Comment =
                    "Independent workflow approval for the quantity revision.",
                RowVersion = submitted.RowVersion,
                EvidenceReference = "DMS-PO-AMD-APPROVAL"
            },
            "corr-lifecycle-approve");

        applied.Status.Should().Be(
            ProcurementPurchaseOrderAmendmentStatus.Applied);
        applied.CommitmentAdjustments.Should().ContainSingle()
            .Which.DeltaAmount.Should().Be(10m);

        var dispatch = await fixture.Service.DispatchAsync(
            applied.Id,
            new DispatchProcurementPurchaseOrderAmendmentRequest
            {
                Channel =
                    ProcurementPurchaseOrderDispatchChannel.SupplierPortal,
                Destination = fixture.Supplier.Id.ToString(),
                DispatchReference = "DISPATCH-POA-0406",
                DocumentReference = "DMS-SIGNED-POA-0406",
                OrganizationSignatureEvidenceReference =
                    "DMS-SIGNATURE-POA-0406",
                DispatchEvidenceReference = "DMS-DISPATCH-POA-0406",
                IdempotencyKey = "dispatch-poa-0406"
            },
            "corr-lifecycle-dispatch");

        fixture.ActorUserId = fixture.ExternalUserId;
        fixture.IsExternal = true;
        var acknowledgement = await fixture.Service.AcknowledgeAsync(
            dispatch.Id,
            new AcknowledgeProcurementPurchaseOrderAmendmentRequest
            {
                Outcome =
                    ProcurementPurchaseOrderAcknowledgementOutcome.Accepted,
                AcknowledgementChannel = "SupplierPortal",
                AcknowledgementReference = "SUP-ACK-POA-0406",
                EvidenceReference = "DMS-SUP-ACK-POA-0406",
                Comments = "Accepted revised purchase order.",
                IdempotencyKey = "supplier-ack-poa-0406"
            },
            "corr-lifecycle-ack",
            external: true);

        acknowledgement.Outcome.Should().Be(
            ProcurementPurchaseOrderAcknowledgementOutcome.Accepted);
        acknowledgement.AcknowledgedByBusinessPartnerId.Should()
            .Be(fixture.Supplier.Id);
        var purchaseOrder = await fixture.Context.PurchaseOrders
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == fixture.PurchaseOrder.Id);
        purchaseOrder.RevisionNumber.Should().Be(2);
        purchaseOrder.TotalAmount.Should().Be(30m);
        purchaseOrder.Status.Should().Be("Acknowledged");
        purchaseOrder.Items.Should().ContainSingle()
            .Which.OrderedQuantity.Should().Be(3m);
        fixture.Budget.CommittedAmount.Should().Be(30m);
        fixture.Budget.RemainingAmount.Should().Be(70m);
        fixture.Commitment.ReservedAmount.Should().Be(30m);
        fixture.Context.ProcurementPurchaseOrderAmendmentDispatches
            .Should().ContainSingle();
        fixture.Context.ProcurementPurchaseOrderAmendmentAcknowledgements
            .Should().ContainSingle();
        fixture.ControlEvents.Verify(item => item.RecordAsync(
                It.Is<ProcurementControlEventWriteRequest>(write =>
                    write.RuleCode == "PO-005" &&
                    write.RuleVersion == "TDC-0406" &&
                    write.DecisionKeys.Count == 14),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        fixture.ControlEvents.Verify(item => item.RecordAsync(
                It.Is<ProcurementControlEventWriteRequest>(write =>
                    write.RuleCode == "PO-006" &&
                    write.RuleVersion == "TDC-0406" &&
                    write.DecisionKeys.Count == 14),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        fixture.Sod.Verify(item => item.EnforceApprovalAsync(
                It.Is<PurchaseOrder>(order =>
                    order.Id == fixture.PurchaseOrder.Id),
                "corr-lifecycle-approve",
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Compliance.Verify(item => item.EnforceAsync(
                It.Is<PurchaseOrder>(order =>
                    order.Id == fixture.PurchaseOrder.Id &&
                    order.RevisionNumber == 2),
                "Approve",
                "corr-lifecycle-approve",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FrameworkCallOffAndReceiptActivityRemainHardStops()
    {
        await using var fixture = new Fixture();
        fixture.FrameworkCallOff = true;

        var frameworkAction = () => fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            fixture.Request(quantity: 3m),
            "corr-framework");

        await frameworkAction.Should()
            .ThrowAsync<ProcurementPurchaseOrderAmendmentConflictException>()
            .Where(exception =>
                exception.Code == "PO_AMENDMENT_FRAMEWORK_IMMUTABLE");

        fixture.FrameworkCallOff = false;
        fixture.Context.Add(new PurchaseOrderReceipt
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            PurchaseOrderId = fixture.PurchaseOrder.Id,
            ReceiptNumber = "GRN-0406",
            ReceiptDate = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var receiptAction = () => fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            fixture.Request(
                quantity: 4m,
                idempotencyKey: "amendment-with-receipt"),
            "corr-receipt");

        await receiptAction.Should()
            .ThrowAsync<ProcurementPurchaseOrderAmendmentConflictException>()
            .Where(exception =>
                exception.Code == "PO_AMENDMENT_NOT_ALLOWED");
    }

    [Fact]
    public async Task InternalReadIsTenantSafeAndExternalUserCannotUseIt()
    {
        await using var fixture = new Fixture();
        var otherTenantPurchaseOrderId = Guid.NewGuid();
        fixture.Context.Add(new PurchaseOrder
        {
            Id = otherTenantPurchaseOrderId,
            TenantId = Guid.NewGuid(),
            OrderNumber = "PO-OTHER",
            BusinessPartnerId = Guid.NewGuid(),
            Status = "Approved"
        });
        await fixture.Context.SaveChangesAsync();

        var tenantAction = () =>
            fixture.Service.GetOverviewAsync(otherTenantPurchaseOrderId);
        await tenantAction.Should()
            .ThrowAsync<ProcurementPurchaseOrderAmendmentNotFoundException>()
            .Where(exception =>
                exception.Code ==
                "PO_AMENDMENT_PURCHASE_ORDER_NOT_FOUND");

        fixture.IsExternal = true;
        var externalAction = () =>
            fixture.Service.GetOverviewAsync(fixture.PurchaseOrder.Id);
        await externalAction.Should()
            .ThrowAsync<ProcurementPurchaseOrderAmendmentAuthorizationException>();
    }

    [Fact]
    public async Task InternalOverviewUsesRegisteredProcurementReadPermission()
    {
        await using var fixture = new Fixture();
        fixture.IsAdministrator = false;

        var result = await fixture.Service.GetOverviewAsync(
            fixture.PurchaseOrder.Id);

        result.PurchaseOrderId.Should().Be(fixture.PurchaseOrder.Id);
        fixture.Access.Verify(item => item.EnforceCapabilityAsync(
                It.Is<ProcurementAccessCapabilityRequest>(request =>
                    request.PermissionCode == "procurement.records.read" &&
                    ProcurementAccessControlRegistry.FindPermission(
                        request.PermissionCode) != null),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StaleSubmissionIsRejectedWithoutWorkflowOrPoMutation()
    {
        await using var fixture = new Fixture();
        var created = await fixture.Service.CreateAsync(
            fixture.PurchaseOrder.Id,
            fixture.Request(quantity: 3m),
            "corr-stale-create");
        var amendment =
            await fixture.Context.ProcurementPurchaseOrderAmendments
                .SingleAsync(item => item.Id == created.Id);
        amendment.RowVersion = [1, 2, 3, 4];
        await fixture.Context.SaveChangesAsync();

        var action = () => fixture.Service.SubmitAsync(
            created.Id,
            new ProcurementPurchaseOrderAmendmentLifecycleRequest
            {
                Comment = "Attempt with a stale revision.",
                RowVersion = Convert.ToBase64String([9, 9, 9, 9]),
                EvidenceReference = "DMS-PO-AMD-STALE"
            },
            "corr-stale-submit");

        await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderAmendmentConflictException>()
            .Where(exception =>
                exception.Code == "PO_AMENDMENT_CONCURRENCY_CONFLICT");
        fixture.Workflow.Verify(item => item.SubmitAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>()),
            Times.Never);
        (await fixture.Context.PurchaseOrders
                .SingleAsync(item => item.Id == fixture.PurchaseOrder.Id))
            .Status.Should().Be("Approved");
        (await fixture.Context.ProcurementPurchaseOrderAmendments
                .SingleAsync(item => item.Id == created.Id))
            .Status.Should().Be(
                ProcurementPurchaseOrderAmendmentStatus.Draft);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly Mock<IProcurementPurchaseOrderSourceService> _sources =
            new();
        private readonly Mock<IProcurementFrameworkCallOffService> _framework =
            new();
        private bool _frameworkCallOff;
        private bool _isExternal;
        private bool _isAdministrator = true;
        private Guid _actorUserId;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            ApproverUserId = Guid.NewGuid();
            ExternalUserId = Guid.NewGuid();
            _actorUserId = UserId;
            Supplier = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "SUP-0406",
                PartnerName = "Controlled Supplier",
                PartnerType = "Supplier",
                IsActive = true,
                IsBlacklisted = false,
                ApprovalStatus =
                    BusinessPartnerLifecyclePolicy
                        .ApprovedApprovalStatus,
                RegistrationStatus =
                    BusinessPartnerLifecyclePolicy
                        .ActiveRegistrationStatus
            };
            var requisitionId = Guid.NewGuid();
            var sourceId = Guid.NewGuid();
            var releaseId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var readinessId = Guid.NewGuid();
            var inventoryItemId = Guid.NewGuid();
            PurchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrderNumber = "PO-0406-001",
                BusinessPartnerId = Supplier.Id,
                BusinessPartner = Supplier,
                Status = "Approved",
                RevisionNumber = 1,
                RequiredDate = DateTime.UtcNow.Date.AddDays(5),
                PromisedDate = DateTime.UtcNow.Date.AddDays(7),
                SourceRequisitionId = requisitionId,
                ProcurementSourceType =
                    ProcurementPurchaseOrderSourceType.RfqAward,
                ProcurementSourceId = sourceId,
                ProcurementSourceReference = "RFQ-0406",
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                AwardReadinessDecisionId = readinessId,
                SourceSnapshotJson = "{}",
                SourceIntegrityHash = new string('a', 64),
                SourceValidatedAtUtc = DateTime.UtcNow,
                Currency = "GHS",
                SubTotal = 20m,
                TotalAmount = 20m
            };
            PurchaseOrder.Items.Add(new PurchaseOrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseOrderId = PurchaseOrder.Id,
                InventoryItemId = inventoryItemId,
                ItemDescription = "Controlled item",
                OrderedQuantity = 2m,
                RemainingQuantity = 2m,
                UnitOfMeasure = "EA",
                UnitPrice = 10m,
                LineTotal = 20m
            });

            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(
                            InMemoryEventId
                                .TransactionIgnoredWarning))
                    .Options;
            Context = new ApplicationDbContext(options);
            Budget = new ProcurementBudget
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BudgetCode = "BUD-0406",
                Title = "PO amendment budget",
                DepartmentId = Guid.NewGuid(),
                FiscalYear = DateTime.UtcNow.Year,
                AllocatedAmount = 100m,
                UtilizedAmount = 0m,
                CommittedAmount = 20m,
                RemainingAmount = 80m,
                Currency = "GHS",
                Status = "Active"
            };
            Commitment = new ProcurementBudgetCommitment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ProcurementBudgetId = Budget.Id,
                PurchaseRequisitionId = requisitionId,
                ReservationReference = "COMMIT-0406",
                ReservationSequence = 1,
                Status = ProcurementBudgetCommitmentStatus.Reserved,
                ReservedAmount = 20m,
                Currency = "GHS",
                BudgetAllocatedSnapshot = 100m,
                BudgetCommittedAfter = 20m,
                BudgetAvailableAfter = 80m,
                ReservedAtUtc = DateTime.UtcNow,
                ReservedById = UserId,
                ReservedByName = "TDC Administrator",
                CorrelationId = "commitment-fixture"
            };
            WorkflowInstanceId = Guid.NewGuid();
            WorkflowDefinitionId = Guid.NewGuid();
            Context.AddRange(
                Supplier,
                PurchaseOrder,
                Budget,
                Commitment,
                new BusinessPartnerUser
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    BusinessPartnerId = Supplier.Id,
                    BusinessPartner = Supplier,
                    UserId = ExternalUserId,
                    Role = "Viewer",
                    IsActive = true,
                    GrantedAt = DateTime.UtcNow
                },
                new WorkflowInstance
                {
                    Id = WorkflowInstanceId,
                    TenantId = TenantId,
                    WorkflowDefinitionId = WorkflowDefinitionId,
                    EntityId = PurchaseOrder.Id,
                    EntityTypeId = Guid.NewGuid(),
                    Status = WorkflowInstanceStatus.InProgress,
                    InitiatedById = UserId
                });
            Context.SaveChanges();
            var unitOfWork = new UnitOfWork(Context);

            _currentUser.SetupGet(item => item.TenantId)
                .Returns(() => TenantId);
            _currentUser.SetupGet(item => item.UserId)
                .Returns(() => _actorUserId);
            _currentUser.SetupGet(item => item.Username)
                .Returns("admin@tdc.test");
            _currentUser.SetupGet(item => item.FullName)
                .Returns("TDC Administrator");
            _currentUser.SetupGet(item => item.IsAuthenticated)
                .Returns(true);
            _currentUser.SetupGet(item => item.IsExternalUser)
                .Returns(() => _isExternal);
            _currentUser.Setup(item => item.HasRole(
                    It.IsAny<string>()))
                .Returns((string role) =>
                    _isAdministrator && role == "Administrator");

            Source = new ProcurementPurchaseOrderSourceResolution
            {
                SourceType =
                    ProcurementPurchaseOrderSourceType.RfqAward,
                SourceId = sourceId,
                SourceReference = "RFQ-0406",
                PurchaseRequisitionId = requisitionId,
                PurchaseRequisitionNumber = "PR-0406",
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                AwardReadinessDecisionId = readinessId,
                BusinessPartnerId = Supplier.Id,
                CurrencyCode = "GHS",
                SourceSnapshotJson = "{}",
                SourceIntegrityHash = new string('a', 64),
                ValidatedAtUtc = DateTime.UtcNow
            };
            _sources.Setup(item => item.RevalidateAsync(
                    It.IsAny<PurchaseOrder>(),
                    "Amend",
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Source);
            _sources.Setup(item => item.ValidateOrderAsync(
                    It.IsAny<ProcurementPurchaseOrderSourceResolution>(),
                    It.IsAny<IReadOnlyCollection<
                        ProcurementPurchaseOrderSourceOrderLine>>(),
                    It.IsAny<decimal>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _framework.Setup(item =>
                    item.IsFrameworkCallOffPurchaseOrderAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _frameworkCallOff);
            Workflow.Setup(item => item.SubmitAsync(
                    "PurchaseOrder",
                    PurchaseOrder.Id))
                .ReturnsAsync(new WorkflowIntegrationResult(
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = WorkflowInstanceStatus.InProgress,
                        WorkflowInstanceId = WorkflowInstanceId
                    },
                    WorkflowOutcome.Pending));
            Workflow.Setup(item => item.CanUserApproveAsync(
                    "PurchaseOrder",
                    PurchaseOrder.Id,
                    It.IsAny<Guid>()))
                .ReturnsAsync(true);
            Workflow.Setup(item => item.ProcessApprovalAsync(
                    "PurchaseOrder",
                    PurchaseOrder.Id,
                    It.IsAny<Guid>(),
                    "approve",
                    It.IsAny<string?>()))
                .ReturnsAsync(new WorkflowIntegrationResult(
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = WorkflowInstanceStatus.Completed,
                        WorkflowInstanceId = WorkflowInstanceId
                    },
                    WorkflowOutcome.Approved));
            Sod.Setup(item => item.EnforceApprovalAsync(
                    It.IsAny<PurchaseOrder>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementPurchaseOrderSodReadinessDto
                {
                    CanApprove = true,
                    Code = "PO_SOD_ALLOWED"
                });
            Compliance.Setup(item => item.EnforceAsync(
                    It.IsAny<PurchaseOrder>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementPurchaseOrderComplianceDto
                {
                    IsCompliant = true,
                    Code = "PO_COMPLIANT"
                });
            BudgetStore.SetupGet(item => item.HasRequiredTransaction)
                .Returns(true);
            BudgetStore.Setup(item => item.GetBudgetForUpdateAsync(
                    TenantId,
                    Budget.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Budget);
            ControlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementControlEventDto());
            Access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ACCESS_ALLOWED",
                    Message = "Allowed"
                });
            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            Service = new ProcurementPurchaseOrderAmendmentService(
                unitOfWork,
                _currentUser.Object,
                Access.Object,
                _sources.Object,
                Sod.Object,
                Compliance.Object,
                _framework.Object,
                Workflow.Object,
                BudgetStore.Object,
                new ProcurementPurchaseOrderAmendmentStore(Context),
                ControlEvents.Object,
                notifications.Object,
                NullLogger<
                    ProcurementPurchaseOrderAmendmentService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public Guid ApproverUserId { get; }
        public Guid ExternalUserId { get; }
        public Guid WorkflowInstanceId { get; }
        public Guid WorkflowDefinitionId { get; }
        public BusinessPartner Supplier { get; }
        public PurchaseOrder PurchaseOrder { get; }
        public ProcurementBudget Budget { get; }
        public ProcurementBudgetCommitment Commitment { get; }
        public ProcurementPurchaseOrderSourceResolution Source { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementPurchaseOrderAmendmentService Service { get; }
        public Mock<IProcurementAccessControlService> Access { get; } =
            new();
        public Mock<IProcurementPurchaseOrderSodService> Sod { get; } =
            new();
        public Mock<IProcurementPurchaseOrderComplianceService> Compliance
            { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } =
            new();
        public Mock<IProcurementBudgetReservationStore> BudgetStore
            { get; } = new();
        public Mock<IProcurementControlEventService> ControlEvents { get; } =
            new();

        public bool FrameworkCallOff
        {
            set => _frameworkCallOff = value;
        }

        public bool IsExternal
        {
            set => _isExternal = value;
        }

        public Guid ActorUserId
        {
            set => _actorUserId = value;
        }

        public bool IsAdministrator
        {
            set => _isAdministrator = value;
        }

        public CreateProcurementPurchaseOrderAmendmentRequest Request(
            decimal quantity,
            string idempotencyKey = "po-amendment-create-0406") =>
            new()
            {
                Reason =
                    "Approved delivery demand requires a documented quantity change.",
                ChangeScope = "Quantity",
                RequiredDate = PurchaseOrder.RequiredDate,
                PromisedDate = PurchaseOrder.PromisedDate,
                TaxAmount = PurchaseOrder.TaxAmount,
                ShippingCost = PurchaseOrder.ShippingCost,
                MiscellaneousCost =
                    PurchaseOrder.MiscellaneousCost,
                DiscountAmount = PurchaseOrder.DiscountAmount,
                EvidenceReference = "DMS-PO-AMD-0406",
                IdempotencyKey = idempotencyKey,
                Items =
                [
                    new ProcurementPurchaseOrderAmendmentItemRequest
                    {
                        PurchaseOrderItemId =
                            PurchaseOrder.Items.Single().Id,
                        InventoryItemId =
                            PurchaseOrder.Items.Single()
                                .InventoryItemId!.Value,
                        ItemDescription = "Controlled item",
                        OrderedQuantity = quantity,
                        UnitOfMeasure = "EA",
                        UnitPrice = 10m
                    }
                ]
            };

        public async ValueTask DisposeAsync() =>
            await Context.DisposeAsync();
    }
}
