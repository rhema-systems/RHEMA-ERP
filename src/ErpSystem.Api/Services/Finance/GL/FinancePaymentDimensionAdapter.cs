using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Loads trusted Finance AP/AR aggregates and adapts them to exact invoice-line settlement
/// evidence. It never accepts originating ids, dimension sets, snapshots, or rates from a browser.
/// </summary>
public sealed class FinancePaymentDimensionAdapter : IFinancePaymentDimensionAdapter
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceSettlementDimensionService _settlements;

    public FinancePaymentDimensionAdapter(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IFinanceSettlementDimensionService settlements)
    {
        _db = db;
        _currentUser = currentUser;
        _settlements = settlements;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Set<VendorPayment>()
            .Include(item => item.Allocations).ThenInclude(item => item.VendorInvoice)
                .ThenInclude(item => item.LineItems)
            .SingleOrDefaultAsync(item => item.Id == vendorPaymentId
                && item.TenantId == TenantId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Vendor payment was not found for this tenant.");
        var allocations = EffectiveVendorAllocations(payment.Allocations);
        var inputs = await BuildVendorInputsAsync(payment, allocations, cancellationToken);
        return await _settlements.SynchronizeDraftAsync(
            Producer(FinanceDimensionRouteId.FinanceApVendorPayment), payment.Id, inputs, cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default) =>
        await SynchronizeCustomerPaymentAsync(
            customerPaymentId, Producer(FinanceDimensionRouteId.FinanceArCustomerPayment), cancellationToken);

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeCustomerPaymentAsync(
        Guid customerPaymentId,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerPaymentRoute(producer);
        var payment = await _db.Set<CustomerPayment>()
            .Include(item => item.Allocations).ThenInclude(item => item.Invoice)
                .ThenInclude(item => item.LineItems)
            .SingleOrDefaultAsync(item => item.Id == customerPaymentId
                && item.TenantId == TenantId && !item.IsDeleted && !item.IsCreditNote,
                cancellationToken)
            ?? throw new KeyNotFoundException("Customer payment was not found for this tenant.");
        var allocations = EffectiveCustomerAllocations(payment.Allocations);
        var inputs = await BuildCustomerInputsAsync(payment, allocations, producer, cancellationToken);
        return await _settlements.SynchronizeDraftAsync(
            producer, payment.Id, inputs, cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default)
    {
        var allocationIds = await EffectiveVendorAllocationIdsAsync(vendorPaymentId, cancellationToken);
        return await _settlements.ValidateAndFreezeAsync(
            Producer(FinanceDimensionRouteId.FinanceApVendorPayment),
            vendorPaymentId,
            allocationIds,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default) =>
        await ValidateAndFreezeCustomerPaymentAsync(
            customerPaymentId, Producer(FinanceDimensionRouteId.FinanceArCustomerPayment), cancellationToken);

    public async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeCustomerPaymentAsync(
        Guid customerPaymentId,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerPaymentRoute(producer);
        var allocationIds = await EffectiveCustomerAllocationIdsAsync(customerPaymentId, cancellationToken);
        return await _settlements.ValidateAndFreezeAsync(
            producer,
            customerPaymentId,
            allocationIds,
            cancellationToken);
    }

    public Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetVendorPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default) =>
        _settlements.GetAsync(
            Producer(FinanceDimensionRouteId.FinanceApVendorPayment), vendorPaymentId, cancellationToken);

    public Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetCustomerPaymentAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default) =>
        GetCustomerPaymentAsync(
            customerPaymentId, Producer(FinanceDimensionRouteId.FinanceArCustomerPayment), cancellationToken);

    public Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetCustomerPaymentAsync(
        Guid customerPaymentId,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerPaymentRoute(producer);
        return
        _settlements.GetAsync(
            producer, customerPaymentId, cancellationToken);
    }

    public Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolveVendorPostingDimensionsAsync(
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        CancellationToken cancellationToken = default) =>
        _settlements.ResolvePostingDimensionsAsync(
            Producer(FinanceDimensionRouteId.FinanceApVendorPayment),
            componentEvidenceId,
            postingAccountId,
            postingDate,
            cancellationToken);

    public Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolveCustomerPostingDimensionsAsync(
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        CancellationToken cancellationToken = default) =>
        ResolveCustomerPostingDimensionsAsync(
            componentEvidenceId, postingAccountId, postingDate,
            Producer(FinanceDimensionRouteId.FinanceArCustomerPayment), cancellationToken);

    public Task<IReadOnlyList<FinancePostingDimensionValueDto>> ResolveCustomerPostingDimensionsAsync(
        Guid componentEvidenceId,
        Guid postingAccountId,
        DateTime postingDate,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerPaymentRoute(producer);
        return
        _settlements.ResolvePostingDimensionsAsync(
            producer,
            componentEvidenceId,
            postingAccountId,
            postingDate,
            cancellationToken);
    }

    private static void EnsureCustomerPaymentRoute(FinancePostingProducerContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.RouteId is not (FinanceDimensionRouteId.FinanceArCustomerPayment
            or FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleReceipt
            or FinanceDimensionRouteId.MobilePosCustomerPayment))
            throw new InvalidOperationException("The trusted producer context is not a supported Finance customer-payment route.");
    }

    private async Task<IReadOnlyList<FinanceSettlementAllocationInput>> BuildVendorInputsAsync(
        VendorPayment payment,
        IReadOnlyList<VendorPaymentAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var invoiceIds = allocations.Select(item => item.VendorInvoiceId).Distinct().ToArray();
        var assignments = await LoadSourceAssignmentsAsync(
            FinanceDimensionRouteId.FinanceApVendorInvoice, invoiceIds, cancellationToken);
        return allocations.Select(allocation =>
        {
            var invoice = allocation.VendorInvoice;
            var origins = BuildOrigins(invoice.LineItems, assignments, invoice.Id,
                line => line.Id,
                line => Math.Max(line.LineTotal - line.DiscountAmount + line.TaxAmount, 0m));
            var grossInvoiceAmount = RoundMoney(
                allocation.AllocatedAmount + allocation.DiscountAmount + allocation.WithholdingTaxAmount);
            var isAdvanceApplication = payment.IsSupplierAdvance && payment.JournalEntryId.HasValue;
            var historicalFunctional = isAdvanceApplication
                ? RoundMoney(allocation.PaymentFunctionalAmount)
                : RoundMoney(grossInvoiceAmount * PositiveRate(invoice.ExchangeRate));
            var realizedFx = RoundMoney(allocation.SettlementFunctionalAmount - historicalFunctional);
            var components = new List<FinanceSettlementComponentAmountInput>
            {
                Component(
                    FinanceSettlementComponentType.Principal,
                    allocation.PaymentCurrencyCode,
                    allocation.PaymentCurrencyAmount,
                    allocation.PaymentFunctionalAmount,
                    allocation.PaymentExchangeRateId,
                    allocation.PaymentExchangeRate,
                    allocation.InvoiceSettlementExchangeRateId,
                    allocation.InvoiceSettlementExchangeRate)
            };
            AddComponent(components, FinanceSettlementComponentType.Discount,
                allocation.InvoiceCurrencyCode, allocation.DiscountAmount,
                allocation.DiscountFunctionalAmount, allocation.InvoiceSettlementExchangeRateId,
                allocation.InvoiceSettlementExchangeRate);
            AddComponent(components, FinanceSettlementComponentType.WithholdingTax,
                allocation.InvoiceCurrencyCode, allocation.WithholdingTaxAmount,
                allocation.WithholdingTaxFunctionalAmount,
                allocation.WithholdingTaxStatutoryExchangeRateId
                    ?? allocation.InvoiceSettlementExchangeRateId,
                allocation.WithholdingTaxStatutoryExchangeRate
                    ?? allocation.InvoiceSettlementExchangeRate);
            AddComponent(components, FinanceSettlementComponentType.RealizedFx,
                allocation.InvoiceCurrencyCode, 0m, realizedFx,
                allocation.InvoiceSettlementExchangeRateId, allocation.InvoiceSettlementExchangeRate,
                isAdvanceApplication ? allocation.PaymentExchangeRateId : invoice.ExchangeRateId,
                isAdvanceApplication
                    ? PositiveRate(allocation.PaymentExchangeRate)
                    : PositiveRate(invoice.ExchangeRate));
            return new FinanceSettlementAllocationInput(
                allocation.Id, allocation.Id, invoice.Id, components, origins);
        }).ToArray();
    }

    private async Task<IReadOnlyList<FinanceSettlementAllocationInput>> BuildCustomerInputsAsync(
        CustomerPayment payment,
        IReadOnlyList<PaymentAllocation> allocations,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        var invoiceIds = allocations.Select(item => item.InvoiceId).Distinct().ToArray();
        var invoiceRouteId = producer.RouteId switch
        {
            FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleReceipt =>
                FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleInvoice,
            FinanceDimensionRouteId.MobilePosCustomerPayment =>
                FinanceDimensionRouteId.MobilePosCustomerInvoice,
            _ => FinanceDimensionRouteId.FinanceArCustomerInvoice
        };
        var auctionIds = invoiceRouteId == FinanceDimensionRouteId.FinanceArCustomerInvoice
            ? await _db.Set<ErpSystem.Core.Entities.Inventory.InventoryDisposalAuctionInvoice>().AsNoTracking()
                .Where(value => value.TenantId == TenantId && invoiceIds.Contains(value.InvoiceId))
                .Select(value => value.InvoiceId).ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        var salesIds = invoiceRouteId == FinanceDimensionRouteId.FinanceArCustomerInvoice
            ? await _db.SalesOrders.AsNoTracking().Where(value => value.TenantId == TenantId && value.InvoiceId.HasValue &&
                invoiceIds.Contains(value.InvoiceId.Value)).Select(value => value.InvoiceId!.Value).ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        if (salesIds.Intersect(auctionIds).Any())
            throw new InvalidOperationException("An invoice cannot belong to both Sales and Inventory auction producers.");
        var assignments = await LoadSourceAssignmentsAsync(
            invoiceRouteId, invoiceIds.Except(auctionIds).Except(salesIds).ToArray(), cancellationToken);
        if (salesIds.Length != 0)
        {
            var salesAssignments = await LoadSourceAssignmentsAsync(FinanceDimensionRouteId.SalesOrderCustomerInvoice, salesIds, cancellationToken);
            foreach (var pair in salesAssignments) assignments.Add(pair.Key, pair.Value);
        }
        if (auctionIds.Length != 0)
        {
            var auctionAssignments = await LoadSourceAssignmentsAsync(FinanceDimensionRouteId.InventoryDisposalAuctionInvoice, auctionIds, cancellationToken);
            foreach (var pair in auctionAssignments) assignments.Add(pair.Key, pair.Value);
        }
        return allocations.Select(allocation =>
        {
            var invoice = allocation.Invoice;
            var origins = BuildOrigins(invoice.LineItems, assignments, invoice.Id,
                line => line.Id,
                line => Math.Max(line.LineTotal - line.DiscountAmount + line.TaxAmount, 0m));
            var grossInvoiceAmount = RoundMoney(allocation.AllocatedAmount + allocation.DiscountAmount
                + allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount);
            var isAdvanceApplication = payment.IsCustomerAdvance && payment.JournalEntryId.HasValue;
            var historicalFunctional = isAdvanceApplication
                ? RoundMoney(allocation.PaymentFunctionalAmount)
                : RoundMoney(grossInvoiceAmount * PositiveRate(invoice.ExchangeRate));
            var realizedFx = RoundMoney(allocation.SettlementFunctionalAmount - historicalFunctional);
            var components = new List<FinanceSettlementComponentAmountInput>
            {
                Component(
                    FinanceSettlementComponentType.Principal,
                    allocation.PaymentCurrencyCode,
                    allocation.PaymentCurrencyAmount,
                    allocation.PaymentFunctionalAmount,
                    allocation.PaymentExchangeRateId,
                    allocation.PaymentExchangeRate,
                    allocation.InvoiceSettlementExchangeRateId,
                    allocation.InvoiceSettlementExchangeRate)
            };
            AddComponent(components, FinanceSettlementComponentType.Discount,
                allocation.InvoiceCurrencyCode, allocation.DiscountAmount,
                allocation.DiscountFunctionalAmount, allocation.InvoiceSettlementExchangeRateId,
                allocation.InvoiceSettlementExchangeRate);
            AddComponent(components, FinanceSettlementComponentType.WithholdingTax,
                allocation.InvoiceCurrencyCode, allocation.WithholdingTaxAmount,
                allocation.WithholdingTaxFunctionalAmount, allocation.InvoiceSettlementExchangeRateId,
                allocation.InvoiceSettlementExchangeRate);
            AddComponent(components, FinanceSettlementComponentType.VatWithholdingTax,
                allocation.InvoiceCurrencyCode, allocation.VatWithholdingAmount,
                allocation.VatWithholdingFunctionalAmount, allocation.InvoiceSettlementExchangeRateId,
                allocation.InvoiceSettlementExchangeRate);
            AddComponent(components, FinanceSettlementComponentType.RealizedFx,
                allocation.InvoiceCurrencyCode, 0m, realizedFx,
                allocation.InvoiceSettlementExchangeRateId, allocation.InvoiceSettlementExchangeRate,
                isAdvanceApplication ? allocation.PaymentExchangeRateId : invoice.ExchangeRateId,
                isAdvanceApplication
                    ? PositiveRate(allocation.PaymentExchangeRate)
                    : PositiveRate(invoice.ExchangeRate));
            return new FinanceSettlementAllocationInput(
                allocation.Id, allocation.Id, invoice.Id, components, origins);
        }).ToArray();
    }

    private async Task<Dictionary<(Guid DocumentId, Guid LineId), FinanceSourceDimensionAssignment>>
        LoadSourceAssignmentsAsync(
            FinanceDimensionRouteId routeId,
            IReadOnlyCollection<Guid> documentIds,
            CancellationToken cancellationToken)
    {
        if (documentIds.Count == 0)
            return new Dictionary<(Guid, Guid), FinanceSourceDimensionAssignment>();
        return await _db.FinanceSourceDimensionAssignments.AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.RouteId == routeId
                && documentIds.Contains(item.SourceDocumentId)
                && item.SourceLineId.HasValue && !item.IsDeleted)
            .ToDictionaryAsync(
                item => (item.SourceDocumentId, item.SourceLineId!.Value),
                cancellationToken);
    }

    private static IReadOnlyList<FinanceSettlementOriginLineInput> BuildOrigins<TLine>(
        IEnumerable<TLine> sourceLines,
        IReadOnlyDictionary<(Guid DocumentId, Guid LineId), FinanceSourceDimensionAssignment> assignments,
        Guid documentId,
        Func<TLine, Guid> id,
        Func<TLine, decimal> weight)
    {
        var lines = sourceLines.Select(line => new { Id = id(line), Weight = weight(line) })
            .Where(item => item.Id != Guid.Empty)
            .OrderBy(item => item.Id)
            .ToArray();
        if (lines.Length == 0)
            throw new InvalidOperationException("A settlement invoice has no persisted economic lines from which Finance dimensions can be inherited.");
        var positiveWeightTotal = lines.Sum(item => Math.Max(item.Weight, 0m));
        var eligible = positiveWeightTotal > 0m
            ? lines.Where(item => item.Weight > 0m).ToArray()
            : lines;
        return eligible.Select(item =>
        {
            assignments.TryGetValue((documentId, item.Id), out var assignment);
            return new FinanceSettlementOriginLineInput(
                item.Id,
                positiveWeightTotal > 0m ? item.Weight : 1m,
                assignment?.FinanceDimensionSetId,
                assignment?.FinanceDimensionSnapshotId);
        }).ToArray();
    }

    private async Task<Guid[]> EffectiveVendorAllocationIdsAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Set<VendorPaymentAllocation>().AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.VendorPaymentId == paymentId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return EffectiveVendorAllocations(rows).Select(item => item.Id).ToArray();
    }

    private async Task<Guid[]> EffectiveCustomerAllocationIdsAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Set<PaymentAllocation>().AsNoTracking()
            .Where(item => item.TenantId == TenantId && item.CustomerPaymentId == paymentId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        return EffectiveCustomerAllocations(rows).Select(item => item.Id).ToArray();
    }

    private static List<VendorPaymentAllocation> EffectiveVendorAllocations(
        IEnumerable<VendorPaymentAllocation> allocations)
    {
        var rows = allocations.Where(item => !item.IsDeleted).ToArray();
        var reversed = rows.Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
            .Select(item => item.OriginalAllocationId!.Value).ToHashSet();
        return rows.Where(item => !item.IsReversal && !reversed.Contains(item.Id))
            .OrderBy(item => item.AllocationDate).ThenBy(item => item.Id).ToList();
    }

    private static List<PaymentAllocation> EffectiveCustomerAllocations(
        IEnumerable<PaymentAllocation> allocations)
    {
        var rows = allocations.Where(item => !item.IsDeleted).ToArray();
        var reversed = rows.Where(item => item.IsReversal && item.OriginalAllocationId.HasValue)
            .Select(item => item.OriginalAllocationId!.Value).ToHashSet();
        return rows.Where(item => !item.IsReversal && !reversed.Contains(item.Id))
            .OrderBy(item => item.AllocationDate).ThenBy(item => item.Id).ToList();
    }

    private static FinanceSettlementComponentAmountInput Component(
        FinanceSettlementComponentType type,
        string currency,
        decimal transactionAmount,
        decimal functionalAmount,
        Guid? rateId,
        decimal rate,
        Guid? comparisonRateId = null,
        decimal? comparisonRate = null) =>
        new(type, currency, RoundMoney(transactionAmount), RoundMoney(functionalAmount),
            rateId, PositiveRate(rate), comparisonRateId, comparisonRate is null ? null : PositiveRate(comparisonRate.Value));

    private static void AddComponent(
        ICollection<FinanceSettlementComponentAmountInput> target,
        FinanceSettlementComponentType type,
        string currency,
        decimal transactionAmount,
        decimal functionalAmount,
        Guid? rateId,
        decimal rate,
        Guid? comparisonRateId = null,
        decimal? comparisonRate = null)
    {
        if (transactionAmount == 0m && functionalAmount == 0m) return;
        target.Add(Component(type, currency, transactionAmount, functionalAmount,
            rateId, rate, comparisonRateId, comparisonRate));
    }

    private static FinancePostingProducerContext Producer(FinanceDimensionRouteId routeId) => new(routeId);
    private static decimal PositiveRate(decimal rate) => rate <= 0m ? 1m : rate;
    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
