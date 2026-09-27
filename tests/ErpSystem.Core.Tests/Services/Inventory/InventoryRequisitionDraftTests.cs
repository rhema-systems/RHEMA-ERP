using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryRequisitionDraftTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _location = Guid.NewGuid();
    private readonly Warehouse _warehouse = new() { Code = "DRAFT-WH", Name = "Draft test warehouse" };
    private readonly OrganizationUnit _organizationUnit = new() { Name = "Operations", Code = "OPS", AccountCode = "CC-OPS", IsActive = true };
    private readonly InventoryItem _item = new()
    {
        ItemCode = "DRAFT-PVC", Name = "Draft PVC", UnitOfMeasure = "EACH",
        ValuationMethod = ValuationMethod.FIFO, AverageCost = 0, StandardCost = 2000, LastPurchaseCost = 1900
    };

    [Theory]
    [InlineData(ValuationMethod.FIFO, 1900)]
    [InlineData(ValuationMethod.LIFO, 2100)]
    [InlineData(ValuationMethod.WeightedAverage, 2000)]
    [InlineData(ValuationMethod.StandardCost, 2000)]
    public async Task Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(
        ValuationMethod method, decimal expectedCost)
    {
        var service = await Setup(method);
        var result = await service.CreateAsync(Request(2));
        _db.ChangeTracker.Clear();
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(1);
        saved.TotalQuantity.Should().Be(2);
        saved.TotalValue.Should().Be(expectedCost * 2);
        result.Items.Should().ContainSingle().Which.UnitCost.Should().Be(expectedCost);
        (await _db.Set<InventoryLayer>().Where(x => x.TenantId == _tenant && x.LocationId == _location)
            .SumAsync(x => x.RemainingQuantity)).Should().Be(4);
        (await _db.Set<InventoryMovement>().CountAsync()).Should().Be(0);
        (await _db.Set<InventoryIssueVoucher>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_update_remove_merge_unsaved_changes_into_persisted_totals()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(0));
        var added = await service.AddItemAsync(created.Id, new AddRequisitionItemDto
        { InventoryItemId = _item.Id, RequestedQuantity = 2, LocationId = _location });
        _db.ChangeTracker.Clear();
        (await _db.Set<InventoryRequisition>().SingleAsync()).TotalValue.Should().Be(3800);

        var updated = await service.UpdateItemAsync(created.Id, added.Id,
            new UpdateRequisitionItemDto { RequestedQuantity = 4, LocationId = _location });
        updated.UnitCost.Should().Be(2000);
        _db.ChangeTracker.Clear();
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(1);
        saved.TotalQuantity.Should().Be(4);
        saved.TotalValue.Should().Be(8000);

        await service.RemoveItemAsync(created.Id, added.Id);
        _db.ChangeTracker.Clear();
        saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(0);
        saved.TotalQuantity.Should().Be(0);
        saved.TotalValue.Should().Be(0);
        (await service.GetByIdAsync(created.Id))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Reads_reconcile_legacy_header_without_rewriting_approved_record()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var result = await service.CreateAsync(Request(2));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems = 0;
        saved.TotalQuantity = 0;
        saved.TotalValue = 0;
        saved.Status = RequisitionStatus.Approved;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var list = await service.GetAllAsync();
        list.Should().ContainSingle().Which.TotalItems.Should().Be(1);
        (await service.GetPendingIssueAsync()).Single().TotalQuantity.Should().Be(2);
        (await service.GetByIdAsync(result.Id))!.TotalValue.Should().Be(3800);
        _db.ChangeTracker.Clear();
        (await _db.Set<InventoryRequisition>().SingleAsync()).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Approved_lines_cannot_be_repriced_through_draft_edit()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var result = await service.CreateAsync(Request(2));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.Status = RequisitionStatus.Approved;
        await _db.SaveChangesAsync();
        var action = () => service.UpdateItemAsync(saved.Id, result.Items.Single().Id,
            new UpdateRequisitionItemDto { RequestedQuantity = 4 });
        await action.Should().ThrowAsync<InvalidOperationException>();
        (await _db.Set<InventoryRequisitionItem>().SingleAsync()).UnitCost.Should().Be(1900);
    }

    private CreateInventoryRequisitionDto Request(decimal quantity) => new()
    {
        OrganizationUnitId = _organizationUnit.Id, DepartmentName = "Operations", WarehouseId = _warehouse.Id,
        LocationId = _location, CostCenter = "Operations", Purpose = "Automated test only",
        Items = quantity == 0 ? [] : [new() { InventoryItemId = _item.Id, RequestedQuantity = quantity, LocationId = _location }]
    };

    [Fact]
    public async Task Detail_resolves_the_saved_issue_location_without_request_header_or_loaded_navigation()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(2));
        var source = await _db.Set<InventoryRequisition>().SingleAsync();
        source.LocationId = null;
        source.Items.Single().LocationId = _location;
        _db.Add(new WarehouseLocation { Id = _location, TenantId = _tenant,
            WarehouseId = _warehouse.Id, LocationCode = "LOC-001", Name = "Main", IsActive = false });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var result = (await service.GetByIdAsync(created.Id))!;
        result.LocationId.Should().BeNull();
        result.Items.Single().LocationName.Should().Be("LOC-001 - Main");
        // Historical labels remain readable even when a bin is no longer available for new issues.
        result.Items.Single().LocationId.Should().Be(_location);
        _db.ChangeTracker.HasChanges().Should().BeFalse();
        (await service.GetByRequisitionNumberAsync(created.RequisitionNumber))!.Items.Single()
            .LocationName.Should().Be("LOC-001 - Main");
    }

    [Fact]
    public async Task Requester_can_leave_storage_location_unspecified()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var request = Request(2);
        request.LocationId = null;
        request.Items.Single().LocationId = null;
        var created = await service.CreateAsync(request);
        var result = (await service.GetByIdAsync(created.Id))!;
        result.LocationId.Should().BeNull();
        result.Items.Single().LocationId.Should().BeNull();
        result.Items.Single().LocationName.Should().BeNull();
        result.Status.Should().Be(RequisitionStatus.Draft);
        (await _db.Set<InventoryIssueVoucher>().CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Issue_requires_a_real_location_id(bool emptyGuid)
    {
        var action = () => InventoryRequisitionService.RequireIssueLocationId(emptyGuid ? Guid.Empty : null);
        action.Should().Throw<InventoryIssueControlException>().Which.Code.Should().Be("INV_ISSUE_LOCATION_REQUIRED");
        InventoryRequisitionService.RequireIssueLocationId(_location).Should().Be(_location);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("other-tenant")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    [InlineData("other-warehouse")]
    public void Issue_rejects_unavailable_or_out_of_scope_locations(string kind)
    {
        var location = new WarehouseLocation { TenantId = kind == "other-tenant" ? Guid.NewGuid() : _tenant,
            WarehouseId = kind == "other-warehouse" ? Guid.NewGuid() : _warehouse.Id,
            IsActive = kind != "inactive", IsDeleted = kind == "deleted" };
        var action = () => InventoryRequisitionService.ValidateIssueLocation(kind == "missing" ? null : location,
            _tenant, _warehouse.Id);
        if (kind is "missing" or "other-tenant" or "deleted")
            action.Should().Throw<InventoryIssueNotFoundException>();
        else
            action.Should().Throw<InventoryIssueControlException>().Which.Code.Should().Be(
                kind == "inactive" ? "INV_ISSUE_LOCATION_INACTIVE" : "INV_ISSUE_LOCATION_WAREHOUSE_MISMATCH");
    }

    [Fact]
    public void Issue_accepts_an_active_location_in_the_approved_warehouse()
    {
        var location = new WarehouseLocation { TenantId = _tenant, WarehouseId = _warehouse.Id, IsActive = true };
        var action = () => InventoryRequisitionService.ValidateIssueLocation(location, _tenant, _warehouse.Id);
        action.Should().NotThrow();
    }

    private async Task<InventoryRequisitionService> Setup(ValuationMethod method)
    {
        _warehouse.TenantId = _tenant;
        _organizationUnit.TenantId = _tenant;
        _item.TenantId = _tenant;
        _item.ValuationMethod = method;
        var user = new ApplicationUser { Id = _actor, TenantId = _tenant, UserName = "requester", FirstName = "Test", LastName = "Requester" };
        _db.AddRange(user, _warehouse, _item, _organizationUnit,
            Layer(_tenant, _location, 1900, -2), Layer(_tenant, _location, 2100, -1),
            Layer(Guid.NewGuid(), _location, 1, -10), Layer(_tenant, Guid.NewGuid(), 1, -10),
            new InventoryBalance { TenantId = _tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse.Id,
                LocationId = _location, QuantityOnHand = 4, TotalValue = 8000, AverageUnitCost = 2000 });
        await _db.SaveChangesAsync();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.UserId).Returns(_actor);
        current.SetupGet(x => x.TenantId).Returns(_tenant);
        var items = new Mock<IInventoryItemRepository>();
        items.Setup(x => x.GetByIdAsync(_item.Id)).ReturnsAsync(_item);
        var warehouses = new Mock<IWarehouseRepository>();
        warehouses.Setup(x => x.GetByIdAsync(_warehouse.Id)).ReturnsAsync(_warehouse);
        var quantities = new Mock<IWarehouseQuantityRepository>();
        quantities.Setup(x => x.GetByWarehouseAndItemAsync(_warehouse.Id, _item.Id))
            .ReturnsAsync(new WarehouseQuantity { TenantId = _tenant, AverageCost = 1900 });
        return new InventoryRequisitionService(
            new InventoryRequisitionRepository(_db), new InventoryRequisitionItemRepository(_db),
            items.Object, warehouses.Object, Mock.Of<IWarehouseLocationRepository>(), quantities.Object,
            Mock.Of<IStockMovementRepository>(), Mock.Of<IConsignmentSettlementService>(),
            Mock.Of<IProjectRepository>(), Mock.Of<IProjectService>(), new UnitOfWork(_db), current.Object,
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
            Mock.Of<IInventoryProjectReservationService>(), Mock.Of<IProcurementAccessControlService>(),
            Mock.Of<IProcurementControlEventService>(), Mock.Of<IInventoryReturnControlService>(),
            Mock.Of<IInventoryIssueFinanceAssetService>(), Mock.Of<IInventoryValuationService>(),
            NullLogger<InventoryRequisitionService>.Instance);
    }

    [Theory]
    [InlineData(2, 1, 1, RequisitionStatus.Issued, 0)]
    [InlineData(2, 0, 2, RequisitionStatus.Issued, 0)]
    [InlineData(5, 1, 1, RequisitionStatus.PartiallyIssued, 3)]
    public async Task Reads_preserve_gross_fulfilment_and_show_returns_without_reopening_issue_capacity(
        decimal approved, decimal netIssued, decimal returned, RequisitionStatus expected, decimal remaining)
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(approved));
        var source = await _db.Set<InventoryRequisition>().Include(x => x.Items).SingleAsync();
        var item = source.Items.Single();
        source.Status = RequisitionStatus.PartiallyIssued; // Includes records affected by the old return calculation.
        item.ApprovedQuantity = approved;
        item.IssuedQuantity = netIssued;
        item.LineValue = netIssued * 1900;
        _db.Add(new InventoryReturnVoucher
        {
            TenantId = _tenant, InventoryRequisitionId = source.Id, WarehouseId = source.WarehouseId,
            RequestedById = _actor, Status = InventoryReturnVoucherStatus.Posted,
            Lines = [new() { TenantId = _tenant, InventoryRequisitionItemId = item.Id,
                InventoryItemId = item.InventoryItemId, Quantity = returned }]
        });
        await _db.SaveChangesAsync();
        var result = (await service.GetByIdAsync(created.Id))!;
        result.Status.Should().Be(expected);
        var actual = result.Items.Single();
        actual.RequestedQuantity.Should().Be(approved);
        actual.ApprovedQuantity.Should().Be(approved);
        actual.GrossIssuedQuantity.Should().Be(netIssued + returned);
        actual.ReturnedQuantity.Should().Be(returned);
        actual.NetIssuedQuantity.Should().Be(netIssued);
        actual.RemainingToIssueQuantity.Should().Be(remaining);
        (await service.GetAllAsync()).Single().Status.Should().Be(expected);
        (await service.GetPendingIssueAsync()).Any(x => x.Id == source.Id).Should().Be(remaining > 0);
        // Read projection must never rewrite audited source balances or the saved approval.
        item.IssuedQuantity.Should().Be(netIssued);
        item.LineValue.Should().Be(netIssued * 1900);
        _db.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Return_totals_exclude_pending_reversed_deleted_other_tenant_and_other_requisition_documents()
    {
        await Setup(ValuationMethod.FIFO);
        var sourceId = Guid.NewGuid();
        var item = new InventoryRequisitionItem { ApprovedQuantity = 2, IssuedQuantity = 1 };
        foreach (var kind in new[] { "posted", "pending", "approved", "reversed", "deleted", "line-deleted", "other-tenant", "other-requisition" })
        {
            var tenant = kind == "other-tenant" ? Guid.NewGuid() : _tenant;
            _db.Add(new InventoryReturnVoucher
            {
                TenantId = tenant, InventoryRequisitionId = kind == "other-requisition" ? Guid.NewGuid() : sourceId,
                Status = kind switch { "pending" => InventoryReturnVoucherStatus.PendingApproval,
                    "approved" => InventoryReturnVoucherStatus.Approved, "reversed" => InventoryReturnVoucherStatus.Reversed,
                    _ => InventoryReturnVoucherStatus.Posted }, IsDeleted = kind == "deleted",
                Lines = [new() { TenantId = tenant, InventoryRequisitionItemId = item.Id,
                    Quantity = 1, IsDeleted = kind == "line-deleted" }]
            });
        }
        await _db.SaveChangesAsync();
        var returns = await InventoryRequisitionFulfilment.LoadReturnsAsync(new UnitOfWork(_db), _tenant, [sourceId]);
        InventoryRequisitionFulfilment.Returned(item, returns).Should().Be(1);
        InventoryRequisitionFulfilment.GrossIssued(item, returns).Should().Be(2);
        InventoryRequisitionFulfilment.Remaining(item, returns).Should().Be(0);
    }

    [Theory]
    [InlineData(RequisitionStatus.Completed)]
    [InlineData(RequisitionStatus.Cancelled)]
    [InlineData(RequisitionStatus.Draft)]
    [InlineData(RequisitionStatus.Submitted)]
    public void Fulfilment_does_not_override_terminal_or_approval_workflow_status(RequisitionStatus status)
    {
        var source = new InventoryRequisition { Status = status };
        source.Items.Add(new() { ApprovedQuantity = 2, IssuedQuantity = 2 });
        InventoryRequisitionFulfilment.Status(source, new Dictionary<Guid, decimal>()).Should().Be(status);
    }

    [Fact]
    public void Reversed_return_restores_net_without_increasing_gross_or_issue_capacity()
    {
        var source = new InventoryRequisition { Status = RequisitionStatus.Issued };
        var item = new InventoryRequisitionItem { ApprovedQuantity = 2, IssuedQuantity = 1 };
        source.Items.Add(item);
        var returns = new Dictionary<Guid, decimal> { [item.Id] = 1 };
        InventoryRequisitionFulfilment.GrossIssued(item, returns).Should().Be(2);
        item.IssuedQuantity = 2;
        returns.Clear();
        InventoryRequisitionFulfilment.GrossIssued(item, returns).Should().Be(2);
        InventoryRequisitionFulfilment.Returned(item, returns).Should().Be(0);
        InventoryRequisitionFulfilment.Remaining(item, returns).Should().Be(0);
        InventoryRequisitionFulfilment.Status(source, returns).Should().Be(RequisitionStatus.Issued);
    }

    [Theory]
    [InlineData(" CC-OPS ", "OPS", "CC-OPS")]
    [InlineData("", " OPS ", "OPS")]
    public async Task Create_derives_cost_centre_from_organization_unit_not_client_text(string account, string code, string expected)
    {
        var service = await Setup(ValuationMethod.FIFO);
        _organizationUnit.AccountCode = account;
        _organizationUnit.Code = code;
        await _db.SaveChangesAsync();
        var request = Request(0);
        request.CostCenter = "CLIENT-OVERRIDE";
        request.DepartmentName = "Not the real department";
        var created = await service.CreateAsync(request);
        created.CostCenter.Should().Be(expected);
        created.DepartmentName.Should().Be("Operations");
        created.OrganizationUnitId.Should().Be(_organizationUnit.Id);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("other-tenant")]
    [InlineData("unconfigured")]
    public async Task Create_rejects_invalid_organization_unit_before_saving(string problem)
    {
        var service = await Setup(ValuationMethod.FIFO);
        var request = Request(0);
        if (problem == "missing") request.OrganizationUnitId = Guid.NewGuid();
        if (problem == "inactive") _organizationUnit.IsActive = false;
        if (problem == "other-tenant") _organizationUnit.TenantId = Guid.NewGuid();
        if (problem == "unconfigured") { _organizationUnit.Code = " "; _organizationUnit.AccountCode = " "; }
        await _db.SaveChangesAsync();
        var action = () => service.CreateAsync(request);
        await action.Should().ThrowAsync<ArgumentException>();
        (await _db.Set<InventoryRequisition>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Draft_edit_preserves_saved_cost_centre_but_organization_unit_change_rederives_it()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(0));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.CostCenter = "HISTORIC";
        var other = new OrganizationUnit { TenantId = _tenant, Name = "Engineering", Code = "ENG", AccountCode = "CC-ENG", IsActive = true };
        _db.Add(other);
        await _db.SaveChangesAsync();
        (await service.UpdateAsync(created.Id, new() { Purpose = "Updated", CostCenter = "CLIENT" }))
            .CostCenter.Should().Be("HISTORIC");
        var updated = await service.UpdateAsync(created.Id, new() { OrganizationUnitId = other.Id, CostCenter = "CLIENT" });
        updated.CostCenter.Should().Be("CC-ENG");
        updated.DepartmentName.Should().Be("Engineering");
        updated.OrganizationUnitId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Legacy_blank_draft_gets_cost_centre_on_save_without_rewriting_approved_history()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(0));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.CostCenter = null;
        await _db.SaveChangesAsync();
        (await service.UpdateAsync(created.Id, new() { Purpose = "Updated" })).CostCenter.Should().Be("CC-OPS");
        saved.Status = RequisitionStatus.Approved;
        saved.CostCenter = "APPROVED-HISTORY";
        await _db.SaveChangesAsync();
        var action = () => service.UpdateAsync(created.Id, new() { CostCenter = "NEW" });
        await action.Should().ThrowAsync<InvalidOperationException>();
        saved.CostCenter.Should().Be("APPROVED-HISTORY");
    }

    private InventoryLayer Layer(Guid tenant, Guid location, decimal cost, int day) => new()
    {
        TenantId = tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse.Id, LocationId = location,
        LayerNumber = Guid.NewGuid().ToString("N"), LayerDate = DateTime.UtcNow.Date.AddDays(day),
        OriginalQuantity = 2, RemainingQuantity = 2, UnitCost = cost, RemainingValue = 2 * cost
    };

    public void Dispose() => _db.Dispose();
}
