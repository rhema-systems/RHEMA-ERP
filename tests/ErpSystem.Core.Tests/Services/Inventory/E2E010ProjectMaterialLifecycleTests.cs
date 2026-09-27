using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using ErpSystem.Data.Repositories.Projects;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class E2E010ProjectMaterialLifecycleTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _requesterId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();
    private readonly Guid _issuerId = Guid.NewGuid();
    private readonly Guid _receiverId = Guid.NewGuid();
    private readonly Guid _returnApproverId = Guid.NewGuid();
    private readonly Guid _projectManagerId = Guid.NewGuid();
    private readonly Guid _departmentId = Guid.NewGuid();
    private readonly Guid _organizationUnitId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _requisitionId = Guid.NewGuid();
    private readonly Guid _requisitionLineId = Guid.NewGuid();
    private readonly byte[] _requisitionRowVersion = [1, 2, 3, 4];
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly MutableCurrentUser _currentUser;
    private readonly List<CreateNotificationDto> _notifications = [];
    private readonly InventoryProjectReservationService _reservations;
    private readonly InventoryRequisitionService _requisitions;
    private readonly InventoryReturnControlService _returns;
    private readonly Mock<IInventoryIssueFinanceAssetService> _issueFinanceAssets = new();

    public E2E010ProjectMaterialLifecycleTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"e2e-010-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
        _unitOfWork = new UnitOfWork(_context);
        _currentUser = new MutableCurrentUser(_tenantId, _issuerId, "stores.issuer");

        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ALLOWED",
                    Message = "Representative E2E actor is in scope.",
                    ActorUserId = _currentUser.UserId,
                    TenantId = _tenantId,
                    PermissionCode = request.PermissionCode,
                    WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId,
                    CorrelationId = correlationId,
                    EvaluatedAtUtc = DateTime.UtcNow
                });
        access.Setup(service => service.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlationId, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Code = "ALLOWED",
                    Message = "Representative E2E actor is in scope.",
                    ActorUserId = _currentUser.UserId,
                    TenantId = _tenantId,
                    PermissionCode = request.PermissionCode,
                    WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId,
                    CorrelationId = correlationId,
                    EvaluatedAtUtc = DateTime.UtcNow
                });

        var controlEvents = new Mock<IProcurementControlEventService>();
        controlEvents.Setup(service => service.RecordAsync(
                It.IsAny<ProcurementControlEventWriteRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementControlEventWriteRequest request, CancellationToken _) =>
                new ProcurementControlEventDto
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantId,
                    EventKey = request.EventKey,
                    EventType = request.EventType,
                    Action = request.Action,
                    Result = request.Result,
                    SourceType = request.SourceType,
                    SourceId = request.SourceId,
                    SourceReference = request.SourceReference,
                    CorrelationId = request.CorrelationId,
                    OccurredAtUtc = request.OccurredAtUtc,
                    RecordedAtUtc = DateTime.UtcNow,
                    IntegrityValid = true
                });

        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.CreateNotificationAsync(
                It.IsAny<CreateNotificationDto>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((CreateNotificationDto request, Guid _, Guid _) =>
            {
                _notifications.Add(request);
                return new NotificationDto
                {
                    Id = Guid.NewGuid(),
                    Type = request.Type,
                    Title = request.Title,
                    Message = request.Message,
                    ActionUrl = request.ActionUrl,
                    EntityType = request.EntityType,
                    EntityId = request.EntityId,
                    Metadata = request.Metadata,
                    Timestamp = DateTime.UtcNow
                };
            });

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            null!,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!);
        userManager.SetupGet(manager => manager.Users).Returns(_context.Users);

        _reservations = new InventoryProjectReservationService(
            _unitOfWork,
            _currentUser,
            access.Object,
            controlEvents.Object,
            notifications.Object,
            userManager.Object,
            NullLogger<InventoryProjectReservationService>.Instance);

        var requisitionRepository = new InventoryRequisitionRepository(_context);
        var requisitionItemRepository = new InventoryRequisitionItemRepository(_context);
        var itemRepository = new InventoryItemRepository(_context);
        var warehouseRepository = new WarehouseRepository(_context);
        var locationRepository = new WarehouseLocationRepository(_context);
        var warehouseQuantityRepository = new WarehouseQuantityRepository(_context);
        var movementRepository = new StockMovementRepository(_context);
        var projectRepository = new ProjectRepository(_context, _currentUser);

        var tenantSettings = new Mock<ITenantSettingsService>();
        tenantSettings.Setup(service => service.GetBaseCurrencyAsync()).ReturnsAsync("GHS");
        var projectService = new ProjectService(
            projectRepository,
            Mock.Of<IProjectManagementSettingsRepository>(),
            Mock.Of<IProjectTemplateRepository>(),
            Mock.Of<IProjectTypeRepository>(),
            Mock.Of<IProjectPortfolioRepository>(),
            Mock.Of<IProjectProgramRepository>(),
            Mock.Of<IContractService>(),
            Mock.Of<IBusinessPartnerService>(),
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<IUserService>(),
            Mock.Of<IJobCardService>(),
            Mock.Of<IWorkOrderService>(),
            Mock.Of<ISalesAgreementService>(),
            Mock.Of<ISalesOrderService>(),
            Mock.Of<IEstateManagedAssetService>(),
            _unitOfWork,
            tenantSettings.Object,
            _currentUser,
            Mock.Of<IAppEventBus>(),
            NullLogger<ProjectService>.Instance);

        var tracking = new Mock<IInventoryTrackingControlService>();
        tracking.Setup(service => service.StageEventAsync(
                It.IsAny<InventoryTrackingMutationRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var negativeStock = new Mock<IInventoryNegativeStockControlService>();
        negativeStock.Setup(service => service.PrepareDecreaseAsync(
                It.IsAny<InventoryStockDecreaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryStockDecreaseAuthorization());
        negativeStock.Setup(service => service.ClearMutationContextAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var consignment = new Mock<IConsignmentSettlementService>();
        consignment.Setup(service => service.TryCreateFromStockMovementAsync(It.IsAny<StockMovement>()))
            .Returns(Task.CompletedTask);

        var workflow = new Mock<IWorkflowIntegrationService>();
        workflow.Setup(service => service.SubmitAsync("InventoryReturnVoucher", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Pending));
        workflow.Setup(service => service.CanUserApproveAsync(
                "InventoryReturnVoucher", It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow.Setup(service => service.ProcessApprovalAsync(
                "InventoryReturnVoucher",
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                "Approve",
                It.IsAny<string?>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.Completed,
                    WorkflowInstanceId = Guid.NewGuid()
                },
                WorkflowOutcome.Approved));

        var sod = new Mock<IProcurementSodGuardService>();
        sod.Setup(service => service.EnforceAsync(
                It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto
            {
                Allowed = true,
                Code = "ALLOWED",
                Message = "Independent representative return approver.",
                ActorUserId = _returnApproverId,
                ControlCode = "SOD-INITIATOR-APPROVER",
                EvaluatedAtUtc = DateTime.UtcNow,
                WasAudited = true
            });
        var issueFinanceAssets = _issueFinanceAssets.Object;
        // This fixture does not host Finance. Supply its posted issue-line evidence,
        // using actual voucher values; never manufacture a return cost from the request estimate.
        _issueFinanceAssets.Setup(value => value.PostIssueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid voucherId, CancellationToken token) =>
            {
                var lines = await _context.Set<InventoryIssueVoucherLine>()
                    .Where(value => value.InventoryIssueVoucherId == voucherId).ToListAsync(token);
                foreach (var line in lines)
                    _context.Add(new InventoryIssueFinanceLineage
                    {
                        TenantId = _tenantId, InventoryIssueVoucherLineId = line.Id,
                        IssuedQuantity = line.Quantity, IssuedValue = line.TotalValue,
                        MovementReasonCode = "PROJECT_CONSUMPTION"
                    });
                await _context.SaveChangesAsync(token);
            });
        var valuation = new InventoryValuationService(_unitOfWork, NullLogger<InventoryValuationService>.Instance,
            _currentUser, Mock.Of<IProcurementReceiptSourceControlService>());

        _returns = new InventoryReturnControlService(
            _unitOfWork,
            requisitionRepository,
            requisitionItemRepository,
            itemRepository,
            warehouseRepository,
            locationRepository,
            warehouseQuantityRepository,
            movementRepository,
            consignment.Object,
            tracking.Object,
            access.Object,
            sod.Object,
            workflow.Object,
            controlEvents.Object,
            projectService,
            issueFinanceAssets,
            valuation,
            _currentUser);

        _requisitions = new InventoryRequisitionService(
            requisitionRepository,
            requisitionItemRepository,
            itemRepository,
            warehouseRepository,
            locationRepository,
            warehouseQuantityRepository,
            movementRepository,
            consignment.Object,
            projectRepository,
            projectService,
            _unitOfWork,
            _currentUser,
            workflow.Object,
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            tracking.Object,
            negativeStock.Object,
            _reservations,
            access.Object,
            controlEvents.Object,
            _returns,
            issueFinanceAssets,
            valuation,
            NullLogger<InventoryRequisitionService>.Instance);
    }

    public async Task InitializeAsync()
    {
        var users = new[]
        {
            User(_requesterId, "project.requester"),
            User(_approverId, "stores.approver"),
            User(_issuerId, "stores.issuer"),
            User(_receiverId, "site.receiver"),
            User(_returnApproverId, "stores.return-approver"),
            User(_projectManagerId, "project.manager")
        };
        var category = new InventoryCategory
        {
            TenantId = _tenantId,
            Code = "MAT",
            Name = "Project materials",
            IsActive = true
        };
        var warehouse = new Warehouse
        {
            Id = _warehouseId,
            TenantId = _tenantId,
            Code = "MAIN",
            Name = "Main Stores",
            IsActive = true
        };
        var location = new WarehouseLocation
        {
            Id = _locationId,
            TenantId = _tenantId,
            WarehouseId = _warehouseId,
            LocationCode = "PROJECT-A",
            Name = "Project picking bin",
            IsActive = true,
            IsPickingLocation = true,
            IsReturnLocation = true
        };
        var item = new InventoryItem
        {
            Id = _itemId,
            TenantId = _tenantId,
            ItemCode = "MAT-001",
            Name = "Representative project material",
            CategoryId = category.Id,
            UnitOfMeasure = "EA",
            Status = ItemStatus.Active,
            AverageCost = 10m,
            ValuationMethod = ValuationMethod.WeightedAverage,
            CurrentStock = 20m,
            AvailableStock = 20m,
            IsProjectApplicable = true
        };
        var project = new Project
        {
            Id = _projectId,
            TenantId = _tenantId,
            ProjectCode = "PRJ-E2E-010",
            Title = "Representative inventory lifecycle",
            Status = ProjectStatuses.InProgress,
            DepartmentId = _departmentId,
            ProjectManagerId = _projectManagerId,
            BaseCurrencyCode = "GHS"
        };
        var requisition = new InventoryRequisition
        {
            Id = _requisitionId,
            TenantId = _tenantId,
            RequisitionNumber = "REQ-E2E-010",
            OrganizationUnitId = _organizationUnitId,
            DepartmentName = "Projects",
            WarehouseId = _warehouseId,
            LocationId = _locationId,
            ProjectId = _projectId,
            ProjectCode = project.ProjectCode,
            RequestDate = DateTime.UtcNow,
            RequiredDate = DateTime.UtcNow.AddDays(2),
            ApprovalDate = DateTime.UtcNow,
            Status = RequisitionStatus.Approved,
            RequisitionType = RequisitionType.ProjectRequisition,
            RequestedById = _requesterId,
            ApprovedById = _approverId,
            TotalItems = 1,
            TotalQuantity = 10m,
            TotalValue = 100m,
            RowVersion = _requisitionRowVersion
        };
        var line = new InventoryRequisitionItem
        {
            Id = _requisitionLineId,
            TenantId = _tenantId,
            InventoryRequisitionId = _requisitionId,
            InventoryItemId = _itemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            RequestedQuantity = 10m,
            ApprovedQuantity = 10m,
            IssuedQuantity = 0m,
            UnitCost = 10m,
            LineValue = 0m,
            UnitOfMeasure = "EA",
            LocationId = _locationId
        };

        await _context.AddRangeAsync(users.Cast<object>().Concat(new object[]
        {
            category,
            new OrganizationUnit
            {
                Id = _organizationUnitId, TenantId = _tenantId,
                Name = "Projects", Code = "PROJECTS", AccountCode = "PROJECTS",
                IsActive = true,
                OrganizationLevel = new OrganizationLevel
                {
                    TenantId = _tenantId, Name = "Department", Code = "DEPT", LevelNumber = 1,
                    OrganizationStructure = new OrganizationStructure
                    {
                        TenantId = _tenantId, Name = "E2E organization", Code = "E2E-010"
                    }
                }
            },
            warehouse,
            location,
            item,
            new InventoryBalance
            {
                TenantId = _tenantId, InventoryItemId = _itemId, WarehouseId = _warehouseId,
                LocationId = _locationId, QuantityOnHand = 20m, QuantityAvailable = 20m,
                TotalValue = 200m, AverageUnitCost = 10m
            },
            new WarehouseQuantity
            {
                TenantId = _tenantId,
                WarehouseId = _warehouseId,
                InventoryItemId = _itemId,
                CurrentStock = 20m,
                AvailableStock = 20m,
                AverageCost = 10m
            },
            new InventoryLocation
            {
                TenantId = _tenantId,
                LocationId = _locationId,
                InventoryItemId = _itemId,
                Quantity = 20m,
                AvailableQuantity = 20m,
                AverageCost = 10m
            },
            project,
            requisition,
            line,
            new UserTenant
            {
                TenantId = _tenantId,
                UserId = _receiverId,
                Status = UserTenantStatus.Active,
                IsDefault = true
            }
        }));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Reservation_substitution_refreshes_item_unit_and_requisition_value_atomically()
    {
        var replacementId = Guid.NewGuid();
        var original = await _context.Set<InventoryItem>().AsNoTracking().SingleAsync(value => value.Id == _itemId);
        await _context.AddRangeAsync(
            new InventoryItem
            {
                Id = replacementId, TenantId = _tenantId, CategoryId = original.CategoryId,
                ItemCode = "MAT-REPLACEMENT", Name = "Replacement project material", UnitOfMeasure = "BOX",
                Status = ItemStatus.Active, AverageCost = 15m, CurrentStock = 20m, AvailableStock = 20m,
                IsProjectApplicable = true
            },
            new WarehouseQuantity
            {
                TenantId = _tenantId, WarehouseId = _warehouseId, InventoryItemId = replacementId,
                CurrentStock = 20m, AvailableStock = 20m, AverageCost = 15m
            },
            new InventoryLocation
            {
                TenantId = _tenantId, LocationId = _locationId, InventoryItemId = replacementId,
                Quantity = 20m, AvailableQuantity = 20m, AverageCost = 15m
            });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var reservation = await _reservations.ReserveAsync(new CreateInventoryProjectReservationRequest
        {
            InventoryRequisitionItemId = _requisitionLineId,
            LocationId = _locationId,
            Quantity = 5m,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            IdempotencyKey = "substitution-reserve",
            CorrelationId = "substitution-test"
        });
        _context.ChangeTracker.Clear();
        var reservedEntity = await _context.Set<InventoryAllocation>().SingleAsync(value => value.Id == reservation.Id);
        reservedEntity.RowVersion = [51, 52, 53, 54];
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _reservations.SubstituteAsync(reservation.Id, new SubstituteInventoryProjectReservationRequest
        {
            ReplacementInventoryItemId = replacementId,
            Reason = "Approved material equivalent.",
            IdempotencyKey = "substitution-replace",
            CorrelationId = "substitution-test",
            RowVersion = Convert.ToBase64String([51, 52, 53, 54])
        });

        _context.ChangeTracker.Clear();
        var requisition = await _context.Set<InventoryRequisition>().Include(value => value.Items)
            .SingleAsync(value => value.Id == _requisitionId);
        requisition.TotalValue.Should().Be(150m);
        requisition.Items.Single().Should().Match<InventoryRequisitionItem>(value =>
            value.InventoryItemId == replacementId && value.ItemCode == "MAT-REPLACEMENT" &&
            value.ItemName == "Replacement project material" && value.UnitOfMeasure == "BOX" &&
            value.UnitCost == 15m);
    }

    [Fact]
    [Trait("Batch", "E2E-010")]
    public async Task Approved_project_reservation_partial_issue_and_return_reconcile_notifications_stock_and_cost()
    {
        var reservation = await _reservations.ReserveAsync(new CreateInventoryProjectReservationRequest
        {
            InventoryRequisitionItemId = _requisitionLineId,
            LocationId = _locationId,
            Quantity = 8m,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(3),
            IdempotencyKey = "e2e-010-reserve",
            CorrelationId = "e2e-010",
            Notes = "Representative project demand reservation."
        });

        reservation.Status.Should().Be(InventoryProjectReservationStatus.Reserved);
        reservation.OrganizationUnitId.Should().Be(_organizationUnitId);
        reservation.ReservedQuantity.Should().Be(8m);
        reservation.RemainingQuantity.Should().Be(8m);
        (await WarehouseStock()).AllocatedStock.Should().Be(8m);
        _notifications.Should().Contain(notification =>
            notification.RecipientId == _requesterId &&
            notification.Type == "InventoryProjectReservationReserved");

        await _requisitions.IssueAsync(_requisitionId, new IssueRequisitionDto
        {
            IdempotencyKey = "e2e-010-issue",
            CorrelationId = "e2e-010",
            RowVersion = Convert.ToBase64String(_requisitionRowVersion),
            ReceiverUserId = _receiverId,
            MovementReasonCode = InventoryIssueMovementReasons.ProjectConsumption,
            Notes = "Partial issue for representative project demand.",
            Items =
            [
                new IssueRequisitionItemDto
                {
                    ItemId = _requisitionLineId,
                    IssuedQuantity = 5m,
                    LocationId = _locationId
                }
            ]
        });

        _context.ChangeTracker.Clear();
        var partialReservation = await _reservations.GetByIdAsync(reservation.Id);
        partialReservation.Status.Should().Be(InventoryProjectReservationStatus.PartiallyFulfilled);
        partialReservation.FulfilledQuantity.Should().Be(5m);
        partialReservation.RemainingQuantity.Should().Be(3m);
        var issuedRequisition = await _context.Set<InventoryRequisition>()
            .Include(value => value.Items)
            .SingleAsync(value => value.Id == _requisitionId);
        issuedRequisition.Status.Should().Be(RequisitionStatus.PartiallyIssued);
        issuedRequisition.Items.Single().IssuedQuantity.Should().Be(5m);
        (await WarehouseStock()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 15m && value.AllocatedStock == 3m && value.AvailableStock == 12m);
        _notifications.Should().Contain(notification =>
            notification.RecipientId == _requesterId &&
            notification.Type == "InventoryProjectReservationPartiallyFulfilled");
        await AssertProjectCostAsync(expectedIssue: 50m, expectedReturn: 0m, expectedActual: 50m);

        _currentUser.Switch(_issuerId, "stores.issuer");
        var requestedReturn = await _returns.RequestAsync(_requisitionId, new ReturnRequisitionDto
        {
            IdempotencyKey = "e2e-010-return-request",
            CorrelationId = "e2e-010",
            RowVersion = Convert.ToBase64String(_requisitionRowVersion),
            ReasonCode = InventoryReturnReasonCodes.Unused,
            Reason = "Two unused units returned from the project site.",
            Items =
            [
                new ReturnRequisitionItemDto
                {
                    ItemId = _requisitionLineId,
                    ReturnedQuantity = 2m,
                    LocationId = _locationId
                }
            ]
        });
        requestedReturn.Status.Should().Be(InventoryReturnVoucherStatus.PendingApproval.ToString());

        _currentUser.Switch(_returnApproverId, "stores.return-approver");
        var decisionRequest = new DecideInventoryReturnVoucherRequest
        {
            Approved = true,
            Comment = "Independent approval of unused project stock return.",
            IdempotencyKey = "e2e-010-return-approve",
            RowVersion = requestedReturn.RowVersion
        };
        var approvedReturn = await _returns.DecideAsync(requestedReturn.Id, decisionRequest);
        approvedReturn.Status.Should().Be(InventoryReturnVoucherStatus.Approved.ToString());

        _context.ChangeTracker.Clear();
        var approvedEntity = await _context.Set<InventoryReturnVoucher>().SingleAsync(value => value.Id == requestedReturn.Id);
        approvedEntity.RowVersion = [31, 32, 33, 34];
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var decisionReplay = await _returns.DecideAsync(requestedReturn.Id, decisionRequest);
        decisionReplay.Status.Should().Be(InventoryReturnVoucherStatus.Approved.ToString(),
            "an identical decision retry must be recognized before its stale row version");

        var postRequest = new PostInventoryReturnVoucherRequest
        {
            IdempotencyKey = "e2e-010-return-post",
            RowVersion = decisionReplay.RowVersion
        };
        var postedReturn = await _returns.PostAsync(approvedReturn.Id, postRequest);
        postedReturn.Status.Should().Be(InventoryReturnVoucherStatus.Posted.ToString());

        _context.ChangeTracker.Clear();
        var postedEntity = await _context.Set<InventoryReturnVoucher>().SingleAsync(value => value.Id == requestedReturn.Id);
        postedEntity.RowVersion = [41, 42, 43, 44];
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var postingReplay = await _returns.PostAsync(approvedReturn.Id, postRequest);
        postingReplay.Status.Should().Be(InventoryReturnVoucherStatus.Posted.ToString(),
            "an identical posting retry must be recognized before its stale row version");

        _context.ChangeTracker.Clear();
        var finalLine = await _context.Set<InventoryRequisitionItem>()
            .SingleAsync(value => value.Id == _requisitionLineId);
        finalLine.IssuedQuantity.Should().Be(3m);
        (await WarehouseStock()).Should().Match<WarehouseQuantity>(value =>
            value.CurrentStock == 17m && value.AllocatedStock == 3m && value.AvailableStock == 14m);
        var movements = await _context.Set<StockMovement>()
            .Where(value => value.ReferenceId == _requisitionId)
            .OrderBy(value => value.MovementDate)
            .ToListAsync();
        movements.Should().ContainSingle(value => value.MovementType == "Issue" && value.TotalValue == -50m);
        movements.Should().ContainSingle(value => value.MovementType == "Return" && value.TotalValue == 20m);
        await AssertProjectCostAsync(expectedIssue: 50m, expectedReturn: -20m, expectedActual: 30m);
    }

    [Fact]
    public async Task Issue_uses_actual_fifo_cost_preserves_approval_and_replay_does_not_consume_twice()
    {
        var item = await _context.Set<InventoryItem>().SingleAsync(value => value.Id == _itemId);
        item.ValuationMethod = ValuationMethod.FIFO;
        item.StandardCost = 2000m;
        var source = await _context.Set<InventoryRequisitionItem>().SingleAsync(value => value.Id == _requisitionLineId);
        source.UnitCost = 2000m;
        var balance = await _context.Set<InventoryBalance>().SingleAsync();
        balance.TotalValue = 38000m;
        balance.AverageUnitCost = 1900m;
        _context.Add(new InventoryLayer
        {
            TenantId = _tenantId, InventoryItemId = _itemId, WarehouseId = _warehouseId,
            LocationId = _locationId, LayerNumber = "FIFO-UAT", LayerDate = DateTime.UtcNow.AddDays(-1),
            OriginalQuantity = 20m, RemainingQuantity = 20m, UnitCost = 1900m, RemainingValue = 38000m
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var request = new IssueRequisitionDto
        {
            IdempotencyKey = "actual-fifo-issue", CorrelationId = "actual-fifo-issue",
            RowVersion = Convert.ToBase64String(_requisitionRowVersion), ReceiverUserId = _receiverId,
            MovementReasonCode = InventoryIssueMovementReasons.ProjectConsumption,
            Items = [new() { ItemId = _requisitionLineId, IssuedQuantity = 2m, LocationId = _locationId }]
        };
        await _requisitions.IssueAsync(_requisitionId, request);
        await _requisitions.IssueAsync(_requisitionId, request);
        _context.ChangeTracker.Clear();
        var voucher = await _context.Set<InventoryIssueVoucher>().SingleAsync();
        var line = await _context.Set<InventoryIssueVoucherLine>().SingleAsync();
        line.UnitCost.Should().Be(1900m);
        line.TotalValue.Should().Be(3800m);
        voucher.SourceSnapshotJson.Should().Contain("\"UnitCost\":2000");
        source = await _context.Set<InventoryRequisitionItem>().SingleAsync();
        source.UnitCost.Should().Be(2000m, "the approved demand estimate must not be rewritten");
        source.IssuedQuantity.Should().Be(2m);
        source.LineValue.Should().Be(3800m);
        (await _context.Set<StockMovement>().SingleAsync()).TotalValue.Should().Be(-3800m);
        var movement = await _context.Set<InventoryMovement>().SingleAsync();
        movement.ReferenceId.Should().Be(_requisitionId);
        movement.ReferenceNumber.Should().Be("REQ-E2E-010");
        movement.TotalValue.Should().Be(3800m);
        movement.RunningBalance.Should().Be(18m);
        movement.RunningValue.Should().Be(34200m);
        balance = await _context.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(18m);
        balance.TotalValue.Should().Be(34200m);
        (await _context.Set<InventoryLayer>().SingleAsync()).RemainingQuantity.Should().Be(18m);
        (await WarehouseStock()).CurrentStock.Should().Be(18m);
        _issueFinanceAssets.Verify(value => value.PostIssueAsync(voucher.Id, It.IsAny<CancellationToken>()), Times.Once);
        await AssertProjectCostAsync(3800m, 0m, 3800m);
        var returned = await _returns.RequestAsync(_requisitionId, new ReturnRequisitionDto
        {
            IdempotencyKey = "actual-fifo-return", CorrelationId = "actual-fifo-return",
            RowVersion = Convert.ToBase64String(_requisitionRowVersion),
            ReasonCode = InventoryReturnReasonCodes.Unused, Reason = "Unused original issue returned.",
            Items = [new() { ItemId = _requisitionLineId, ReturnedQuantity = 1, LocationId = _locationId }]
        });
        returned.TotalValue.Should().Be(1900);
        _currentUser.Switch(_returnApproverId, "stores.return-approver");
        var approved = await _returns.DecideAsync(returned.Id, new DecideInventoryReturnVoucherRequest
        {
            Approved = true, Comment = "Independent review", IdempotencyKey = "fifo-return-approved", RowVersion = returned.RowVersion
        });
        var post = new PostInventoryReturnVoucherRequest { IdempotencyKey = "fifo-return-posted", RowVersion = approved.RowVersion };
        await _returns.PostAsync(returned.Id, post);
        await _returns.PostAsync(returned.Id, post);
        _context.ChangeTracker.Clear();
        source = await _context.Set<InventoryRequisitionItem>().SingleAsync();
        source.UnitCost.Should().Be(2000);
        source.LineValue.Should().Be(1900);
        source.IssuedQuantity.Should().Be(1);
        balance = await _context.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(19);
        balance.TotalValue.Should().Be(36100);
        (await _context.Set<InventoryLayer>().SumAsync(value => value.RemainingValue)).Should().Be(36100);
        (await _context.Set<InventoryMovement>().SingleAsync(value => value.MovementType == InventoryMovementType.RequisitionReturn))
            .RunningValue.Should().Be(36100);
        (await WarehouseStock()).CurrentStock.Should().Be(19);
        _issueFinanceAssets.Verify(value => value.PostReturnAsync(returned.Id, It.IsAny<CancellationToken>()), Times.Once);
        await AssertProjectCostAsync(3800, -1900, 1900);
    }

    public Task DisposeAsync()
    {
        _unitOfWork.Dispose();
        return Task.CompletedTask;
    }

    private async Task<WarehouseQuantity> WarehouseStock()
    {
        _context.ChangeTracker.Clear();
        return await _context.Set<WarehouseQuantity>().SingleAsync(value =>
            value.TenantId == _tenantId && value.WarehouseId == _warehouseId && value.InventoryItemId == _itemId);
    }

    private async Task AssertProjectCostAsync(decimal expectedIssue, decimal expectedReturn, decimal expectedActual)
    {
        _context.ChangeTracker.Clear();
        var entries = await _context.Set<ProjectMaterialCostEntry>()
            .Where(value => value.ProjectId == _projectId)
            .ToListAsync();
        entries.Where(value => value.EntryType == "InventoryIssue").Sum(value => value.Amount)
            .Should().Be(expectedIssue);
        entries.Where(value => value.EntryType == "InventoryReturn").Sum(value => value.Amount)
            .Should().Be(expectedReturn);
        (await _context.Set<Project>().SingleAsync(value => value.Id == _projectId)).ActualCost
            .Should().Be(expectedActual);
    }

    private ApplicationUser User(Guid id, string username) => new()
    {
        Id = id,
        TenantId = _tenantId,
        UserName = username,
        NormalizedUserName = username.ToUpperInvariant(),
        Email = $"{username}@e2e.local",
        NormalizedEmail = $"{username}@e2e.local".ToUpperInvariant(),
        FirstName = username,
        LastName = "E2E",
        IsActive = true,
        EmailConfirmed = true
    };

    private sealed class MutableCurrentUser(Guid tenantId, Guid userId, string username) : ICurrentUserProvider
    {
        public Guid UserId { get; private set; } = userId;
        public Guid TenantId { get; } = tenantId;
        public string Username { get; private set; } = username;
        public string FullName => Username;
        public bool IsAuthenticated => true;
        public IEnumerable<string> Roles => ["TDC_STORES_MANAGER"];
        public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        public IDictionary<string, string> Claims { get; } = new Dictionary<string, string>();
        public bool IsExternalUser => false;
        public string AuthenticationProvider => "Local";

        public void Switch(Guid nextUserId, string nextUsername)
        {
            UserId = nextUserId;
            Username = nextUsername;
        }
    }
}
