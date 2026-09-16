using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed partial class ProcurementReceiptDistributionService
{
    private sealed record DistributionDraft(int SchemaVersion, Guid ChangedBy, DateTime ChangedAtUtc,
        List<SavePurchaseReceiptDistributionLine> Lines);

    public async Task<IReadOnlyList<PurchaseReceiptDistributionAccountDto>> AccountsAsync(Guid tenantId, CancellationToken ct)
    {
        var accounts = await db.Accounts.AsNoTracking().Where(value => value.TenantId == tenantId && !value.IsDeleted &&
            value.Status == AccountStatus.Active && (value.AllowDirectPosting || value.IsControlAccount) &&
            (value.AccountType == AccountType.Asset || value.AccountType == AccountType.Liability ||
             value.AccountType == AccountType.Expense || value.AccountType == AccountType.Revenue))
            .OrderBy(value => value.AccountNumber).Select(value => new { value.Id, value.AccountNumber, value.AccountName, value.AccountType })
            .ToListAsync(ct);
        return accounts.Select(value => new PurchaseReceiptDistributionAccountDto
        {
            Id = value.Id, AccountCode = value.AccountNumber, AccountName = value.AccountName, AccountType = value.AccountType.ToString()
        }).ToList();
    }

    public async Task<PurchaseReceiptDistributionDto> SaveAsync(Guid tenantId, Guid receiptId,
        SavePurchaseReceiptDistributionRequest request, Guid actorId,
        Func<string?, string?, Task> audit, CancellationToken ct = default) =>
        await WriteAsync(tenantId, receiptId, request, request.Lines, actorId, audit, ct);

    public async Task<PurchaseReceiptDistributionDto> ResetAsync(Guid tenantId, Guid receiptId,
        PurchaseReceiptDistributionVersionRequest request, Guid actorId,
        Func<string?, string?, Task> audit, CancellationToken ct = default) =>
        await WriteAsync(tenantId, receiptId, request, null, actorId, audit, ct);

    private async Task<PurchaseReceiptDistributionDto> WriteAsync(Guid tenantId, Guid receiptId,
        PurchaseReceiptDistributionVersionRequest request, List<SavePurchaseReceiptDistributionLine>? lines,
        Guid actorId, Func<string?, string?, Task> audit, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var ownsTransaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            await LockReceiptAsync(tenantId, receiptId, ct);
            var receipt = await LoadReceiptAsync(tenantId, receiptId, ct);
            if (receipt.Status is "Cancelled" or "Rejected" || await HasPostedJournalAsync(tenantId, receiptId, ct))
                throw new ProcurementReceiptInspectionConflictException("RCV_DISTRIBUTION_LOCKED",
                    "This receipt cannot be changed. Posted distributions remain in the original journal.");
            var defaults = await BuildDefaultsAsync(receipt, false, ct);
            if (request.Version != Version(receipt) || request.BasisVersion != BasisVersion(defaults))
                throw new ProcurementReceiptInspectionConflictException("RCV_DISTRIBUTION_CHANGED",
                    "The receipt or its account defaults changed. Refresh the distribution and review your changes before saving.");
            if (lines is not null)
            {
                ValidateLines(lines, defaults);
                await ValidateAccountsAsync(tenantId, lines, ct);
            }
            else
                await ValidateAccountsAsync(tenantId, defaults.Lines.Select(value => new SavePurchaseReceiptDistributionLine
                    { AccountId = value.AccountId, Purpose = PurposeKey(value.TransactionTag) }), ct);
            var before = receipt.DistributionDraftJson;
            var after = lines is null ? null : JsonSerializer.Serialize(new DistributionDraft(1, actorId, DateTime.UtcNow, lines));
            var tracked = db.PurchaseOrderReceipts.Local.FirstOrDefault(value => value.Id == receiptId && value.TenantId == tenantId);
            if (tracked is null)
            {
                tracked = new PurchaseOrderReceipt { Id = receipt.Id, TenantId = tenantId, RowVersion = receipt.RowVersion };
                db.PurchaseOrderReceipts.Attach(tracked);
            }
            db.Entry(tracked).Property(value => value.RowVersion).OriginalValue = receipt.RowVersion;
            tracked.DistributionDraftJson = after;
            db.Entry(tracked).Property(value => value.DistributionDraftJson).IsModified = true;
            await db.SaveChangesAsync(ct);
            await audit(before, after);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return await GetAsync(tenantId, receiptId, ct);
        });
    }

    // Save and posting acquire the same lock and retain it through their enclosing transaction.
    internal async Task LockReceiptAsync(Guid tenantId, Guid receiptId, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Receipt distribution changes require a transaction.");
        await db.PurchaseOrderReceipts.FromSqlInterpolated($"SELECT * FROM [PurchaseOrderReceipts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {receiptId} AND [TenantId] = {tenantId}")
            .AsNoTracking().Select(value => value.Id).SingleAsync(ct);
    }

    private Task<bool> HasPostedJournalAsync(Guid tenantId, Guid receiptId, CancellationToken ct) =>
        db.FinancePostingEvents.AsNoTracking().AnyAsync(value => value.TenantId == tenantId && !value.IsDeleted &&
            value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" && value.SourceDocumentId == receiptId &&
            value.PostingAction == "PostAcceptedInventoryReceipt" && value.PostingStatus == "Posted" && value.JournalEntryId != null, ct);

    private static void ValidateLines(List<SavePurchaseReceiptDistributionLine> lines, ReceiptPostingDistribution defaults)
    {
        if (lines.Count is 0 or > 5000 || lines.Any(value => value is null || value.LineId == Guid.Empty || value.InventoryItemId == Guid.Empty ||
            value.AccountId == Guid.Empty || value.Debit < 0 || value.Credit < 0 || value.Debit > 99999999999999.99m ||
            value.Credit > 99999999999999.99m || (value.Debit == 0) == (value.Credit == 0) ||
            decimal.Round(value.Debit, 2) != value.Debit || decimal.Round(value.Credit, 2) != value.Credit) ||
            lines.Select(value => value.LineId).Distinct().Count() != lines.Count)
            throw Invalid("Use unique distribution lines with one positive debit or credit, rounded to two decimal places.");
        var sources = defaults.Lines.ToDictionary(value => (value.SourceDocumentLineId!.Value, PurposeKey(value.TransactionTag)));
        var groups = lines.GroupBy(value => (value.InventoryItemId, value.Purpose)).ToList();
        if (groups.Count != sources.Count || groups.Any(group => !sources.ContainsKey(group.Key)))
            throw Invalid("Keep a distribution for each receipt item and posting purpose. You can replace accounts or split its amount between accounts.");
        foreach (var group in groups)
        {
            var source = sources[group.Key];
            if (group.Sum(value => value.Debit) != source.DebitAmount || group.Sum(value => value.Credit) != source.CreditAmount)
                throw Invalid($"The {Purpose(source.TransactionTag)} split must total debit {source.DebitAmount:0.00} and credit {source.CreditAmount:0.00} for this item.");
        }
        if (lines.Sum(value => value.Debit) != lines.Sum(value => value.Credit))
            throw Invalid("Total debits and credits must balance.");
    }

    private async Task ValidateAccountsAsync(Guid tenantId, IEnumerable<SavePurchaseReceiptDistributionLine> lines, CancellationToken ct)
    {
        var rows = lines.ToList();
        var ids = rows.Select(value => value.AccountId).Distinct().ToArray();
        var accounts = await db.Accounts.AsNoTracking().Where(value => value.TenantId == tenantId && ids.Contains(value.Id) &&
            !value.IsDeleted && value.Status == AccountStatus.Active && (value.AllowDirectPosting || value.IsControlAccount))
            .ToDictionaryAsync(value => value.Id, ct);
        foreach (var line in rows)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account) || !(line.Purpose switch
                {
                    "Inventory" => account.AccountType == AccountType.Asset,
                    "AccruedPurchases" => account.AccountType is AccountType.Asset or AccountType.Liability,
                    "PurchasePriceVariance" => account.AccountType is AccountType.Expense or AccountType.Revenue,
                    _ => false
                }))
                throw Invalid($"Select an active, eligible {line.Purpose} posting account in this tenant.");
        }
    }

    private static ReceiptPostingDistribution ApplyDraft(PurchaseOrderReceipt receipt, ReceiptPostingDistribution defaults, bool strict)
    {
        if (string.IsNullOrWhiteSpace(receipt.DistributionDraftJson)) return defaults;
        DistributionDraft draft;
        try
        {
            draft = JsonSerializer.Deserialize<DistributionDraft>(receipt.DistributionDraftJson)
                ?? throw new JsonException();
            if (draft.SchemaVersion != 1 || draft.Lines is null) throw new JsonException();
        }
        catch (JsonException) { throw Invalid("The saved distribution could not be read. Reset and save it again before posting."); }
        var result = new ReceiptPostingDistribution(defaults.Currency,
            defaults.Basis + " Saved account splits scale to final accepted values. Overrides apply to this receipt only; later stock movements use item defaults.", defaults.PostingDate);
        var groups = draft.Lines.GroupBy(value => (value.InventoryItemId, value.Purpose)).ToDictionary(value => value.Key, value => value.ToList());
        foreach (var source in defaults.Lines)
        {
            var itemId = source.SourceDocumentLineId!.Value;
            var key = (itemId, PurposeKey(source.TransactionTag));
            if (!groups.TryGetValue(key, out var rows) || rows.Count == 0 ||
                rows.Any(value => value.Debit < 0 || value.Credit < 0 || (value.Debit == 0) == (value.Credit == 0) ||
                    (source.DebitAmount > 0 ? value.Credit > 0 : value.Debit > 0)))
            {
                if (strict) throw Invalid("The receipt's posting purposes changed. Review and save Distribution again before posting.");
                result.NeedsReview = true;
                Add(result, source.AccountId, source.DebitAmount - source.CreditAmount, source.TransactionTag!,
                    "Review default", receipt.ReceiptNumber, itemId);
                continue;
            }
            // Exact final valuation is retained. Fully rejected sources disappear; remaining sources retain their own splits.
            var amounts = MonetaryAllocation.Allocate(rows.Select(value => value.Debit + value.Credit).ToArray(), source.DebitAmount + source.CreditAmount);
            for (var index = 0; index < rows.Count; index++)
            {
                if (amounts[index] == 0m) continue;
                Add(result, rows[index].AccountId, source.DebitAmount > 0 ? amounts[index] : -amounts[index], source.TransactionTag!,
                    "Receipt override", receipt.ReceiptNumber, itemId, rows[index].LineId);
            }
        }
        return result;
    }

    private static string PurposeKey(string? tag) => tag switch
    {
        "INV-RECEIPT-CONTROL" => "Inventory", "INV-RECEIPT-GRV-ACCRUAL" => "AccruedPurchases",
        "INV-RECEIPT-PRICE-VARIANCE" => "PurchasePriceVariance", _ => ""
    };

    private static string Version(PurchaseOrderReceipt receipt) => Hash(Convert.ToBase64String(receipt.RowVersion) + ":" + receipt.DistributionDraftJson);
    private static string BasisVersion(ReceiptPostingDistribution defaults) => Hash(JsonSerializer.Serialize(new
    {
        defaults.Currency,
        Lines = defaults.Lines.Select(value => new { value.SourceDocumentLineId, value.TransactionTag, value.AccountId, value.DebitAmount, value.CreditAmount })
    }));
    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Guid StableLineId(Guid itemId, string tag, Guid accountId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{itemId:N}:{tag}:{accountId:N}"))[..16]);
    private static ProcurementReceiptInspectionValidationException Invalid(string message) => new("RCV_DISTRIBUTION_INVALID", message);
}
