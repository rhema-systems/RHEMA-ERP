using System.Collections;
using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryScanningServiceTests : IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly Mock<ICurrentUserProvider> _currentUser = new();
    private readonly Mock<IProcurementAccessControlService> _access = new();
    private readonly Mock<IInventoryItemIdentifierService> _identifiers = new();
    private readonly Mock<IGoodsReceiptNoteService> _goodsReceipts = new();
    private readonly Mock<IInventoryRequisitionService> _requisitions = new();
    private readonly Mock<IInventoryTransferService> _transfers = new();
    private readonly Mock<IPhysicalCountService> _counts = new();
    private readonly InventoryScanningService _service;

    public InventoryScanningServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
        _currentUser.SetupGet(item => item.TenantId).Returns(_tenantId);
        _currentUser.SetupGet(item => item.UserId).Returns(_userId);
        _currentUser.SetupGet(item => item.Username).Returns("stores.officer@tenant.test");
        _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        _access.Setup(item => item.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });

        _service = new InventoryScanningService(
            _unitOfWork,
            _currentUser.Object,
            _identifiers.Object,
            _access.Object,
            _goodsReceipts.Object,
            _requisitions.Object,
            _transfers.Object,
            _counts.Object,
            NullLogger<InventoryScanningService>.Instance);
    }

    [Fact]
    public async Task Label_profile_lifecycle_is_tenant_scoped_default_safe_and_audited()
    {
        var foreignProfile = new InventoryLabelProfile
        {
            TenantId = Guid.NewGuid(), Name = "Other tenant", Symbology = "QR", IsActive = true, IsDefault = true
        };
        await _context.Set<InventoryLabelProfile>().AddAsync(foreignProfile);
        await _context.SaveChangesAsync();

        var first = await _service.SaveLabelProfileAsync(null, Request("Receiving", true), "corr-1");
        var second = await _service.SaveLabelProfileAsync(null, Request("Warehouse transfer", true), "corr-2");

        var visible = await _service.GetLabelProfilesAsync();
        visible.Should().HaveCount(2);
        visible.Should().NotContain(item => item.Name == "Other tenant");
        visible.Single(item => item.Id == first.Id).IsDefault.Should().BeFalse();
        visible.Single(item => item.Id == second.Id).IsDefault.Should().BeTrue();
        (await _context.Set<AuditLog>().CountAsync(item => item.TenantId == _tenantId &&
            item.Resource == "InventoryScanning" && item.Action == "InventoryLabelProfile.Created")).Should().Be(2);

        var inventoryItem = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ItemCode = "ITEM-PRINT", Name = "Printed item"
        };
        await _context.AddRangeAsync(inventoryItem, new InventoryLabelPrintEvent
        {
            TenantId = _tenantId, LabelProfileId = first.Id, InventoryItemId = inventoryItem.Id,
            Identifier = "ITEM-PRINT", IdentifierKind = "PrimaryBarcode", LabelCount = 1,
            PrintedById = _userId, PrintedAtUtc = DateTime.UtcNow, CorrelationId = "corr-print"
        }, new InventoryLabelPrintEvent
        {
            TenantId = _tenantId, LabelProfileId = foreignProfile.Id, InventoryItemId = inventoryItem.Id,
            Identifier = "FORGED-CROSS-TENANT", IdentifierKind = "PrimaryBarcode", LabelCount = 1,
            PrintedById = _userId, PrintedAtUtc = DateTime.UtcNow, CorrelationId = "corr-forged"
        });
        await _context.SaveChangesAsync();
        await _service.DeleteLabelProfileAsync(first.Id, "corr-delete");

        var retainedPrints = await _service.GetRecentPrintsAsync();
        retainedPrints.Should().ContainSingle().Which.LabelProfileName.Should().Be("Receiving");

        var delete = () => _service.DeleteLabelProfileAsync(second.Id, "corr-3");
        await delete.Should().ThrowAsync<InventoryScanningException>()
            .Where(error => error.Code == "INV_LABEL_DEFAULT_DELETE_BLOCKED");
    }

    [Fact]
    public async Task External_actor_cannot_read_internal_scanning_controls()
    {
        _currentUser.SetupGet(item => item.IsExternalUser).Returns(true);

        var act = () => _service.GetLabelProfilesAsync();

        await act.Should().ThrowAsync<InventoryScanningAuthorizationException>();
    }

    [Fact]
    public async Task Authorization_records_only_denials_so_an_allowed_retry_does_not_conflict()
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "WH-SCAN", Name = "Scanning warehouse", IsActive = true
        };
        var count = new PhysicalCount
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = warehouse.Id, Warehouse = warehouse,
            CountNumber = "COUNT-SCAN-001", Status = "InProgress", CountDate = DateTime.UtcNow
        };
        await _context.AddRangeAsync(warehouse, count);
        await _context.SaveChangesAsync();

        _access.SetupSequence(item => item.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false })
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        _access.Setup(item => item.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });

        var denied = () => _service.GetDocumentContextAsync(InventoryScanOperation.PhysicalCount, count.Id);
        await denied.Should().ThrowAsync<InventoryScanningAuthorizationException>();

        var allowed = await _service.GetDocumentContextAsync(InventoryScanOperation.PhysicalCount, count.Id);

        allowed.DocumentId.Should().Be(count.Id);
        count.Status = "Cancelled";
        await _context.SaveChangesAsync();
        var terminal = () => _service.GetDocumentContextAsync(InventoryScanOperation.PhysicalCount, count.Id);
        await terminal.Should().ThrowAsync<InventoryScanningException>()
            .Where(error => error.Code == "INV_SCAN_DOCUMENT_STATE_INVALID");
        _access.Verify(item => item.CheckCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _access.Verify(item => item.EnforceCapabilityAsync(
            It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Document_context_denies_the_whole_payload_when_any_line_location_is_out_of_scope()
    {
        var warehouse = new Warehouse
        {
            TenantId = _tenantId, Code = "WH-CONTEXT", Name = "Context warehouse", IsActive = true
        };
        var allowedLocation = new WarehouseLocation
        {
            TenantId = _tenantId, WarehouseId = warehouse.Id, LocationCode = "CTX-ALLOWED", IsActive = true
        };
        var deniedLocation = new WarehouseLocation
        {
            TenantId = _tenantId, WarehouseId = warehouse.Id, LocationCode = "CTX-DENIED", IsActive = true
        };
        var item = new InventoryItem
        {
            TenantId = _tenantId, CategoryId = Guid.NewGuid(), ItemCode = "CTX-ITEM", Name = "Context item"
        };
        var count = new PhysicalCount
        {
            TenantId = _tenantId, WarehouseId = warehouse.Id, Warehouse = warehouse,
            CountNumber = "COUNT-CONTEXT", Status = "InProgress", CountDate = DateTime.UtcNow,
            Items =
            {
                new PhysicalCountItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    LocationId = allowedLocation.Id, Location = allowedLocation, ItemCode = item.ItemCode, ItemName = item.Name
                },
                new PhysicalCountItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    LocationId = deniedLocation.Id, Location = deniedLocation, ItemCode = item.ItemCode, ItemName = item.Name
                }
            }
        };
        await _context.AddRangeAsync(warehouse, allowedLocation, deniedLocation, item, count);
        await _context.SaveChangesAsync();
        _access.Setup(value => value.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken __) =>
                new ProcurementAccessCapabilityDecisionDto { Allowed = request.LocationId != deniedLocation.Id });
        _access.Setup(value => value.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });

        var load = () => _service.GetDocumentContextAsync(InventoryScanOperation.PhysicalCount, count.Id);

        await load.Should().ThrowAsync<InventoryScanningAuthorizationException>();
        _access.Verify(value => value.EnforceCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.LocationId == deniedLocation.Id &&
                request.RequireLocationScope), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scan_batch_reads_filter_list_and_deny_direct_cross_location_access()
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ItemCode = "SCAN-SCOPE", Name = "Scoped scan item"
        };
        var allowedWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "SCAN-A", Name = "Allowed scan warehouse", IsActive = true
        };
        var deniedWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "SCAN-B", Name = "Denied scan warehouse", IsActive = true
        };
        var allowedLocation = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = allowedWarehouse.Id,
            Warehouse = allowedWarehouse, LocationCode = "A-01", Name = "Allowed bin", IsActive = true
        };
        var deniedLocation = new WarehouseLocation
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, WarehouseId = deniedWarehouse.Id,
            Warehouse = deniedWarehouse, LocationCode = "B-01", Name = "Denied bin", IsActive = true
        };
        var allowed = Batch(allowedWarehouse, allowedLocation, "SCAN-ALLOWED", DateTime.UtcNow);
        var denied = Batch(deniedWarehouse, deniedLocation, "SCAN-DENIED", DateTime.UtcNow.AddMinutes(-1));
        await _context.AddRangeAsync(item, allowedWarehouse, deniedWarehouse, allowedLocation, deniedLocation, allowed, denied);
        await _context.SaveChangesAsync();

        _access.Reset();
        _access.Setup(service => service.CheckCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementAccessCapabilityRequest request, string correlation, CancellationToken _) =>
                new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = request.PermissionCode == "procurement.inventory.read" &&
                        request.WarehouseId == allowedWarehouse.Id && request.LocationId == allowedLocation.Id,
                    PermissionCode = request.PermissionCode,
                    WarehouseId = request.WarehouseId,
                    LocationId = request.LocationId,
                    CorrelationId = correlation
                });
        _access.Setup(service => service.EnforceCapabilityAsync(
                It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = false });

        var visible = await _service.GetRecentBatchesAsync(10);
        var direct = () => _service.GetBatchAsync(denied.Id);

        visible.Should().ContainSingle().Which.Id.Should().Be(allowed.Id);
        await direct.Should().ThrowAsync<InventoryScanningAuthorizationException>();
        _access.Verify(service => service.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request => request.RequireLocationScope &&
                request.LocationId == allowedLocation.Id && request.PermissionCode == "procurement.inventory.read"),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        InventoryScanBatch Batch(Warehouse warehouse, WarehouseLocation location, string reference, DateTime captured)
        {
            var batch = new InventoryScanBatch
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, DeviceId = "scope-scanner",
                IdempotencyKey = reference, PayloadHash = new string('A', 64),
                Operation = InventoryScanOperation.RequisitionIssue, DocumentId = Guid.NewGuid(),
                DocumentReference = reference, WarehouseId = warehouse.Id, Warehouse = warehouse,
                ActorUserId = _userId, CapturedAtUtc = captured, CorrelationId = reference,
                Status = InventoryScanBatchStatus.Captured
            };
            batch.Lines.Add(new InventoryScanLine
            {
                TenantId = _tenantId, ScanBatchId = batch.Id, ScanBatch = batch, ClientLineId = Guid.NewGuid(),
                Sequence = 1, RawIdentifier = item.ItemCode, IdentifierKind = "PrimaryBarcode",
                InventoryItemId = item.Id, InventoryItem = item, DocumentLineId = Guid.NewGuid(),
                ScannedQuantity = 1, ConversionToBase = 1, BaseQuantity = 1,
                LocationId = location.Id, Location = location, ScannedAtUtc = captured
            });
            return batch;
        }
    }

    [Fact]
    public void Ef_model_enforces_profile_idempotency_and_scan_evidence_constraints()
    {
        var profile = _context.Model.FindEntityType(typeof(InventoryLabelProfile))!;
        profile.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "Name" }) &&
            index.GetFilter()!.Contains("IsDeleted"));
        profile.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId" }) &&
            index.GetFilter()!.Contains("IsDefault"));

        var batch = _context.Model.FindEntityType(typeof(InventoryScanBatch))!;
        batch.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { "TenantId", "DeviceId", "IdempotencyKey" }));

        var sqlServerOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Tdc0602ModelOnly;Trusted_Connection=True")
            .Options;
        using var sqlServerContext = new ApplicationDbContext(sqlServerOptions);
        var line = sqlServerContext.Model.FindEntityType(typeof(InventoryScanLine))!;
        line.FindProperty(nameof(InventoryScanLine.LocationIdentifier))!.GetMaxLength().Should().Be(100);
        line.FindProperty(nameof(InventoryScanLine.BaseQuantity))!.GetColumnType().Should().Be("decimal(18,4)");
        line.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "ScanBatchId", "ClientLineId" }));
    }

    [Fact]
    [Trait("Batch", "E2E-024")]
    public async Task Every_scan_operation_delegates_application_to_the_existing_inventory_owner()
    {
        foreach (var operation in Enum.GetValues<InventoryScanOperation>())
            await InvokeApplyTransactionAsync(operation);

        _goodsReceipts.Invocations.Select(item => item.Method.Name).Should().Equal(
            nameof(IGoodsReceiptNoteService.ApplyScanMetadataAsync),
            nameof(IGoodsReceiptNoteService.PostToInventoryAsync));
        _requisitions.Invocations.Select(item => item.Method.Name).Should().Equal(
            nameof(IInventoryRequisitionService.IssueAsync),
            nameof(IInventoryRequisitionService.ReturnAsync));
        _transfers.Invocations.Select(item => item.Method.Name).Should().Equal(
            nameof(IInventoryTransferService.ApplyScanMetadataAsync),
            nameof(IInventoryTransferService.ShipAsync),
            nameof(IInventoryTransferService.ApplyScanMetadataAsync),
            nameof(IInventoryTransferService.ReceiveAsync));
        _counts.Invocations.Select(item => item.Method.Name).Should().Equal(
            nameof(IPhysicalCountService.RecordCountItemAsync));

        var issue = (IssueRequisitionDto)_requisitions.Invocations
            .Single(item => item.Method.Name == nameof(IInventoryRequisitionService.IssueAsync)).Arguments[1];
        issue.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            IssuedQuantity = 2m,
            LocationId = (Guid?)null,
            LotNumber = "LOT-01",
            SerialNumber = "SERIAL-01"
        });
        var countItem = (RecordCountItemDto)_counts.Invocations.Single().Arguments[0];
        countItem.Should().BeEquivalentTo(new
        {
            CountedQuantity = 2m,
            LotNumber = "LOT-01",
            SerialNumber = "SERIAL-01",
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        });
        countItem.IdempotencyKey.Should().StartWith("scan:test-batch:");
    }

    [Fact]
    public async Task Reconciliation_reports_only_movements_created_by_the_current_scan_batch()
    {
        var item = new InventoryItem
        {
            TenantId = _tenantId, CategoryId = Guid.NewGuid(), ItemCode = "SCAN-DELTA", Name = "Delta item"
        };
        var warehouse = new Warehouse
        {
            TenantId = _tenantId, Code = "WH-DELTA", Name = "Delta warehouse", IsActive = true
        };
        var requisition = new InventoryRequisition
        {
            TenantId = _tenantId, RequisitionNumber = "REQ-DELTA", DepartmentId = Guid.NewGuid(),
            WarehouseId = warehouse.Id, Warehouse = warehouse, Status = RequisitionStatus.Approved,
            Items =
            {
                new InventoryRequisitionItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    ItemCode = item.ItemCode, ItemName = item.Name, ApprovedQuantity = 5
                }
            }
        };
        await _context.AddRangeAsync(item, warehouse, requisition, new StockMovement
        {
            TenantId = _tenantId, InventoryItemId = item.Id, WarehouseId = warehouse.Id,
            MovementType = "Issue", Quantity = -2, ReferenceId = requisition.Id,
            ReferenceType = ReferenceType.Requisition, MovementDate = DateTime.UtcNow.AddHours(-1)
        });
        await _context.SaveChangesAsync();
        _identifiers.Setup(value => value.ResolveAsync(_tenantId, item.ItemCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryIdentifierMatchDto
            {
                InventoryItemId = item.Id, ItemCode = item.ItemCode, ItemName = item.Name,
                Identifier = item.ItemCode, IdentifierKind = "ItemCode", ConversionToBase = 1
            });
        _requisitions.Setup(value => value.IssueAsync(requisition.Id, It.IsAny<IssueRequisitionDto>()))
            .Returns(async () =>
            {
                await _context.AddAsync(new StockMovement
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, WarehouseId = warehouse.Id,
                    MovementType = "Issue", Quantity = -1, ReferenceId = requisition.Id,
                    ReferenceType = ReferenceType.Requisition, MovementDate = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return true;
            });

        var result = await _service.SynchronizeAsync(new SynchronizeInventoryScanBatchRequest
        {
            DeviceId = "delta-scanner", IdempotencyKey = "delta-batch", Operation = InventoryScanOperation.RequisitionIssue,
            DocumentId = requisition.Id, WarehouseId = warehouse.Id, ApplyTransaction = true,
            Lines =
            {
                new InventoryScanInputDto
                {
                    ClientLineId = Guid.NewGuid(), RawIdentifier = item.ItemCode,
                    DocumentLineId = requisition.Items.Single().Id, Quantity = 1, ScannedAtUtc = DateTime.UtcNow
                }
            }
        }, "corr-delta");

        result.Reconciliation.Should().NotBeNull();
        result.Reconciliation!.StockMovementCount.Should().Be(1);
        result.Reconciliation.NetStockMovementQuantity.Should().Be(-1);
    }

    [Fact]
    [Trait("Batch", "E2E-024")]
    public async Task Required_mobile_operations_synchronize_once_in_capture_order_and_replay_idempotently()
    {
        var item = new InventoryItem
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, ItemCode = "ITEM-E2E024", Name = "Mobile scan item"
        };
        var source = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "WH-E2E024-SRC", Name = "Scan source", IsActive = true
        };
        var destination = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, Code = "WH-E2E024-DST", Name = "Scan destination", IsActive = true
        };
        var receipt = new GoodsReceiptNote
        {
            TenantId = _tenantId, GRNNumber = "GRN-E2E024", WarehouseId = source.Id,
            Warehouse = source, Status = GRNStatus.Accepted, StockUpdated = false,
            Items =
            {
                new GoodsReceiptNoteItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    ItemCode = item.ItemCode, ItemName = item.Name, ReceivedQuantity = 2, AcceptedQuantity = 2
                }
            }
        };
        var issue = new InventoryRequisition
        {
            TenantId = _tenantId, RequisitionNumber = "REQ-E2E024-ISSUE", DepartmentId = Guid.NewGuid(),
            WarehouseId = source.Id, Warehouse = source, Status = RequisitionStatus.Approved,
            Items =
            {
                new InventoryRequisitionItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    ItemCode = item.ItemCode, ItemName = item.Name, RequestedQuantity = 2, ApprovedQuantity = 2
                }
            }
        };
        var requisitionReturn = new InventoryRequisition
        {
            TenantId = _tenantId, RequisitionNumber = "REQ-E2E024-RETURN", DepartmentId = Guid.NewGuid(),
            WarehouseId = source.Id, Warehouse = source, Status = RequisitionStatus.Issued,
            Items =
            {
                new InventoryRequisitionItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    ItemCode = item.ItemCode, ItemName = item.Name, RequestedQuantity = 2,
                    ApprovedQuantity = 2, IssuedQuantity = 2
                }
            }
        };
        var shipment = Transfer("TRF-E2E024-SHIP", TransferStatus.Approved, 0);
        var transferReceipt = Transfer("TRF-E2E024-RECEIVE", TransferStatus.InTransit, 2);

        await _context.AddRangeAsync(source, destination, item, receipt, issue, requisitionReturn, shipment, transferReceipt);
        await _context.SaveChangesAsync();

        _identifiers.Setup(service => service.ResolveAsync(_tenantId, "ITEM-E2E024", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryIdentifierMatchDto
            {
                InventoryItemId = item.Id, ItemCode = item.ItemCode, ItemName = item.Name,
                Identifier = item.ItemCode, IdentifierKind = "PrimaryBarcode", ConversionToBase = 1
            });
        _goodsReceipts.Setup(service => service.ApplyScanMetadataAsync(
            It.IsAny<Guid>(), It.IsAny<IReadOnlyList<InventoryTransactionScanLineDto>>(), _userId)).Returns(Task.CompletedTask);
        _goodsReceipts.Setup(service => service.PostToInventoryAsync(It.IsAny<Guid>(), _userId)).ReturnsAsync(true);
        _requisitions.Setup(service => service.IssueAsync(It.IsAny<Guid>(), It.IsAny<IssueRequisitionDto>())).ReturnsAsync(true);
        _requisitions.Setup(service => service.ReturnAsync(It.IsAny<Guid>(), It.IsAny<ReturnRequisitionDto>())).ReturnsAsync(true);
        _transfers.Setup(service => service.ApplyScanMetadataAsync(
            It.IsAny<Guid>(), It.IsAny<InventoryScanOperation>(),
            It.IsAny<IReadOnlyList<InventoryTransactionScanLineDto>>(), _userId)).Returns(Task.CompletedTask);
        _transfers.Setup(service => service.ShipAsync(
            It.IsAny<Guid>(), _userId, It.IsAny<string?>(), It.IsAny<Dictionary<Guid, decimal>?>(), null)).ReturnsAsync(true);
        _transfers.Setup(service => service.ReceiveAsync(
            It.IsAny<Guid>(), _userId, It.IsAny<List<InventoryTransferItemDto>?>(), null)).ReturnsAsync(true);

        var requests = new[]
        {
            Request(InventoryScanOperation.GoodsReceipt, receipt.Id, source.Id, receipt.Items.Single().Id),
            Request(InventoryScanOperation.RequisitionIssue, issue.Id, source.Id, issue.Items.Single().Id),
            Request(InventoryScanOperation.RequisitionReturn, requisitionReturn.Id, source.Id, requisitionReturn.Items.Single().Id),
            Request(InventoryScanOperation.TransferShipment, shipment.Id, source.Id, shipment.Items.Single().Id),
            Request(InventoryScanOperation.TransferReceipt, transferReceipt.Id, destination.Id, transferReceipt.Items.Single().Id)
        };

        var synchronized = new List<InventoryScanBatchDto>();
        foreach (var request in requests)
            synchronized.Add(await _service.SynchronizeAsync(request, request.IdempotencyKey));

        synchronized.Select(batch => batch.Operation).Should().Equal(
            InventoryScanOperation.GoodsReceipt,
            InventoryScanOperation.RequisitionIssue,
            InventoryScanOperation.RequisitionReturn,
            InventoryScanOperation.TransferShipment,
            InventoryScanOperation.TransferReceipt);
        synchronized.Should().OnlyContain(batch => batch.Status == InventoryScanBatchStatus.Applied &&
            batch.ApplyTransaction && batch.Lines.Count == 1 && batch.Reconciliation != null);
        (await _context.Set<InventoryScanBatch>().CountAsync()).Should().Be(5);
        (await _context.Set<InventoryScanLine>().CountAsync()).Should().Be(5);
        (await _context.Set<AuditLog>().CountAsync(log => log.TenantId == _tenantId &&
            log.Resource == "InventoryScanning" && log.Action == "InventoryScanBatch.Applied")).Should().Be(5);

        var replay = await _service.SynchronizeAsync(requests[0], requests[0].IdempotencyKey);
        replay.Id.Should().Be(synchronized[0].Id);
        (await _context.Set<InventoryScanBatch>().CountAsync()).Should().Be(5);
        _goodsReceipts.Verify(service => service.PostToInventoryAsync(receipt.Id, _userId), Times.Once);
        _requisitions.Verify(service => service.IssueAsync(issue.Id, It.IsAny<IssueRequisitionDto>()), Times.Once);
        _requisitions.Verify(service => service.ReturnAsync(requisitionReturn.Id, It.IsAny<ReturnRequisitionDto>()), Times.Once);
        _transfers.Verify(service => service.ShipAsync(
            shipment.Id, _userId, It.IsAny<string?>(), It.IsAny<Dictionary<Guid, decimal>?>(), null), Times.Once);
        _transfers.Verify(service => service.ReceiveAsync(
            transferReceipt.Id, _userId, It.IsAny<List<InventoryTransferItemDto>?>(), null), Times.Once);

        InventoryTransfer Transfer(string number, TransferStatus status, decimal shippedQuantity) => new()
        {
            TenantId = _tenantId, TransferNumber = number, SourceWarehouseId = source.Id,
            SourceWarehouse = source, DestinationWarehouseId = destination.Id, DestinationWarehouse = destination,
            Status = status,
            Items =
            {
                new InventoryTransferItem
                {
                    TenantId = _tenantId, InventoryItemId = item.Id, InventoryItem = item,
                    ItemCode = item.ItemCode, ItemName = item.Name, RequestedQuantity = 2, ShippedQuantity = shippedQuantity
                }
            }
        };

        static SynchronizeInventoryScanBatchRequest Request(
            InventoryScanOperation operation,
            Guid documentId,
            Guid warehouseId,
            Guid documentLineId) => new()
        {
            DeviceId = "scanner-e2e024",
            IdempotencyKey = $"e2e024-{operation}",
            Operation = operation,
            DocumentId = documentId,
            WarehouseId = warehouseId,
            ApplyTransaction = true,
            Lines =
            {
                new InventoryScanInputDto
                {
                    ClientLineId = Guid.NewGuid(), RawIdentifier = "ITEM-E2E024", DocumentLineId = documentLineId,
                    Quantity = 2, ScannedAtUtc = DateTime.UtcNow
                }
            }
        };
    }

    private async Task InvokeApplyTransactionAsync(InventoryScanOperation operation)
    {
        var documentId = Guid.NewGuid();
        var documentLineId = Guid.NewGuid();
        if (operation == InventoryScanOperation.RequisitionReturn)
        {
            await _context.AddAsync(new InventoryRequisition
            {
                Id = documentId, TenantId = _tenantId, RequisitionNumber = "REQ-RETURN-DELEGATION",
                DepartmentId = Guid.NewGuid(), WarehouseId = Guid.NewGuid(), Status = RequisitionStatus.Issued
            });
            await _context.SaveChangesAsync();
        }
        if (operation == InventoryScanOperation.PhysicalCount)
        {
            await _context.AddAsync(new PhysicalCountItem
            {
                Id = documentLineId,
                TenantId = _tenantId,
                PhysicalCountId = documentId,
                InventoryItemId = Guid.NewGuid(),
                RowVersion = new byte[] { 1, 2, 3 }
            });
            await _context.SaveChangesAsync();
        }
        var resolvedLineType = typeof(InventoryScanningService)
            .GetNestedType("ResolvedLine", BindingFlags.NonPublic)!;
        var resolvedLine = Activator.CreateInstance(
            resolvedLineType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new object?[]
            {
                new InventoryScanInputDto
                {
                    ClientLineId = Guid.NewGuid(), RawIdentifier = "ITEM-01", Quantity = 2,
                    LotNumber = "LOT-01", SerialNumber = "SERIAL-01",
                    DocumentLineRowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 })
                },
                new InventoryIdentifierMatchDto
                {
                    InventoryItemId = Guid.NewGuid(), ItemCode = "ITEM-01", ItemName = "Scan item",
                    Identifier = "ITEM-01", IdentifierKind = "PrimaryBarcode", ConversionToBase = 1
                },
                new InventoryScanDocumentLineDto
                {
                    DocumentLineId = documentLineId, InventoryItemId = Guid.NewGuid(),
                    ItemCode = "ITEM-01", ItemName = "Scan item", ExpectedQuantity = 2
                },
                2m,
                null
            },
            null)!;
        var lines = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(resolvedLineType))!;
        lines.Add(resolvedLine);
        var apply = typeof(InventoryScanningService).GetMethod(
            "ApplyTransactionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)apply.Invoke(_service, new object[]
        {
            operation,
            documentId,
            lines,
            "scan:test-batch",
            "scan:test-correlation",
            CancellationToken.None
        })!;
    }

    private static SaveInventoryLabelProfileRequest Request(string name, bool isDefault) => new()
    {
        Name = name,
        Symbology = "QR",
        WidthMm = 60,
        HeightMm = 40,
        Dpi = 203,
        IncludeItemCode = true,
        IncludeItemName = true,
        IncludeUnit = true,
        IsDefault = isDefault,
        IsActive = true
    };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        _unitOfWork.Dispose();
    }
}
