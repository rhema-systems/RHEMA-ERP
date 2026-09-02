using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

/// <summary>
/// Controlled correction workflow for FIN-LIM-0037. This is a partial of the existing valuation
/// service deliberately: tenant resolution, posting, accounting-book selection, and audit must
/// remain the same machinery used by ordinary valuations, not a parallel correction subsystem.
/// </summary>
public partial class AssetValuationService
{
    public async Task<IReadOnlyList<AssetValuationCorrectionDto>> GetCorrectionsAsync(
        Guid valuationId,
        CancellationToken cancellationToken = default)
    {
        var valuation = await LoadValuationForCorrectionAsync(valuationId, cancellationToken);
        var corrections = await _context.AssetValuationCorrections.AsNoTracking()
            .Where(c => c.TenantId == TenantId && c.OriginalValuationId == valuationId && !c.IsDeleted)
            .OrderByDescending(c => c.RequestedAt)
            .ToListAsync(cancellationToken);
        return corrections.Select(c => MapCorrection(c, valuation)).ToList();
    }

    public async Task<AssetValuationCorrectionDto> RequestCorrectionAsync(
        Guid valuationId,
        RequestAssetValuationCorrectionDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureCorrectionServicesConfigured();
        var requesterId = UserId ?? throw new InvalidOperationException(
            "A resolved user identity is required to request a valuation correction.");
        var valuation = await LoadValuationForCorrectionAsync(valuationId, cancellationToken);
        await EnsureValuationIsLatestReversibleAccountingAsync(valuation, cancellationToken);

        var pendingExists = await _context.AssetValuationCorrections.AnyAsync(c =>
            c.TenantId == TenantId && c.OriginalValuationId == valuationId && !c.IsDeleted &&
            c.Status != AssetValuationCorrectionStatuses.Rejected &&
            c.Status != AssetValuationCorrectionStatuses.Posted,
            cancellationToken);
        if (pendingExists)
            throw new InvalidOperationException("This valuation already has a correction awaiting review or posting.");

        var policy = await _financeReversalPolicyService!.ResolveAsync(
            valuation.AccountingDate, dto.Reason, dto.ReversalDate, cancellationToken);
        var impact = dto.ImpactAssessment?.Trim() ?? string.Empty;
        if (impact.Length < 20)
            throw new ArgumentException("The valuation correction impact assessment must contain at least 20 characters.", nameof(dto));

        var correction = new AssetValuationCorrection
        {
            TenantId = TenantId,
            OriginalValuationId = valuation.Id,
            OriginalPostingEventId = valuation.PostingEventId!.Value,
            OriginalJournalEntryId = valuation.JournalEntryId!.Value,
            Status = AssetValuationCorrectionStatuses.PendingApproval,
            Reason = policy.Reason,
            ImpactAssessment = impact,
            RequestedReversalDate = policy.ReversalDate,
            RequestedByUserId = requesterId,
            RequestedByUserName = UserName,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = requesterId
        };
        _context.AssetValuationCorrections.Add(correction);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordValuationAuditAsync(
            FinanceAuditEvents.FixedAssetValuationCorrectionRequested,
            valuation,
            afterValues: MapCorrection(correction, valuation),
            reason: correction.Reason,
            comment: correction.ImpactAssessment,
            cancellationToken: cancellationToken);
        return MapCorrection(correction, valuation);
    }

    public async Task<AssetValuationCorrectionDto> ReviewCorrectionAsync(
        Guid valuationId,
        Guid correctionId,
        ReviewAssetValuationCorrectionDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var reviewerId = UserId ?? throw new InvalidOperationException(
            "A resolved user identity is required to review a valuation correction.");
        var correction = await LoadCorrectionAsync(valuationId, correctionId, cancellationToken);
        if (correction.Status != AssetValuationCorrectionStatuses.PendingApproval)
            throw new InvalidOperationException("Only a valuation correction awaiting approval can be reviewed.");
        if (correction.RequestedByUserId == reviewerId)
            throw new InvalidOperationException("The valuation correction requester cannot review the same request.");

        var comment = dto.ReviewComment?.Trim() ?? string.Empty;
        if (comment.Length < 20)
            throw new ArgumentException("The valuation correction review comment must contain at least 20 characters.", nameof(dto));
        if (dto.Approved)
            await EnsureValuationIsLatestReversibleAccountingAsync(correction.OriginalValuation, cancellationToken);

        correction.Status = dto.Approved
            ? AssetValuationCorrectionStatuses.Approved
            : AssetValuationCorrectionStatuses.Rejected;
        correction.ReviewedByUserId = reviewerId;
        correction.ReviewedByUserName = UserName;
        correction.ReviewedAt = DateTime.UtcNow;
        correction.ReviewComment = comment;
        correction.FailureReason = null;
        correction.UpdatedAt = DateTime.UtcNow;
        correction.UpdatedBy = UserName;
        correction.LastModifiedById = reviewerId;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordValuationAuditAsync(
            dto.Approved
                ? FinanceAuditEvents.FixedAssetValuationCorrectionApproved
                : FinanceAuditEvents.FixedAssetValuationCorrectionRejected,
            correction.OriginalValuation,
            afterValues: new { correction.Id, correction.Status, correction.ReviewedByUserName, correction.ReviewedAt },
            reason: correction.Reason,
            comment: comment,
            cancellationToken: cancellationToken);
        return MapCorrection(correction, correction.OriginalValuation);
    }

    public async Task<AssetValuationCorrectionDto> PostCorrectionAsync(
        Guid valuationId,
        Guid correctionId,
        CancellationToken cancellationToken = default)
    {
        EnsureCorrectionServicesConfigured();
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
                    var correction = await LoadCorrectionAsync(valuationId, correctionId, cancellationToken);
                    if (correction.Status == AssetValuationCorrectionStatuses.Posted)
                    {
                        if (transaction != null) await transaction.CommitAsync(cancellationToken);
                        return MapCorrection(correction, correction.OriginalValuation);
                    }
                    if (correction.Status != AssetValuationCorrectionStatuses.Approved)
                        throw new InvalidOperationException("Only an independently approved valuation correction can be posted.");

                    var valuation = correction.OriginalValuation;
                    await EnsureValuationIsLatestReversibleAccountingAsync(valuation, cancellationToken);
                    var policy = await _financeReversalPolicyService!.ResolveAsync(
                        valuation.AccountingDate, correction.Reason, correction.RequestedReversalDate, cancellationToken);
                    var plan = await _financePostingEngine!.GetReversalPlanAsync(
                        correction.OriginalPostingEventId, policy.Reason, policy.ReversalDate, cancellationToken);
                    var reversalLines = plan.ReversalLines.ToList();
                    if (_fixedAssetDimensions is not null)
                        await _fixedAssetDimensions.RegisterHistoricalReversalAsync(
                            ValuationCorrectionProducer,
                            correction.Id,
                            plan.OriginalJournalEntryId,
                            reversalLines,
                            cancellationToken);
                    var posting = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
                    {
                        SourceModule = ValuationCorrectionProducer.Definition.PostingSourceModule,
                        OriginModuleCode = FinanceModuleLockCatalog.Finance,
                        SourceDocumentType = ValuationCorrectionProducer.Definition.DocumentType,
                        SourceDocumentId = correction.Id,
                        SourceDocumentTenantId = correction.TenantId,
                        PostingAction = "Reverse",
                        SourceDocumentReference = $"VAL-COR-{valuation.Id:N}",
                        Description = $"Correct {valuation.ValuationType} for {valuation.FixedAsset.AssetCode}",
                        PostingDate = plan.ReversalDate,
                        JournalType = "Fixed Asset Valuation Correction",
                        BookClassification = valuation.BookClassification,
                        FunctionalCurrencyCode = await GetFunctionalCurrencyAsync(),
                        ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
                        ReversalReason = policy.Reason,
                        ReversalType = "FA Valuation",
                        IdempotencyKey = $"FA:ValuationCorrection:{correction.TenantId:N}:{correction.Id:N}",
                        ReturnExistingOnDuplicate = true,
                        Lines = reversalLines
                    }, ValuationCorrectionProducer, cancellationToken);

                    // The posting engine may detach tracked entities while resolving an idempotency
                    // race. Reload before applying book state so journal and subledger evidence are atomic.
                    if (_context.Entry(correction).State == EntityState.Detached)
                        correction = await LoadCorrectionAsync(valuationId, correctionId, cancellationToken);
                    valuation = correction.OriginalValuation;
                    ApplyValuationCorrection(correction, valuation, posting);
                    await _context.SaveChangesAsync(cancellationToken);

                    await RecordValuationAuditAsync(
                        FinanceAuditEvents.FixedAssetValuationCorrected,
                        valuation,
                        posting.PostingEventId,
                        posting.JournalEntryId,
                        beforeValues: new { valuation.CarryingAmountAfter, valuation.RevisedUsefulLifeMonths },
                        afterValues: new { valuation.CarryingAmountBefore, valuation.UsefulLifeMonthsBefore, correction.Id },
                        reason: correction.Reason,
                        comment: correction.ImpactAssessment,
                        cancellationToken: cancellationToken);
                    if (transaction != null) await transaction.CommitAsync(cancellationToken);
                    return MapCorrection(correction, valuation);
                }
                catch
                {
                    if (transaction != null) await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _context.ChangeTracker.Clear();
            var failed = await _context.AssetValuationCorrections.SingleOrDefaultAsync(c =>
                c.TenantId == TenantId && c.Id == correctionId && c.OriginalValuationId == valuationId && !c.IsDeleted,
                cancellationToken);
            if (failed != null && failed.Status != AssetValuationCorrectionStatuses.Posted)
            {
                // Approved requests intentionally stay retryable; failure evidence is recorded
                // without forcing Finance through another maker-checker cycle for a transient fault.
                failed.FailureReason = ex.Message;
                failed.UpdatedAt = DateTime.UtcNow;
                failed.UpdatedBy = UserName;
                await _context.SaveChangesAsync(cancellationToken);
            }
            throw;
        }
    }

    private void EnsureCorrectionServicesConfigured()
    {
        if (_financePostingEngine == null || _financeReversalPolicyService == null)
            throw new InvalidOperationException("Finance posting and reversal policy services are required for valuation correction.");
    }

    private async Task<AssetValuation> LoadValuationForCorrectionAsync(Guid valuationId, CancellationToken cancellationToken)
        => await _context.AssetValuations
            .AsSplitQuery()
            .Include(v => v.FixedAsset).ThenInclude(a => a.BookValues).ThenInclude(b => b.AccountingBook)
            .SingleOrDefaultAsync(v => v.TenantId == TenantId && v.Id == valuationId && !v.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Valuation not found.");

    private async Task<AssetValuationCorrection> LoadCorrectionAsync(
        Guid valuationId, Guid correctionId, CancellationToken cancellationToken)
        => await _context.AssetValuationCorrections
            .AsSplitQuery()
            .Include(c => c.OriginalValuation).ThenInclude(v => v.FixedAsset)
                .ThenInclude(a => a.BookValues).ThenInclude(b => b.AccountingBook)
            .SingleOrDefaultAsync(c => c.TenantId == TenantId && c.Id == correctionId &&
                c.OriginalValuationId == valuationId && !c.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Valuation correction request not found.");

    private async Task EnsureValuationIsLatestReversibleAccountingAsync(
        AssetValuation valuation, CancellationToken cancellationToken)
    {
        if (!valuation.IsPostedToGL || !valuation.PostingEventId.HasValue || !valuation.JournalEntryId.HasValue)
            throw new InvalidOperationException("Only a posted valuation with complete journal lineage can be corrected.");
        if (valuation.IsCorrected)
            throw new InvalidOperationException("This valuation has already been corrected.");

        var book = ResolveBookValue(valuation.FixedAsset, valuation.AccountingBookId, valuation.BookClassification);
        if (RoundMoney(book.NetBookValue) != RoundMoney(valuation.CarryingAmountAfter))
        {
            throw new InvalidOperationException(
                "Valuation correction is blocked because later carrying-value activity has changed the accounting book.");
        }

        var laterValuation = await _context.AssetValuations.AnyAsync(v =>
            v.TenantId == TenantId && v.FixedAssetId == valuation.FixedAssetId &&
            v.BookClassification == valuation.BookClassification && v.Id != valuation.Id &&
            v.IsPostedToGL && !v.IsCorrected && !v.IsDeleted &&
            (v.AccountingDate > valuation.AccountingDate || v.CreatedAt > valuation.CreatedAt),
            cancellationToken);
        if (laterValuation)
            throw new InvalidOperationException("A later posted valuation must be corrected first.");

        var laterDepreciation = await _context.AssetDepreciationSchedules.AnyAsync(s =>
            s.TenantId == TenantId && s.FixedAssetId == valuation.FixedAssetId &&
            s.BookClassification == valuation.BookClassification && s.IsPosted && !s.IsReversed && !s.IsDeleted &&
            (s.PostingDate ?? s.PostedDate) > valuation.AccountingDate,
            cancellationToken);
        if (laterDepreciation)
            throw new InvalidOperationException("Later posted depreciation must be reversed before correcting this valuation.");
    }

    private void ApplyValuationCorrection(
        AssetValuationCorrection correction,
        AssetValuation valuation,
        FinancePostingResultDto posting)
    {
        var book = ResolveBookValue(valuation.FixedAsset, valuation.AccountingBookId, valuation.BookClassification);
        book.NetBookValue = valuation.CarryingAmountBefore;
        book.UsefulLifeMonths = valuation.UsefulLifeMonthsBefore;
        book.RemainingUsefulLifeMonths = valuation.RemainingUsefulLifeMonthsBefore;
        book.UpdatedAt = DateTime.UtcNow;
        book.UpdatedBy = UserName;

        if (book.AccountingBook?.IsDefault == true || book.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
        {
            valuation.FixedAsset.NetBookValue = valuation.CarryingAmountBefore;
            valuation.FixedAsset.UsefulLifeMonths = valuation.UsefulLifeMonthsBefore;
            valuation.FixedAsset.UpdatedAt = DateTime.UtcNow;
            valuation.FixedAsset.UpdatedBy = UserName;
        }

        valuation.IsCorrected = true;
        valuation.CorrectionId = correction.Id;
        valuation.CorrectedAt = posting.PostingDate;
        valuation.UpdatedAt = DateTime.UtcNow;
        valuation.UpdatedBy = UserName;
        correction.Status = AssetValuationCorrectionStatuses.Posted;
        correction.ReversalPostingEventId = posting.PostingEventId;
        correction.ReversalJournalEntryId = posting.JournalEntryId;
        correction.PostedAt = posting.PostingDate;
        correction.FailureReason = null;
        correction.UpdatedAt = DateTime.UtcNow;
        correction.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = valuation.FixedAssetId,
            AccountingBookId = valuation.AccountingBookId,
            BookClassification = valuation.BookClassification,
            TransactionDate = posting.PostingDate,
            TransactionType = "ValuationCorrection",
            Description = $"Correction of posted {valuation.ValuationType} {valuation.Id:N}",
            Amount = -valuation.AdjustmentAmount,
            ResultingBookValue = valuation.CarryingAmountBefore,
            RelatedEntityId = posting.PostingEventId,
            PerformedByUserId = UserId ?? correction.RequestedByUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        });
    }

    private static AssetValuationCorrectionDto MapCorrection(
        AssetValuationCorrection correction, AssetValuation valuation) => new()
    {
        Id = correction.Id,
        OriginalValuationId = correction.OriginalValuationId,
        FixedAssetId = valuation.FixedAssetId,
        AssetCode = valuation.FixedAsset?.AssetCode,
        ValuationType = valuation.ValuationType.ToString(),
        Status = correction.Status,
        Reason = correction.Reason,
        ImpactAssessment = correction.ImpactAssessment,
        RequestedReversalDate = correction.RequestedReversalDate,
        RequestedByUserId = correction.RequestedByUserId,
        RequestedByUserName = correction.RequestedByUserName,
        RequestedAt = correction.RequestedAt,
        ReviewedByUserId = correction.ReviewedByUserId,
        ReviewedByUserName = correction.ReviewedByUserName,
        ReviewedAt = correction.ReviewedAt,
        ReviewComment = correction.ReviewComment,
        OriginalPostingEventId = correction.OriginalPostingEventId,
        OriginalJournalEntryId = correction.OriginalJournalEntryId,
        ReversalPostingEventId = correction.ReversalPostingEventId,
        ReversalJournalEntryId = correction.ReversalJournalEntryId,
        PostedAt = correction.PostedAt,
        FailureReason = correction.FailureReason
    };
}
