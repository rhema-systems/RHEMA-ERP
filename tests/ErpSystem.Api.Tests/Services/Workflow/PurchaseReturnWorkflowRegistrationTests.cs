using System.Linq.Expressions;
using System.Reflection;
using ErpSystem.Api.Services;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FinanceSupplierReturn = ErpSystem.Core.Entities.Finance.SupplierReturn;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class PurchaseReturnWorkflowRegistrationTests
{
    [Fact]
    public void Catalog_registers_physical_purchase_return_as_inventory_not_finance_supplier_return()
    {
        var entries = new WorkflowEntityTypeCatalogService().GetDefaultEntityTypes();
        var entry = Assert.Single(entries, value => value.Name == "PurchaseReturn");
        Assert.Equal("Inventory", entry.Module);
        Assert.Equal("PURCHASE_RETURN", entry.Code);
        Assert.Equal("Supplier returns from accepted stock receipts", entry.Description);
        Assert.DoesNotContain(entries, value => value.Name == "SupplierReturn" && value.Code == entry.Code);
    }

    [Theory]
    [InlineData("PurchaseReturn")]
    [InlineData("Purchase Return")]
    [InlineData("INVENTORY_SUPPLIER_RETURN")]
    public async Task Context_uses_exact_physical_return_and_preserves_source_and_real_actor_fields(string alias)
    {
        var fixture = new Fixture();
        var context = await fixture.ContextAsync(alias);

        Assert.Equal(fixture.Return.Id, context["entityId"]);
        Assert.Equal(fixture.Return.ReturnNumber, context["returnNumber"]);
        Assert.Equal(fixture.Return.Status, context["status"]);
        Assert.Equal(fixture.Return.WarehouseId, context["warehouseId"]);
        Assert.Equal(fixture.Return.SupplierId, context["supplierId"]);
        Assert.Equal(fixture.Return.GoodsReceiptNoteId, context["goodsReceiptNoteId"]);
        Assert.Equal(fixture.Return.PurchaseOrderId, context["purchaseOrderId"]);
        Assert.Equal(fixture.Return.RequestedById, context["requestedById"]);
        Assert.Equal(string.Empty, context["approvedById"]);
        Assert.Equal(2m, context["totalQuantity"]);
        Assert.Equal(250m, context["totalValue"]);
        Assert.Null(fixture.Return.ApprovedById);
        fixture.Returns.Verify(value => value.FirstOrDefaultAsync(It.IsAny<Expression<Func<PurchaseReturn, bool>>>()), Times.Once);
        fixture.Unit.Verify(value => value.Repository<FinanceSupplierReturn>(), Times.Never);
        fixture.Unit.Verify(value => value.SaveChangesAsync(default), Times.Never);
    }

    [Theory]
    [InlineData("wrong-id")]
    [InlineData("wrong-tenant")]
    [InlineData("deleted")]
    public async Task Context_rejects_a_record_outside_the_exact_active_source_identity(string condition)
    {
        var fixture = new Fixture();
        var requestedId = fixture.Return.Id;
        if (condition == "wrong-id") fixture.Return.Id = Guid.NewGuid();
        if (condition == "wrong-tenant") fixture.Return.TenantId = Guid.NewGuid();
        if (condition == "deleted") fixture.Return.IsDeleted = true;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ContextAsync("PurchaseReturn", requestedId));
        Assert.Equal("Inventory supplier return not found", error.Message);
        fixture.Unit.Verify(value => value.Repository<FinanceSupplierReturn>(), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Context_rejects_missing_or_mismatched_authenticated_tenant_before_source_lookup(bool missingTenant)
    {
        var fixture = new Fixture();
        fixture.User.SetupGet(value => value.TenantId).Returns(missingTenant ? (Guid?)null : Guid.NewGuid());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ContextAsync("PurchaseReturn"));
        Assert.Equal("Inventory supplier return workflow is outside the current tenant.", error.Message);
        fixture.Returns.Verify(value => value.FirstOrDefaultAsync(It.IsAny<Expression<Func<PurchaseReturn, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task Finance_supplier_return_context_does_not_resolve_the_physical_inventory_owner()
    {
        var fixture = new Fixture();
        var context = await fixture.ContextAsync("SupplierReturn");

        Assert.Equal("SupplierReturn", context["entityType"]);
        Assert.False(context.ContainsKey("returnNumber"));
        Assert.False(context.ContainsKey("goodsReceiptNoteId"));
        fixture.Unit.Verify(value => value.Repository<PurchaseReturn>(), Times.Never);
    }

    [Theory]
    [InlineData("PurchaseReturn")]
    [InlineData("Purchase Return")]
    [InlineData("INVENTORY_SUPPLIER_RETURN")]
    public async Task Display_uses_inventory_register_and_physical_document_metadata(string alias)
    {
        var fixture = new Fixture();
        var result = await fixture.Display().GetEntityDisplayInfoAsync(alias, fixture.Return.Id);

        Assert.Equal("PurchaseReturn", result.EntityType);
        Assert.Equal(fixture.Return.Id, result.EntityId);
        Assert.Equal(fixture.Return.ReturnNumber, result.EntityNumber);
        Assert.Equal(fixture.Return.SupplierName, result.EntityName);
        Assert.Equal("/inventory/supplier-returns", result.ActionUrl);
        fixture.Unit.Verify(value => value.Repository<FinanceSupplierReturn>(), Times.Never);
        fixture.Unit.Verify(value => value.SaveChangesAsync(default), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Display_does_not_expose_metadata_from_a_deleted_or_different_return(bool deleted)
    {
        var fixture = new Fixture();
        var requestedId = fixture.Return.Id;
        if (deleted) fixture.Return.IsDeleted = true;
        else fixture.Return.Id = Guid.NewGuid();

        var result = await fixture.Display().GetEntityDisplayInfoAsync("PurchaseReturn", requestedId);

        Assert.Equal("/inventory/supplier-returns", result.ActionUrl);
        Assert.Null(result.EntityNumber);
        Assert.Null(result.EntityName);
    }

    [Fact]
    public async Task Existing_finance_display_does_not_alias_into_the_physical_return_register()
    {
        var fixture = new Fixture();
        var finance = new FinanceSupplierReturn { Id = fixture.Return.Id, ReturnNumber = "LEGACY-FIN-RETURN", VendorName = "Legacy supplier" };
        var repository = new Mock<IGenericRepository<FinanceSupplierReturn>>();
        repository.Setup(value => value.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<FinanceSupplierReturn, bool>>>(),
                It.IsAny<Expression<Func<FinanceSupplierReturn, object>>[]>()))
            .ReturnsAsync((Expression<Func<FinanceSupplierReturn, bool>> predicate, Expression<Func<FinanceSupplierReturn, object>>[] _) =>
                predicate.Compile()(finance) ? finance : null);
        fixture.Unit.Setup(value => value.Repository<FinanceSupplierReturn>()).Returns(repository.Object);

        var result = await fixture.Display().GetEntityDisplayInfoAsync("SupplierReturn", finance.Id);

        Assert.Equal("SupplierReturn", result.EntityType);
        Assert.Equal(finance.ReturnNumber, result.EntityNumber);
        Assert.Equal($"/finance/ap/returns/{finance.Id}", result.ActionUrl);
        Assert.NotEqual("/inventory/supplier-returns", result.ActionUrl);
        fixture.Unit.Verify(value => value.Repository<PurchaseReturn>(), Times.Never);
    }

    private sealed class Fixture
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IUnitOfWork> Unit { get; } = new();
        public Mock<IGenericRepository<PurchaseReturn>> Returns { get; } = new();
        public PurchaseReturn Return { get; }

        public Fixture()
        {
            Return = new PurchaseReturn
            {
                Id = Guid.NewGuid(), TenantId = Tenant, ReturnNumber = "RTN-UAT-WORKFLOW",
                Status = "Draft", SupplierName = "Physical receipt supplier", SupplierId = Guid.NewGuid(),
                WarehouseId = Guid.NewGuid(), GoodsReceiptNoteId = Guid.NewGuid(), PurchaseOrderId = Guid.NewGuid(),
                RequestedById = Guid.NewGuid(), TotalQuantity = 2m, TotalValue = 250m
            };
            User.SetupGet(value => value.TenantId).Returns(Tenant);
            Unit.Setup(value => value.Repository<PurchaseReturn>()).Returns(Returns.Object);
            Returns.Setup(value => value.FirstOrDefaultAsync(It.IsAny<Expression<Func<PurchaseReturn, bool>>>()))
                .ReturnsAsync((Expression<Func<PurchaseReturn, bool>> predicate) => predicate.Compile()(Return) ? Return : null);
        }

        public async Task<Dictionary<string, object>> ContextAsync(string alias, Guid? id = null)
        {
            var service = new SimpleWorkflowService(
                null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
                null!, null!, null!, null!, null!, null!, User.Object, null!, Unit.Object,
                NullLogger<SimpleWorkflowService>.Instance);
            var type = new WorkflowEntityType { Id = Guid.NewGuid(), TenantId = Tenant, Code = alias, Name = alias, IsActive = true };
            var method = typeof(SimpleWorkflowService).GetMethod("BuildEntityContextAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The central workflow context owner was not found.");
            return await (Task<Dictionary<string, object>>)method.Invoke(service, new object[] { type, id ?? Return.Id })!;
        }

        public WorkflowEntityDisplayService Display() => new(
            Unit.Object,
            Mock.Of<IPurchaseRequisitionRepository>(), Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<ITenderRepository>(), Mock.Of<IProcurementPlanRepository>(), Mock.Of<IProjectRepository>(),
            Mock.Of<IBusinessPartnerRepository>(), Mock.Of<IJobCardRepository>(),
            Mock.Of<IInventoryTransferRepository>(), Mock.Of<IInventoryRequisitionRepository>(),
            NullLogger<WorkflowEntityDisplayService>.Instance);
    }
}
