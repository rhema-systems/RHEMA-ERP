using ErpSystem.Core.Services.Inventory;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class SupplierReturnQuantityAllocationTests
{
    private static SupplierReturnQuantityAllocation.PostedShare Share(decimal quantity, decimal returned = 0m,
        decimal conversion = 1m, int day = 1) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc), quantity, quantity / conversion, returned);

    [Fact]
    public void Partially_invoiced_receipt_returns_forty_uninvoiced_and_ten_invoiced()
    {
        var posted = Share(60);
        var result = SupplierReturnQuantityAllocation.Plan(100, 1, 50, 0, 0, [posted]);
        Assert.Collection(result,
            item => { Assert.Null(item.InvoiceId); Assert.Equal(40, item.BaseQuantity); },
            item => { Assert.Equal(posted.ReceiptAllocationId, item.ReceiptAllocationId); Assert.Equal(10, item.BaseQuantity); });
    }

    [Fact]
    public void Multiple_invoice_shares_use_oldest_posting_then_stable_identity()
    {
        var first = Share(20, day: 1);
        var later = Share(40, day: 2);
        var result = SupplierReturnQuantityAllocation.Plan(100, 1, 75, 0, 0, [later, first]);
        Assert.Equal(new decimal[] { 40, 20, 15 }, result.Select(x => x.BaseQuantity));
        Assert.Equal(first.InvoiceId, result[1].InvoiceId);
        Assert.Equal(later.InvoiceId, result[2].InvoiceId);
    }

    [Fact]
    public void Prior_returns_cannot_be_claimed_again_and_draft_invoice_is_not_posted_ap()
    {
        var posted = Share(60, returned: 10);
        var result = SupplierReturnQuantityAllocation.Plan(100, 1, 50, 20, 10, [posted]);
        Assert.Equal(new decimal[] { 10, 40 }, result.Select(x => x.BaseQuantity));
        Assert.Null(result[0].InvoiceId);
        Assert.Equal(posted.InvoiceId, result[1].InvoiceId);
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 61, 20, 10, [posted]));
    }

    [Fact]
    public void Pure_uninvoiced_and_pure_invoiced_receipts_keep_different_lineage()
    {
        Assert.Null(Assert.Single(SupplierReturnQuantityAllocation.Plan(100, 1, 50, 0, 0, [])).InvoiceId);
        var share = Share(100);
        Assert.Equal(share.InvoiceLineId,
            Assert.Single(SupplierReturnQuantityAllocation.Plan(100, 1, 50, 0, 0, [share])).InvoiceLineId);
    }

    [Fact]
    public void Retained_conversion_controls_purchase_quantities()
    {
        var result = SupplierReturnQuantityAllocation.Plan(100, 10, 50, 0, 0, [Share(60, conversion: 10)]);
        Assert.Equal(new decimal[] { 4, 1 }, result.Select(x => x.PurchaseQuantity));
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 3, 1, 0, 0, []));
    }

    [Fact]
    public void Duplicate_original_allocation_or_missing_posting_evidence_is_rejected()
    {
        var share = Share(30);
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 50, 0, 0, [share, share]));
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 50, 0, 0,
            [share with { PostedAt = default }]));
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(60, 41, 0)]
    [InlineData(60, 0, 41)]
    public void Overclaimed_receipt_is_rejected(decimal posted, decimal returned, decimal reserved) =>
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 1, returned, reserved, [Share(posted)]));

    [Fact]
    public void Invalid_quantity_and_overreturned_invoice_share_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 0, 0, 0, []));
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 0.00001m, 0, 0, []));
        Assert.Throws<InvalidOperationException>(() => SupplierReturnQuantityAllocation.Plan(100, 1, 1, 0, 0, [Share(60, returned: 61)]));
    }
}
