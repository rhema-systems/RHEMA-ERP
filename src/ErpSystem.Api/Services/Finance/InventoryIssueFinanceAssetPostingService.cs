using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

/// <summary>
/// Atomic Inventory-to-Finance/Fixed-Assets bridge. Callers already own a serializable
/// Inventory transaction; this service deliberately joins that transaction so a stock move,
/// balanced journal, asset/custody record and immutable lineage either all commit or all roll back.
/// </summary>
public sealed class InventoryIssueFinanceAssetPostingService : IInventoryIssueFinanceAssetService
{
    private const string IssueSourceType = "InventoryIssueVoucher";
    private const string ReturnSourceType = "InventoryReturnVoucher";
    private readonly ApplicationDbContext _db;
    private readonly IFinancePostingEngine _posting;
    private readonly IFixedAssetService _fixedAssets;
    private readonly ICurrentUserProvider _currentUser;

    public InventoryIssueFinanceAssetPostingService(
        ApplicationDbContext db,
        IFinancePostingEngine posting,
        IFixedAssetService fixedAssets,
        ICurrentUserProvider currentUser)
    {
        _db = db;
        _posting = posting;
        _fixedAssets = fixedAssets;
        _currentUser = currentUser;
    }

    public async Task PostIssueAsync(Guid issueVoucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await _db.InventoryIssueVouchers
            .Include(value => value.Warehouse)
            .Include(value => value.ReceiverUser)
            .Include(value => value.Lines).ThenInclude(value => value.InventoryItem).ThenInclude(value => value.Category)
            .SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.Id == issueVoucherId && !value.IsDeleted, cancellationToken)
            ?? throw Control("INV_ISSUE_VOUCHER_NOT_FOUND", "The issue voucher was not found for Finance posting.");
        EnsureAmbientTransaction("issue");

        var existingLineages = await _db.InventoryIssueFinanceLineages
            .Where(value => value.TenantId == voucher.TenantId &&
                value.InventoryIssueVoucherLine.InventoryIssueVoucherId == voucher.Id && !value.IsDeleted)
            .ToListAsync(cancellationToken);
        if (voucher.FinancePostingEventId.HasValue || voucher.FinanceJournalEntryId.HasValue || existingLineages.Count > 0)
        {
            if (voucher.FinancePostingEventId.HasValue && voucher.FinanceJournalEntryId.HasValue &&
                existingLineages.Count == voucher.Lines.Count &&
                existingLineages.All(value => value.PostingEventId == voucher.FinancePostingEventId &&
                                              value.JournalEntryId == voucher.FinanceJournalEntryId))
                return;
            throw Control("INV_ISSUE_FINANCE_LINEAGE_CONFLICT",
                "The issue voucher has incomplete or contradictory Finance lineage and cannot be posted again.");
        }

        var movementReason = NormalizeReason(voucher.MovementReasonCode);
        var now = voucher.IssuedAtUtc;
        var rules = await _db.InventoryIssueAccountingRules.AsNoTracking()
            .Include(value => value.ExpenseAccount)
            .Include(value => value.FixedAssetCategory)
            .Where(value => value.TenantId == voucher.TenantId && value.IsActive && !value.IsDeleted &&
                value.MovementReasonCode == movementReason && value.EffectiveFromUtc <= now &&
                (!value.EffectiveToUtc.HasValue || value.EffectiveToUtc.Value > now))
            .ToListAsync(cancellationToken);

        var resolved = new List<(InventoryIssueVoucherLine Line, InventoryIssueAccountingRule Rule)>();
        foreach (var line in voucher.Lines.OrderBy(value => value.CreatedAt).ThenBy(value => value.Id))
        {
            var rule = rules.SingleOrDefault(value =>
                value.InventoryCategoryId == line.InventoryItem.CategoryId &&
                value.ItemType == line.InventoryItem.ItemType)
                ?? throw Control("INV_ISSUE_ACCOUNTING_RULE_MISSING",
                    $"No effective issue-accounting rule covers {line.InventoryItem.ItemCode} for {movementReason}.");
            ValidateRule(rule, line);
            resolved.Add((line, rule));
        }

        var settings = await GetFinanceSettingsAsync(voucher.TenantId, cancellationToken);
        var inventoryAccountId = settings.ControlAccountInventoryId
            ?? throw Control("INV_ISSUE_INVENTORY_ACCOUNT_MISSING",
                "Inventory Control Account is not configured in Finance Settings.");
        await RequirePostingAccountAsync(voucher.TenantId, inventoryAccountId, "Inventory Control Account", cancellationToken);

        var currency = Currency(settings.BaseCurrency);
        var lines = new List<FinancePostingLineDto>();
        var sequence = 1;
        decimal total = 0m;
        foreach (var (line, rule) in resolved)
        {
            var amount = Money(line.TotalValue);
            if (amount <= 0m)
                throw Control("INV_ISSUE_VALUE_REQUIRED",
                    $"A positive server-derived issue value is required for {line.InventoryItem.ItemCode}.");
            var debitAccountId = rule.Treatment == InventoryIssueAccountingTreatment.FixedAsset
                ? rule.FixedAssetCategory!.AssetAccountId
                : rule.ExpenseAccountId!.Value;
            var token = line.Id.ToString("N");
            lines.Add(Line(
                debitAccountId,
                $"Issue {voucher.VoucherNumber} - {line.InventoryItem.ItemCode}",
                amount,
                0m,
                currency,
                sequence++,
                voucher.VoucherNumber,
                $"InventoryIssueVoucherLineId={token}",
                rule.Treatment == InventoryIssueAccountingTreatment.FixedAsset
                    ? $"INV-ISSUE-FA-{token}" : $"INV-ISSUE-EXP-{token}"));
            total += amount;
        }
        lines.Add(Line(
            inventoryAccountId,
            $"Inventory control release {voucher.VoucherNumber}",
            0m,
            Money(total),
            currency,
            sequence,
            voucher.VoucherNumber,
            $"InventoryIssueVoucherId={voucher.Id:N}",
            "INV-ISSUE-CONTROL"));

        var result = await _posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = IssueSourceType,
            SourceDocumentId = voucher.Id,
            SourceDocumentTenantId = voucher.TenantId,
            PostingAction = "PostInventoryIssue",
            SourceDocumentReference = voucher.VoucherNumber,
            Description = $"Governed inventory issue {voucher.VoucherNumber} - {movementReason}",
            PostingDate = voucher.IssuedAtUtc,
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"InventoryIssueVoucher:{voucher.TenantId:N}:{voucher.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        }, cancellationToken);

        voucher.FinancePostingEventId = result.PostingEventId;
        voucher.FinanceJournalEntryId = result.JournalEntryId;
        voucher.UpdatedAt = DateTime.UtcNow;
        voucher.IntegrityHash = IssueVoucherIntegrity(voucher);

        foreach (var (line, rule) in resolved)
        {
            Guid? fixedAssetId = null;
            if (rule.Treatment == InventoryIssueAccountingTreatment.FixedAsset)
            {
                if (!voucher.ReceiverUser.EmployeeId.HasValue)
                    throw Control("INV_ISSUE_ASSET_CUSTODIAN_REQUIRED",
                        "The selected receiver is not linked to an active employee and cannot hold a fixed asset.");
                var asset = await _fixedAssets.RegisterInventoryIssueAssetAsync(new RegisterInventoryIssueFixedAssetDto
                {
                    IssueVoucherId = voucher.Id,
                    IssueVoucherLineId = line.Id,
                    IssueVoucherNumber = voucher.VoucherNumber,
                    FixedAssetCategoryId = rule.FixedAssetCategoryId!.Value,
                    ItemCode = line.InventoryItem.ItemCode,
                    ItemName = line.InventoryItem.Name,
                    Description = line.InventoryItem.Description,
                    Location = voucher.Warehouse.Name,
                    SerialNumber = line.SerialNumber!,
                    CustodianEmployeeId = voucher.ReceiverUser.EmployeeId.Value,
                    IssueDate = voucher.IssuedAtUtc,
                    PostingEventId = result.PostingEventId,
                    JournalEntryId = result.JournalEntryId
                }, cancellationToken);
                fixedAssetId = asset.Id;
            }

            var lineage = new InventoryIssueFinanceLineage
            {
                TenantId = voucher.TenantId,
                InventoryIssueVoucherLineId = line.Id,
                InventoryIssueAccountingRuleId = rule.Id,
                Treatment = rule.Treatment,
                MovementReasonCode = movementReason,
                IssuedQuantity = line.Quantity,
                ReturnedQuantity = 0m,
                IssuedValue = Money(line.TotalValue),
                PostingEventId = result.PostingEventId,
                JournalEntryId = result.JournalEntryId,
                FixedAssetId = fixedAssetId,
                Status = InventoryIssueFinanceLineageStatus.Posted,
                CreatedAt = DateTime.UtcNow
            };
            lineage.IntegrityHash = Hash(new
            {
                lineage.TenantId,
                lineage.InventoryIssueVoucherLineId,
                lineage.InventoryIssueAccountingRuleId,
                lineage.Treatment,
                lineage.MovementReasonCode,
                lineage.IssuedQuantity,
                lineage.IssuedValue,
                lineage.PostingEventId,
                lineage.JournalEntryId,
                lineage.FixedAssetId
            });
            _db.InventoryIssueFinanceLineages.Add(lineage);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task PostReturnAsync(Guid returnVoucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await LoadReturnAsync(returnVoucherId, cancellationToken);
        EnsureAmbientTransaction("return");
        if (voucher.Status != InventoryReturnVoucherStatus.Posted)
            throw Control("INV_RETURN_FINANCE_STATE", "Only a posted Store Return Voucher can update Finance and Fixed Assets.");

        var existing = await _db.InventoryIssueReturnAllocations
            .Where(value => value.TenantId == voucher.TenantId &&
                value.InventoryReturnVoucherLine.InventoryReturnVoucherId == voucher.Id && !value.IsDeleted)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            if (existing.All(value => !value.ReversalPostingEventId.HasValue) &&
                existing.Select(value => value.InventoryReturnVoucherLineId).Distinct().Count() == voucher.Lines.Count)
                return;
            throw Control("INV_RETURN_FINANCE_LINEAGE_CONFLICT",
                "The return voucher has incomplete, reversed or contradictory Finance lineage.");
        }

        var candidates = await _db.InventoryIssueFinanceLineages
            .Include(value => value.InventoryIssueAccountingRule).ThenInclude(value => value.FixedAssetCategory)
            .Include(value => value.InventoryIssueVoucherLine).ThenInclude(value => value.InventoryIssueVoucher)
            .Where(value => value.TenantId == voucher.TenantId &&
                value.InventoryIssueVoucherLine.InventoryIssueVoucher.InventoryRequisitionId == voucher.InventoryRequisitionId &&
                value.ReturnedQuantity < value.IssuedQuantity && !value.IsDeleted)
            .OrderBy(value => value.InventoryIssueVoucherLine.InventoryIssueVoucher.IssuedAtUtc)
            .ThenBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);

        var planned = new List<(InventoryReturnVoucherLine ReturnLine, InventoryIssueFinanceLineage Lineage,
            decimal Quantity, decimal Value, Guid CreditAccountId)>();
        foreach (var returnLine in voucher.Lines.OrderBy(value => value.CreatedAt).ThenBy(value => value.Id))
        {
            var remaining = returnLine.Quantity;
            var matching = candidates.Where(value =>
                    value.InventoryIssueVoucherLine.InventoryRequisitionItemId == returnLine.InventoryRequisitionItemId &&
                    TrackingMatches(value.InventoryIssueVoucherLine, returnLine))
                .ToList();
            foreach (var lineage in matching)
            {
                if (remaining <= 0m) break;
                var available = lineage.IssuedQuantity - lineage.ReturnedQuantity;
                if (available <= 0m) continue;
                var quantity = Math.Min(remaining, available);
                if (lineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset &&
                    (quantity != 1m || returnLine.Quantity != 1m || available != 1m || !lineage.FixedAssetId.HasValue))
                    throw Control("INV_RETURN_ASSET_WHOLE_UNIT_REQUIRED",
                        "A fixed asset must be returned as its complete serial-tracked unit.");
                var unitValue = lineage.IssuedValue / lineage.IssuedQuantity;
                var value = Money(quantity * unitValue);
                var accountId = lineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset
                    ? lineage.InventoryIssueAccountingRule.FixedAssetCategory!.AssetAccountId
                    : lineage.InventoryIssueAccountingRule.ExpenseAccountId!.Value;
                planned.Add((returnLine, lineage, quantity, value, accountId));
                remaining -= quantity;
            }
            if (remaining > 0m)
                throw Control("INV_RETURN_ISSUE_LINEAGE_INSUFFICIENT",
                    "The return quantity cannot be reconciled to unreversed issue and tracking lineage.");
            var plannedValue = Money(planned.Where(value => value.ReturnLine.Id == returnLine.Id).Sum(value => value.Value));
            if (Math.Abs(plannedValue - Money(returnLine.TotalValue)) > 0.01m)
                throw Control("INV_RETURN_VALUE_MISMATCH",
                    "The return value does not reconcile to the original issue value.");
        }

        var settings = await GetFinanceSettingsAsync(voucher.TenantId, cancellationToken);
        var inventoryAccountId = settings.ControlAccountInventoryId
            ?? throw Control("INV_RETURN_INVENTORY_ACCOUNT_MISSING",
                "Inventory Control Account is not configured in Finance Settings.");
        await RequirePostingAccountAsync(voucher.TenantId, inventoryAccountId, "Inventory Control Account", cancellationToken);
        var currency = Currency(settings.BaseCurrency);
        var postingLines = new List<FinancePostingLineDto>();
        var sequence = 1;
        foreach (var group in planned.GroupBy(value => value.ReturnLine.Id))
        {
            var amount = Money(group.Sum(value => value.Value));
            var sourceLine = group.First().ReturnLine;
            var token = sourceLine.Id.ToString("N");
            postingLines.Add(Line(inventoryAccountId,
                $"Store return {voucher.VoucherNumber}", amount, 0m, currency, sequence++, voucher.VoucherNumber,
                $"InventoryReturnVoucherLineId={token}", $"INV-RET-CTL-{token}"));
            foreach (var allocation in group)
                postingLines.Add(Line(allocation.CreditAccountId,
                    $"Reverse issue value {voucher.VoucherNumber}", 0m, allocation.Value, currency, sequence++,
                    voucher.VoucherNumber, $"InventoryReturnVoucherLineId={token}",
                    allocation.Lineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset
                        ? $"INV-RETURN-FA-{token}" : $"INV-RETURN-EXP-{token}"));
        }

        var result = await _posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = ReturnSourceType,
            SourceDocumentId = voucher.Id,
            SourceDocumentTenantId = voucher.TenantId,
            PostingAction = "PostInventoryReturn",
            SourceDocumentReference = voucher.VoucherNumber,
            Description = $"Governed Store Return Voucher {voucher.VoucherNumber}",
            PostingDate = voucher.PostedAtUtc ?? DateTime.UtcNow,
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = currency,
            IdempotencyKey = $"InventoryReturnVoucher:{voucher.TenantId:N}:{voucher.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            Lines = postingLines
        }, cancellationToken);

        foreach (var item in planned)
        {
            item.Lineage.ReturnedQuantity += item.Quantity;
            item.Lineage.Status = item.Lineage.ReturnedQuantity == item.Lineage.IssuedQuantity
                ? InventoryIssueFinanceLineageStatus.Returned
                : InventoryIssueFinanceLineageStatus.PartiallyReturned;
            item.Lineage.UpdatedAt = DateTime.UtcNow;
            var allocation = new InventoryIssueReturnAllocation
            {
                TenantId = voucher.TenantId,
                InventoryReturnVoucherLineId = item.ReturnLine.Id,
                InventoryIssueFinanceLineageId = item.Lineage.Id,
                Quantity = item.Quantity,
                Value = item.Value,
                ReturnPostingEventId = result.PostingEventId,
                ReturnJournalEntryId = result.JournalEntryId,
                PostedAtUtc = DateTime.UtcNow
            };
            allocation.IntegrityHash = Hash(new
            {
                allocation.TenantId,
                allocation.InventoryReturnVoucherLineId,
                allocation.InventoryIssueFinanceLineageId,
                allocation.Quantity,
                allocation.Value,
                allocation.ReturnPostingEventId,
                allocation.ReturnJournalEntryId
            });
            _db.InventoryIssueReturnAllocations.Add(allocation);

            if (item.Lineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset)
                await _fixedAssets.ReverseInventoryIssueAssetAsync(item.Lineage.FixedAssetId!.Value,
                    new ReverseInventoryIssueFixedAssetDto
                    {
                        ReturnVoucherId = voucher.Id,
                        ReturnVoucherLineId = item.ReturnLine.Id,
                        ReturnVoucherNumber = voucher.VoucherNumber,
                        ReturnDate = voucher.PostedAtUtc ?? DateTime.UtcNow,
                        Reason = voucher.Reason,
                        PostingEventId = result.PostingEventId,
                        JournalEntryId = result.JournalEntryId
                    }, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReverseReturnAsync(
        Guid returnVoucherId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var voucher = await LoadReturnAsync(returnVoucherId, cancellationToken);
        EnsureAmbientTransaction("return reversal");
        if (voucher.Status != InventoryReturnVoucherStatus.Reversed)
            throw Control("INV_RETURN_REVERSAL_FINANCE_STATE",
                "Only a reversed Store Return Voucher can compensate Finance and Fixed Assets.");

        var allocations = await _db.InventoryIssueReturnAllocations
            .Include(value => value.InventoryIssueFinanceLineage)
            .Include(value => value.InventoryReturnVoucherLine)
            .Where(value => value.TenantId == voucher.TenantId &&
                value.InventoryReturnVoucherLine.InventoryReturnVoucherId == voucher.Id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        if (allocations.Count == 0)
            throw Control("INV_RETURN_REVERSAL_LINEAGE_MISSING",
                "The return has no Finance lineage to reverse.");
        if (allocations.All(value => value.ReversalPostingEventId.HasValue && value.ReversalJournalEntryId.HasValue))
            return;
        if (allocations.Any(value => value.ReversalPostingEventId.HasValue || value.ReversalJournalEntryId.HasValue) ||
            allocations.Select(value => value.ReturnPostingEventId).Distinct().Count() != 1)
            throw Control("INV_RETURN_REVERSAL_LINEAGE_CONFLICT",
                "The return has partial or contradictory reversal lineage.");

        var originalEventId = allocations[0].ReturnPostingEventId;
        var plan = await _posting.GetReversalPlanAsync(originalEventId, reason, voucher.ReversedAtUtc, cancellationToken);
        if (!plan.IsDefined || plan.ReversalLines.Count == 0)
            throw Control("INV_RETURN_REVERSAL_PLAN_MISSING",
                "Finance could not derive a balanced reversal for the Store Return Voucher.");
        var fixedAssetAllocations = allocations
            .Where(value => value.InventoryIssueFinanceLineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset)
            .Select(value => new KeyValuePair<Guid, decimal>(value.InventoryReturnVoucherLineId, value.Value))
            .ToList();
        if (fixedAssetAllocations.Count > 0)
        {
            var originalLines = await _db.AccountTransactions.AsNoTracking()
                .Where(value => value.TenantId == voucher.TenantId &&
                    value.JournalEntryId == plan.OriginalJournalEntryId && !value.IsDeleted)
                .ToListAsync(cancellationToken);
            PreserveFixedAssetReversalLineage(plan.ReversalLines, originalLines, fixedAssetAllocations, reason);
        }
        var settings = await GetFinanceSettingsAsync(voucher.TenantId, cancellationToken);
        var result = await _posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "Inventory",
            OriginModuleCode = FinanceModuleLockCatalog.Inventory,
            SourceDocumentType = ReturnSourceType,
            SourceDocumentId = voucher.Id,
            SourceDocumentTenantId = voucher.TenantId,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = reason,
            ReversalType = "Full",
            PostingAction = "ReverseInventoryReturn",
            SourceDocumentReference = voucher.VoucherNumber,
            Description = $"Reversal of Store Return Voucher {voucher.VoucherNumber}",
            PostingDate = plan.ReversalDate,
            JournalType = "System Generated",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = Currency(settings.BaseCurrency),
            IdempotencyKey = $"InventoryReturnVoucher:{voucher.TenantId:N}:{voucher.Id:N}:Reverse",
            ReturnExistingOnDuplicate = true,
            Lines = plan.ReversalLines
        }, cancellationToken);

        foreach (var allocation in allocations)
        {
            allocation.ReversalPostingEventId = result.PostingEventId;
            allocation.ReversalJournalEntryId = result.JournalEntryId;
            allocation.ReversedAtUtc = DateTime.UtcNow;
            allocation.UpdatedAt = DateTime.UtcNow;
            var lineage = allocation.InventoryIssueFinanceLineage;
            lineage.ReturnedQuantity -= allocation.Quantity;
            if (lineage.ReturnedQuantity < 0m)
                throw Control("INV_RETURN_REVERSAL_QUANTITY_CONFLICT",
                    "The return reversal would make issue-line returned quantity negative.");
            lineage.Status = lineage.ReturnedQuantity == 0m
                ? InventoryIssueFinanceLineageStatus.Posted
                : InventoryIssueFinanceLineageStatus.PartiallyReturned;
            lineage.UpdatedAt = DateTime.UtcNow;

            if (lineage.Treatment == InventoryIssueAccountingTreatment.FixedAsset)
                await _fixedAssets.ReinstateInventoryIssueAssetAsync(lineage.FixedAssetId!.Value,
                    new ReinstateInventoryIssueFixedAssetDto
                    {
                        ReturnVoucherId = voucher.Id,
                        ReturnVoucherLineId = allocation.InventoryReturnVoucherLineId,
                        ReturnVoucherNumber = voucher.VoucherNumber,
                        ReversalDate = plan.ReversalDate,
                        Reason = reason,
                        PostingEventId = result.PostingEventId,
                        JournalEntryId = result.JournalEntryId
                    }, cancellationToken);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    internal static void PreserveFixedAssetReversalLineage(
        IReadOnlyList<FinancePostingLineDto> reversalLines,
        IReadOnlyList<AccountTransaction> originalLines,
        IReadOnlyList<KeyValuePair<Guid, decimal>> fixedAssetAllocations,
        string reason)
    {
        var claimedLineNumbers = new HashSet<int>();
        foreach (var allocation in fixedAssetAllocations)
        {
            var token = allocation.Key.ToString("N");
            var amount = Money(allocation.Value);
            var original = originalLines.SingleOrDefault(value =>
                value.CreditAmount == amount && value.DebitAmount == 0m &&
                (value.Notes?.Contains(token, StringComparison.OrdinalIgnoreCase) == true ||
                 value.TransactionTag?.Contains(token, StringComparison.OrdinalIgnoreCase) == true));
            if (original is null)
                throw Control("INV_RETURN_ASSET_REVERSAL_SOURCE_MISSING",
                    "The original return journal does not contain the fixed-asset return-line value.");

            var reversal = reversalLines.SingleOrDefault(value =>
                value.LineNumber == original.LineNumber && value.AccountId == original.AccountId &&
                Money(value.DebitAmount) == amount && value.CreditAmount == 0m);
            if (reversal is null || !reversal.LineNumber.HasValue ||
                !claimedLineNumbers.Add(reversal.LineNumber.Value))
                throw Control("INV_RETURN_ASSET_REVERSAL_LINEAGE_MISSING",
                    "The Finance reversal plan does not preserve the fixed-asset return-line value.");

            reversal.Notes = $"InventoryReturnVoucherLineId={token};ReversalReason={reason}";
            reversal.TransactionTag = $"INV-RET-FA-R-{token}";
        }
    }

    public async Task<IReadOnlyList<InventoryIssueFinanceLineageDto>> GetIssueLineageAsync(
        Guid issueVoucherId,
        CancellationToken cancellationToken = default)
    {
        return await _db.InventoryIssueFinanceLineages.AsNoTracking()
            .Where(value => value.TenantId == _currentUser.TenantId &&
                value.InventoryIssueVoucherLine.InventoryIssueVoucherId == issueVoucherId && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id)
            .Select(value => new InventoryIssueFinanceLineageDto
            {
                Id = value.Id,
                InventoryIssueVoucherLineId = value.InventoryIssueVoucherLineId,
                Treatment = value.Treatment,
                MovementReasonCode = value.MovementReasonCode,
                IssuedQuantity = value.IssuedQuantity,
                ReturnedQuantity = value.ReturnedQuantity,
                IssuedValue = value.IssuedValue,
                PostingEventId = value.PostingEventId,
                JournalEntryId = value.JournalEntryId,
                FixedAssetId = value.FixedAssetId,
                Status = value.Status
            }).ToListAsync(cancellationToken);
    }

    private async Task<InventoryReturnVoucher> LoadReturnAsync(Guid id, CancellationToken cancellationToken)
        => await _db.InventoryReturnVouchers
            .Include(value => value.Lines)
            .SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                value.Id == id && !value.IsDeleted, cancellationToken)
           ?? throw Control("INV_RETURN_VOUCHER_NOT_FOUND", "The Store Return Voucher was not found for Finance posting.");

    private async Task<FinanceSettings> GetFinanceSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _db.FinanceSettings.AsNoTracking()
            .SingleOrDefaultAsync(value => value.TenantId == tenantId && !value.IsDeleted, cancellationToken)
           ?? throw Control("INV_FINANCE_SETTINGS_MISSING", "Finance settings are not configured for this tenant.");

    private async Task RequirePostingAccountAsync(
        Guid tenantId,
        Guid accountId,
        string role,
        CancellationToken cancellationToken)
    {
        var valid = await _db.Accounts.AsNoTracking().AnyAsync(value =>
            value.TenantId == tenantId && value.Id == accountId && value.Status == AccountStatus.Active &&
            value.AllowDirectPosting && !value.IsDeleted,
            cancellationToken);
        if (!valid) throw Control("INV_ISSUE_ACCOUNT_INVALID", $"{role} must be an active posting account for this tenant.");
    }

    private void EnsureAmbientTransaction(string action)
    {
        if (_db.Database.CurrentTransaction == null)
            throw Control("INV_ISSUE_ATOMIC_TRANSACTION_REQUIRED",
                $"Inventory {action} accounting must run inside the governing stock transaction.");
    }

    private static void ValidateRule(InventoryIssueAccountingRule rule, InventoryIssueVoucherLine line)
    {
        if (rule.Treatment == InventoryIssueAccountingTreatment.Expense)
        {
            if (!rule.ExpenseAccountId.HasValue || rule.FixedAssetCategoryId.HasValue || rule.ExpenseAccount == null ||
                rule.ExpenseAccount.Status != AccountStatus.Active || !rule.ExpenseAccount.AllowDirectPosting ||
                rule.ExpenseAccount.AccountType != AccountType.Expense)
                throw Control("INV_ISSUE_EXPENSE_RULE_INVALID",
                    $"The expense mapping for {line.InventoryItem.ItemCode} is not an active Finance expense posting account.");
            if (line.InventoryItem.ItemType == ItemType.FixedAsset ||
                string.Equals(rule.MovementReasonCode, InventoryIssueMovementReasons.AssetCustody, StringComparison.Ordinal))
                throw Control("INV_ISSUE_ASSET_MAPPING_REQUIRED",
                    $"Fixed-asset item {line.InventoryItem.ItemCode} must use the asset-custody mapping.");
            return;
        }

        if (rule.Treatment != InventoryIssueAccountingTreatment.FixedAsset ||
            !rule.FixedAssetCategoryId.HasValue || rule.ExpenseAccountId.HasValue || rule.FixedAssetCategory == null ||
            line.InventoryItem.ItemType != ItemType.FixedAsset ||
            !string.Equals(rule.MovementReasonCode, InventoryIssueMovementReasons.AssetCustody, StringComparison.Ordinal) ||
            line.Quantity != 1m || string.IsNullOrWhiteSpace(line.SerialNumber))
            throw Control("INV_ISSUE_FIXED_ASSET_RULE_INVALID",
                $"Fixed-asset issue {line.InventoryItem.ItemCode} requires one serial-tracked unit and a Fixed Assets category mapping.");
    }

    private static string NormalizeReason(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!InventoryIssueMovementReasons.Labels.ContainsKey(code))
            throw Control("INV_ISSUE_MOVEMENT_REASON_INVALID", "A controlled inventory movement reason is required.");
        return code;
    }

    private static bool TrackingMatches(InventoryIssueVoucherLine issued, InventoryReturnVoucherLine returned)
        => Same(issued.SerialNumber, returned.SerialNumber) &&
           Same(issued.LotNumber, returned.LotNumber) &&
           Same(issued.BatchNumber, returned.BatchNumber);

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static FinancePostingLineDto Line(
        Guid accountId,
        string description,
        decimal debit,
        decimal credit,
        string currency,
        int number,
        string reference,
        string notes,
        string tag) => new()
    {
        AccountId = accountId,
        Description = description,
        DebitAmount = Money(debit),
        CreditAmount = Money(credit),
        TransactionCurrency = currency,
        TransactionDebitAmount = Money(debit),
        TransactionCreditAmount = Money(credit),
        ExchangeRate = 1m,
        ExchangeRateSource = "Functional currency",
        ExchangeRateDate = DateTime.UtcNow,
        SourceReferenceNumber = reference,
        LineNumber = number,
        Notes = notes,
        TransactionTag = tag
    };

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Currency(string? value) => string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
    private static string IssueVoucherIntegrity(InventoryIssueVoucher voucher) => Hash(new
    {
        voucher.TenantId,
        voucher.Id,
        voucher.VoucherNumber,
        voucher.InventoryRequisitionId,
        voucher.Status,
        voucher.WarehouseId,
        voucher.LocationId,
        voucher.DepartmentId,
        voucher.CostCenter,
        voucher.ProjectId,
        voucher.RequestedById,
        voucher.ApprovedById,
        voucher.IssuedById,
        voucher.ReceiverUserId,
        voucher.AcknowledgedById,
        voucher.IssuedAtUtc,
        voucher.AcknowledgedAtUtc,
        voucher.ReceiverComment,
        voucher.MovementReasonCode,
        voucher.FinancePostingEventId,
        voucher.FinanceJournalEntryId,
        voucher.IdempotencyKey,
        voucher.PayloadHash,
        voucher.CorrelationId,
        voucher.SourceSnapshotJson
    });
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static InventoryIssueAccountingControlException Control(string code, string message) => new(code, message);
}
