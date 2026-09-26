using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public sealed class InventoryValuationReconciliationService : IInventoryValuationReconciliationService,
    IInventoryValuationReconciliationReportSource
{
    private const string EntityType = "InventoryValuationReconciliation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IFiscalPeriodService _fiscalPeriods;
    private readonly IProcurementControlEventService _controlEvents;

    public InventoryValuationReconciliationService(
        ApplicationDbContext db,
        ICurrentUserProvider currentUser,
        IFiscalPeriodService fiscalPeriods,
        IProcurementControlEventService controlEvents)
    {
        _db = db;
        _currentUser = currentUser;
        _fiscalPeriods = fiscalPeriods;
        _controlEvents = controlEvents;
    }

    public async Task<IReadOnlyList<InventoryValuationReconciliationDto>> GetAsync(
        Guid? fiscalPeriodId,
        InventoryValuationReconciliationStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        take = Math.Clamp(take, 1, 500);
        var query = FullQuery().Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted);
        if (fiscalPeriodId.HasValue) query = query.Where(value => value.FiscalPeriodId == fiscalPeriodId.Value);
        if (status.HasValue) query = query.Where(value => value.Status == status.Value);
        var rows = await query.AsNoTracking().OrderByDescending(value => value.GeneratedAtUtc)
            .Take(take).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<InventoryValuationReconciliationDto>> GetReportSourceAsync(
        Guid? fiscalPeriodId,
        InventoryValuationReconciliationStatus? status,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var query = FullQuery().Where(value => value.TenantId == _currentUser.TenantId && !value.IsDeleted);
        if (fiscalPeriodId.HasValue) query = query.Where(value => value.FiscalPeriodId == fiscalPeriodId.Value);
        if (status.HasValue) query = query.Where(value => value.Status == status.Value);
        var rows = await query.AsNoTracking().OrderByDescending(value => value.GeneratedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<InventoryValuationReconciliationDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var row = await FullQuery().AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == _currentUser.TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new InventoryValuationReconciliationNotFoundException(
                "The inventory valuation reconciliation was not found in the current tenant.");
        return Map(row);
    }

    public async Task<InventoryValuationReconciliationDto> GenerateAsync(
        GenerateInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        if (request.FiscalPeriodId == Guid.Empty || request.ToleranceAmount is < 0m or > 1000000m)
            throw Error("INV_VALUATION_RECONCILIATION_REQUEST_INVALID",
                "A fiscal period and a non-negative reconciliation tolerance are required.");
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var correlation = Correlation(request.CorrelationId);
        var requestHash = Hash(new { request.FiscalPeriodId, request.ToleranceAmount });
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var replay = await FullQuery().SingleOrDefaultAsync(value =>
                value.TenantId == _currentUser.TenantId && value.FiscalPeriodId == request.FiscalPeriodId &&
                value.IdempotencyKey == key && !value.IsDeleted, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadHash, requestHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_VALUATION_RECONCILIATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies a different reconciliation request.");
                await transaction.CommitAsync(cancellationToken);
                return Map(replay);
            }

            var period = await _db.FiscalPeriods.Include(value => value.FiscalYear)
                .SingleOrDefaultAsync(value => value.TenantId == _currentUser.TenantId &&
                                               value.Id == request.FiscalPeriodId && !value.IsDeleted,
                    cancellationToken)
                ?? throw new InventoryValuationReconciliationNotFoundException(
                    "The fiscal period was not found in the current tenant.");
            if (period.IsClosed || period.IsLocked || !period.IsOpen)
                throw Error("INV_VALUATION_RECONCILIATION_PERIOD_NOT_OPEN",
                    "A reconciliation snapshot can be generated only while the fiscal period is open.");
            var snapshot = await BuildSnapshotAsync(period, request.ToleranceAmount, cancellationToken);
            var now = DateTime.UtcNow;
            var id = Guid.NewGuid();
            var row = new InventoryValuationReconciliation
            {
                Id = id,
                TenantId = _currentUser.TenantId,
                ReconciliationNumber = $"IVR-{period.PeriodCode}-{id.ToString("N")[..8].ToUpperInvariant()}",
                FiscalPeriodId = period.Id,
                CutoffDateUtc = snapshot.CutoffDateUtc,
                Status = snapshot.Exceptions.Count == 0
                    ? InventoryValuationReconciliationStatus.Reconciled
                    : InventoryValuationReconciliationStatus.Exception,
                FunctionalCurrencyCode = snapshot.FunctionalCurrencyCode,
                InventoryControlAccountId = snapshot.InventoryControlAccountId,
                ReceiptInventoryValue = snapshot.ReceiptInventoryValue,
                PostedLandedCostValue = snapshot.PostedLandedCostValue,
                LandedCostInventoryValue = snapshot.LandedCostInventoryValue,
                LandedCostVarianceValue = snapshot.LandedCostVarianceValue,
                InventorySubledgerValue = snapshot.InventorySubledgerValue,
                InventoryBalanceCacheValue = snapshot.InventoryBalanceCacheValue,
                CurrentMovementValue = snapshot.CurrentMovementValue,
                GeneralLedgerValue = snapshot.GeneralLedgerValue,
                ReconciliationVariance = snapshot.ReconciliationVariance,
                ToleranceAmount = snapshot.ToleranceAmount,
                ReceiptExceptionCount = snapshot.Exceptions.Count(value => value.Area == "Receipt"),
                LandedCostExceptionCount = snapshot.Exceptions.Count(value => value.Area == "LandedCost"),
                ValuationExceptionCount = snapshot.Exceptions.Count(value => value.Area == "Valuation"),
                GeneralLedgerExceptionCount = snapshot.Exceptions.Count(value => value.Area == "GeneralLedger"),
                ExceptionCount = snapshot.Exceptions.Count,
                SnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions),
                ExceptionsJson = JsonSerializer.Serialize(snapshot.Exceptions, JsonOptions),
                SnapshotHash = snapshot.Hash,
                GeneratedById = _currentUser.UserId,
                GeneratedAtUtc = now,
                IdempotencyKey = key,
                PayloadHash = requestHash,
                CorrelationId = correlation,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedById = _currentUser.UserId
            };
            _db.InventoryValuationReconciliations.Add(row);
            AddAction(row, InventoryValuationReconciliationActionType.Generated, null, row.Status,
                $"{key}:generated", requestHash, correlation,
                row.ExceptionCount == 0 ? "Snapshot reconciled." : $"Snapshot contains {row.ExceptionCount} exception(s).");
            AddAudit(row, "Generated", snapshot, correlation);
            await _db.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(row, "Generate", "Allowed", correlation, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == row.Id, cancellationToken));
        });
    }

    public async Task<InventoryValuationReconciliationDto> FreezeAsync(
        Guid id,
        FreezeInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureActor();
        var key = Required(request.IdempotencyKey, 100, "Idempotency key");
        var reason = Required(request.Reason, 500, "Freeze reason");
        var correlation = Correlation(request.CorrelationId);
        var payloadHash = Hash(new { id, reason });
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var row = await FullQuery().SingleOrDefaultAsync(value =>
                value.TenantId == _currentUser.TenantId && value.Id == id && !value.IsDeleted, cancellationToken)
                ?? throw new InventoryValuationReconciliationNotFoundException(
                    "The inventory valuation reconciliation was not found in the current tenant.");
            var replay = row.Actions.SingleOrDefault(value => value.IdempotencyKey == key);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadHash, payloadHash, StringComparison.OrdinalIgnoreCase))
                    throw Error("INV_VALUATION_RECONCILIATION_IDEMPOTENCY_CONFLICT",
                        "The idempotency key already identifies a different freeze request.");
                await transaction.CommitAsync(cancellationToken);
                return Map(row);
            }
            EnsureRowVersion(row.RowVersion, request.RowVersion);
            if (row.Status != InventoryValuationReconciliationStatus.Reconciled || row.ExceptionCount != 0 ||
                Math.Abs(row.ReconciliationVariance) > row.ToleranceAmount)
                throw Error("INV_VALUATION_RECONCILIATION_NOT_CLEAN",
                    "Only a clean reconciled snapshot can be frozen.");
            if (row.GeneratedById == _currentUser.UserId)
                throw Error("INV_VALUATION_RECONCILIATION_SOD",
                    "The actor who generated the reconciliation cannot freeze it.");
            var latestId = await _db.InventoryValuationReconciliations.Where(value =>
                    value.TenantId == row.TenantId && value.FiscalPeriodId == row.FiscalPeriodId && !value.IsDeleted)
                .OrderByDescending(value => value.GeneratedAtUtc).ThenByDescending(value => value.Id)
                .Select(value => value.Id).FirstAsync(cancellationToken);
            if (latestId != row.Id)
                throw Error("INV_VALUATION_RECONCILIATION_NOT_LATEST",
                    "Only the latest reconciliation snapshot for the period can be frozen.");
            var live = await BuildSnapshotAsync(row.FiscalPeriod, row.ToleranceAmount, cancellationToken);
            if (!string.Equals(live.Hash, row.SnapshotHash, StringComparison.OrdinalIgnoreCase))
                throw Error("INV_VALUATION_RECONCILIATION_STALE",
                    "Valuation or Finance data changed after generation. Generate a new reconciliation snapshot.");

            await _fiscalPeriods.LockPeriodForModuleAsync(row.FiscalPeriodId, Constants.Modules.Inventory,
                $"Inventory valuation frozen by {row.ReconciliationNumber}: {reason}", cancellationToken);
            var moduleLock = await _db.PeriodModuleLocks.Include(value => value.ModuleDefinition)
                .SingleAsync(value => value.TenantId == row.TenantId && value.FiscalPeriodId == row.FiscalPeriodId &&
                                      value.ModuleDefinition.ModuleCode == Constants.Modules.Inventory,
                    cancellationToken);
            var previous = row.Status;
            var now = DateTime.UtcNow;
            row.Status = InventoryValuationReconciliationStatus.Frozen;
            row.FrozenById = _currentUser.UserId;
            row.FrozenAtUtc = now;
            row.PeriodModuleLockId = moduleLock.Id;
            row.UpdatedAt = now;
            row.LastModifiedById = _currentUser.UserId;
            row.FiscalPeriod.InventoryValuationComplete = true;
            row.FiscalPeriod.InventoryValuationDate = now;
            row.FiscalPeriod.UpdatedAt = now;
            row.FiscalPeriod.LastModifiedById = _currentUser.UserId;
            AddAction(row, InventoryValuationReconciliationActionType.Frozen, previous, row.Status,
                key, payloadHash, correlation, reason);
            AddAudit(row, "Frozen", new { reason, moduleLock.Id, row.SnapshotHash }, correlation);
            await _db.SaveChangesAsync(cancellationToken);
            await RecordControlEventAsync(row, "Freeze", "Allowed", correlation, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(await FullQuery().AsNoTracking().SingleAsync(value => value.Id == row.Id, cancellationToken));
        });
    }

    private async Task<Snapshot> BuildSnapshotAsync(
        FiscalPeriod period,
        decimal tolerance,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(value =>
            value.TenantId == period.TenantId && !value.IsDeleted, cancellationToken)
            ?? throw Error("INV_VALUATION_RECONCILIATION_FINANCE_SETTINGS_MISSING",
                "Finance settings are required before inventory valuation can be reconciled.");
        var cutoff = DateTime.SpecifyKind(period.EndDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        var primaryBook = await ResolveEffectivePrimaryBookAsync(period.TenantId, cutoff, cancellationToken);
        var mappedAccounts = await _db.InventoryItems.AsNoTracking().Where(value => value.TenantId == period.TenantId && !value.IsDeleted)
            .Select(value => value.InventoryAccountId).ToListAsync(cancellationToken);
        var accountIds = mappedAccounts
            .Append(settings.ControlAccountInventoryId).Where(value => value.HasValue).Select(value => value!.Value).ToHashSet();
        // Retain historical inventory targets even when a master default has subsequently changed.
        var historicalAccounts = await _db.AccountTransactions.AsNoTracking().Where(value => value.TenantId == period.TenantId &&
            !value.IsDeleted && value.PostingStatus == "Posted" && value.TransactionDate <= cutoff && value.TransactionTag != null &&
            (primaryBook == null || value.AccountingBookId == primaryBook.Id ||
                (value.AccountingBookId == Guid.Empty && value.BookClassification == primaryBook.Code)) &&
            (value.TransactionTag == "INV-RECEIPT-CONTROL" || value.TransactionTag == "INV-LANDED-COST-CONTROL" ||
             value.TransactionTag == "INV-OPEN-CONTROL" || value.TransactionTag == "INV-ADJ-CONTROL" ||
             value.TransactionTag == "INV-ISSUE-CONTROL" || value.TransactionTag.StartsWith("INV-ISSUE-CTL-") ||
             value.TransactionTag.StartsWith("INV-RET-CTL-") || value.TransactionTag == "RTV-Dispatch-Inventory"))
            .Select(value => value.AccountId).Distinct().ToListAsync(cancellationToken);
        accountIds.UnionWith(historicalAccounts);
        var accountId = settings.ControlAccountInventoryId ?? accountIds.OrderBy(value => value).Select(value => (Guid?)value).FirstOrDefault()
            ?? throw Error("INV_VALUATION_RECONCILIATION_ACCOUNT_MISSING", "An Inventory account is required on the items or in Finance Settings.");
        var exceptions = new List<InventoryValuationReconciliationExceptionDto>();
        var cutoffMovements = await _db.InventoryMovements.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && value.IsPosted && !value.IsDeleted &&
                value.PostingDate <= cutoff)
            .Select(value => new MovementValue(value.Id, value.ReferenceId, value.ReferenceNumber,
                value.MovementType, value.Direction, value.TotalValue, value.VarianceAmount))
            .ToListAsync(cancellationToken);
        var currentMovements = await _db.InventoryMovements.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && value.IsPosted && !value.IsDeleted)
            .Select(value => new { value.MovementType, value.Direction, value.TotalValue })
            .ToListAsync(cancellationToken);
        var inventorySubledger = Round(cutoffMovements.Sum(SignedValue));
        var currentMovementValue = Round(currentMovements.Sum(value =>
            value.MovementType == InventoryMovementType.LandedCostRevaluation
                ? value.TotalValue
                : value.Direction == MovementDirection.In ? Math.Abs(value.TotalValue) : -Math.Abs(value.TotalValue)));
        var cacheValue = Round(await _db.InventoryBalances.AsNoTracking().Where(value =>
            value.TenantId == period.TenantId && !value.IsDeleted).SumAsync(value => value.TotalValue, cancellationToken));
        if (Math.Abs(cacheValue - currentMovementValue) > tolerance)
            exceptions.Add(Exception("INV_VALUATION_BALANCE_CACHE_MISMATCH", "Valuation",
                "The inventory balance cache does not equal the complete posted movement subledger.", null,
                currentMovementValue, cacheValue));

        var accountBalances = await _db.AccountTransactions.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && accountIds.Contains(value.AccountId) && !value.IsDeleted &&
                value.PostingStatus == "Posted" && value.TransactionDate <= cutoff &&
                (primaryBook == null || value.AccountingBookId == primaryBook.Id ||
                    (value.AccountingBookId == Guid.Empty && value.BookClassification == primaryBook.Code)))
            .GroupBy(value => value.AccountId).Select(group => new { AccountId = group.Key, Balance = group.Sum(value => value.DebitAmount - value.CreditAmount) })
            .OrderBy(value => value.AccountId).ToListAsync(cancellationToken);
        var glValue = Round(accountBalances.Sum(value => value.Balance));
        var variance = Round(inventorySubledger - glValue);
        if (Math.Abs(variance) > tolerance)
            exceptions.Add(Exception("INV_VALUATION_GL_MISMATCH", "GeneralLedger",
                "The movement subledger does not equal the combined Inventory account balances at cut-off.", null,
                inventorySubledger, glValue));

        var receiptMovements = cutoffMovements.Where(value =>
            value.MovementType == InventoryMovementType.PurchaseReceipt).ToList();
        var receiptIds = receiptMovements.Where(value => value.ReferenceId.HasValue)
            .Select(value => value.ReferenceId!.Value).Distinct().ToList();
        var postedReceiptIds = await _db.FinancePostingEvents.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && !value.IsDeleted && value.PostingStatus == "Posted" &&
                value.SourceDocumentType == "ProcurementPurchaseOrderReceipt" &&
                value.PostingAction == "PostAcceptedInventoryReceipt" && receiptIds.Contains(value.SourceDocumentId))
            .Select(value => value.SourceDocumentId).Distinct().ToListAsync(cancellationToken);
        foreach (var receipt in receiptMovements.Where(value => value.ReferenceId.HasValue &&
                     !postedReceiptIds.Contains(value.ReferenceId.Value)).GroupBy(value => value.ReferenceId!.Value))
            exceptions.Add(Exception("INV_RECEIPT_GL_POSTING_MISSING", "Receipt",
                "Accepted receipt valuation has no posted Finance event.",
                receipt.First().ReferenceNumber ?? receipt.Key.ToString(), receipt.Sum(SignedValue), null));

        var acceptedReceipts = await _db.PurchaseOrderReceipts.AsNoTracking()
            .Where(value => value.TenantId == period.TenantId && value.ReceiptDate <= cutoff && !value.IsDeleted &&
                            value.Items.Any(line => !line.IsDeleted && line.AcceptedQuantity > 0m &&
                                !line.PurchaseOrderItem.IsDeleted &&
                                line.PurchaseOrderItem.InventoryItemId.HasValue))
            .Select(value => new { value.Id, value.ReceiptNumber }).ToListAsync(cancellationToken);
        var movementReceiptIds = receiptIds.ToHashSet();
        foreach (var receipt in acceptedReceipts.Where(value => !movementReceiptIds.Contains(value.Id)))
            exceptions.Add(Exception("INV_RECEIPT_VALUATION_MISSING", "Receipt",
                "An accepted governed receipt has no posted inventory valuation movement.", receipt.ReceiptNumber));

        var landedCosts = await _db.LandedCosts.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && !value.IsDeleted && value.PostedDate <= cutoff &&
                value.Status == "Posted")
            .Select(value => new { value.Id, value.LandedCostNumber, value.TotalCost,
                value.AllocatedAmount, value.UnallocatedAmount }).ToListAsync(cancellationToken);
        var landedIds = landedCosts.Select(value => value.Id).ToList();
        var postedLandedIds = await _db.FinancePostingEvents.AsNoTracking().Where(value =>
                value.TenantId == period.TenantId && !value.IsDeleted && value.PostingStatus == "Posted" &&
                value.SourceDocumentType == "InventoryLandedCost" && value.PostingAction == "PostLandedCost" &&
                landedIds.Contains(value.SourceDocumentId))
            .Select(value => value.SourceDocumentId).Distinct().ToListAsync(cancellationToken);
        foreach (var landed in landedCosts)
        {
            if (Math.Abs(Round(landed.TotalCost - landed.AllocatedAmount)) > tolerance ||
                Math.Abs(landed.UnallocatedAmount) > tolerance)
                exceptions.Add(Exception("INV_LANDED_COST_ALLOCATION_MISMATCH", "LandedCost",
                    "Posted landed cost is not fully allocated.", landed.LandedCostNumber,
                    landed.TotalCost, landed.AllocatedAmount));
            if (!postedLandedIds.Contains(landed.Id))
                exceptions.Add(Exception("INV_LANDED_COST_GL_POSTING_MISSING", "LandedCost",
                    "Posted landed cost has no posted Finance event.", landed.LandedCostNumber,
                    landed.TotalCost, null));
        }
        var landedMovements = cutoffMovements.Where(value =>
            value.MovementType == InventoryMovementType.LandedCostRevaluation).ToList();
        var landedInventory = Round(landedMovements.Sum(value => value.TotalValue));
        var landedVariance = Round(landedMovements.Sum(value => value.VarianceAmount ?? 0m));
        var postedLanded = Round(landedCosts.Sum(value => value.TotalCost));
        if (Math.Abs(postedLanded - Round(landedInventory + landedVariance)) > tolerance)
            exceptions.Add(Exception("INV_LANDED_COST_VALUATION_MISMATCH", "LandedCost",
                "Posted landed costs do not equal their inventory revaluation and variance movements.", null,
                postedLanded, Round(landedInventory + landedVariance)));

        var receiptValue = Round(receiptMovements.Sum(SignedValue));
        var hashPayload = new
        {
            period.Id,
            CutoffDateUtc = cutoff,
            FunctionalCurrencyCode = Currency(settings.BaseCurrency),
            AccountingBookId = primaryBook?.Id,
            AccountingBookCode = primaryBook?.Code,
            InventoryControlAccountId = accountId,
            ReceiptInventoryValue = receiptValue,
            PostedLandedCostValue = postedLanded,
            LandedCostInventoryValue = landedInventory,
            LandedCostVarianceValue = landedVariance,
            InventorySubledgerValue = inventorySubledger,
            InventoryBalanceCacheValue = cacheValue,
            CurrentMovementValue = currentMovementValue,
            GeneralLedgerValue = glValue,
            ReconciliationVariance = variance,
            ToleranceAmount = Round(tolerance),
            Exceptions = exceptions.OrderBy(value => value.Area).ThenBy(value => value.Code)
                .ThenBy(value => value.Reference).ToList()
        };
        // Preserve the hash contract of existing single-control snapshots; multi-account
        // snapshots additionally pin each account balance so offsetting changes cannot hide.
        var snapshotHash = accountIds.Count == 1 ? Hash(hashPayload)
            : Hash(new { Snapshot = hashPayload, InventoryAccountIds = accountIds.OrderBy(value => value).ToArray(), InventoryAccountBalances = accountBalances });
        return new Snapshot(cutoff, hashPayload.FunctionalCurrencyCode, accountId, receiptValue,
            postedLanded, landedInventory, landedVariance, inventorySubledger, cacheValue,
            currentMovementValue, glValue, variance, hashPayload.ToleranceAmount,
            hashPayload.Exceptions, snapshotHash);
    }

    private async Task<AccountingBook?> ResolveEffectivePrimaryBookAsync(
        Guid tenantId,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var books = await _db.AccountingBooks.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted)
            .ToListAsync(cancellationToken);
        if (books.Count == 0)
            return null;

        var date = cutoff.Date;
        var designation = await _db.AccountingBookPrimaryDesignations.AsNoTracking()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                value.ReversedAtUtc == null && value.EffectiveFrom <= date)
            .OrderByDescending(value => value.EffectiveFrom)
            .ThenByDescending(value => value.ApprovedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        Guid? bookId = designation?.NewPrimaryBookId;
        if (!bookId.HasValue)
        {
            bookId = await _db.AccountingBookPrimaryDesignations.AsNoTracking()
                .Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                    value.ReversedAtUtc == null && value.EffectiveFrom > date)
                .OrderBy(value => value.EffectiveFrom)
                .ThenBy(value => value.ApprovedAtUtc)
                .Select(value => (Guid?)value.PreviousPrimaryBookId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var matches = bookId.HasValue
            ? books.Where(value => value.Id == bookId.Value).ToList()
            : books.Where(value => value.IsDefault && value.BookType == AccountingBookType.PrimaryFull).ToList();
        if (matches.Count != 1)
            throw Error("INV_VALUATION_RECONCILIATION_PRIMARY_BOOK_AMBIGUOUS",
                "Exactly one effective primary accounting book is required for inventory valuation reconciliation.");
        return matches[0];
    }

    private IQueryable<InventoryValuationReconciliation> FullQuery() =>
        _db.InventoryValuationReconciliations.Include(value => value.FiscalPeriod)
            .Include(value => value.InventoryControlAccount)
            .Include(value => value.Actions.OrderBy(action => action.Sequence));

    private void AddAction(InventoryValuationReconciliation row,
        InventoryValuationReconciliationActionType type,
        InventoryValuationReconciliationStatus? previous,
        InventoryValuationReconciliationStatus next,
        string key,
        string payloadHash,
        string correlation,
        string? reason)
    {
        var last = row.Actions.OrderByDescending(value => value.Sequence).FirstOrDefault();
        var action = new InventoryValuationReconciliationAction
        {
            Id = Guid.NewGuid(), TenantId = row.TenantId, ReconciliationId = row.Id,
            Sequence = (last?.Sequence ?? 0) + 1, ActionType = type, PreviousStatus = previous,
            NewStatus = next, ActorUserId = _currentUser.UserId, OccurredAtUtc = DateTime.UtcNow,
            IdempotencyKey = key, PayloadHash = payloadHash, CorrelationId = correlation,
            Reason = reason, PreviousHash = last?.IntegrityHash, CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUser.UserId
        };
        action.IntegrityHash = Hash(new { action.ReconciliationId, action.Sequence, action.ActionType,
            action.PreviousStatus, action.NewStatus, action.ActorUserId, action.OccurredAtUtc,
            action.PayloadHash, action.CorrelationId, action.Reason, action.PreviousHash });
        row.Actions.Add(action);
    }

    private void AddAudit(InventoryValuationReconciliation row, string action, object payload, string correlation) =>
        _db.Set<AuditLog>().Add(new AuditLog
        {
            TenantId = row.TenantId, UserId = _currentUser.UserId, Username = "Inventory valuation reconciliation",
            Action = $"InventoryValuationReconciliation.{action}", Resource = EntityType,
            ResourceId = row.Id.ToString(), NewValues = JsonSerializer.Serialize(payload, JsonOptions),
            IpAddress = "system", UserAgent = correlation, Timestamp = DateTime.UtcNow
        });

    private Task RecordControlEventAsync(InventoryValuationReconciliation row, string action,
        string result, string correlation, CancellationToken cancellationToken) =>
        _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
        {
            EventKey = $"inventory-valuation:{row.TenantId:N}:{row.Id:N}:{action}:{row.SnapshotHash}",
            EventType = EntityType, Action = action,
            Result = string.Equals(result, "Allowed", StringComparison.OrdinalIgnoreCase)
                ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
            RuleCode = "TDC-0613", RuleVersion = "1",
            DecisionKeys = Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}").ToList(),
            SourceType = "FiscalPeriod", SourceId = row.FiscalPeriodId,
            SourceReference = row.ReconciliationNumber,
            Reason = result,
            InputValues = new { row.CutoffDateUtc, row.ReceiptInventoryValue, row.PostedLandedCostValue,
                row.InventorySubledgerValue, row.GeneralLedgerValue },
            ResultValues = new { row.Status, row.ReconciliationVariance, row.ExceptionCount,
                row.SnapshotHash, row.PeriodModuleLockId },
            CorrelationId = correlation, OccurredAtUtc = DateTime.UtcNow
        }, cancellationToken);

    private static InventoryValuationReconciliationDto Map(InventoryValuationReconciliation row) => new()
    {
        Id = row.Id, ReconciliationNumber = row.ReconciliationNumber, FiscalPeriodId = row.FiscalPeriodId,
        FiscalPeriodCode = row.FiscalPeriod.PeriodCode, FiscalPeriodName = row.FiscalPeriod.PeriodName,
        IsYearEnd = row.FiscalPeriod.IsYearEnd, CutoffDateUtc = row.CutoffDateUtc, Status = row.Status,
        FunctionalCurrencyCode = row.FunctionalCurrencyCode, InventoryControlAccountId = row.InventoryControlAccountId,
        InventoryControlAccountCode = row.InventoryControlAccount.AccountCode,
        InventoryControlAccountName = row.InventoryControlAccount.AccountName,
        ReceiptInventoryValue = row.ReceiptInventoryValue, PostedLandedCostValue = row.PostedLandedCostValue,
        LandedCostInventoryValue = row.LandedCostInventoryValue, LandedCostVarianceValue = row.LandedCostVarianceValue,
        InventorySubledgerValue = row.InventorySubledgerValue,
        InventoryBalanceCacheValue = row.InventoryBalanceCacheValue, CurrentMovementValue = row.CurrentMovementValue,
        GeneralLedgerValue = row.GeneralLedgerValue, ReconciliationVariance = row.ReconciliationVariance,
        ToleranceAmount = row.ToleranceAmount, ReceiptExceptionCount = row.ReceiptExceptionCount,
        LandedCostExceptionCount = row.LandedCostExceptionCount, ValuationExceptionCount = row.ValuationExceptionCount,
        GeneralLedgerExceptionCount = row.GeneralLedgerExceptionCount, ExceptionCount = row.ExceptionCount,
        SnapshotHash = row.SnapshotHash, GeneratedById = row.GeneratedById, GeneratedAtUtc = row.GeneratedAtUtc,
        FrozenById = row.FrozenById, FrozenAtUtc = row.FrozenAtUtc, PeriodModuleLockId = row.PeriodModuleLockId,
        CorrelationId = row.CorrelationId, RowVersion = Convert.ToBase64String(row.RowVersion),
        Exceptions = JsonSerializer.Deserialize<List<InventoryValuationReconciliationExceptionDto>>(
            row.ExceptionsJson, JsonOptions) ?? [],
        Actions = row.Actions.OrderBy(value => value.Sequence).Select(value =>
            new InventoryValuationReconciliationActionDto
            {
                Sequence = value.Sequence, ActionType = value.ActionType, PreviousStatus = value.PreviousStatus,
                NewStatus = value.NewStatus, ActorUserId = value.ActorUserId, OccurredAtUtc = value.OccurredAtUtc,
                Reason = value.Reason, IntegrityHash = value.IntegrityHash
            }).ToList()
    };

    private void EnsureActor()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty || _currentUser.UserId == Guid.Empty)
            throw Error("INV_VALUATION_RECONCILIATION_FORBIDDEN", "An authenticated tenant actor is required.");
    }

    private static void EnsureRowVersion(byte[] actual, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException) { throw Error("INV_VALUATION_RECONCILIATION_ROW_VERSION_INVALID", "A valid row version is required."); }
        if (expected.Length == 0 || !actual.SequenceEqual(expected))
            throw Error("INV_VALUATION_RECONCILIATION_CONCURRENCY_CONFLICT",
                "The reconciliation changed after it was loaded. Refresh and retry.");
    }

    private static decimal SignedValue(MovementValue value) =>
        value.MovementType == InventoryMovementType.LandedCostRevaluation
            ? value.TotalValue
            : value.Direction == MovementDirection.In ? Math.Abs(value.TotalValue) : -Math.Abs(value.TotalValue);
    private static InventoryValuationReconciliationExceptionDto Exception(string code, string area,
        string message, string? reference = null, decimal? expected = null, decimal? actual = null) => new()
    {
        Code = code, Area = area, Message = message, Reference = reference,
        ExpectedAmount = expected, ActualAmount = actual,
        VarianceAmount = expected.HasValue && actual.HasValue ? Round(expected.Value - actual.Value) : null
    };
    private static InventoryValuationReconciliationException Error(string code, string message) => new(code, message);
    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    private static string Currency(string? value) => string.IsNullOrWhiteSpace(value)
        ? "GHS" : value.Trim().ToUpperInvariant()[..Math.Min(3, value.Trim().Length)];
    private static string Required(string? value, int max, string label) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max ? value.Trim()
            : throw Error("INV_VALUATION_RECONCILIATION_VALUE_REQUIRED",
                $"{label} is required and must not exceed {max} characters.");
    private static string Correlation(string? value) => string.IsNullOrWhiteSpace(value)
        ? Guid.NewGuid().ToString("N") : value.Trim().Length <= 100 ? value.Trim() : value.Trim()[..100];
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions))));

    private sealed record MovementValue(Guid Id, Guid? ReferenceId, string? ReferenceNumber,
        InventoryMovementType MovementType, MovementDirection Direction, decimal TotalValue, decimal? VarianceAmount);
    private sealed record Snapshot(DateTime CutoffDateUtc, string FunctionalCurrencyCode,
        Guid InventoryControlAccountId, decimal ReceiptInventoryValue, decimal PostedLandedCostValue,
        decimal LandedCostInventoryValue, decimal LandedCostVarianceValue, decimal InventorySubledgerValue,
        decimal InventoryBalanceCacheValue, decimal CurrentMovementValue, decimal GeneralLedgerValue,
        decimal ReconciliationVariance, decimal ToleranceAmount,
        IReadOnlyList<InventoryValuationReconciliationExceptionDto> Exceptions, string Hash);
}
