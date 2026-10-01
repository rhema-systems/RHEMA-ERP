using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Ehc;

/// <summary>
/// Bridges the public-enquiry deposit lifecycle into canonical AR settlement. The prospect receipt
/// remains a prospect liability until customer conversion; this service only consumes the posted
/// customer-advance record created by that conversion.
/// </summary>
public sealed class PropertyEnquiryDepositApplicationService(
    ApplicationDbContext db,
    ICurrentUserProvider currentUser,
    IPaymentService payments,
    ILogger<PropertyEnquiryDepositApplicationService> logger)
    : IPropertyEnquiryDepositApplicationService
{
    public async Task<ProspectDepositApplicationResult> ApplyToPostedSalesInvoiceAsync(
        Guid salesOrderId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to apply a prospect deposit.");

        var order = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == salesOrderId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Sales order not found in the current tenant.");
        if (order.InvoiceId != invoiceId)
            throw new InvalidOperationException("The posted invoice is not the invoice retained by this Sales order.");
        if (!order.OpportunityId.HasValue)
            return ProspectDepositApplicationResult.None;

        var prospect = await db.Set<EhcPropertyEnquiryProspect>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId
                && item.OpportunityId == order.OpportunityId.Value
                && !item.IsDeleted, cancellationToken);
        if (prospect is null)
            return ProspectDepositApplicationResult.None;

        // Public money remains in the prospect-deposit liability until an approved customer exists.
        // An opportunity or Sales order alone must never make it allocatable in AR.
        if (!string.Equals(prospect.Status, EhcPropertyProspectStatuses.Converted, StringComparison.Ordinal)
            || !prospect.BusinessPartnerId.HasValue)
            return ProspectDepositApplicationResult.None;
        if (prospect.BusinessPartnerId.Value != order.BusinessPartnerId)
            throw new InvalidOperationException("The converted prospect customer does not match the linked Sales order customer.");

        var approvedCustomer = await db.BusinessPartners.AsNoTracking().AnyAsync(item =>
            item.Id == order.BusinessPartnerId
            && item.TenantId == tenantId
            && !item.IsDeleted
            && item.IsActive
            && item.ApprovalStatus == "Approved", cancellationToken);
        if (!approvedCustomer)
            throw new InvalidOperationException("Complete Customer Business Partner approval before applying the prospect deposit.");

        var invoice = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == invoiceId
            && item.TenantId == tenantId
            && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The Sales order invoice was not found in the current tenant.");
        if (invoice.BusinessPartnerId != order.BusinessPartnerId)
            throw new InvalidOperationException("The Sales invoice customer does not match the converted prospect customer.");
        if (!invoice.JournalEntryId.HasValue)
            throw new InvalidOperationException("Post the Sales invoice before applying the prospect deposit.");

        var receipts = await db.Set<ProspectDepositReceipt>().AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.ProspectId == prospect.Id
                && item.OpportunityId == order.OpportunityId.Value
                && item.BusinessPartnerId == order.BusinessPartnerId
                && item.Status == ProspectDepositReceiptStatuses.Cleared
                && !item.IsDeleted
                && !item.ReversedAt.HasValue
                && item.TransferredToCustomerAdvanceAt.HasValue
                && item.CustomerPaymentId.HasValue)
            .OrderBy(item => item.ReceivedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (receipts.Count == 0)
            return ProspectDepositApplicationResult.None;

        var paymentIds = receipts.Select(item => item.CustomerPaymentId!.Value).Distinct().ToArray();
        var customerAdvances = await db.Set<CustomerPayment>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && paymentIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var appliedReceiptCount = 0;
        var appliedAmount = 0m;
        foreach (var receipt in receipts)
        {
            var paymentId = receipt.CustomerPaymentId!.Value;
            if (!customerAdvances.TryGetValue(paymentId, out var advance))
                throw new InvalidOperationException($"Customer advance for prospect receipt {receipt.ReceiptNumber} was not found.");
            if (advance.BusinessPartnerId != order.BusinessPartnerId
                || !advance.IsCustomerAdvance
                || !advance.JournalEntryId.HasValue
                || advance.ReversedAt.HasValue
                || advance.Status is "Cancelled" or "Bounced")
            {
                throw new InvalidOperationException($"Prospect receipt {receipt.ReceiptNumber} is not a valid posted customer advance.");
            }
            if (!string.Equals(advance.CurrencyCode, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Prospect advance {receipt.ReceiptNumber} is in {advance.CurrencyCode}, but the linked Sales invoice is in {invoice.CurrencyCode}. Correct the Sales currency before applying the deposit.");
            }

            var outstanding = (await payments.GetOutstandingInvoicesAsync(order.BusinessPartnerId, cancellationToken))
                .SingleOrDefault(item => item.Id == invoiceId);
            if (outstanding is null || outstanding.BalanceAmount <= 0m)
                break;

            var available = decimal.Round(advance.TotalAmount - advance.AllocatedAmount, 2, MidpointRounding.AwayFromZero);
            if (available <= 0m)
                continue;
            var amount = Math.Min(available, outstanding.BalanceAmount);
            var result = await payments.AllocatePaymentAsync(new PaymentAllocation_CreateDto
            {
                CustomerPaymentId = paymentId,
                Allocations =
                [
                    new InvoiceAllocationDto
                    {
                        InvoiceId = invoiceId,
                        AllocatedAmount = amount,
                        PaymentCurrencyAmount = amount,
                        Notes = $"Apply public property prospect deposit {receipt.ReceiptNumber} to Sales order {order.DocumentNumber}."
                    }
                ]
            }, cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? $"Prospect receipt {receipt.ReceiptNumber} could not be applied to the Sales invoice.");

            appliedReceiptCount++;
            appliedAmount += amount;
        }

        if (appliedReceiptCount > 0)
        {
            logger.LogInformation(
                "Applied {ReceiptCount} converted property prospect deposit receipt(s), total {Amount}, to Sales order {SalesOrderId} invoice {InvoiceId}",
                appliedReceiptCount,
                appliedAmount,
                salesOrderId,
                invoiceId);
        }

        return new ProspectDepositApplicationResult(appliedReceiptCount, appliedAmount);
    }
}
