using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Controlled correction workflow for FIN-LIM-0033. It intentionally lives beside, and reuses,
/// the existing depreciation service so calculation, tenant resolution, posting, and audit rules
/// cannot drift into a parallel fixed-asset subsystem.
/// </summary>
public partial class FixedAssetDepreciationService
{
    public async Task<IReadOnlyList<FixedAssetDepreciationReversalDto>> GetReversalsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _context.FixedAssetDepreciationRuns
            .AsNoTracking()
            .Include(item => item.FiscalPeriod)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == runId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset depreciation run not found.");

        var reversals = await _context.FixedAssetDepreciationReversals
            .AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                item.OriginalDepreciationRunId == runId &&
                !item.IsDeleted)
            .OrderByDescending(item => item.RequestedAt)
            .ToListAsync(cancellationToken);

        return reversals.Select(item => MapReversal(item, run)).ToList();
    }

    public async Task<FixedAssetDepreciationReversalDto> RequestReversalAsync(
        Guid runId,
        RequestFixedAssetDepreciationReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureReversalServicesConfigured();
        var userId = GetRequiredUserId("request");
        var run = await LoadRunForReversalAsync(runId, cancellationToken);
        ValidatePostedRunLineage(run);
        await EnsureRunIsLatestReversibleAccountingAsync(run, cancellationToken);

        var pendingExists = await _context.FixedAssetDepreciationReversals.AnyAsync(item =>
            item.TenantId == TenantId &&
            item.OriginalDepreciationRunId == run.Id &&
            item.Status != FixedAssetDepreciationReversalStatuses.Rejected &&
            item.Status != FixedAssetDepreciationReversalStatuses.Posted &&
            !item.IsDeleted,
            cancellationToken);
        if (pendingExists)
            throw new InvalidOperationException("This depreciation run already has a reversal awaiting review or posting.");

        var policy = await _financeReversalPolicyService!.ResolveAsync(
            run.PostingDate,
            dto.Reason,
            dto.ReversalDate,
            cancellationToken);
        var impactAssessment = dto.ImpactAssessment?.Trim() ?? string.Empty;
        if (impactAssessment.Length < 20)
        {
            throw new ArgumentException(
                "The depreciation reversal impact assessment must contain at least 20 characters.",
                nameof(dto));
        }

        var reversal = new FixedAssetDepreciationReversal
        {
            TenantId = TenantId,
            OriginalDepreciationRunId = run.Id,
            OriginalPostingEventId = run.PostingEventId!.Value,
            OriginalJournalEntryId = run.JournalEntryId!.Value,
            Status = FixedAssetDepreciationReversalStatuses.PendingApproval,
            Reason = policy.Reason,
            ImpactAssessment = impactAssessment,
            RequestedReversalDate = policy.ReversalDate,
            RequestedByUserId = userId,
            RequestedByUserName = _currentUser.UserName ?? "Unknown user",
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserName,
            CreatedById = userId
        };
        _context.FixedAssetDepreciationReversals.Add(reversal);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordDepreciationAuditAsync(
            FinanceAuditEvents.FixedAssetDepreciationReversalRequested,
            run.Id,
            beforeValues: new
            {
                run.Status,
                run.PostingEventId,
                run.JournalEntryId,
                run.TotalDepreciationAmount,
                LineCount = run.Lines.Count
            },
            afterValues: MapReversal(reversal, run),
            reason: policy.Reason,
            comment: impactAssessment,
            cancellationToken: cancellationToken);

        return MapReversal(reversal, run);
    }

    public async Task<FixedAssetDepreciationReversalDto> ReviewReversalAsync(
        Guid runId,
        Guid reversalId,
        ReviewFixedAssetDepreciationReversalDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reviewerId = GetRequiredUserId("review");
        var reversal = await LoadReversalAsync(runId, reversalId, cancellationToken);
        if (reversal.Status != FixedAssetDepreciationReversalStatuses.PendingApproval)
            throw new InvalidOperationException("Only a depreciation reversal awaiting approval can be reviewed.");
        if (reversal.RequestedByUserId == reviewerId)
            throw new InvalidOperationException("The depreciation reversal requester cannot review the same request.");

        var reviewComment = dto.ReviewComment?.Trim() ?? string.Empty;
        if (reviewComment.Length < 20)
        {
            throw new ArgumentException(
                "The depreciation reversal review comment must contain at least 20 characters.",
                nameof(dto));
        }

        // Re-run the accounting-state guard at approval time because a valuation, disposal, or
        // later depreciation could have been posted after the maker prepared the request.
        if (dto.Approved)
        {
            ValidatePostedRunLineage(reversal.OriginalDepreciationRun);
            await EnsureRunIsLatestReversibleAccountingAsync(
                reversal.OriginalDepreciationRun,
                cancellationToken);
        }

        reversal.Status = dto.Approved
            ? FixedAssetDepreciationReversalStatuses.Approved
            : FixedAssetDepreciationReversalStatuses.Rejected;
        reversal.ReviewedByUserId = reviewerId;
        reversal.ReviewedByUserName = _currentUser.UserName ?? "Unknown user";
        reversal.ReviewedAt = DateTime.UtcNow;
        reversal.ReviewComment = reviewComment;
        reversal.FailureReason = null;
        reversal.UpdatedAt = DateTime.UtcNow;
        reversal.UpdatedBy = _currentUser.UserName;
        reversal.LastModifiedById = reviewerId;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordDepreciationAuditAsync(
            dto.Approved
                ? FinanceAuditEvents.FixedAssetDepreciationReversalApproved
                : FinanceAuditEvents.FixedAssetDepreciationReversalRejected,
            reversal.OriginalDepreciationRunId,
            beforeValues: new { Status = FixedAssetDepreciationReversalStatuses.PendingApproval },
            afterValues: new { reversal.Id, reversal.Status, reversal.ReviewedByUserName, reversal.ReviewedAt },
            reason: reversal.Reason,
            comment: reviewComment,
            cancellationToken: cancellationToken);

        return MapReversal(reversal, reversal.OriginalDepreciationRun);
    }

    public async Task<FixedAssetDepreciationReversalDto> PostReversalAsync(
        Guid runId,
        Guid reversalId,
        CancellationToken cancellationToken = default)
    {
        EnsureReversalServicesConfigured();
        var strategy = _context.Database.CreateExecutionStrategy();

        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
                    ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                    : null;
                try
                {
                    var reversal = await LoadReversalAsync(runId, reversalId, cancellationToken);
                    if (reversal.Status == FixedAssetDepreciationReversalStatuses.Posted)
                    {
                        if (transaction != null)
                            await transaction.CommitAsync(cancellationToken);
                        return MapReversal(reversal, reversal.OriginalDepreciationRun);
                    }

                    if (reversal.Status != FixedAssetDepreciationReversalStatuses.Approved)
                    {
                        throw new InvalidOperationException(
                            "Only an independently approved depreciation reversal can be posted.");
                    }

                    var run = reversal.OriginalDepreciationRun;
                    ValidatePostedRunLineage(run);
                    await EnsureRunIsLatestReversibleAccountingAsync(run, cancellationToken);
                    var policy = await _financeReversalPolicyService!.ResolveAsync(
                        run.PostingDate,
                        reversal.Reason,
                        reversal.RequestedReversalDate,
                        cancellationToken);
                    var plan = await _financePostingEngine!.GetReversalPlanAsync(
                        reversal.OriginalPostingEventId,
                        policy.Reason,
                        policy.ReversalDate,
                        cancellationToken);
                    var posting = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = SourceModule,
                        SourceDocumentType = "FixedAssetDepreciationReversal",
                        SourceDocumentId = reversal.Id,
                        SourceDocumentTenantId = reversal.TenantId,
                        PostingAction = "Reverse",
                        SourceDocumentReference = $"DEP-REV-{run.FiscalPeriod.PeriodCode}-C{run.CorrectionSequence}",
                        Description = $"Reverse fixed asset depreciation - {run.FiscalPeriod.PeriodCode} - {run.BookClassification}",
                        PostingDate = plan.ReversalDate,
                        JournalType = "Fixed Asset Depreciation Reversal",
                        BookClassification = run.BookClassification == "ALL_ACTIVE_BOOKS" ? "IFRS" : run.BookClassification,
                        FunctionalCurrencyCode = await GetFunctionalCurrencyAsync(cancellationToken),
                        ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                        ReversalReason = policy.Reason,
                        ReversalType = "FA Depreciation",
                        IdempotencyKey = $"FA:DepreciationReversal:{reversal.TenantId:N}:{reversal.Id:N}",
                        ReturnExistingOnDuplicate = true,
                        Lines = plan.ReversalLines
                    }, cancellationToken);

                    // The posting engine can detach tracked entities while resolving an
                    // idempotency race. Reload before applying register changes so a successful
                    // compensating journal can never be committed without its subledger evidence.
                    if (_context.Entry(reversal).State == EntityState.Detached ||
                        _context.Entry(run).State == EntityState.Detached)
                    {
                        reversal = await LoadReversalAsync(runId, reversalId, cancellationToken);
                        if (reversal.Status == FixedAssetDepreciationReversalStatuses.Posted)
                        {
                            if (transaction != null)
                                await transaction.CommitAsync(cancellationToken);
                            return MapReversal(reversal, reversal.OriginalDepreciationRun);
                        }

                        run = reversal.OriginalDepreciationRun;
                    }

                    var priorDepreciationDates = await LoadPriorDepreciationDatesAsync(run, cancellationToken);
                    ApplyDepreciationReversal(
                        reversal,
                        run,
                        posting.JournalEntryId,
                        posting.PostingEventId,
                        posting.PostingDate,
                        priorDepreciationDates);
                    await _context.SaveChangesAsync(cancellationToken);

                    await RecordDepreciationAuditAsync(
                        FinanceAuditEvents.FixedAssetDepreciationRunReversed,
                        run.Id,
                        postingEventId: posting.PostingEventId,
                        journalEntryId: posting.JournalEntryId,
                        beforeValues: new
                        {
                            reversal.OriginalPostingEventId,
                            reversal.OriginalJournalEntryId,
                            run.TotalDepreciationAmount,
                            LineCount = run.Lines.Count
                        },
                        afterValues: new
                        {
                            reversal.Id,
                            ReversalStatus = reversal.Status,
                            RunStatus = run.Status,
                            posting.PostingEventId,
                            posting.JournalEntryId,
                            posting.PostingDate
                        },
                        reason: policy.Reason,
                        comment: reversal.ImpactAssessment,
                        cancellationToken: cancellationToken);

                    if (transaction != null)
                        await transaction.CommitAsync(cancellationToken);
                    return MapReversal(reversal, run);
                }
                catch
                {
                    if (transaction != null)
                        await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            // Keep an approved request retryable after a transient posting failure. The failure
            // narrative is persisted and audited, while the status remains Approved rather than
            // forcing Finance to create and approve a duplicate request.
            _context.ChangeTracker.Clear();
            var failedRequest = await _context.FixedAssetDepreciationReversals.SingleOrDefaultAsync(item =>
                item.TenantId == TenantId &&
                item.Id == reversalId &&
                item.OriginalDepreciationRunId == runId &&
                !item.IsDeleted,
                cancellationToken);
            if (failedRequest != null && failedRequest.Status != FixedAssetDepreciationReversalStatuses.Posted)
            {
                failedRequest.FailureReason = ex.Message;
                failedRequest.UpdatedAt = DateTime.UtcNow;
                failedRequest.UpdatedBy = _currentUser.UserName;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await RecordDepreciationAuditAsync(
                ex.Message.Contains("period", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("later", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetDepreciationReversalBlocked
                    : FinanceAuditEvents.FixedAssetDepreciationReversalFailed,
                runId,
                afterValues: new { ReversalId = reversalId, Error = ex.Message },
                reason: ex.Message,
                comment: "Fixed asset depreciation reversal posting failed.",
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private void EnsureReversalServicesConfigured()
    {
        if (_financePostingEngine == null || _financeReversalPolicyService == null)
        {
            throw new InvalidOperationException(
                "Finance posting and reversal policy services are required for depreciation reversal.");
        }
    }

    private Guid GetRequiredUserId(string action)
        => UserId ?? throw new InvalidOperationException(
            $"A resolved user identity is required to {action} a depreciation reversal.");

    private async Task<FixedAssetDepreciationRun> LoadRunForReversalAsync(
        Guid runId,
        CancellationToken cancellationToken)
        => await _context.FixedAssetDepreciationRuns
            .AsSplitQuery()
            .Include(item => item.FiscalPeriod)
            .Include(item => item.Lines)
                .ThenInclude(line => line.FixedAsset)
                    .ThenInclude(asset => asset.BookValues)
                        .ThenInclude(value => value.AccountingBook)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == runId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset depreciation run not found.");

    private async Task<FixedAssetDepreciationReversal> LoadReversalAsync(
        Guid runId,
        Guid reversalId,
        CancellationToken cancellationToken)
        => await _context.FixedAssetDepreciationReversals
            .AsSplitQuery()
            .Include(item => item.OriginalDepreciationRun)
                .ThenInclude(run => run.FiscalPeriod)
            .Include(item => item.OriginalDepreciationRun)
                .ThenInclude(run => run.Lines)
                    .ThenInclude(line => line.FixedAsset)
                        .ThenInclude(asset => asset.BookValues)
                            .ThenInclude(value => value.AccountingBook)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId &&
                item.Id == reversalId &&
                item.OriginalDepreciationRunId == runId &&
                !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Fixed asset depreciation reversal request not found.");

    private static void ValidatePostedRunLineage(FixedAssetDepreciationRun run)
    {
        if (string.Equals(run.Status, "Reversed", StringComparison.OrdinalIgnoreCase) ||
            run.Lines.Any(line => line.IsReversed))
        {
            throw new InvalidOperationException("This fixed asset depreciation run has already been reversed.");
        }

        if (!string.Equals(run.Status, "Posted", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only a posted fixed asset depreciation run can be reversed.");
        if (!run.PostingEventId.HasValue || !run.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException(
                "The depreciation run is missing its original posting-event or journal lineage and cannot be reversed safely.");
        }

        if (run.Lines.Count == 0 || run.Lines.Any(line =>
                !line.IsPosted ||
                line.IsProjected ||
                !line.PostingEventId.HasValue ||
                !line.JournalEntryId.HasValue ||
                line.PostingEventId != run.PostingEventId ||
                line.JournalEntryId != run.JournalEntryId))
        {
            throw new InvalidOperationException(
                "The depreciation run contains incomplete or inconsistent schedule posting evidence.");
        }
    }

    private async Task EnsureRunIsLatestReversibleAccountingAsync(
        FixedAssetDepreciationRun run,
        CancellationToken cancellationToken)
    {
        var lineIds = run.Lines.Select(line => line.Id).ToHashSet();
        var assetIds = run.Lines.Select(line => line.FixedAssetId).Distinct().ToList();
        var laterSchedules = await _context.AssetDepreciationSchedules
            .AsNoTracking()
            .Where(line =>
                line.TenantId == TenantId &&
                assetIds.Contains(line.FixedAssetId) &&
                !lineIds.Contains(line.Id) &&
                line.IsPosted &&
                !line.IsProjected &&
                !line.IsReversed &&
                !line.IsDeleted)
            .ToListAsync(cancellationToken);
        if (laterSchedules.Any(candidate => run.Lines.Any(original =>
                original.FixedAssetId == candidate.FixedAssetId &&
                SameBook(original, candidate) &&
                (candidate.PostingDate > original.PostingDate ||
                 candidate.CorrectionSequence > original.CorrectionSequence))))
        {
            throw new InvalidOperationException(
                "Later depreciation exists for at least one asset/book. Reverse later runs before reversing this run.");
        }

        var postedValuations = await _context.AssetValuations
            .AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                assetIds.Contains(item.FixedAssetId) &&
                item.IsPostedToGL &&
                !item.IsDeleted &&
                item.AccountingDate >= run.PostingDate.Date)
            .ToListAsync(cancellationToken);
        if (postedValuations.Any(valuation => run.Lines.Any(line =>
                line.FixedAssetId == valuation.FixedAssetId && SameBook(line, valuation.BookClassification, valuation.AccountingBookId))))
        {
            throw new InvalidOperationException(
                "A later valuation or impairment depends on this depreciation. Reverse that valuation before reversing depreciation.");
        }

        var hasDisposal = await _context.AssetDisposals.AnyAsync(item =>
            item.TenantId == TenantId &&
            assetIds.Contains(item.FixedAssetId) &&
            item.Status != AssetDisposalStatus.Rejected &&
            item.Status != AssetDisposalStatus.Cancelled &&
            !item.IsDeleted,
            cancellationToken);
        if (hasDisposal)
        {
            throw new InvalidOperationException(
                "A disposal depends on this depreciation. Reverse or cancel the disposal before reversing depreciation.");
        }

        // A value reclassification is accounting-dependent; custody/location transfers are not
        // blocked because they do not alter depreciation reserve or NBV.
        var hasGlReclassification = await _context.AssetTransfers.AnyAsync(item =>
            item.TenantId == TenantId &&
            assetIds.Contains(item.FixedAssetId) &&
            item.TransferType == AssetTransferType.GlReclassification &&
            item.PostingEventId.HasValue &&
            !item.IsDeleted,
            cancellationToken);
        if (hasGlReclassification)
        {
            throw new InvalidOperationException(
                "A later GL reclassification depends on this depreciation and must be reversed first.");
        }

        foreach (var line in run.Lines)
        {
            var bookValue = line.FixedAsset.BookValues.SingleOrDefault(value =>
                !value.IsDeleted &&
                ((line.AccountingBookId.HasValue && value.AccountingBookId == line.AccountingBookId) ||
                 value.BookClassification.Equals(line.BookClassification, StringComparison.OrdinalIgnoreCase)))
                ?? throw new InvalidOperationException(
                    "A depreciation schedule no longer has a matching tenant asset book value.");

            // Snapshot equality is an important final guard. It catches any unmodelled carrying
            // value movement without guessing how that later activity should itself be reversed.
            if (RoundMoney(bookValue.AccumulatedDepreciation) != RoundMoney(line.AccumulatedDepreciation) ||
                RoundMoney(bookValue.NetBookValue) != RoundMoney(line.NetBookValue) ||
                FixedAssetDepreciationCalculator.RoundUnits(bookValue.AccumulatedProductionUnits) !=
                FixedAssetDepreciationCalculator.RoundUnits(line.CumulativeProductionUnitsAfter))
            {
                throw new InvalidOperationException(
                    "The asset book value has later accounting activity and no longer matches this depreciation run. Reverse later activity first.");
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, DateTime?>> LoadPriorDepreciationDatesAsync(
        FixedAssetDepreciationRun run,
        CancellationToken cancellationToken)
    {
        var lineIds = run.Lines.Select(line => line.Id).ToHashSet();
        var assetIds = run.Lines.Select(line => line.FixedAssetId).Distinct().ToList();
        var prior = await _context.AssetDepreciationSchedules
            .AsNoTracking()
            .Where(line =>
                line.TenantId == TenantId &&
                assetIds.Contains(line.FixedAssetId) &&
                !lineIds.Contains(line.Id) &&
                line.IsPosted &&
                !line.IsProjected &&
                !line.IsReversed &&
                !line.IsDeleted)
            .ToListAsync(cancellationToken);

        return run.Lines.ToDictionary(
            BuildAssetBookKey,
            line => prior
                .Where(candidate => candidate.FixedAssetId == line.FixedAssetId && SameBook(line, candidate))
                .Select(candidate => candidate.PostingDate)
                .Where(date => date.HasValue)
                .Max());
    }

    private void ApplyDepreciationReversal(
        FixedAssetDepreciationReversal reversal,
        FixedAssetDepreciationRun run,
        Guid reversalJournalEntryId,
        Guid reversalPostingEventId,
        DateTime reversalDate,
        IReadOnlyDictionary<string, DateTime?> priorDepreciationDates)
    {
        var reversedAt = DateTime.UtcNow;
        foreach (var schedule in run.Lines.OrderBy(line => line.FixedAssetId).ThenBy(line => line.BookClassification))
        {
            var bookValue = schedule.FixedAsset.BookValues.Single(value =>
                !value.IsDeleted &&
                ((schedule.AccountingBookId.HasValue && value.AccountingBookId == schedule.AccountingBookId) ||
                 value.BookClassification.Equals(schedule.BookClassification, StringComparison.OrdinalIgnoreCase)));

            bookValue.AccumulatedDepreciation = schedule.AccumulatedDepreciationBefore;
            bookValue.NetBookValue = schedule.NetBookValueBefore;
            // Units-of-production usage is part of the accounting estimate consumed by this run.
            // Restoring its exact pre-run snapshot makes a corrected run deterministic and prevents
            // the reversed usage from exhausting the asset's lifetime capacity.
            bookValue.AccumulatedProductionUnits = schedule.CumulativeProductionUnitsBefore;
            bookValue.LastDepreciationDate = priorDepreciationDates[BuildAssetBookKey(schedule)];
            if (bookValue.RemainingUsefulLifeMonths.HasValue)
                bookValue.RemainingUsefulLifeMonths += 1;
            bookValue.UpdatedAt = reversedAt;
            bookValue.UpdatedBy = _currentUser.UserName;

            if (bookValue.AccountingBook?.IsDefault == true ||
                bookValue.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
            {
                schedule.FixedAsset.NetBookValue = schedule.NetBookValueBefore;
                schedule.FixedAsset.ResidualValue = bookValue.ResidualValue;
                schedule.FixedAsset.UsefulLifeMonths = bookValue.UsefulLifeMonths;
                schedule.FixedAsset.AccumulatedProductionUnits = bookValue.AccumulatedProductionUnits;
                if (schedule.FixedAsset.Status == FixedAssetStatus.FullyDepreciated &&
                    schedule.NetBookValueBefore > bookValue.ResidualValue)
                {
                    schedule.FixedAsset.Status = FixedAssetStatus.Active;
                }
            }

            schedule.FixedAsset.UpdatedAt = reversedAt;
            schedule.FixedAsset.UpdatedBy = _currentUser.UserName;
            schedule.IsReversed = true;
            schedule.ReversedAt = reversedAt;
            schedule.ReversalJournalEntryId = reversalJournalEntryId;
            schedule.ReversalPostingEventId = reversalPostingEventId;
            schedule.DepreciationReversalId = reversal.Id;
            schedule.UpdatedAt = reversedAt;
            schedule.UpdatedBy = _currentUser.UserName;

            // A negative register movement mirrors the compensating GL entry. The positive
            // original movement remains visible, so reports can prove both gross activity and
            // the corrected carrying amount without destructive schedule edits.
            _context.AssetTransactions.Add(new AssetTransaction
            {
                TenantId = TenantId,
                FixedAssetId = schedule.FixedAssetId,
                AccountingBookId = schedule.AccountingBookId,
                BookClassification = schedule.BookClassification,
                TransactionDate = reversalDate.Date,
                TransactionType = "DepreciationReversal",
                Description = reversal.Reason,
                Amount = -schedule.DepreciationAmount,
                ResultingBookValue = schedule.NetBookValueBefore,
                RelatedEntityId = reversalPostingEventId,
                PerformedByUserId = UserId ?? Guid.Empty,
                CreatedAt = reversedAt,
                CreatedBy = _currentUser.UserName
            });
        }

        run.Status = "Reversed";
        run.UpdatedAt = reversedAt;
        run.UpdatedBy = _currentUser.UserName;
        if (!run.FixedAssetId.HasValue)
        {
            run.FiscalPeriod.DepreciationComplete = false;
            run.FiscalPeriod.DepreciationCompletedDate = null;
        }

        reversal.Status = FixedAssetDepreciationReversalStatuses.Posted;
        reversal.ReversalJournalEntryId = reversalJournalEntryId;
        reversal.ReversalPostingEventId = reversalPostingEventId;
        reversal.PostedAt = reversedAt;
        reversal.FailureReason = null;
        reversal.UpdatedAt = reversedAt;
        reversal.UpdatedBy = _currentUser.UserName;
        reversal.LastModifiedById = UserId;
    }

    private static bool SameBook(AssetDepreciationSchedule left, AssetDepreciationSchedule right)
        => SameBook(left, right.BookClassification, right.AccountingBookId);

    private static bool SameBook(
        AssetDepreciationSchedule schedule,
        string bookClassification,
        Guid? accountingBookId)
        => schedule.AccountingBookId.HasValue && accountingBookId.HasValue
            ? schedule.AccountingBookId == accountingBookId
            : schedule.BookClassification.Equals(bookClassification, StringComparison.OrdinalIgnoreCase);

    private static string BuildAssetBookKey(AssetDepreciationSchedule schedule)
        => $"{schedule.FixedAssetId:N}|{schedule.AccountingBookId?.ToString("N") ?? schedule.BookClassification.ToUpperInvariant()}";

    private static FixedAssetDepreciationReversalDto MapReversal(
        FixedAssetDepreciationReversal reversal,
        FixedAssetDepreciationRun run)
        => new()
        {
            Id = reversal.Id,
            OriginalDepreciationRunId = reversal.OriginalDepreciationRunId,
            PeriodCode = run.FiscalPeriod?.PeriodCode ?? string.Empty,
            BookClassification = run.BookClassification,
            TotalDepreciationAmount = run.TotalDepreciationAmount,
            OriginalCorrectionSequence = run.CorrectionSequence,
            OriginalPostingEventId = reversal.OriginalPostingEventId,
            OriginalJournalEntryId = reversal.OriginalJournalEntryId,
            ReversalPostingEventId = reversal.ReversalPostingEventId,
            ReversalJournalEntryId = reversal.ReversalJournalEntryId,
            Status = reversal.Status,
            Reason = reversal.Reason,
            ImpactAssessment = reversal.ImpactAssessment,
            RequestedReversalDate = reversal.RequestedReversalDate,
            RequestedByUserId = reversal.RequestedByUserId,
            RequestedByUserName = reversal.RequestedByUserName,
            RequestedAt = reversal.RequestedAt,
            ReviewedByUserId = reversal.ReviewedByUserId,
            ReviewedByUserName = reversal.ReviewedByUserName,
            ReviewedAt = reversal.ReviewedAt,
            ReviewComment = reversal.ReviewComment,
            PostedAt = reversal.PostedAt,
            FailureReason = reversal.FailureReason
        };
}
