namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Quantity-only plan for one accepted receipt line. Callers must obtain the receipt,
/// posted invoice evidence and retained prior claims under the shared source locks.
/// This planner neither authorizes a return nor posts Inventory or AP.
/// </summary>
public static class SupplierReturnQuantityAllocation
{
    public sealed record PostedShare(Guid ReceiptAllocationId, Guid InvoiceId, Guid InvoiceLineId,
        DateTime PostedAt, decimal BaseQuantity, decimal PurchaseQuantity, decimal PreviouslyReturnedBaseQuantity);

    public sealed record Slice(Guid? ReceiptAllocationId, Guid? InvoiceId, Guid? InvoiceLineId,
        decimal BaseQuantity, decimal PurchaseQuantity);

    public static IReadOnlyList<Slice> Plan(decimal acceptedBaseQuantity, decimal purchaseToBase,
        decimal requestedBaseQuantity, decimal previouslyReturnedUninvoicedBaseQuantity,
        decimal unpostedInvoiceReservedBaseQuantity, IReadOnlyList<PostedShare> postedShares)
    {
        ArgumentNullException.ThrowIfNull(postedShares);
        RequireQuantity(acceptedBaseQuantity, false);
        RequireQuantity(requestedBaseQuantity, false);
        RequireQuantity(previouslyReturnedUninvoicedBaseQuantity, true);
        RequireQuantity(unpostedInvoiceReservedBaseQuantity, true);
        if (purchaseToBase <= 0m)
            throw Invalid("UNIT_CONVERSION", "The retained receipt unit conversion must be positive.");
        if (postedShares.Select(x => x.ReceiptAllocationId).Distinct().Count() != postedShares.Count)
            throw Invalid("DUPLICATE_INVOICE_SHARE", "Each original receipt allocation may appear only once.");

        foreach (var share in postedShares)
        {
            if (share.ReceiptAllocationId == Guid.Empty || share.InvoiceId == Guid.Empty || share.InvoiceLineId == Guid.Empty ||
                share.PostedAt == default)
                throw Invalid("INVOICE_EVIDENCE", "Every invoiced share requires exact posted invoice and receipt allocation evidence.");
            RequireQuantity(share.BaseQuantity, false);
            RequireQuantity(share.PurchaseQuantity, false);
            RequireQuantity(share.PreviouslyReturnedBaseQuantity, true);
            if (share.BaseQuantity != share.PurchaseQuantity * purchaseToBase ||
                share.PreviouslyReturnedBaseQuantity > share.BaseQuantity)
                throw Invalid("INVOICE_QUANTITY", "Posted and previously returned quantities must reconcile to the retained receipt units.");
        }

        var postedBase = postedShares.Sum(x => x.BaseQuantity);
        var uninvoiced = acceptedBaseQuantity - postedBase - previouslyReturnedUninvoicedBaseQuantity;
        if (uninvoiced < 0m || unpostedInvoiceReservedBaseQuantity > uninvoiced)
            throw Invalid("SOURCE_OVERCLAIMED", "Invoice and prior return claims exceed the accepted receipt quantity.");

        // Draft invoice reservations are not AP recognized. They retain their source
        // capacity and must be released explicitly before those units can be returned.
        uninvoiced -= unpostedInvoiceReservedBaseQuantity;
        var available = uninvoiced + postedShares.Sum(x => x.BaseQuantity - x.PreviouslyReturnedBaseQuantity);
        if (requestedBaseQuantity > available)
            throw Invalid("CAPACITY", "The return exceeds the remaining receipt quantity after invoice reservations and prior returns.");

        var result = new List<Slice>();
        var remaining = requestedBaseQuantity;
        if (uninvoiced > 0m)
        {
            var take = Math.Min(remaining, uninvoiced);
            result.Add(new Slice(null, null, null, take, ToPurchase(take, purchaseToBase)));
            remaining -= take;
        }
        foreach (var share in postedShares.OrderBy(x => x.PostedAt).ThenBy(x => x.InvoiceId).ThenBy(x => x.ReceiptAllocationId))
        {
            if (remaining == 0m) break;
            var take = Math.Min(remaining, share.BaseQuantity - share.PreviouslyReturnedBaseQuantity);
            if (take == 0m) continue;
            result.Add(new Slice(share.ReceiptAllocationId, share.InvoiceId, share.InvoiceLineId,
                take, ToPurchase(take, purchaseToBase)));
            remaining -= take;
        }
        if (remaining != 0m || result.Sum(x => x.BaseQuantity) != requestedBaseQuantity)
            throw Invalid("CONSERVATION", "Return allocation quantities do not reconcile.");
        return result;
    }

    private static decimal ToPurchase(decimal quantity, decimal conversion)
    {
        var purchase = quantity / conversion;
        if (decimal.Round(purchase, 4, MidpointRounding.AwayFromZero) != purchase || purchase * conversion != quantity)
            throw Invalid("UNIT_PRECISION", "Returned base quantity cannot be represented exactly in the original purchase units.");
        return purchase;
    }

    private static void RequireQuantity(decimal quantity, bool allowZero)
    {
        if (quantity < 0m || (!allowZero && quantity == 0m) || decimal.Round(quantity, 4, MidpointRounding.AwayFromZero) != quantity)
            throw Invalid("QUANTITY", "Receipt, invoice and return quantities must be nonnegative and have at most four decimal places.");
    }

    private static InvalidOperationException Invalid(string code, string message) => new($"RTV_ALLOCATION_{code}: {message}");
}
