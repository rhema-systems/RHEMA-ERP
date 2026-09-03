using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Finance-owned GL reclassification behavior for the existing fixed-asset transfer workflow.
/// Keeping this as a partial of <see cref="AssetTransferService"/> is intentional: physical and
/// financial transfers share one request, workflow, audit trail, and idempotency boundary, while
/// the accounting-specific code remains reviewable without creating a competing transfer service.
/// </summary>
public partial class AssetTransferService
{
    private const string ReclassificationPostingAction = "Reclassify";

    private async Task<ReclassificationPreparation> PrepareReclassificationAsync(
        FixedAsset asset,
        RequestAssetTransferDto dto,
        string? targetSegment)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("The Finance posting engine is required for fixed asset GL reclassification.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length < 20)
        {
            throw new InvalidOperationException("GL reclassification requires an accounting reason of at least 20 characters.");
        }

        var hasPendingReclassification = await _context.AssetTransfers.AnyAsync(t =>
            t.TenantId == TenantId &&
            t.FixedAssetId == asset.Id &&
            t.TransferType == AssetTransferType.GlReclassification &&
            (t.Status == AssetTransferStatus.PendingApproval || t.Status == AssetTransferStatus.Approved) &&
            !t.IsDeleted);
        if (hasPendingReclassification)
        {
            throw new InvalidOperationException("This fixed asset already has a pending or approved GL reclassification.");
        }

        var bookClassification = NormalizeBookClassification(dto.BookClassification);
        var bookValue = ResolveReclassificationBookValue(asset, dto.AccountingBookId, bookClassification);
        if (bookValue.AccountingBook == null ||
            bookValue.AccountingBook.TenantId != TenantId ||
            !bookValue.AccountingBook.IsActive ||
            !bookValue.AccountingBook.AllowsPosting)
        {
            throw new InvalidOperationException("GL reclassification requires an active same-tenant accounting book that allows posting.");
        }

        // A category is a single current classification on the asset master, whereas balances
        // can exist in several posting books. Changing that category after moving only one book
        // would leave the other books pointing at the wrong control accounts. TDC currently uses
        // one posting book per asset, so fail closed if a future configuration introduces more.
        var postingBookCount = asset.BookValues.Count(value =>
            value.AccountingBook != null &&
            value.AccountingBook.TenantId == TenantId &&
            value.AccountingBook.IsActive &&
            value.AccountingBook.AllowsPosting);
        if (postingBookCount != 1)
        {
            throw new InvalidOperationException(
                "GL reclassification currently requires exactly one active posting book for the asset.");
        }

        var targetCategory = dto.ToFixedAssetCategoryId.HasValue
            ? await _context.FixedAssetCategories.AsNoTracking().FirstOrDefaultAsync(c =>
                c.TenantId == TenantId && c.Id == dto.ToFixedAssetCategoryId.Value && !c.IsDeleted)
            : asset.Category;
        if (targetCategory == null)
        {
            await RecordBlockedTransferAuditAsync(asset.Id, "Target fixed asset category belongs to another tenant or does not exist.");
            throw new InvalidOperationException("Target fixed asset category belongs to another tenant or does not exist.");
        }

        var accountingDate = (dto.AccountingDate ?? dto.TransferDate).Date;
        var period = await _context.FiscalPeriods.AsNoTracking().FirstOrDefaultAsync(p =>
            p.TenantId == TenantId &&
            !p.IsDeleted &&
            accountingDate >= p.StartDate.Date &&
            accountingDate <= p.EndDate.Date);
        if (period == null)
        {
            throw new InvalidOperationException("No fiscal period covers the GL reclassification accounting date.");
        }

        // The posting engine rechecks period and module locks at posting time. This early check
        // gives the maker a clear response before a workflow request is created.
        if (!period.IsOpen || period.IsClosed || period.IsLocked)
        {
            await RecordBlockedTransferAuditAsync(asset.Id, "The GL reclassification accounting period is closed or locked.");
            throw new InvalidOperationException("The GL reclassification accounting period must be open and unlocked.");
        }

        var balances = await CalculateCurrentReclassificationBalancesAsync(asset.Id, bookValue);
        if (balances.AssetCarryingAmount <= 0m)
        {
            throw new InvalidOperationException("The selected fixed asset book has no positive carrying-account balance to reclassify.");
        }

        await ValidateReclassificationAccountAsync(asset.Id, asset.Category.AssetAccountId, "source asset account", AccountType.Asset);
        await ValidateReclassificationAccountAsync(asset.Id, targetCategory.AssetAccountId, "target asset account", AccountType.Asset);
        await ValidateReclassificationAccountAsync(asset.Id, asset.Category.AccumulatedDepreciationAccountId, "source accumulated depreciation account", AccountType.Asset);
        await ValidateReclassificationAccountAsync(asset.Id, targetCategory.AccumulatedDepreciationAccountId, "target accumulated depreciation account", AccountType.Asset);

        if (balances.AccumulatedImpairment > 0m)
        {
            await ValidateReclassificationAccountAsync(asset.Id, asset.Category.AccumulatedImpairmentAccountId, "source accumulated impairment account", AccountType.Asset);
            await ValidateReclassificationAccountAsync(asset.Id, targetCategory.AccumulatedImpairmentAccountId, "target accumulated impairment account", AccountType.Asset);
        }

        if (balances.RevaluationSurplus > 0m)
        {
            await ValidateReclassificationAccountAsync(asset.Id, asset.Category.RevaluationSurplusAccountId, "source revaluation surplus account", AccountType.Equity);
            await ValidateReclassificationAccountAsync(asset.Id, targetCategory.RevaluationSurplusAccountId, "target revaluation surplus account", AccountType.Equity);
        }

        var effectiveTargetSegment = NormalizeSegmentString(targetSegment) ?? NormalizeSegmentString(asset.CurrentSegmentString);
        var changesCurrentLedgerClassification =
            asset.Category.AssetAccountId != targetCategory.AssetAccountId ||
            asset.Category.AccumulatedDepreciationAccountId != targetCategory.AccumulatedDepreciationAccountId ||
            (balances.AccumulatedImpairment > 0m && asset.Category.AccumulatedImpairmentAccountId != targetCategory.AccumulatedImpairmentAccountId) ||
            (balances.RevaluationSurplus > 0m && asset.Category.RevaluationSurplusAccountId != targetCategory.RevaluationSurplusAccountId) ||
            !string.Equals(NormalizeSegmentString(asset.CurrentSegmentString), effectiveTargetSegment, StringComparison.OrdinalIgnoreCase);
        if (!changesCurrentLedgerClassification)
        {
            throw new InvalidOperationException("The requested GL reclassification does not change an account or reporting segment.");
        }

        return new ReclassificationPreparation(
            asset.Category.Id,
            targetCategory.Id,
            bookValue.AccountingBookId,
            bookValue.BookClassification,
            accountingDate,
            period.Id,
            asset.Category.AssetAccountId,
            targetCategory.AssetAccountId,
            asset.Category.AccumulatedDepreciationAccountId,
            targetCategory.AccumulatedDepreciationAccountId,
            asset.Category.AccumulatedImpairmentAccountId,
            targetCategory.AccumulatedImpairmentAccountId,
            asset.Category.RevaluationSurplusAccountId,
            targetCategory.RevaluationSurplusAccountId,
            balances.AssetCarryingAmount,
            balances.AccumulatedDepreciation,
            balances.AccumulatedImpairment,
            balances.RevaluationSurplus);
    }

    private async Task<AssetTransferDto> CompleteGlReclassificationAsync(Guid transferId)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("The Finance posting engine is required for fixed asset GL reclassification.");
        }

        IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database.IsRelational())
            {
                // The balance snapshot, posting, and asset master update form one accounting unit.
                // Serializable isolation prevents depreciation or valuation from changing the
                // asset between the checker-approved snapshot and the reclassification journal.
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            var transfer = await LoadReclassificationForPostingAsync(transferId);
            if (transfer.Status == AssetTransferStatus.Completed)
            {
                if (transaction != null)
                {
                    await transaction.CommitAsync();
                    await transaction.DisposeAsync();
                    transaction = null;
                }
                return MapToDto(transfer);
            }

            if (transfer.Status != AssetTransferStatus.Approved)
            {
                throw new InvalidOperationException("Only an approved GL reclassification can be posted.");
            }

            ValidateReclassificationMasterState(transfer);
            ValidateReclassificationConfigurationState(transfer);
            var bookValue = ResolveReclassificationBookValue(
                transfer.FixedAsset,
                transfer.AccountingBookId,
                transfer.BookClassification);
            ValidateReclassificationBookState(transfer, bookValue);
            var currentBalances = await CalculateCurrentReclassificationBalancesAsync(transfer.FixedAssetId, bookValue);
            ValidateApprovedBalanceSnapshot(transfer, currentBalances);

            var postingRequest = await BuildReclassificationPostingRequestAsync(transfer);
            var mutableLines = postingRequest.Lines.ToList();
            if (_fixedAssetDimensions is not null)
            {
                var hasDimensionProvenance = await _context.FinanceSourceDimensionAssignments.AsNoTracking()
                    .AnyAsync(item => item.TenantId == TenantId
                        && item.RouteId == ReclassificationProducer.RouteId
                        && item.SourceDocumentId == transfer.Id && !item.IsDeleted);
                if (!hasDimensionProvenance)
                {
                    var inheritedByLine = transfer.FixedAsset.JournalEntryId.HasValue
                        ? mutableLines
                            .Where(line => line.SourceDocumentLineId.HasValue
                                && line.Description?.Contains("source classification", StringComparison.OrdinalIgnoreCase) == true)
                            .ToDictionary(
                                line => line.SourceDocumentLineId!.Value,
                                _ => transfer.FixedAsset.JournalEntryId.Value)
                        : new Dictionary<Guid, Guid>();
                    await _fixedAssetDimensions.SynchronizeAsync(
                        ReclassificationProducer,
                        transfer.Id,
                        transfer.AccountingDate ?? transfer.TransferDate,
                        mutableLines,
                        input: null,
                        inheritedByLine,
                        "Legacy approved fixed asset reclassification adapted before posting.");
                }
                await _fixedAssetDimensions.ValidateFreezeAndApplyAsync(
                    ReclassificationProducer,
                    transfer.Id,
                    transfer.AccountingDate ?? transfer.TransferDate,
                    mutableLines);
            }
            postingRequest.Lines = mutableLines;
            var posting = await _financePostingEngine.PostAsync(
                postingRequest, ReclassificationProducer);

            var asset = transfer.FixedAsset;
            var beforeValues = new
            {
                asset.FixedAssetCategoryId,
                asset.CurrentSegmentString,
                asset.CurrentSegmentLookupValueId,
                transfer.ReclassificationAssetCarryingAmount,
                transfer.ReclassificationAccumulatedDepreciation,
                transfer.ReclassificationAccumulatedImpairment,
                transfer.ReclassificationRevaluationSurplus
            };

            // Historical journals and measurement records remain untouched. Only the current
            // category/segment used by future Finance postings changes after the compensating
            // journal succeeds.
            asset.FixedAssetCategoryId = transfer.ToFixedAssetCategoryId!.Value;
            asset.Category = transfer.ToFixedAssetCategory!;
            asset.CurrentSegmentString = NormalizeSegmentString(transfer.ToSegmentString);
            asset.CurrentSegmentLookupValueId = transfer.ToSegmentLookupValueId;
            asset.Location = transfer.ToLocation;
            asset.CurrentCustodianId = transfer.ToCustodianId;
            asset.UpdatedAt = DateTime.UtcNow;
            asset.UpdatedBy = UserName;

            transfer.Status = AssetTransferStatus.Completed;
            transfer.JournalEntryId = posting.JournalEntryId;
            transfer.PostingEventId = posting.PostingEventId;
            transfer.PostedAt = DateTime.UtcNow;
            transfer.CompletedAt = transfer.PostedAt;
            transfer.FailedAt = null;
            transfer.FailureReason = null;
            transfer.UpdatedAt = DateTime.UtcNow;
            transfer.UpdatedBy = UserName;

            _context.AssetTransactions.Add(new AssetTransaction
            {
                TenantId = TenantId,
                FixedAssetId = transfer.FixedAssetId,
                TransactionDate = transfer.AccountingDate ?? transfer.TransferDate,
                TransactionType = "GL Reclassification",
                Description = BuildTransferDescription(transfer),
                Amount = 0m,
                ResultingBookValue = bookValue.NetBookValue,
                RelatedEntityId = transfer.Id,
                PerformedByUserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            });

            await _context.SaveChangesAsync();

            // Keep the evidence record inside the same relational transaction as the journal and
            // asset-master update. Committing first would allow an audit-write failure to be
            // reported to the caller even though the accounting move had already become durable.
            await RecordTransferAuditAsync(
                FinanceAuditEvents.FixedAssetTransferPosted,
                transfer,
                beforeValues,
                new
                {
                    asset.FixedAssetCategoryId,
                    asset.CurrentSegmentString,
                    asset.CurrentSegmentLookupValueId,
                    transfer.Status,
                    transfer.JournalEntryId,
                    transfer.PostingEventId,
                    transfer.PostedAt
                },
                transfer.Comments ?? transfer.Reason);

            if (transaction != null)
            {
                await transaction.CommitAsync();
                await transaction.DisposeAsync();
                transaction = null;
            }

            return await GetByIdAsync(transfer.Id)
                ?? throw new InvalidOperationException("Posted fixed asset GL reclassification could not be reloaded.");
        }
        catch (Exception ex)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
                await transaction.DisposeAsync();
                transaction = null;
            }

            // Preserve the approved request for a controlled retry. A posting failure must never
            // mutate the asset's category or fabricate a completed transfer. Dispose the failed
            // database transaction before saving retry evidence through the same DbContext.
            _context.ChangeTracker.Clear();
            var failed = await _context.AssetTransfers.FirstOrDefaultAsync(t =>
                t.TenantId == TenantId && t.Id == transferId && !t.IsDeleted);
            if (failed != null && failed.Status != AssetTransferStatus.Completed)
            {
                // Balance/master/configuration drift invalidates what the checker approved. Mark
                // that request terminal so the maker can submit a fresh snapshot. Infrastructure,
                // lock, or other transient failures deliberately remain Approved for safe retry.
                if (ex is StaleReclassificationApprovalException)
                {
                    failed.Status = AssetTransferStatus.Cancelled;
                }
                failed.FailedAt = DateTime.UtcNow;
                failed.FailureReason = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                failed.UpdatedAt = DateTime.UtcNow;
                failed.UpdatedBy = UserName;
                await _context.SaveChangesAsync();
                await RecordTransferAuditAsync(
                    FinanceAuditEvents.FixedAssetTransferPostingFailed,
                    failed,
                    afterValues: new { failed.Status, failed.FailedAt, failed.FailureReason },
                    comment: failed.FailureReason);
            }

            throw;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private async Task<AssetTransfer> LoadReclassificationForPostingAsync(Guid transferId)
        => await _context.AssetTransfers
            .Include(t => t.FixedAsset)
                .ThenInclude(a => a.Category)
            .Include(t => t.FixedAsset)
                .ThenInclude(a => a.BookValues)
                    .ThenInclude(b => b.AccountingBook)
            .Include(t => t.FromFixedAssetCategory)
            .Include(t => t.ToFixedAssetCategory)
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Id == transferId && !t.IsDeleted)
            ?? throw new KeyNotFoundException("GL reclassification transfer not found.");

    private static void ValidateReclassificationMasterState(AssetTransfer transfer)
    {
        if (!transfer.FromFixedAssetCategoryId.HasValue || !transfer.ToFixedAssetCategoryId.HasValue)
        {
            throw new StaleReclassificationApprovalException("GL reclassification category snapshots are incomplete.");
        }

        var asset = transfer.FixedAsset;
        if (asset.FixedAssetCategoryId != transfer.FromFixedAssetCategoryId.Value ||
            !string.Equals(NormalizeSegmentString(asset.CurrentSegmentString), NormalizeSegmentString(transfer.FromSegmentString), StringComparison.OrdinalIgnoreCase) ||
            asset.CurrentSegmentLookupValueId != transfer.FromSegmentLookupValueId)
        {
            throw new StaleReclassificationApprovalException("The asset's category or segment changed after this request was raised. Submit a new GL reclassification.");
        }
    }

    private static void ValidateReclassificationConfigurationState(AssetTransfer transfer)
    {
        var source = transfer.FromFixedAssetCategory;
        var target = transfer.ToFixedAssetCategory;
        if (source == null || target == null ||
            source.AssetAccountId != transfer.FromAssetAccountId ||
            target.AssetAccountId != transfer.ToAssetAccountId ||
            source.AccumulatedDepreciationAccountId != transfer.FromAccumulatedDepreciationAccountId ||
            target.AccumulatedDepreciationAccountId != transfer.ToAccumulatedDepreciationAccountId ||
            source.AccumulatedImpairmentAccountId != transfer.FromAccumulatedImpairmentAccountId ||
            target.AccumulatedImpairmentAccountId != transfer.ToAccumulatedImpairmentAccountId ||
            source.RevaluationSurplusAccountId != transfer.FromRevaluationSurplusAccountId ||
            target.RevaluationSurplusAccountId != transfer.ToRevaluationSurplusAccountId)
        {
            // Category account mappings are mutable configuration. Posting the approved snapshot
            // after an administrator changes those mappings would make the asset master disagree
            // with its new GL balance, so require a fresh maker/checker decision instead.
            throw new StaleReclassificationApprovalException(
                "A source or target fixed-asset category account mapping changed after approval. Submit a new GL reclassification.");
        }
    }

    private static void ValidateReclassificationBookState(AssetTransfer transfer, FixedAssetBookValue bookValue)
    {
        var book = bookValue.AccountingBook;
        var activePostingBookCount = transfer.FixedAsset.BookValues.Count(value =>
            value.AccountingBook != null &&
            value.AccountingBook.TenantId == transfer.TenantId &&
            value.AccountingBook.IsActive &&
            value.AccountingBook.AllowsPosting);

        if (book == null ||
            book.Id != transfer.AccountingBookId ||
            book.TenantId != transfer.TenantId ||
            !book.IsActive ||
            !book.AllowsPosting ||
            activePostingBookCount != 1)
        {
            // The asset category is global current master data. A changed book configuration can
            // therefore invalidate the approved assumption that exactly one ledger is being moved.
            throw new StaleReclassificationApprovalException(
                "The fixed-asset posting-book configuration changed after approval. Submit a new GL reclassification.");
        }
    }

    private static void ValidateApprovedBalanceSnapshot(AssetTransfer transfer, ReclassificationBalances current)
    {
        if (RoundMoney(transfer.ReclassificationAssetCarryingAmount) != current.AssetCarryingAmount ||
            RoundMoney(transfer.ReclassificationAccumulatedDepreciation) != current.AccumulatedDepreciation ||
            RoundMoney(transfer.ReclassificationAccumulatedImpairment) != current.AccumulatedImpairment ||
            RoundMoney(transfer.ReclassificationRevaluationSurplus) != current.RevaluationSurplus)
        {
            throw new StaleReclassificationApprovalException("Fixed asset balances changed after approval. Submit a new GL reclassification with a fresh balance snapshot.");
        }
    }

    private async Task<FinancePostingRequestV2Dto> BuildReclassificationPostingRequestAsync(AssetTransfer transfer)
    {
        var lines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        // FinanceSettings is the authority for the ledger's functional currency. Using the asset
        // master value here could make an otherwise balanced journal fail if stale asset data does
        // not match the tenant-wide accounting configuration.
        var functionalCurrency = await GetFunctionalCurrencyAsync();
        AddNormalDebitBalanceMove(
            lines, transfer.FromAssetAccountId, transfer.ToAssetAccountId,
            transfer.ReclassificationAssetCarryingAmount, "Fixed asset carrying account", "FA-ReclassAsset",
            functionalCurrency, transfer, ref lineNumber);
        AddNormalCreditBalanceMove(
            lines, transfer.FromAccumulatedDepreciationAccountId, transfer.ToAccumulatedDepreciationAccountId,
            transfer.ReclassificationAccumulatedDepreciation, "Accumulated depreciation", "FA-ReclassAccumDep",
            functionalCurrency, transfer, ref lineNumber);
        AddNormalCreditBalanceMove(
            lines, transfer.FromAccumulatedImpairmentAccountId, transfer.ToAccumulatedImpairmentAccountId,
            transfer.ReclassificationAccumulatedImpairment, "Accumulated impairment", "FA-ReclassImpairment",
            functionalCurrency, transfer, ref lineNumber);
        AddNormalCreditBalanceMove(
            lines, transfer.FromRevaluationSurplusAccountId, transfer.ToRevaluationSurplusAccountId,
            transfer.ReclassificationRevaluationSurplus, "Revaluation surplus", "FA-ReclassReserve",
            functionalCurrency, transfer, ref lineNumber);

        if (lines.Count < 2)
        {
            throw new InvalidOperationException("GL reclassification produced no current account or segment balance movement.");
        }

        return new FinancePostingRequestV2Dto
        {
            SourceModule = ReclassificationProducer.Definition.PostingSourceModule,
            // Fixed Assets is a Finance subledger. Period locks operate on canonical top-level
            // modules, so the journal must use FIN rather than the descriptive FA source value.
            OriginModuleCode = FinanceModuleLockCatalog.Finance,
            SourceDocumentType = ReclassificationProducer.Definition.DocumentType,
            SourceDocumentId = transfer.Id,
            SourceDocumentTenantId = transfer.TenantId,
            PostingAction = ReclassificationPostingAction,
            SourceDocumentReference = transfer.ReferenceNumber,
            Description = $"Fixed asset GL reclassification - {transfer.FixedAsset.AssetCode} - {transfer.FixedAsset.Name}",
            PostingDate = (transfer.AccountingDate ?? transfer.TransferDate).Date,
            FiscalPeriodId = transfer.FiscalPeriodId,
            JournalType = "Fixed Asset Reclassification",
            AccountingBookCode = transfer.BookClassification,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FA:TransferReclassification:{transfer.TenantId:N}:{transfer.Id:N}",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private static void AddNormalDebitBalanceMove(
        ICollection<FinancePostingLineDto> lines,
        Guid? fromAccountId,
        Guid? toAccountId,
        decimal amount,
        string label,
        string tag,
        string functionalCurrency,
        AssetTransfer transfer,
        ref int lineNumber)
    {
        amount = RoundMoney(amount);
        if (amount <= 0m || !RequiresLedgerMove(fromAccountId, toAccountId, transfer)) return;
        if (!fromAccountId.HasValue || !toAccountId.HasValue)
            throw new InvalidOperationException($"{label} reclassification accounts are incomplete.");

        AddReclassificationLine(lines, toAccountId.Value, amount, 0m, $"Move {label} to target classification", tag, functionalCurrency, transfer.ToSegmentString, transfer, lineNumber++);
        AddReclassificationLine(lines, fromAccountId.Value, 0m, amount, $"Clear {label} from source classification", tag, functionalCurrency, transfer.FromSegmentString, transfer, lineNumber++);
    }

    private static void AddNormalCreditBalanceMove(
        ICollection<FinancePostingLineDto> lines,
        Guid? fromAccountId,
        Guid? toAccountId,
        decimal amount,
        string label,
        string tag,
        string functionalCurrency,
        AssetTransfer transfer,
        ref int lineNumber)
    {
        amount = RoundMoney(amount);
        if (amount <= 0m || !RequiresLedgerMove(fromAccountId, toAccountId, transfer)) return;
        if (!fromAccountId.HasValue || !toAccountId.HasValue)
            throw new InvalidOperationException($"{label} reclassification accounts are incomplete.");

        AddReclassificationLine(lines, fromAccountId.Value, amount, 0m, $"Clear {label} from source classification", tag, functionalCurrency, transfer.FromSegmentString, transfer, lineNumber++);
        AddReclassificationLine(lines, toAccountId.Value, 0m, amount, $"Move {label} to target classification", tag, functionalCurrency, transfer.ToSegmentString, transfer, lineNumber++);
    }

    private static bool RequiresLedgerMove(Guid? fromAccountId, Guid? toAccountId, AssetTransfer transfer)
        => fromAccountId != toAccountId ||
           !string.Equals(NormalizeSegmentString(transfer.FromSegmentString), NormalizeSegmentString(transfer.ToSegmentString), StringComparison.OrdinalIgnoreCase);

    private static void AddReclassificationLine(
        ICollection<FinancePostingLineDto> lines,
        Guid accountId,
        decimal debit,
        decimal credit,
        string description,
        string tag,
        string functionalCurrency,
        string? segment,
        AssetTransfer transfer,
        int lineNumber)
        => lines.Add(new FinancePostingLineDto
        {
            AccountId = accountId,
            SourceDocumentLineId = FinanceSourceLineIdentity.Create(
                transfer.Id, $"{tag}:{description}", transfer.FixedAssetId),
            DebitAmount = debit,
            CreditAmount = credit,
            TransactionCurrency = functionalCurrency,
            TransactionDebitAmount = debit,
            TransactionCreditAmount = credit,
            Description = $"{description} - {transfer.FixedAsset.AssetCode}",
            SourceReferenceNumber = transfer.ReferenceNumber,
            LineNumber = lineNumber,
            SegmentString = NormalizeSegmentString(segment),
            Notes = $"FixedAssetId={transfer.FixedAssetId:N};AssetTransferId={transfer.Id:N};Book={transfer.BookClassification}",
            TransactionTag = tag
        });

    private async Task<ReclassificationBalances> CalculateCurrentReclassificationBalancesAsync(
        Guid assetId,
        FixedAssetBookValue bookValue)
    {
        var valuations = await _context.AssetValuations.AsNoTracking()
            .Where(v => v.TenantId == TenantId &&
                        v.FixedAssetId == assetId &&
                        !v.IsDeleted &&
                        v.IsPostedToGL &&
                        !v.IsCorrected &&
                        v.BookClassification == bookValue.BookClassification)
            .ToListAsync();

        var revaluationAssetAdjustment = valuations
            .Where(v => v.ValuationType == ValuationType.Revaluation)
            .Sum(v => v.RevaluationSurplus - v.RevaluationDeficit);
        var accumulatedImpairment = valuations.Sum(v => v.ImpairmentLoss - v.ImpairmentReversal);
        var revaluationSurplus = valuations
            .Where(v => v.ValuationType == ValuationType.Revaluation)
            .Sum(v => v.RevaluationSurplus - v.RevaluationSurplusApplied);

        return new ReclassificationBalances(
            RoundMoney(bookValue.AcquisitionCost + revaluationAssetAdjustment),
            Math.Max(0m, RoundMoney(bookValue.AccumulatedDepreciation)),
            Math.Max(0m, RoundMoney(accumulatedImpairment)),
            Math.Max(0m, RoundMoney(revaluationSurplus)));
    }

    private FixedAssetBookValue ResolveReclassificationBookValue(
        FixedAsset asset,
        Guid? accountingBookId,
        string bookClassification)
    {
        var normalizedBook = NormalizeBookClassification(bookClassification);
        var bookValue = asset.BookValues
            .OrderByDescending(b => accountingBookId.HasValue && b.AccountingBookId == accountingBookId.Value)
            .ThenByDescending(b => b.AccountingBook != null && b.AccountingBook.IsDefault)
            .ThenByDescending(b => b.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(b =>
                (!accountingBookId.HasValue || b.AccountingBookId == accountingBookId.Value) &&
                b.BookClassification.Equals(normalizedBook, StringComparison.OrdinalIgnoreCase));

        return bookValue ?? throw new InvalidOperationException("The selected fixed asset accounting-book value was not found.");
    }

    private async Task ValidateReclassificationAccountAsync(
        Guid assetId,
        Guid? accountId,
        string label,
        AccountType expectedType)
    {
        if (!accountId.HasValue)
        {
            throw new InvalidOperationException($"The {label} is not configured.");
        }

        var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a =>
            a.TenantId == TenantId && a.Id == accountId.Value && !a.IsDeleted);
        if (account == null || account.AccountType != expectedType || account.Status != AccountStatus.Active || !account.AllowDirectPosting)
        {
            await RecordBlockedTransferAuditAsync(assetId, $"The {label} is invalid, inactive, cross-tenant, or does not allow posting.");
            throw new InvalidOperationException($"The {label} must be an active same-tenant posting account of type {expectedType}.");
        }
    }

    private async Task<string> GetFunctionalCurrencyAsync()
    {
        var settingsCurrency = await _context.FinanceSettings.AsNoTracking()
            .Where(s => s.TenantId == TenantId && !s.IsDeleted)
            .Select(s => s.BaseCurrency)
            .FirstOrDefaultAsync();
        var tenantCurrency = await _context.Tenants.AsNoTracking()
            .Where(t => t.Id == TenantId)
            .Select(t => t.BaseCurrency)
            .FirstOrDefaultAsync();
        return NormalizeCurrency(settingsCurrency ?? tenantCurrency ?? "GHS");
    }

    private static string NormalizeBookClassification(string? value)
        => string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string? value)
        => string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ReclassificationPreparation(
        Guid FromCategoryId,
        Guid ToCategoryId,
        Guid AccountingBookId,
        string BookClassification,
        DateTime AccountingDate,
        Guid FiscalPeriodId,
        Guid FromAssetAccountId,
        Guid ToAssetAccountId,
        Guid FromAccumulatedDepreciationAccountId,
        Guid ToAccumulatedDepreciationAccountId,
        Guid? FromAccumulatedImpairmentAccountId,
        Guid? ToAccumulatedImpairmentAccountId,
        Guid? FromRevaluationSurplusAccountId,
        Guid? ToRevaluationSurplusAccountId,
        decimal AssetCarryingAmount,
        decimal AccumulatedDepreciation,
        decimal AccumulatedImpairment,
        decimal RevaluationSurplus);

    private sealed record ReclassificationBalances(
        decimal AssetCarryingAmount,
        decimal AccumulatedDepreciation,
        decimal AccumulatedImpairment,
        decimal RevaluationSurplus);

    /// <summary>
    /// Distinguishes an approval whose accounting evidence is permanently stale from a transient
    /// posting failure. The former must become terminal so a new maker-checker cycle can start;
    /// the latter retains the approved request and stable idempotency key for controlled retry.
    /// </summary>
    private sealed class StaleReclassificationApprovalException(string message) : InvalidOperationException(message);
}
