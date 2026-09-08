using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

/// <summary>
/// Builds Stock Adjustment valuation economics before an owner transaction exists. This boundary is
/// deliberately read-only: C5 selection, dimension synchronization/freezing and posting remain in their
/// governed Finance services, while Inventory remains responsible for staging the owner graph.
/// </summary>
public sealed partial class StockAdjustmentValuationIntentBuilder(ApplicationDbContext db)
    : IStockAdjustmentValuationIntentBuilder
{
    public const string ParticipantCode = "INVENTORY.STOCK_ADJUSTMENT.V1";

    public async Task<ProducerAccountingIntentDto> BuildAsync(
        StockAdjustment adjustment,
        ProducerOwnerEffectIdentityDto? expectedOwnerEffect = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSource(adjustment);
        var isOpeningStock = adjustment.ReasonCode == StockAdjustmentReasonCodes.InitialStock;
        if (isOpeningStock &&
            (adjustment.Status != "Approved" || !adjustment.ApprovedById.HasValue || !adjustment.ApprovedAt.HasValue ||
             string.IsNullOrWhiteSpace(adjustment.PayloadHash) || string.IsNullOrWhiteSpace(adjustment.IntegrityHash)))
            throw new InvalidOperationException(
                "Finance can consume only independently approved, immutable INITIAL_STOCK evidence.");

        var settings = await db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == adjustment.TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var inventory = settings.ControlAccountInventoryId
            ?? throw new InvalidOperationException("Inventory Control Account is not configured in Finance Settings.");
        Guid? migrationClearing = isOpeningStock
            ? settings.MigrationClearingAccountId
                ?? throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.")
            : null;
        Guid? expense = isOpeningStock ? null : settings.WriteOffExpenseAccountId;
        Guid? recovery = isOpeningStock ? null : settings.WriteOffRecoveryAccountId;
        var currency = string.IsNullOrWhiteSpace(settings.BaseCurrency)
            ? "GHS"
            : settings.BaseCurrency.Trim().ToUpperInvariant();
        var lines = new List<FinancePostingLineDto>();
        var number = 1;

        foreach (var item in adjustment.Items.Where(item => !item.IsDeleted)
                     .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id))
        {
            var amount = decimal.Round(Math.Abs(item.AdjustmentValue), 2);
            if (amount <= 0) continue;
            var description = $"Stock adjustment {adjustment.AdjustmentNumber} - {item.InventoryItem?.Name ?? item.InventoryItemId.ToString()}";
            if (isOpeningStock)
            {
                if (item.AdjustmentQuantity <= 0 || item.UnitCost <= 0 || item.AdjustmentValue <= 0)
                    throw new InvalidOperationException("Opening stock can post only positive quantity and unit-cost evidence.");
                lines.Add(Line(inventory, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-OPEN-CONTROL", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "opening-control")));
                lines.Add(Line(migrationClearing!.Value, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-OPEN-MIGRATION", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "opening-migration")));
            }
            else if (item.AdjustmentQuantity < 0)
            {
                var expenseAccount = expense
                    ?? throw new InvalidOperationException("Write-off Expense Account is not configured in Finance Settings.");
                lines.Add(Line(expenseAccount, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-EXPENSE", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "writeoff-expense")));
                lines.Add(Line(inventory, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-CONTROL", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "inventory-control")));
            }
            else
            {
                var recoveryAccount = recovery
                    ?? throw new InvalidOperationException("Write-off Recovery Account is not configured in Finance Settings.");
                lines.Add(Line(inventory, description, amount, 0m, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-CONTROL", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "inventory-control")));
                lines.Add(Line(recoveryAccount, description, 0m, amount, currency, number++, adjustment.AdjustmentNumber,
                    "INV-ADJ-RECOVERY", adjustment.AdjustmentDate, SourceLine(adjustment.Id, item.Id, "recovery-income")));
            }
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("The stock adjustment has no non-zero value to post to Finance.");

        var posting = new ProducerFinancePostingRequestDto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = "StockAdjustment",
            SourceDocumentId = adjustment.Id,
            SourceDocumentTenantId = adjustment.TenantId,
            PostingAction = isOpeningStock ? "PostOpeningStock" : "PostStockAdjustment",
            SourceDocumentReference = adjustment.AdjustmentNumber,
            Description = isOpeningStock
                ? $"Inventory opening stock {adjustment.AdjustmentNumber} - source schedule {adjustment.Reference}"
                : $"Inventory stock adjustment {adjustment.AdjustmentNumber} - {adjustment.ReasonCode}",
            PostingDate = adjustment.AdjustmentDate,
            JournalType = "System Generated",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"StockAdjustment:{adjustment.TenantId:N}:{adjustment.Id:N}:Post",
            Lines = lines
        };
        var owner = expectedOwnerEffect is null
            ? DefaultOwnerEffect(adjustment, posting)
            : CanonicalOwnerEffect(expectedOwnerEffect);

        return new ProducerAccountingIntentDto
        {
            AccountingEventId = DeterministicEventId(adjustment.TenantId, adjustment.Id),
            IdempotencyKey = $"STOCK_ADJUSTMENT:{adjustment.TenantId:N}:{adjustment.Id:N}:POST".ToUpperInvariant(),
            ParticipantIdentity = owner.ParticipantCode,
            ExpectedOwnerEffect = owner,
            PostingRequest = posting
        };
    }

    private static void ValidateSource(StockAdjustment adjustment)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        if (adjustment.Id == Guid.Empty || adjustment.TenantId == Guid.Empty)
            throw new InvalidOperationException("STOCK_ADJUSTMENT_SOURCE_IDENTITY_REQUIRED: a stable adjustment and tenant are required.");
        if (adjustment.Items is null)
            throw new InvalidOperationException("STOCK_ADJUSTMENT_GRAPH_REQUIRED: the complete adjustment item graph is required.");
        var active = adjustment.Items.Where(item => !item.IsDeleted).ToList();
        if (active.Any(item => item.Id == Guid.Empty || item.InventoryItemId == Guid.Empty
                               || item.TenantId != adjustment.TenantId
                               || (item.InventoryItem is not null && item.InventoryItem.TenantId != adjustment.TenantId)))
            throw new InvalidOperationException("STOCK_ADJUSTMENT_SOURCE_CONFLICT: every active item must retain its stable tenant and source identity.");
        if (active.Select(item => item.Id).Distinct().Count() != active.Count)
            throw new InvalidOperationException("STOCK_ADJUSTMENT_SOURCE_CONFLICT: active item identities must be unique.");
    }

    private static ProducerOwnerEffectIdentityDto DefaultOwnerEffect(
        StockAdjustment adjustment,
        ProducerFinancePostingRequestDto posting) => new()
    {
        ParticipantCode = ParticipantCode,
        OwnerEntityType = "STOCK_ADJUSTMENT",
        OwnerEntityId = adjustment.Id,
        OwnerAction = posting.PostingAction == "PostOpeningStock" ? "POST_OPENING_STOCK" : "POST_STOCK_ADJUSTMENT",
        EffectFingerprint = EconomicFingerprint(posting)
    };

    private static ProducerOwnerEffectIdentityDto CanonicalOwnerEffect(ProducerOwnerEffectIdentityDto owner)
    {
        var participant = owner.ParticipantCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var entity = owner.OwnerEntityType?.Trim().ToUpperInvariant() ?? string.Empty;
        var action = owner.OwnerAction?.Trim().ToUpperInvariant() ?? string.Empty;
        var fingerprint = owner.EffectFingerprint?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CanonicalIdentityPattern().IsMatch(participant) || !CanonicalIdentityPattern().IsMatch(entity)
            || !CanonicalIdentityPattern().IsMatch(action) || participant.Length > 100 || entity.Length > 100
            || action.Length > 60 || owner.OwnerEntityId == Guid.Empty || fingerprint.Length != 64
            || IsPseudoIdentity(participant) || IsPseudoIdentity(entity) || IsPseudoIdentity(action)
            || fingerprint.All(character => character == '0') || fingerprint.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("STOCK_ADJUSTMENT_OWNER_EFFECT_INVALID: stable canonical owner-effect evidence is required.");
        return new ProducerOwnerEffectIdentityDto
        {
            ParticipantCode = participant,
            OwnerEntityType = entity,
            OwnerEntityId = owner.OwnerEntityId,
            OwnerAction = action,
            EffectFingerprint = fingerprint
        };
    }

    private static string EconomicFingerprint(ProducerFinancePostingRequestDto posting)
    {
        var canonical = new StringBuilder("FIN:C9:STOCK_ADJUSTMENT:V1");
        Add(canonical, posting.SourceModule);
        Add(canonical, posting.OriginModuleCode);
        Add(canonical, posting.SourceDocumentType);
        Add(canonical, posting.SourceDocumentId.ToString("N"));
        Add(canonical, posting.SourceDocumentTenantId?.ToString("N"));
        Add(canonical, posting.PostingAction);
        Add(canonical, posting.SourceDocumentReference);
        Add(canonical, posting.Description);
        Add(canonical, posting.PostingDate.ToString("O", CultureInfo.InvariantCulture));
        Add(canonical, posting.FunctionalCurrencyCode);
        Add(canonical, posting.IdempotencyKey);
        foreach (var line in posting.Lines)
        {
            Add(canonical, line.AccountId.ToString("N"));
            Add(canonical, line.SourceDocumentLineId?.ToString("N"));
            Add(canonical, line.Description);
            Add(canonical, line.DebitAmount.ToString("G29", CultureInfo.InvariantCulture));
            Add(canonical, line.CreditAmount.ToString("G29", CultureInfo.InvariantCulture));
            Add(canonical, line.TransactionCurrency);
            Add(canonical, line.LineNumber?.ToString(CultureInfo.InvariantCulture));
            Add(canonical, line.TransactionTag);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Add(StringBuilder target, string? value)
    {
        var normalized = value ?? string.Empty;
        target.Append('|').Append(normalized.Length).Append(':').Append(normalized);
    }

    private static Guid DeterministicEventId(Guid tenantId, Guid adjustmentId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"FIN:C9:STOCK_ADJUSTMENT_EVENT:V1:{tenantId:N}:{adjustmentId:N}:POST"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static bool IsPseudoIdentity(string value) => value is "ALL" or "ALL_ACTIVE_BOOKS"
        or "ALL_CLASSIFIED_BOOKS" or "ALLCLASSIFIEDBOOKS";

    private static FinancePostingLineDto Line(Guid accountId, string description, decimal debit, decimal credit,
        string currency, int lineNumber, string reference, string tag, DateTime exchangeRateDate, Guid sourceLineId) => new()
    {
        AccountId = accountId,
        SourceDocumentLineId = sourceLineId,
        Description = description,
        DebitAmount = debit,
        CreditAmount = credit,
        TransactionCurrency = currency,
        TransactionDebitAmount = debit,
        TransactionCreditAmount = credit,
        ExchangeRate = 1m,
        ExchangeRateSource = "Functional currency",
        ExchangeRateDate = exchangeRateDate,
        SourceReferenceNumber = reference,
        LineNumber = lineNumber,
        TransactionTag = tag
    };

    private static Guid SourceLine(Guid documentId, Guid itemId, string kind) =>
        FinanceExternalDimensionIdentity.SourceLine(
            FinanceExternalProducerContractId.InventoryStockAdjustment, documentId, kind, itemId);

    [GeneratedRegex("^[A-Z][A-Z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalIdentityPattern();
}
