using System.Data;
using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Inventory;

internal sealed partial class InventoryDisposalService
{
    internal static readonly FinancePostingProducerContext AuctionProducer = new(FinanceDimensionRouteId.InventoryDisposalAuctionInvoice);

    public Task<InventoryDisposalDto> CreateAuctionInvoiceAsync(Guid id,
        CreateInventoryDisposalAuctionInvoiceRequest request, CancellationToken cancellationToken = default) =>
        ProtectContextAsync(async () =>
        {
            EnsureActor();
            if (_invoices is null) throw Error("INV_DISPOSAL_AR_UNAVAILABLE", "The Finance AR invoice service is not configured.");
            var key = Required(request.IdempotencyKey, 100, "Idempotency key");
            var fingerprint = Hash(new { request.BusinessPartnerId, request.BusinessPartnerRoleId, request.InvoiceDate,
                request.DueDate, request.PaymentTermId, request.FinanceDimensions, Lines = request.Lines.OrderBy(value => value.DisposalLineId) });
            return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await AcquireDisposalLockAsync(id, cancellationToken);
                var disposal = await FullQuery().SingleOrDefaultAsync(value => value.Id == id &&
                    value.TenantId == _currentUser.TenantId && !value.IsDeleted, cancellationToken)
                    ?? throw new InventoryDisposalNotFoundException("The disposal was not found in the current tenant.");
                await RequireAccessAsync("procurement.inventory.disposal.request", disposal, cancellationToken);
                var existing = await _db.Set<InventoryDisposalAuctionInvoice>().AsNoTracking()
                    .SingleOrDefaultAsync(value => value.TenantId == disposal.TenantId && value.InventoryDisposalCaseId == id, cancellationToken);
                if (existing is not null)
                {
                    EnsurePayload(existing.PayloadHash, fingerprint);
                    await transaction.CommitAsync(cancellationToken);
                    return await MapForActorAsync(disposal, cancellationToken);
                }
                EnsureRowVersion(disposal.RowVersion, request.RowVersion);
                if (disposal.AccountingVersion != 1 || disposal.Method != InventoryDisposalMethod.Auction ||
                    disposal.Status is not (InventoryDisposalStatus.Approved or InventoryDisposalStatus.ReadyForExecution))
                    throw State(disposal, "An approved new auction disposal is required before creating its invoice.");
                var sources = disposal.Lines.Where(value => !value.IsDeleted).OrderBy(value => value.Id).ToArray();
                if (request.BusinessPartnerId == Guid.Empty || request.InvoiceDate == default ||
                    request.Lines.Count != sources.Length || request.Lines.Select(value => value.DisposalLineId).Distinct().Count() != sources.Length ||
                    request.Lines.Any(value => !sources.Any(source => source.Id == value.DisposalLineId) || value.UnitPrice <= 0 ||
                        decimal.Round(value.UnitPrice, 2) != value.UnitPrice || value.TaxTreatment is not
                            (TaxTreatment.Standard or TaxTreatment.Exempt or TaxTreatment.ZeroRated or TaxTreatment.OutOfScope) ||
                        (value.TaxTreatment == TaxTreatment.Standard && !value.TaxGroupId.HasValue)))
                    throw Error("INV_DISPOSAL_AUCTION_LINES_INVALID", "Select a customer, invoice date, positive price and tax treatment for every disposal line.");
                var settings = await _db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(value =>
                    value.TenantId == disposal.TenantId && !value.IsDeleted, cancellationToken)
                    ?? throw Error("INV_DISPOSAL_FINANCE_SETTINGS_MISSING", "Finance settings are required.");
                if (string.IsNullOrWhiteSpace(settings.BaseCurrency))
                    throw Error("INV_DISPOSAL_CURRENCY_REQUIRED", "Configure the Finance functional currency before creating an auction invoice.");
                var itemIds = sources.Select(value => value.InventoryItemId).Distinct().ToArray();
                var items = await _db.InventoryItems.AsNoTracking().Where(value => value.TenantId == disposal.TenantId &&
                    itemIds.Contains(value.Id) && !value.IsDeleted).ToDictionaryAsync(value => value.Id, cancellationToken);
                var lines = new List<InvoiceLineItemCreateDto>();
                foreach (var source in sources)
                {
                    var input = request.Lines.Single(value => value.DisposalLineId == source.Id);
                    if (!items.TryGetValue(source.InventoryItemId, out var item) || !item.InventoryDisposalAccountId.HasValue)
                        throw Error("INV_DISPOSAL_ACCOUNT_REQUIRED", "Configure an Inventory Disposal Account for every auction item.");
                    var account = await _db.Accounts.AsNoTracking().SingleOrDefaultAsync(value =>
                        value.Id == item.InventoryDisposalAccountId && value.TenantId == disposal.TenantId && !value.IsDeleted,
                        cancellationToken);
                    if (account is null || account.Status != AccountStatus.Active || !account.AllowDirectPosting || account.IsControlAccount ||
                        account.AccountType is not (AccountType.Expense or AccountType.Revenue))
                        throw Error("INV_DISPOSAL_ACCOUNT_INVALID", "Select an active, directly postable disposal income or expense account.");
                    lines.Add(new InvoiceLineItemCreateDto
                    {
                        Id = AuctionInvoiceLineId(disposal.TenantId, disposal.Id, source.Id), LineItemType = "GLAccount",
                        GLAccountId = account.Id, ProductId = null, Description = $"{item.ItemCode} - {item.Name}"[..Math.Min(200, item.ItemCode.Length + item.Name.Length + 3)],
                        Quantity = source.Quantity, UnitPrice = input.UnitPrice, Unit = item.UnitOfMeasure,
                        TaxGroupId = input.TaxGroupId, TaxTreatment = input.TaxTreatment
                    });
                }
                var invoice = await _invoices.CreateAsync(new InvoiceCreateDto
                {
                    BusinessPartnerId = request.BusinessPartnerId, BusinessPartnerRoleId = request.BusinessPartnerRoleId,
                    InvoiceDate = request.InvoiceDate.Date, DueDate = request.DueDate?.Date, PaymentTermId = request.PaymentTermId,
                    CurrencyCode = settings.BaseCurrency, ExchangeRate = 1, Reference = disposal.DisposalNumber,
                    Notes = $"Auction of inventory disposal {disposal.DisposalNumber}", LineItems = lines,
                    FinanceDimensions = request.FinanceDimensions
                }, AuctionProducer, cancellationToken);
                var persisted = await _db.Invoices.Include(value => value.LineItems).SingleAsync(value =>
                    value.Id == invoice.Id && value.TenantId == disposal.TenantId, cancellationToken);
                var link = new InventoryDisposalAuctionInvoice
                {
                    Id = Guid.NewGuid(), TenantId = disposal.TenantId, InventoryDisposalCaseId = disposal.Id,
                    InvoiceId = invoice.Id, CreatedByUserId = _currentUser.UserId, CreatedAt = DateTime.UtcNow,
                    IdempotencyKey = key, PayloadHash = fingerprint,
                    InvoiceEconomicsJson = InventoryDisposalAuctionInvoiceGuard.Snapshot(persisted)
                };
                _db.Set<InventoryDisposalAuctionInvoice>().Add(link);
                AddAudit(disposal, "AuctionInvoiceCreated", null, new { invoice.Id, invoice.InvoiceNumber, invoice.TotalAmount });
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return await MapForActorAsync(disposal, cancellationToken);
            });
        });

    internal static Guid AuctionInvoiceLineId(Guid tenantId, Guid disposalId, Guid lineId) =>
        DeterministicGuid($"INVENTORY:DISPOSAL:AUCTION:LINE:V1:{tenantId:N}:{disposalId:N}:{lineId:N}");

    private static bool UsesDirectProceeds(InventoryDisposalCase item) =>
        item.Method == InventoryDisposalMethod.Sale || (item.Method == InventoryDisposalMethod.Auction && item.AccountingVersion == 0);

    private async Task<Invoice> RequireAuctionInvoiceAsync(InventoryDisposalCase item, CancellationToken cancellationToken)
    {
        var link = await _db.Set<InventoryDisposalAuctionInvoice>().AsNoTracking().Include(value => value.Invoice)
            .ThenInclude(value => value.LineItems).SingleOrDefaultAsync(value => value.TenantId == item.TenantId &&
                value.InventoryDisposalCaseId == item.Id && !value.IsDeleted, cancellationToken);
        if (link is null || link.Invoice is null || link.Invoice.TenantId != item.TenantId || link.Invoice.IsDeleted ||
            link.Invoice.Status == InvoiceStatus.Cancelled || link.InvoiceEconomicsJson != InventoryDisposalAuctionInvoiceGuard.Snapshot(link.Invoice))
            throw Error("INV_DISPOSAL_AUCTION_INVOICE_REQUIRED", "Create the linked Finance auction invoice with intact disposal lines before preparing stock posting.");
        return link.Invoice;
    }
}

internal static class InventoryDisposalAuctionInvoiceGuard
{
    internal static async Task RequireNotGeneratedAsync(IUnitOfWork unitOfWork, Guid tenantId, Guid invoiceId, CancellationToken ct)
    {
        if (await unitOfWork.Repository<InventoryDisposalAuctionInvoice>().GetQueryable(value =>
            value.TenantId == tenantId && value.InvoiceId == invoiceId).AnyAsync(ct))
            throw new InvalidOperationException("Auction invoice source lines are retained by Inventory disposal. Cancel through the governed invoice process instead of editing or deleting the source invoice.");
    }

    internal static async Task ValidateAsync(IUnitOfWork unitOfWork, Guid tenantId, Invoice invoice,
        FinancePostingProducerContext? producer, CancellationToken ct)
    {
        var link = await unitOfWork.Repository<InventoryDisposalAuctionInvoice>().GetQueryable(value =>
                value.TenantId == tenantId && value.InvoiceId == invoice.Id).Include(value => value.Disposal).SingleOrDefaultAsync(ct);
        if (link is null)
        {
            if (producer?.RouteId == FinanceDimensionRouteId.InventoryDisposalAuctionInvoice)
                throw new InvalidOperationException("The auction invoice has no verified Inventory disposal source.");
            return;
        }
        if (link.IsDeleted || invoice.TenantId != tenantId || invoice.IsDeleted || link.Disposal.TenantId != tenantId ||
            link.Disposal.IsDeleted || link.Disposal.AccountingVersion != 1 || link.Disposal.Method != InventoryDisposalMethod.Auction ||
            link.Disposal.Status is not (InventoryDisposalStatus.Approved or InventoryDisposalStatus.ReadyForExecution or InventoryDisposalStatus.AdjustmentPending or InventoryDisposalStatus.Completed) ||
            producer?.RouteId != FinanceDimensionRouteId.InventoryDisposalAuctionInvoice || link.InvoiceEconomicsJson != Snapshot(invoice))
            throw new InvalidOperationException("The auction invoice no longer matches its authorized Inventory disposal economics or source route.");
    }

    internal static string Snapshot(Invoice invoice) => JsonSerializer.Serialize(new
    {
        invoice.Id, invoice.TenantId, invoice.BusinessPartnerId, invoice.BusinessPartnerRoleId,
        invoice.CurrencyCode, ExchangeRate = Number(invoice.ExchangeRate), invoice.ExchangeRateId,
        InvoiceDate = invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DueDate = invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        SubTotal = Number(invoice.SubTotal), TaxAmount = Number(invoice.TaxAmount), DiscountAmount = Number(invoice.DiscountAmount),
        TotalAmount = Number(invoice.TotalAmount), invoice.Reference, invoice.IsOpeningBalance,
        Lines = invoice.LineItems.Where(value => !value.IsDeleted).OrderBy(value => value.Id).Select(value => new
        {
            value.Id, value.LineItemType, value.GLAccountId, value.ProductId, value.Description, Quantity = Number(value.Quantity),
            UnitPrice = Number(value.UnitPrice), LineTotal = Number(value.LineTotal), TaxAmount = Number(value.TaxAmount),
            TaxRate = Number(value.TaxRate), value.TaxTreatment, value.TaxGroupId,
            DiscountAmount = Number(value.DiscountAmount), DiscountPercentage = Number(value.DiscountPercentage)
        })
    });
    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
