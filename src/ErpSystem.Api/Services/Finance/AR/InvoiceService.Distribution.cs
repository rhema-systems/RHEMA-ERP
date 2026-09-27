using ErpSystem.Api.Services.Inventory;
using ErpSystem.Api.Services.Sales;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AR;

public partial class InvoiceService
{
    public async Task<InvoiceDistributionDto> GetDistributionPreviewAsync(Guid id,
        FinancePostingProducerContext producer, CancellationToken cancellationToken = default)
    {
        EnsureCustomerInvoiceRoute(producer);
        var invoice = await _unitOfWork.Repository<Invoice>().GetQueryable(value =>
                value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
            .AsNoTracking().Include(value => value.LineItems)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The invoice was not found in the current tenant.");
        await InventoryDisposalAuctionInvoiceGuard.ValidateAsync(_unitOfWork, TenantId, invoice, producer, cancellationToken);
        await SalesOrderInvoiceGuard.ValidateAsync(_unitOfWork, TenantId, invoice, producer, cancellationToken);
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cancelled invoices do not have a current posting distribution.");
        if (invoice.JournalEntryId.HasValue)
        {
            var journal = await _unitOfWork.Repository<JournalEntry>().GetQueryable(value =>
                    value.TenantId == TenantId && value.Id == invoice.JournalEntryId && !value.IsDeleted)
                .AsNoTracking().Include(value => value.Transactions).ThenInclude(value => value.Account)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The invoice's original journal could not be verified.");
            if (journal.SourceDocumentId != invoice.Id || journal.SourceDocumentType != "CustomerInvoice" || journal.PostingStatus != "Posted")
                throw new InvalidOperationException("The invoice's original posting lineage is invalid.");
            var retained = journal.Transactions.Where(value => !value.IsDeleted).OrderBy(value => value.LineNumber).ToArray();
            var currencies = retained.Select(value => value.FunctionalCurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (currencies.Length != 1 || retained.Any(value => value.TenantId != TenantId || value.Account.TenantId != TenantId))
                throw new InvalidOperationException("The original journal currency or account lineage is invalid.");
            return Distribution(invoice.Id, currencies[0], false, retained.Select(value => new InvoiceDistributionLineDto
            {
                AccountId = value.AccountId, AccountCode = value.Account.AccountCode, AccountName = value.Account.AccountName,
                Description = value.Description ?? string.Empty, Debit = value.DebitAmount, Credit = value.CreditAmount,
                SourceLineId = value.SourceDocumentLineId
            }).ToArray());
        }
        var stockLines = invoice.LineItems.Where(value => !value.IsDeleted && value.LineItemType == LineItemType.Inventory)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id).ToArray();
        if (stockLines.Any(value => !value.InventoryItemId.HasValue || !value.WarehouseId.HasValue || value.Quantity <= 0))
            throw new InvalidOperationException("Every stock invoice line requires its exact inventory item, warehouse and positive quantity.");
        if (stockLines.Length != 0)
        {
            if (producer.RouteId == FinanceDimensionRouteId.SalesOrderCustomerInvoice)
                foreach (var line in stockLines)
                    await RequireSalesStockTracking().ValidateAsync(SalesTrackingRequest(invoice, line), cancellationToken);
            var costs = await _inventoryValuationService.PreviewIssueCostsAsync(stockLines.Select(value =>
                new InventoryIssueCostPreviewLineDto
                {
                    LineId = value.Id, InventoryItemId = value.InventoryItemId!.Value, WarehouseId = value.WarehouseId!.Value,
                    LocationId = value.LocationId, Quantity = value.Quantity, LotNumber = value.LotNumber, SerialNumber = value.SerialNumber
                }).ToArray(), cancellationToken);
            foreach (var line in stockLines)
            {
                if (!costs.TryGetValue(line.Id, out var cost) || cost < 0)
                    throw new InvalidOperationException("Inventory could not produce a cost preview for every invoice line.");
                line.CostTotal = cost;
                line.UnitCost = cost / line.Quantity;
            }
        }
        // This is an untracked projection. It never changes the persisted invoice lifecycle.
        invoice.Status = InvoiceStatus.Draft;
        var posting = await BuildArInvoicePostingRequestAsync(invoice, true, producer, cancellationToken);
        var ids = posting.Lines.Select(value => value.AccountId).Distinct().ToArray();
        var accounts = await _unitOfWork.Repository<Account>().GetQueryable(value => value.TenantId == TenantId && ids.Contains(value.Id))
            .AsNoTracking().ToDictionaryAsync(value => value.Id, cancellationToken);
        return Distribution(invoice.Id, posting.FunctionalCurrencyCode, true, posting.Lines.Select(value => new InvoiceDistributionLineDto
        {
            AccountId = value.AccountId, AccountCode = accounts[value.AccountId].AccountCode, AccountName = accounts[value.AccountId].AccountName,
            Description = value.Description ?? string.Empty, Debit = value.DebitAmount, Credit = value.CreditAmount,
            SourceLineId = value.SourceDocumentLineId
        }).ToArray());
    }

    private IInventoryTrackingControlService RequireSalesStockTracking() => _inventoryTracking
        ?? throw new InvalidOperationException("Inventory tracking and warehouse authorization are not configured for Sales stock posting.");

    private static InventoryTrackingMutationRequest SalesTrackingRequest(Invoice invoice, InvoiceLineItem line) => new()
    {
        InventoryItemId = line.InventoryItemId!.Value, WarehouseId = line.WarehouseId!.Value, LocationId = line.LocationId,
        Quantity = line.Quantity, Direction = InventoryTrackingDirection.Issue, ReferenceType = "SalesInvoice",
        ReferenceNumber = invoice.InvoiceNumber, ReferenceId = invoice.Id, ReferenceLineId = line.Id,
        EventKey = $"SALES:INVOICE:ISSUE:{invoice.Id:N}:{line.Id:N}",
        LotNumber = line.LotNumber, SerialNumber = line.SerialNumber, ExpiryDate = line.ExpirationDate,
        CorrelationId = $"SALES:INVOICE:{invoice.Id:N}"
    };

    private static InvoiceDistributionDto Distribution(Guid invoiceId, string currency, bool estimated,
        IReadOnlyList<InvoiceDistributionLineDto> lines) => new()
    {
        InvoiceId = invoiceId, CurrencyCode = currency, IsEstimated = estimated, Lines = lines,
        TotalDebit = lines.Sum(value => value.Debit), TotalCredit = lines.Sum(value => value.Credit)
    };
}
