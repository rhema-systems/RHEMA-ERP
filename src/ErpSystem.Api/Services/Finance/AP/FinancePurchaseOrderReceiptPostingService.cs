using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public sealed class FinancePurchaseOrderReceiptPostingService
{
    private const int ApprovedPurchaseOrder = 2;
    private const int PartiallyReceivedPurchaseOrder = 3;
    private const int ReceivedPurchaseOrder = 4;
    private const int PartiallyInvoicedPurchaseOrder = 5;
    private const int InvoicedPurchaseOrder = 6;
    private const string PostedStatus = "Posted";

    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFinancePostingEngine _financePostingEngine;

    public FinancePurchaseOrderReceiptPostingService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IFinancePostingEngine financePostingEngine)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _financePostingEngine = financePostingEngine;
    }

    public async Task ApproveAndPostAsync(
        Guid tenantId,
        Guid receiptId,
        Guid approvedById,
        string? approvedByName,
        string? approvalComments,
        CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptForPostingAsync(tenantId, receiptId, cancellationToken)
            ?? throw new InvalidOperationException("Finance purchase receipt was not found for this tenant.");

        if (!receipt.ApprovalRequired)
            throw new InvalidOperationException("This finance receipt does not require an approval action.");

        if (receipt.Status == FinancePurchaseOrderReceiptStatus.Rejected)
        {
            throw new InvalidOperationException("A rejected finance GRV cannot be approved and posted.");
        }

        var now = DateTime.UtcNow;
        receipt.Status = FinancePurchaseOrderReceiptStatus.Approved;
        receipt.ApprovedAt = now;
        receipt.ApprovedById = approvedById;
        receipt.RejectedAt = null;
        receipt.RejectedById = null;
        receipt.RejectionReason = null;
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = approvedByName ?? _currentUserService.UserName ?? "system";

        if (!string.IsNullOrWhiteSpace(approvalComments))
        {
            receipt.Remarks = AppendNote(receipt.Remarks, $"Approval comments: {approvalComments.Trim()}");
        }

        if (await HasPostedReceiptAsync(tenantId, receiptId, cancellationToken))
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await PostReceiptThroughFinancePostingEngineAsync(receipt, tenantId, cancellationToken);

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RejectAsync(
        Guid tenantId,
        Guid receiptId,
        Guid rejectedById,
        string? rejectedByName,
        string? reason,
        CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptForPostingAsync(tenantId, receiptId, cancellationToken);
        if (receipt == null)
        {
            return;
        }

        if (!receipt.ApprovalRequired)
            throw new InvalidOperationException("This finance receipt does not require an approval action.");

        if (receipt.Status == FinancePurchaseOrderReceiptStatus.Approved)
        {
            throw new InvalidOperationException("An approved finance GRV cannot be rejected through workflow.");
        }

        var now = DateTime.UtcNow;
        var shouldReleaseReceivedQuantities = receipt.Status != FinancePurchaseOrderReceiptStatus.Rejected;

        if (shouldReleaseReceivedQuantities)
        {
            foreach (var receiptItem in receipt.Items.Where(i => !i.IsDeleted))
            {
                var poItem = receiptItem.FinancePurchaseOrderItem;
                if (poItem == null)
                {
                    continue;
                }

                poItem.ReceivedQuantity = Math.Max(0m, poItem.ReceivedQuantity - receiptItem.QuantityReceived);
                poItem.UpdatedAt = now;
                poItem.UpdatedBy = rejectedByName ?? _currentUserService.UserName ?? "system";
            }

            RecalculatePurchaseOrderStatus(receipt.FinancePurchaseOrder);
            receipt.FinancePurchaseOrder.UpdatedAt = now;
            receipt.FinancePurchaseOrder.UpdatedBy = rejectedByName ?? _currentUserService.UserName ?? "system";
        }

        receipt.Status = FinancePurchaseOrderReceiptStatus.Rejected;
        receipt.RejectedAt = now;
        receipt.RejectedById = rejectedById;
        receipt.RejectionReason = reason;
        receipt.UpdatedAt = now;
        receipt.UpdatedBy = rejectedByName ?? _currentUserService.UserName ?? "system";
        receipt.Remarks = AppendNote(receipt.Remarks, string.IsNullOrWhiteSpace(reason) ? "Rejected." : $"Rejected: {reason.Trim()}");

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteWithoutApprovalAndPostAsync(
        Guid tenantId, Guid receiptId, Guid submittedById, CancellationToken cancellationToken)
    {
        if (tenantId != _currentUserService.GetRequiredFinanceTenantId() || submittedById == Guid.Empty ||
            !Guid.TryParse(_currentUserService.UserId, out var actorId) || actorId != submittedById)
            throw new UnauthorizedAccessException("An authenticated submitting user in this tenant is required.");
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Direct finance receipt completion requires its submission transaction.");
        var receipt = await LoadReceiptForPostingAsync(tenantId, receiptId, cancellationToken)
            ?? throw new InvalidOperationException("Finance purchase receipt was not found for this tenant.");
        if (receipt.ApprovalRequired || receipt.Status != FinancePurchaseOrderReceiptStatus.Draft ||
            receipt.WorkflowInstanceId.HasValue || receipt.ApprovedById.HasValue || receipt.ApprovedAt.HasValue ||
            receipt.SubmittedById != submittedById || !receipt.SubmittedAt.HasValue)
            throw new InvalidOperationException("Only a validated direct submission can complete without approval.");

        // Approved is the retained business-ready state used by AP invoice conversion.
        // ApprovalRequired=false distinguishes completion from a human approval.
        receipt.Status = FinancePurchaseOrderReceiptStatus.Approved;
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = _currentUserService.UserName;
        if (!await HasPostedReceiptAsync(tenantId, receiptId, cancellationToken))
            await PostReceiptThroughFinancePostingEngineAsync(receipt, tenantId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<FinancePurchaseOrderReceipt?> LoadReceiptForPostingAsync(
        Guid tenantId,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.FinancePurchaseOrderReceipts
            .AsTracking()
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.Vendor)
            .Include(r => r.FinancePurchaseOrder)
                .ThenInclude(po => po.Items.Where(i => !i.IsDeleted))
            .Include(r => r.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.FinancePurchaseOrderItem)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == receiptId && !r.IsDeleted, cancellationToken);
    }

    private async Task<bool> HasPostedReceiptAsync(Guid tenantId, Guid receiptId, CancellationToken cancellationToken)
    {
        return await _dbContext.FinancePostingEvents
            .AsNoTracking()
            .AnyAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.SourceDocumentType == "FinancePurchaseOrderReceipt" &&
                e.SourceDocumentId == receiptId &&
                e.PostingAction == "PostFinancePurchaseOrderReceipt" &&
                e.PostingStatus == PostedStatus,
                cancellationToken);
    }

    private async Task PostReceiptThroughFinancePostingEngineAsync(
        FinancePurchaseOrderReceipt receipt,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings not configured for this tenant.");

        var grvAccrualAccountId = settings.ControlAccountGRVAccrualId
            ?? throw new InvalidOperationException("GRV Accrual Control Account is not configured in Finance Settings.");

        var purchaseOrder = receipt.FinancePurchaseOrder;
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var headerCurrencyCode = NormalizeCurrency(purchaseOrder.CurrencyCode, functionalCurrency);
        var headerExchangeRate = NormalizeExchangeRate(purchaseOrder.ExchangeRate);
        var lines = new List<FinancePostingLineDto>();
        var lineNumber = 1;

        foreach (var receiptItem in receipt.Items.Where(i => !i.IsDeleted).OrderBy(i => i.CreatedAt).ThenBy(i => i.Id))
        {
            var poItem = receiptItem.FinancePurchaseOrderItem
                ?? throw new InvalidOperationException($"Finance GRV line {receiptItem.Id} is missing its purchase order line.");

            var grossAmount = RoundMoney(receiptItem.QuantityReceived * poItem.UnitPrice);
            var percentageDiscount = RoundMoney(grossAmount * receiptItem.DiscountPercentage / 100m);
            var lineSourceAmount = RoundMoney(grossAmount - receiptItem.DiscountAmount - percentageDiscount);
            if (lineSourceAmount <= 0m)
            {
                continue;
            }

            var debitAccountId = poItem.LineType == 1
                ? settings.ControlAccountInventoryId
                    ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.")
                : poItem.GlAccountId
                    ?? throw new InvalidOperationException($"No GL account specified for finance GRV line '{poItem.Description}'.");

            var lineCurrencyCode = NormalizeCurrency(poItem.CurrencyCode, headerCurrencyCode);
            var lineExchangeRate = NormalizeExchangeRate(poItem.ExchangeRate > 0m ? poItem.ExchangeRate : headerExchangeRate);
            var lineFunctionalAmount = ToFunctionalAmount(lineSourceAmount, lineCurrencyCode, functionalCurrency, lineExchangeRate);

            lines.Add(BuildPostingLine(
                debitAccountId,
                $"GRV {receipt.ReceiptNumber} - {poItem.Description}",
                lineFunctionalAmount,
                0m,
                lineSourceAmount,
                lineCurrencyCode,
                functionalCurrency,
                lineExchangeRate,
                receipt.ReceiptDate,
                receipt.ReceiptNumber,
                lineNumber++,
                "AP-GRV-Receipt"));

            lines.Add(BuildPostingLine(
                grvAccrualAccountId,
                $"GRV accrual - {receipt.ReceiptNumber} - {poItem.Description}",
                0m,
                lineFunctionalAmount,
                lineSourceAmount,
                lineCurrencyCode,
                functionalCurrency,
                lineExchangeRate,
                receipt.ReceiptDate,
                receipt.ReceiptNumber,
                lineNumber++,
                "AP-GRV-Accrual"));
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"Finance GRV {receipt.ReceiptNumber} has no positive-value lines to post.");
        }

        await _financePostingEngine.PostAsync(new FinancePostingRequestDto
        {
            SourceModule = "AP",
            SourceDocumentType = "FinancePurchaseOrderReceipt",
            SourceDocumentId = receipt.Id,
            SourceDocumentTenantId = tenantId,
            PostingAction = "PostFinancePurchaseOrderReceipt",
            SourceDocumentReference = receipt.ReceiptNumber,
            Description = $"Finance GRV {receipt.ReceiptNumber} - {purchaseOrder.Vendor?.PartnerName ?? purchaseOrder.OrderNumber}",
            PostingDate = receipt.ReceiptDate,
            JournalType = "System Generated",
            BookClassification = "IFRS",
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FinancePurchaseOrderReceipt:{tenantId:N}:{receipt.Id:N}:Post",
            Lines = lines
        }, cancellationToken);
    }

    private static void RecalculatePurchaseOrderStatus(FinancePurchaseOrder purchaseOrder)
    {
        var activeItems = purchaseOrder.Items.Where(i => !i.IsDeleted).ToList();
        if (activeItems.Count == 0)
        {
            purchaseOrder.Status = ApprovedPurchaseOrder;
            return;
        }

        if (activeItems.All(i => i.InvoicedQuantity >= i.OrderedQuantity - i.CancelledQuantity))
        {
            purchaseOrder.Status = InvoicedPurchaseOrder;
            return;
        }

        if (activeItems.Any(i => i.InvoicedQuantity > 0m))
        {
            purchaseOrder.Status = PartiallyInvoicedPurchaseOrder;
            return;
        }

        if (activeItems.All(i => i.OrderedQuantity - i.ReceivedQuantity - i.CancelledQuantity <= 0m))
        {
            purchaseOrder.Status = ReceivedPurchaseOrder;
            return;
        }

        purchaseOrder.Status = activeItems.Any(i => i.ReceivedQuantity > 0m)
            ? PartiallyReceivedPurchaseOrder
            : ApprovedPurchaseOrder;
    }

    private static FinancePostingLineDto BuildPostingLine(
        Guid accountId,
        string description,
        decimal debitAmount,
        decimal creditAmount,
        decimal sourceAmount,
        string transactionCurrency,
        string functionalCurrency,
        decimal exchangeRate,
        DateTime rateDate,
        string reference,
        int lineNumber,
        string tag)
    {
        var sameCurrency = string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            Description = description,
            DebitAmount = RoundMoney(debitAmount),
            CreditAmount = RoundMoney(creditAmount),
            TransactionCurrency = transactionCurrency,
            TransactionDebitAmount = debitAmount > 0m ? RoundMoney(sourceAmount) : 0m,
            TransactionCreditAmount = creditAmount > 0m ? RoundMoney(sourceAmount) : 0m,
            ForeignCurrencyAmount = sameCurrency ? null : RoundMoney(sourceAmount),
            ExchangeRate = sameCurrency ? null : exchangeRate,
            ExchangeRateDate = sameCurrency ? null : rateDate.Date,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            TransactionTag = tag
        };
    }

    private static decimal ToFunctionalAmount(decimal sourceAmount, string transactionCurrency, string functionalCurrency, decimal exchangeRate)
        => string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            ? RoundMoney(sourceAmount)
            : RoundMoney(sourceAmount * exchangeRate);

    private static decimal NormalizeExchangeRate(decimal exchangeRate)
        => exchangeRate <= 0m ? 1m : exchangeRate;

    private static string NormalizeCurrency(string? currencyCode, string fallback)
        => string.IsNullOrWhiteSpace(currencyCode)
            ? fallback.Trim().ToUpperInvariant()
            : currencyCode.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string AppendNote(string? existing, string note)
        => string.IsNullOrWhiteSpace(existing)
            ? note
            : $"{existing}{Environment.NewLine}{note}";
}
