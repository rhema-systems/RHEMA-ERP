using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.MobilePos;

public interface IMobilePosSaleService
{
    Task<MobilePosSaleResultDto> CompleteAsync(
        MobilePosCompleteSaleRequestDto request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Composes one immediate POS sale from the existing AR invoice and receipt boundaries. The outer
/// mutation service owns the serializable transaction so the invoice and every split tender either
/// commit together or leave no accounting document behind.
/// </summary>
public sealed class MobilePosSaleService : IMobilePosSaleService
{
    private const string CommandType = "MobilePos.CompleteSale";
    private const int SchemaVersion = 1;
    private static readonly FinancePostingProducerContext InvoiceProducer =
        FinanceExternalProducerContractCatalog.GetRequired(
            FinanceExternalProducerContractId.MobilePosCustomerInvoice);
    private static readonly FinancePostingProducerContext PaymentProducer =
        FinanceExternalProducerContractCatalog.GetRequired(
            FinanceExternalProducerContractId.MobilePosCustomerPayment);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IMobilePosFoundationService _foundation;
    private readonly IMobilePosMutationExecutionService _mutations;
    private readonly IInvoiceService _invoices;
    private readonly IPaymentService _payments;

    public MobilePosSaleService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IMobilePosFoundationService foundation,
        IMobilePosMutationExecutionService mutations,
        IInvoiceService invoices,
        IPaymentService payments)
    {
        _db = db;
        _currentUser = currentUser;
        _foundation = foundation;
        _mutations = mutations;
        _invoices = invoices;
        _payments = payments;
    }

    private Guid TenantId => _currentUser.TenantId is { } id && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("A current tenant is required for Mobile POS.");

    private Guid UserId => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id
        : throw new UnauthorizedAccessException("An authenticated user is required for Mobile POS.");

    private string UserName => string.IsNullOrWhiteSpace(_currentUser.UserName)
        ? "Unknown"
        : _currentUser.UserName.Trim();

    public async Task<MobilePosSaleResultDto> CompleteAsync(
        MobilePosCompleteSaleRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var installationId = Required(request.InstallationId, 200, "installation ID");
        var bootstrap = await _foundation.GetBootstrapAsync(installationId, cancellationToken);
        if (!bootstrap.CurrentTillSessionId.HasValue)
            throw Reject("MOBILE_POS_TILL_SESSION_REQUIRED", "Open your assigned till session before completing a sale.");

        var execution = await _mutations.ExecuteAsync<MobilePosCompleteSaleRequestDto, MobilePosSaleResultDto>(
            bootstrap.Device.Id,
            request.ClientMutationId,
            CommandType,
            SchemaVersion,
            request,
            token => CompleteCoreAsync(bootstrap, request, token),
            cancellationToken);
        execution.Result.MutationReceiptId = execution.ReceiptId;
        execution.Result.IsReplay = execution.IsReplay;
        return execution.Result;
    }

    private async Task<MobilePosMutationCompletion<MobilePosSaleResultDto>> CompleteCoreAsync(
        MobilePosBootstrapDto bootstrap,
        MobilePosCompleteSaleRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var tenantId = TenantId;
        var userId = UserId;
        var now = DateTime.UtcNow;
        var localReference = Required(request.LocalReference, 100, "local reference");

        var device = await _db.MobilePosDevices.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == bootstrap.Device.Id && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_DEVICE_NOT_FOUND", "The enrolled device was not found.");
        if (device.Status != MobilePosDeviceStatus.Active
            || device.MobilePosStoreId != bootstrap.Store.Id
            || device.MobilePosTillId != bootstrap.Till.Id)
        {
            throw Reject("MOBILE_POS_DEVICE_ASSIGNMENT_CHANGED", "The device assignment changed. Refresh Mobile POS before retrying.");
        }

        var assignmentActive = await _db.MobilePosUserStoreAssignments.AnyAsync(item =>
            item.TenantId == tenantId && item.UserId == userId && item.IsActive && !item.IsDeleted
            && item.MobilePosStoreId == bootstrap.Store.Id
            && item.EffectiveFromUtc <= now
            && (!item.EffectiveToUtc.HasValue || item.EffectiveToUtc > now),
            cancellationToken);
        if (!assignmentActive)
            throw Reject("MOBILE_POS_STORE_ASSIGNMENT_EXPIRED", "Your Mobile POS store assignment is no longer active.");

        var store = await _db.MobilePosStores
            .Include(item => item.DimensionDefaults)
                .ThenInclude(item => item.FinanceDimensionDefinition)
            .Include(item => item.DimensionDefaults)
                .ThenInclude(item => item.FinanceDimensionValue)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == bootstrap.Store.Id && !item.IsDeleted,
                cancellationToken) ?? throw Reject("MOBILE_POS_STORE_NOT_FOUND", "The assigned Mobile POS store was not found.");
        var till = await _db.MobilePosTills.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == bootstrap.Till.Id
            && item.MobilePosStoreId == store.Id && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_TILL_NOT_FOUND", "The assigned Mobile POS till was not found.");
        if (store.Status != MobilePosStoreStatus.Active || till.Status != MobilePosTillStatus.Active)
            throw Reject("MOBILE_POS_STORE_OR_TILL_INACTIVE", "The assigned store or till is no longer active.");

        var sessionId = bootstrap.CurrentTillSessionId!.Value;
        var session = await _db.CashierTillSessions.SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == sessionId && !item.IsDeleted,
            cancellationToken) ?? throw Reject("MOBILE_POS_TILL_SESSION_NOT_FOUND", "The till session was not found.");
        if (session.Status != CashierTillSessionStatus.Open
            || session.CashierUserId != userId
            || session.LiquidityAccountId != till.LiquidityAccountId)
        {
            throw Reject("MOBILE_POS_TILL_SESSION_CHANGED", "The till session is no longer open for this operator and till.");
        }

        var selectedPartnerId = request.BusinessPartnerId ?? store.DefaultWalkInBusinessPartnerId;
        var selectedRoleId = request.BusinessPartnerRoleId ?? store.DefaultWalkInBusinessPartnerRoleId;
        var customerRole = await _db.EligibleMobilePosCustomerRoles(tenantId, now)
            .AsNoTracking()
            .Include(role => role.BusinessPartner)
            .SingleOrDefaultAsync(role => role.Id == selectedRoleId
                && role.BusinessPartnerId == selectedPartnerId, cancellationToken)
            ?? throw Reject("MOBILE_POS_CUSTOMER_INELIGIBLE", "The selected customer is not active, approved, and transaction ready.");
        var usedDefaultCustomer = selectedPartnerId == store.DefaultWalkInBusinessPartnerId
            && selectedRoleId == store.DefaultWalkInBusinessPartnerRoleId;
        if (!string.IsNullOrWhiteSpace(customerRole.BusinessPartner.Currency)
            && !string.Equals(customerRole.BusinessPartner.Currency, store.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw Reject(
                "MOBILE_POS_CUSTOMER_CURRENCY_MISMATCH",
                $"The selected customer uses {customerRole.BusinessPartner.Currency}; this store transacts in {store.CurrencyCode}.");
        }

        var itemIds = request.Lines.Select(line => line.InventoryItemId).Distinct().ToArray();
        var items = await _db.InventoryItems.AsNoTracking()
            .Where(item => item.TenantId == tenantId && itemIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (items.Count != itemIds.Length)
            throw Reject("MOBILE_POS_ITEM_NOT_FOUND", "One or more sale items were not found in the current tenant.");

        var defaultDimensions = store.DimensionDefaults
            .Where(item => !item.IsDeleted
                && item.FinanceDimensionDefinition.IsActive
                && item.FinanceDimensionValue.IsActive)
            .OrderBy(item => item.FinanceDimensionDefinition.DisplayOrder)
            .Select(item => new FinancePostingDimensionValueDto
            {
                DimensionCode = item.FinanceDimensionDefinition.Code,
                ValueCode = item.FinanceDimensionValue.Code,
                SourceEntityType = nameof(MobilePosStore),
                SourceEntityId = store.Id
            }).ToArray();

        var invoiceLines = new List<InvoiceLineItemCreateDto>(request.Lines.Count);
        var resolvedLines = new List<(MobilePosSaleLineInputDto Input, InventoryItem Item, Guid AccountId)>();
        foreach (var input in request.Lines)
        {
            var item = items[input.InventoryItemId];
            if (item.Status != ItemStatus.Active)
                throw Reject("MOBILE_POS_ITEM_INACTIVE", $"Item {item.ItemCode} is not active for sale.");
            if (input.UnitPrice != item.SalePrice)
                throw Reject("MOBILE_POS_PRICE_CHANGED", $"The price for {item.ItemCode} changed. Refresh the catalogue and confirm the sale again.");
            var accountId = item.SalesAccountId
                ?? throw Reject("MOBILE_POS_REVENUE_ACCOUNT_MISSING", $"Item {item.ItemCode} has no Sales Account configured.");
            var isStockItem = item.ItemType == ItemType.StockItem;
            if (isStockItem && !store.WarehouseId.HasValue)
                throw Reject("MOBILE_POS_WAREHOUSE_REQUIRED", $"Store {store.Code} needs a default warehouse before selling stock items.");

            invoiceLines.Add(new InvoiceLineItemCreateDto
            {
                Id = input.ClientLineId,
                LineItemType = isStockItem ? "Inventory" : "GLAccount",
                InventoryItemId = isStockItem ? item.Id : null,
                WarehouseId = isStockItem ? store.WarehouseId : null,
                GLAccountId = accountId,
                Description = item.Name,
                Quantity = input.Quantity,
                UnitPrice = input.UnitPrice,
                TaxGroupId = input.TaxGroupId ?? item.DefaultTaxGroupId,
                TaxTreatment = input.TaxTreatment,
                Unit = item.UnitOfMeasure,
                DiscountPercentage = input.DiscountPercentage
            });
            resolvedLines.Add((input, item, accountId));
        }

        var dimensionInput = new FinanceSourceDocumentDimensionInputDto
        {
            DefaultDimensions = defaultDimensions,
            ApplyDefaultToEligibleLines = true,
            Lines = resolvedLines.Select(line => new FinanceSourceLineDimensionInputDto
            {
                SourceLineId = line.Input.ClientLineId,
                AccountId = line.AccountId,
                Dimensions = defaultDimensions
            }).ToArray()
        };

        var invoice = await _invoices.CreateAsync(new InvoiceCreateDto
        {
            BusinessPartnerId = selectedPartnerId,
            BusinessPartnerRoleId = selectedRoleId,
            InvoiceDate = session.BusinessDate.Date,
            DueDate = session.BusinessDate.Date,
            Reference = localReference,
            Notes = $"Mobile POS sale {localReference} at {store.Code}/{till.TillNumber}",
            CurrencyCode = store.CurrencyCode,
            ExchangeRateId = request.ExchangeRateId,
            ExchangeRate = 1m,
            LineItems = invoiceLines,
            FinanceDimensions = dimensionInput
        }, InvoiceProducer, cancellationToken);

        var canonicalDiscount = invoice.DiscountAmount + invoice.LineItems.Sum(line => line.DiscountAmount);
        EnsureExpectedTotals(request, invoice, canonicalDiscount);
        var tenderTotal = RoundMoney(request.Tenders.Sum(tender => tender.Amount));
        if (tenderTotal != RoundMoney(invoice.TotalAmount))
            throw Reject("MOBILE_POS_TENDER_TOTAL_MISMATCH", "Tender amounts must equal the canonical invoice total.");

        var allowedTenders = await _db.MobilePosTillPaymentMethods.AsNoTracking()
            .Where(mapping => mapping.TenantId == tenantId && mapping.MobilePosTillId == till.Id
                && mapping.AllowOnline && !mapping.IsDeleted)
            .Include(mapping => mapping.PaymentMethod)
            .ToDictionaryAsync(mapping => mapping.PaymentMethodId, cancellationToken);
        var requestedMethodIds = request.Tenders.Select(tender => tender.PaymentMethodId).Distinct().ToArray();
        if (requestedMethodIds.Any(id => !allowedTenders.ContainsKey(id)))
            throw Reject("MOBILE_POS_TENDER_NOT_ALLOWED", "One or more tender methods are not enabled for online use at this till.");

        var postedInvoice = await _invoices.PostAsync(invoice.Id, InvoiceProducer, cancellationToken);
        var paymentResults = new List<CustomerPaymentDto>(request.Tenders.Count);
        for (var index = 0; index < request.Tenders.Count; index++)
        {
            var tender = request.Tenders[index];
            var mapping = allowedTenders[tender.PaymentMethodId];
            var reference = Clean(tender.ExternalReference);
            if (mapping.RequireExternalAuthorizationReference && reference is null)
                throw Reject("MOBILE_POS_TENDER_REFERENCE_REQUIRED", $"{mapping.PaymentMethod.Name} requires an external authorization reference.");

            var liquidityAccountId = tender.LiquidityAccountId;
            if (!liquidityAccountId.HasValue && mapping.PaymentMethod.Type == PaymentMethodType.Cash)
                liquidityAccountId = till.LiquidityAccountId;
            var payment = await _payments.CreateAsync(new PaymentCreateDto
            {
                BusinessPartnerId = selectedPartnerId,
                BusinessPartnerRoleId = selectedRoleId,
                PaymentDate = session.BusinessDate.Date,
                TotalAmount = tender.Amount,
                PaymentMethodId = mapping.PaymentMethodId,
                PaymentMethod = mapping.PaymentMethod.Name,
                CurrencyCode = store.CurrencyCode,
                ExchangeRateId = request.ExchangeRateId,
                ExchangeRate = 1m,
                BankAccountId = tender.BankAccountId,
                LiquidityAccountId = liquidityAccountId,
                TransactionReference = reference,
                Notes = $"Mobile POS sale {localReference}; tender {index + 1}/{request.Tenders.Count}",
                Allocations =
                [
                    new InvoiceAllocationDto
                    {
                        InvoiceId = invoice.Id,
                        AllocatedAmount = tender.Amount,
                        PaymentCurrencyAmount = tender.Amount,
                        Notes = $"Mobile POS sale {localReference}"
                    }
                ]
            }, PaymentProducer, cancellationToken);
            paymentResults.Add(payment);
        }

        postedInvoice = await _invoices.GetByIdAsync(invoice.Id, InvoiceProducer, cancellationToken)
            ?? postedInvoice;
        var saleId = Guid.NewGuid();
        var occurredAtUtc = NormalizeOccurredAt(request.OccurredAtUtc, now);
        var sale = new MobilePosSale
        {
            Id = saleId,
            TenantId = tenantId,
            MobilePosStoreId = store.Id,
            MobilePosTillId = till.Id,
            CashierTillSessionId = session.Id,
            MobilePosDeviceId = device.Id,
            OperatorUserId = userId,
            ClientMutationId = Required(request.ClientMutationId, 100, "client mutation ID"),
            LocalReference = localReference,
            BusinessPartnerId = selectedPartnerId,
            BusinessPartnerRoleId = selectedRoleId,
            UsedStoreDefaultCustomer = usedDefaultCustomer,
            BusinessDate = session.BusinessDate.Date,
            OccurredAtUtc = occurredAtUtc,
            CurrencyCode = store.CurrencyCode,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = canonicalDiscount,
            TotalAmount = invoice.TotalAmount,
            Status = MobilePosSaleStatus.Completed,
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            SynchronizedAtUtc = now,
            CreatedAt = now,
            CreatedBy = UserName,
            CreatedById = userId
        };

        foreach (var line in resolvedLines.Select((value, index) => (value, index)))
        {
            var canonical = invoice.LineItems.Single(item => item.Id == line.value.Input.ClientLineId);
            sale.Lines.Add(new MobilePosSaleLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ClientLineId = line.value.Input.ClientLineId,
                Sequence = line.index + 1,
                InventoryItemId = line.value.Item.Id,
                Description = canonical.Description,
                Quantity = canonical.Quantity,
                UnitPrice = canonical.UnitPrice,
                DiscountAmount = canonical.DiscountAmount,
                TaxAmount = canonical.TaxAmount,
                LineTotal = canonical.LineTotal,
                TaxGroupId = canonical.TaxGroupId,
                UnitOfMeasureId = line.value.Item.UnitOfMeasureId,
                UnitOfMeasureCode = line.value.Item.UnitOfMeasure,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = userId
            });
        }

        foreach (var result in paymentResults.Select((payment, index) => (payment, index)))
        {
            var input = request.Tenders[result.index];
            sale.Tenders.Add(new MobilePosTender
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Sequence = result.index + 1,
                PaymentMethodId = input.PaymentMethodId,
                Amount = input.Amount,
                ExternalReference = Clean(input.ExternalReference),
                LiquidityAccountId = result.payment.LiquidityAccountId,
                BankAccountId = result.payment.BankAccountId,
                CustomerPaymentId = result.payment.Id,
                PaymentNumber = result.payment.PaymentNumber,
                ProviderReference = Clean(input.ExternalReference),
                ProviderStatus = result.payment.Status,
                WasRecordedOffline = false,
                Status = MobilePosTenderStatus.Completed,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = userId
            });
        }

        _db.MobilePosSales.Add(sale);
        await _db.SaveChangesAsync(cancellationToken);
        var resultDto = new MobilePosSaleResultDto
        {
            SaleId = sale.Id,
            LocalReference = sale.LocalReference,
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceStatus = postedInvoice.Status,
            CurrencyCode = sale.CurrencyCode,
            SubTotal = sale.SubTotal,
            TaxAmount = sale.TaxAmount,
            DiscountAmount = sale.DiscountAmount,
            TotalAmount = sale.TotalAmount,
            Tenders = sale.Tenders.OrderBy(tender => tender.Sequence).Select(tender =>
                new MobilePosSaleTenderResultDto
                {
                    TenderId = tender.Id,
                    PaymentMethodId = tender.PaymentMethodId,
                    Amount = tender.Amount,
                    CustomerPaymentId = tender.CustomerPaymentId!.Value,
                    PaymentNumber = tender.PaymentNumber ?? string.Empty,
                    PaymentStatus = tender.ProviderStatus ?? string.Empty
                }).ToArray()
        };
        return new MobilePosMutationCompletion<MobilePosSaleResultDto>(
            resultDto,
            sale.Id,
            invoice.Id,
            paymentResults.Select(payment => payment.Id).ToArray());
    }

    private static void ValidateRequest(MobilePosCompleteSaleRequestDto request)
    {
        _ = Required(request.ClientMutationId, 100, "client mutation ID");
        _ = Required(request.LocalReference, 100, "local reference");
        if (request.BusinessPartnerId.HasValue != request.BusinessPartnerRoleId.HasValue)
            throw Reject("MOBILE_POS_CUSTOMER_IDENTITY_INCOMPLETE", "Select both the customer and its Customer role, or leave both blank for the store walk-in customer.");
        if (request.Lines.Count is < 1 or > 200)
            throw Reject("MOBILE_POS_LINES_INVALID", "A sale must contain between 1 and 200 lines.");
        if (request.Tenders.Count is < 1 or > 10)
            throw Reject("MOBILE_POS_TENDERS_INVALID", "A sale must contain between 1 and 10 tenders.");
        if (request.Lines.Any(line => line.ClientLineId == Guid.Empty || line.InventoryItemId == Guid.Empty
                || line.Quantity <= 0m || line.UnitPrice < 0m
                || line.DiscountPercentage is < 0m or > 100m)
            || request.Lines.Select(line => line.ClientLineId).Distinct().Count() != request.Lines.Count)
        {
            throw Reject("MOBILE_POS_LINE_INVALID", "Every sale line needs a unique line ID, an item, positive quantity, non-negative price, and a discount from 0 to 100 percent.");
        }
        if (request.Tenders.Any(tender => tender.PaymentMethodId == Guid.Empty || tender.Amount <= 0m))
            throw Reject("MOBILE_POS_TENDER_INVALID", "Every tender needs a payment method and a positive amount.");
        if (request.Tenders.Any(tender => Clean(tender.ExternalReference)?.Length > 150))
            throw Reject("MOBILE_POS_TENDER_REFERENCE_INVALID", "Tender authorization references cannot exceed 150 characters.");
        if (request.ExpectedSubTotal < 0m || request.ExpectedTaxAmount < 0m
            || request.ExpectedDiscountAmount < 0m || request.ExpectedTotalAmount <= 0m)
        {
            throw Reject("MOBILE_POS_EXPECTED_TOTALS_INVALID", "Expected sale totals must be non-negative and the total must be positive.");
        }
    }

    private static void EnsureExpectedTotals(
        MobilePosCompleteSaleRequestDto request,
        InvoiceDto invoice,
        decimal canonicalDiscount)
    {
        if (RoundMoney(request.ExpectedSubTotal) != RoundMoney(invoice.SubTotal)
            || RoundMoney(request.ExpectedTaxAmount) != RoundMoney(invoice.TaxAmount)
            || RoundMoney(request.ExpectedDiscountAmount) != RoundMoney(canonicalDiscount)
            || RoundMoney(request.ExpectedTotalAmount) != RoundMoney(invoice.TotalAmount))
        {
            throw Reject(
                "MOBILE_POS_TOTALS_CHANGED",
                $"Canonical totals are subtotal {invoice.SubTotal:0.00}, tax {invoice.TaxAmount:0.00}, discount {canonicalDiscount:0.00}, total {invoice.TotalAmount:0.00}. Refresh and confirm the sale again.");
        }
    }

    private static DateTime NormalizeOccurredAt(DateTime? value, DateTime now)
    {
        if (!value.HasValue) return now;
        var occurredAt = value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : value.Value.ToUniversalTime();
        if (occurredAt > now.AddMinutes(5))
            throw Reject("MOBILE_POS_DEVICE_CLOCK_AHEAD", "The device sale time is too far ahead of the server clock.");
        return occurredAt;
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Required(string? value, int maximumLength, string label)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
            throw Reject("MOBILE_POS_REQUIRED_VALUE_INVALID", $"The {label} must contain 1 to {maximumLength} characters.");
        return normalized;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MobilePosCommandRejectedException Reject(string code, string detail) => new(code, detail);
}
