using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSodPolicyTests
{
    [Fact]
    public async Task ProcurementSupplierReturnsHonorSwitchWhileStandaloneInventoryReturnsRemainSeparated()
    {
        using var f = new Fixture();
        await f.DisableAsync();
        var order = new PurchaseOrder { TenantId = f.TenantId, OrderNumber = "RETURN-PO" };
        var linked = new PurchaseReturn { TenantId = f.TenantId, PurchaseOrderId = order.Id, ReturnNumber = "LINKED" };
        var standalone = new PurchaseReturn { TenantId = f.TenantId, ReturnNumber = "STANDALONE" };
        f.Context.AddRange(order, linked, standalone);
        await f.Context.SaveChangesAsync();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "PurchaseReturn", linked.Id)).Should().BeFalse();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "PurchaseReturn", standalone.Id)).Should().BeTrue();
        (await f.Policy.IsRequiredForSourceAsync(Guid.NewGuid(), "PurchaseReturn", linked.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task MissingConfigurationAndOtherTenantKeepSeparationEnabled()
    {
        using var f = new Fixture();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "PurchaseOrder", Guid.NewGuid())).Should().BeTrue();
        f.Context.Add(new ProcurementSettings { TenantId = Guid.NewGuid(), EnforceSegregationOfDuties = false });
        await f.Context.SaveChangesAsync();
        (await f.Policy.IsEnabledAsync(f.TenantId)).Should().BeTrue();
        (await f.Policy.IsEnabledAsync(Guid.Empty)).Should().BeTrue();
    }

    [Theory]
    [InlineData("PurchaseOrder", false)]
    [InlineData("PurchaseRequisition", false)]
    [InlineData("GoodsReceiptNote", false)]
    [InlineData("ProcurementReceiptInspection", false)]
    [InlineData("TenderAward", false)]
    [InlineData("VendorInvoice", true)]
    [InlineData("VendorPayment", true)]
    [InlineData("PaymentBatch", true)]
    [InlineData("LeaveRequest", true)]
    [InlineData("StockAdjustment", true)]
    [InlineData("Unknown", true)]
    public async Task DisabledSwitchIsLimitedToKnownProcurementSources(string source, bool expected)
    {
        using var f = new Fixture();
        await f.DisableAsync();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, source, null)).Should().Be(expected);
    }

    [Fact]
    public async Task InvoiceRequiresTenantOwnedProcurementLineage()
    {
        using var f = new Fixture();
        await f.DisableAsync();
        var po = new ErpSystem.Core.Entities.Procurement.PurchaseOrder { Id = Guid.NewGuid(), TenantId = f.TenantId, OrderNumber = "PO-SOD" };
        f.Context.Add(po);
        var procurement = f.Invoice(po.Id);
        var manual = f.Invoice();
        var foreignPo = new ErpSystem.Core.Entities.Procurement.PurchaseOrder { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OrderNumber = "PO-OTHER" };
        f.Context.Add(foreignPo);
        var foreign = f.Invoice(foreignPo.Id);
        await f.Context.SaveChangesAsync();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "VendorInvoice", procurement.Id)).Should().BeFalse();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "VendorInvoice", manual.Id)).Should().BeTrue();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "VendorInvoice", foreign.Id)).Should().BeTrue();
        (await f.Policy.IsRequiredForSourceAsync(Guid.NewGuid(), "VendorInvoice", procurement.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task MixedPaymentRemainsEnforcedUntilManualAllocationIsReversed()
    {
        using var f = new Fixture();
        await f.DisableAsync();
        var po = new ErpSystem.Core.Entities.Procurement.PurchaseOrder { Id = Guid.NewGuid(), TenantId = f.TenantId, OrderNumber = "PO-SOD" };
        f.Context.Add(po);
        var paymentId = Guid.NewGuid();
        var procurement = f.Invoice(po.Id);
        var manual = f.Invoice();
        var manualAllocation = new VendorPaymentAllocation { Id = Guid.NewGuid(), TenantId = f.TenantId, VendorPaymentId = paymentId, VendorInvoiceId = manual.Id, AllocatedAmount = 10m };
        f.Context.AddRange(manualAllocation, new VendorPaymentAllocation { TenantId = f.TenantId, VendorPaymentId = paymentId, VendorInvoiceId = procurement.Id, AllocatedAmount = 10m });
        await f.Context.SaveChangesAsync();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "VendorPayment", paymentId)).Should().BeTrue();
        f.Context.Add(new VendorPaymentAllocation { TenantId = f.TenantId, VendorPaymentId = paymentId, VendorInvoiceId = manual.Id, AllocatedAmount = -10m, IsReversal = true, OriginalAllocationId = manualAllocation.Id });
        await f.Context.SaveChangesAsync();
        (await f.Policy.IsRequiredForSourceAsync(f.TenantId, "VendorPayment", paymentId)).Should().BeFalse();
    }

    private sealed class Fixture : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        private readonly UnitOfWork _unit;
        public ProcurementSodPolicy Policy { get; }
        public Fixture() { _unit = new UnitOfWork(Context); Policy = new ProcurementSodPolicy(_unit); }
        public async Task DisableAsync()
        {
            Context.Add(new ProcurementSettings { TenantId = TenantId, EnforceSegregationOfDuties = false });
            await Context.SaveChangesAsync();
        }
        public VendorInvoice Invoice(Guid? purchaseOrderId = null)
        {
            var invoice = new VendorInvoice { Id = Guid.NewGuid(), TenantId = TenantId, InvoiceNumber = $"INV-{Guid.NewGuid():N}", SupplierName = "Supplier", PurchaseOrderId = purchaseOrderId };
            Context.Add(invoice);
            return invoice;
        }
        public void Dispose() { _unit.Dispose(); Context.Dispose(); }
    }
}
