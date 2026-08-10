using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class AssetDisposalService : IAssetDisposalService
{
    private const string SourceModule = "FixedAssets";
    private const string SourceDocumentType = "FixedAssetDisposal";
    private const string PostingAction = "Disposal";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _documentNumberingService;
    private readonly IWorkflowService _workflowService;
    private readonly IFinancePostingEngine? _financePostingEngine;
    private readonly IFinanceAuditService? _financeAuditService;

    public AssetDisposalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IWorkflowService workflowService,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _workflowService = workflowService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<AssetDisposalDto?> GetByIdAsync(Guid id)
    {
        var disposal = await BuildDisposalQuery()
            .Where(d => d.TenantId == TenantId && d.Id == id)
            .FirstOrDefaultAsync();

        return disposal == null ? null : MapToDto(disposal);
    }

    public async Task<IEnumerable<AssetDisposalDto>> GetAllAsync()
    {
        var disposals = await BuildDisposalQuery()
            .Where(d => d.TenantId == TenantId)
            .OrderByDescending(d => d.DisposalDate)
            .ToListAsync();

        return disposals.Select(MapToDto).ToList();
    }

    public async Task<AssetDisposalDto> RequestDisposalAsync(RequestAssetDisposalDto dto, Guid requestedById)
    {
        var idempotencyKey = NormalizeIdempotencyKey(dto.IdempotencyKey);
        if (idempotencyKey != null)
        {
            var existing = await BuildDisposalQuery()
                .FirstOrDefaultAsync(d => d.TenantId == TenantId && d.IdempotencyKey == idempotencyKey);
            if (existing != null)
            {
                return MapToDto(existing);
            }
        }

        var asset = await LoadAssetForDisposalAsync(dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        await ValidateDisposalRequestAsync(asset, dto);
        var bookValue = ResolveDefaultBookValue(asset);
        var fiscalPeriod = await ResolveFiscalPeriodAsync(dto.DisposalDate)
            ?? throw new InvalidOperationException("No fiscal period covers the disposal accounting date.");
        FinalDepreciationPreparation finalDepreciation;
        try
        {
            finalDepreciation = await CalculateFinalDepreciationAsync(
                asset,
                bookValue,
                fiscalPeriod,
                dto.DisposalDate,
                dto.FinalDepreciationProductionUnits,
                dto.FinalDepreciationEvidenceReference,
                dto.FinalDepreciationEvidenceNotes);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("depreciation", StringComparison.OrdinalIgnoreCase))
        {
            // Request-time blockers do not yet have an AssetDisposal row, so record the asset ID as
            // source evidence. Completion-time blockers continue to reference the disposal itself.
            await RecordBlockedDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation,
                asset.Id,
                ex.Message);
            throw;
        }
        var snapshot = await BuildDisposalSnapshotAsync(asset, bookValue, dto, finalDepreciation);
        var surplusTransfer = await ResolveRevaluationSurplusTransferAsync(asset, snapshot.RevaluationSurplusBalance);

        var existingActiveDisposal = await _context.AssetDisposals
            .Where(d => d.TenantId == TenantId
                && d.FixedAssetId == asset.Id
                && d.Status != AssetDisposalStatus.Rejected
                && d.Status != AssetDisposalStatus.Cancelled
                && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync();
        if (existingActiveDisposal != null)
        {
            throw new InvalidOperationException("This fixed asset already has an active disposal request.");
        }

        var disposal = new AssetDisposal
        {
            TenantId = TenantId,
            FixedAssetId = dto.FixedAssetId,
            DisposalDate = dto.DisposalDate.Date,
            AccountingDate = dto.DisposalDate.Date,
            FiscalPeriodId = fiscalPeriod?.Id,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = NormalizeBookClassification(bookValue.BookClassification),
            DisposalType = dto.DisposalType,
            Status = AssetDisposalStatus.PendingApproval,
            Reason = dto.Reason?.Trim(),
            SaleProceeds = RoundMoney(dto.SaleProceeds),
            DisposalCost = RoundMoney(dto.DisposalCost),
            NetProceeds = snapshot.NetProceeds,
            ProceedsCurrencyCode = snapshot.ProceedsCurrencyCode,
            ProceedsAccountId = snapshot.ProceedsAccountId,
            CostAtDisposal = snapshot.AssetCarryingAccountAmount,
            AccumulatedDepreciationAtDisposal = snapshot.AccumulatedDepreciation,
            FinalDepreciationAmount = finalDepreciation.Amount,
            FinalDepreciationFromDate = finalDepreciation.FromDate,
            FinalDepreciationToDate = finalDepreciation.ToDate,
            FinalDepreciationPeriodDays = finalDepreciation.PeriodDays,
            FinalDepreciationEligibleDays = finalDepreciation.EligibleDays,
            FinalDepreciationProrationBasis = finalDepreciation.ProrationBasis,
            FinalDepreciationMethodSnapshot = bookValue.DepreciationMethod,
            FinalDepreciationScheduleId = finalDepreciation.Amount > 0m ? Guid.NewGuid() : null,
            FinalDepreciationProductionUnits = finalDepreciation.Calculation.PeriodProductionUnits,
            FinalDepreciationDiminishingRatePercent = finalDepreciation.Calculation.EffectiveDiminishingBalanceRatePercent,
            FinalDepreciationLifetimeProductionCapacity = finalDepreciation.Calculation.LifetimeProductionCapacity,
            FinalDepreciationCumulativeProductionUnitsBefore = finalDepreciation.Calculation.CumulativeProductionUnitsBefore,
            FinalDepreciationCumulativeProductionUnitsAfter = finalDepreciation.Calculation.CumulativeProductionUnitsAfter,
            FinalDepreciationEvidenceReference = finalDepreciation.Calculation.ProductionEvidenceReference,
            FinalDepreciationEvidenceNotes = finalDepreciation.Calculation.ProductionEvidenceNotes,
            AccumulatedImpairmentAtDisposal = snapshot.AccumulatedImpairment,
            RevaluationSurplusAtDisposal = snapshot.RevaluationSurplusBalance,
            RevaluationSurplusAccountId = surplusTransfer.RevaluationSurplusAccountId,
            RetainedEarningsAccountId = surplusTransfer.RetainedEarningsAccountId,
            RevaluationSurplusTransferAmount = snapshot.RevaluationSurplusBalance,
            NetBookValueAtDisposal = snapshot.NetBookValue,
            GainOrLoss = snapshot.GainOrLoss,
            BuyerName = dto.BuyerName?.Trim(),
            RequestedById = requestedById == Guid.Empty ? null : requestedById,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            IdempotencyKey = idempotencyKey,
            ReferenceNumber = await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.AssetDisposal,
                TenantId,
                dto.DisposalDate,
                nameof(AssetDisposal))
        };

        _context.AssetDisposals.Add(disposal);
        await _context.SaveChangesAsync();

        await RecordDisposalAuditAsync(
            FinanceAuditEvents.FixedAssetDisposalRequested,
            disposal,
            afterValues: BuildDisposalAuditSnapshot(disposal),
            comment: disposal.Reason);
        if (disposal.FinalDepreciationAmount > 0m)
        {
            await RecordDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalFinalDepreciationCalculated,
                disposal,
                afterValues: new
                {
                    disposal.FinalDepreciationAmount,
                    disposal.FinalDepreciationFromDate,
                    disposal.FinalDepreciationToDate,
                    disposal.FinalDepreciationEligibleDays,
                    disposal.FinalDepreciationPeriodDays,
                    disposal.FinalDepreciationProrationBasis,
                    disposal.FinalDepreciationMethodSnapshot,
                    disposal.FinalDepreciationProductionUnits,
                    disposal.FinalDepreciationEvidenceReference
                },
                comment: "Final depreciation through the disposal date calculated for maker-checker approval.");
        }

        var workflowResult = await _workflowService.StartApprovalWorkflowAsync("AssetDisposal", disposal.Id);
        if (!workflowResult.Success)
        {
            disposal.Status = AssetDisposalStatus.Rejected;
            disposal.Comments = workflowResult.Message ?? "Unable to start asset disposal approval workflow.";
            disposal.FailedAt = DateTime.UtcNow;
            disposal.FailureReason = disposal.Comments;
            disposal.UpdatedAt = DateTime.UtcNow;
            disposal.UpdatedBy = UserName;
            await _context.SaveChangesAsync();

            await RecordDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalPostingFailed,
                disposal,
                afterValues: new { disposal.Status, disposal.FailureReason },
                reason: disposal.FailureReason,
                comment: "Fixed asset disposal workflow could not be started.");

            throw new InvalidOperationException(disposal.Comments);
        }

        disposal.WorkflowInstanceId = workflowResult.WorkflowInstanceId;
        await _context.SaveChangesAsync();

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to request disposal.");
    }

    public async Task<AssetDisposalDto> ApproveDisposalAsync(Guid disposalId, Guid approvedById, ApproveAssetDisposalDto dto)
    {
        var disposal = await LoadDisposalForMutationAsync(disposalId);

        if (disposal.Status == AssetDisposalStatus.Completed)
        {
            return MapToDto(disposal);
        }

        if (disposal.Status != AssetDisposalStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending disposals can be approved.");
        }

        if (CurrentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to resolve the current approver.");
        }

        if (!await _workflowService.CanUserApproveAsync("AssetDisposal", disposalId, CurrentUserId))
        {
            throw new InvalidOperationException("This asset disposal is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetDisposal", disposalId, CurrentUserId, "Approve", dto.Comments);
        if (!workflowResult.Success)
        {
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset disposal approval.");
        }

        if (workflowResult.Status != WorkflowInstanceStatus.Completed)
        {
            return MapToDto(disposal);
        }

        disposal.Status = AssetDisposalStatus.Approved;
        disposal.ApprovedById = approvedById == Guid.Empty ? null : approvedById;
        disposal.ApprovedAt = DateTime.UtcNow;
        disposal.Comments = dto.Comments;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        await RecordDisposalAuditAsync(
            FinanceAuditEvents.FixedAssetDisposalApproved,
            disposal,
            afterValues: new { disposal.Status, disposal.ApprovedById, disposal.ApprovedAt },
            comment: dto.Comments);

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to approve disposal.");
    }

    public async Task<AssetDisposalDto> CompleteDisposalAsync(Guid disposalId)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset disposal.");
        }

        var disposal = await LoadDisposalForMutationAsync(disposalId);

        if (disposal.Status == AssetDisposalStatus.Completed)
        {
            return MapToDto(disposal);
        }

        if (disposal.Status != AssetDisposalStatus.Approved)
        {
            throw new InvalidOperationException("Only approved disposals can be completed.");
        }

        var asset = disposal.FixedAsset;
        ValidateAssetStillDisposable(asset, disposal);
        var bookValue = ResolveBookValue(asset, disposal.AccountingBookId, disposal.BookClassification);
        var fiscalPeriod = await ResolveFiscalPeriodAsync(disposal.AccountingDate ?? disposal.DisposalDate)
            ?? throw new InvalidOperationException("No fiscal period covers the disposal accounting date.");
        var functionalCurrency = await GetFunctionalCurrencyAsync();
        if (!string.Equals(disposal.ProceedsCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            await RecordBlockedDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalBlockedInvalidTenantAccountProceeds,
                disposal,
                "Foreign-currency disposal proceeds are not supported in the Batch 21C foundation.");
            throw new InvalidOperationException("Foreign-currency disposal proceeds are not supported in the Batch 21C foundation.");
        }

        try
        {
            var finalDepreciation = await CalculateFinalDepreciationAsync(
                asset,
                bookValue,
                fiscalPeriod,
                disposal.DisposalDate,
                disposal.FinalDepreciationProductionUnits == 0m ? null : disposal.FinalDepreciationProductionUnits,
                disposal.FinalDepreciationEvidenceReference,
                disposal.FinalDepreciationEvidenceNotes,
                disposal.Id);
            ValidateApprovedFinalDepreciation(disposal, finalDepreciation, bookValue.DepreciationMethod);
            var snapshot = await BuildDisposalSnapshotAsync(asset, bookValue, disposal, finalDepreciation);
            await ValidateApprovedRevaluationSurplusTransferAsync(disposal, asset, snapshot);
            ApplySnapshot(disposal, snapshot);
            var postingRequest = await BuildDisposalPostingRequestAsync(disposal, asset, fiscalPeriod, functionalCurrency, snapshot);

            await RecordDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalConfigurationUsed,
                disposal,
                afterValues: new
                {
                    asset.FixedAssetCategoryId,
                    asset.Category.AssetAccountId,
                    asset.Category.AccumulatedDepreciationAccountId,
                    asset.Category.AccumulatedImpairmentAccountId,
                    asset.Category.GainOnDisposalAccountId,
                    asset.Category.LossOnDisposalAccountId,
                    disposal.ProceedsAccountId,
                    // These are request-time snapshots, not mutable settings resolved after the
                    // checker decision. Completion separately verifies that policy has not drifted.
                    disposal.RevaluationSurplusAccountId,
                    disposal.RetainedEarningsAccountId,
                    disposal.RevaluationSurplusTransferAmount,
                    disposal.FinalDepreciationAmount,
                    disposal.FinalDepreciationProrationBasis,
                    disposal.FinalDepreciationFromDate,
                    disposal.FinalDepreciationToDate
                },
                comment: "Fixed asset disposal account mappings used for posting.");
            await RecordDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalCalculated,
                disposal,
                afterValues: BuildDisposalAuditSnapshot(disposal),
                comment: "Fixed asset disposal derecognition calculated.");

            var beforeAsset = new
            {
                asset.Status,
                asset.DisposalDate,
                asset.AcquisitionCost,
                asset.NetBookValue,
                BookAccumulatedDepreciation = bookValue.AccumulatedDepreciation,
                BookNetBookValue = bookValue.NetBookValue
            };

            var postingResult = await _financePostingEngine.PostAsync(postingRequest);
            ApplyPostedDisposal(disposal, asset, bookValue, postingResult, snapshot);

            await _context.SaveChangesAsync();

            await RecordDisposalAuditAsync(
                disposal.DisposalType == DisposalType.Sale
                    ? FinanceAuditEvents.FixedAssetDisposalPosted
                    : FinanceAuditEvents.FixedAssetWrittenOff,
                disposal,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                beforeValues: beforeAsset,
                afterValues: new
                {
                    asset.Status,
                    asset.DisposalDate,
                    asset.AcquisitionCost,
                    asset.NetBookValue,
                    BookAccumulatedDepreciation = bookValue.AccumulatedDepreciation,
                    BookNetBookValue = bookValue.NetBookValue,
                    disposal.GainOrLoss,
                    disposal.JournalEntryId,
                    disposal.PostingEventId
                },
                comment: "Fixed asset disposal posted through the central posting engine.");

            if (disposal.FinalDepreciationAmount > 0m)
            {
                await RecordDisposalAuditAsync(
                    FinanceAuditEvents.FixedAssetDisposalFinalDepreciationPosted,
                    disposal,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        disposal.FinalDepreciationAmount,
                        disposal.FinalDepreciationScheduleId,
                        disposal.FinalDepreciationProrationBasis
                    },
                    comment: "Final depreciation posted atomically with fixed asset derecognition.");
            }

            if (disposal.DisposalType == DisposalType.Sale && disposal.NetProceeds > 0m)
            {
                await RecordDisposalAuditAsync(
                    FinanceAuditEvents.FixedAssetDisposalSaleProceedsRecorded,
                    disposal,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new { disposal.NetProceeds, disposal.ProceedsAccountId, disposal.ProceedsCurrencyCode },
                    comment: "Fixed asset sale proceeds recorded to the configured clearing account.");
            }

            if (disposal.RevaluationSurplusTransferAmount > 0m)
            {
                await RecordDisposalAuditAsync(
                    FinanceAuditEvents.FixedAssetDisposalRevaluationSurplusTransferred,
                    disposal,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        disposal.RevaluationSurplusTransferAmount,
                        disposal.RevaluationSurplusAccountId,
                        disposal.RetainedEarningsAccountId
                    },
                    comment: "Asset-specific revaluation surplus transferred directly within equity on derecognition.");
            }

            return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to complete disposal.");
        }
        catch (Exception ex)
        {
            // Evidence or account-policy drift invalidates the checker decision and must permit a
            // fresh request. Operational failures retain Approved status for idempotent retry.
            disposal.Status = ex is StaleDisposalApprovalException
                ? AssetDisposalStatus.Cancelled
                : AssetDisposalStatus.Approved;
            disposal.FailedAt = DateTime.UtcNow;
            disposal.FailureReason = ex.Message;
            disposal.UpdatedAt = DateTime.UtcNow;
            disposal.UpdatedBy = UserName;
            await _context.SaveChangesAsync();

            var eventType = ex.Message.Contains("period", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetDisposalBlockedClosedPeriod
                    : ex.Message.Contains("depreciation", StringComparison.OrdinalIgnoreCase)
                        ? FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation
                        : FinanceAuditEvents.FixedAssetDisposalPostingFailed;

            await RecordDisposalAuditAsync(
                eventType,
                disposal,
                afterValues: new { error = ex.Message },
                reason: ex.Message,
                comment: "Fixed asset disposal posting failed.");
            throw;
        }
    }

    public async Task<AssetDisposalDto> RejectDisposalAsync(Guid disposalId, Guid rejectedById, string comments)
    {
        var disposal = await LoadDisposalForMutationAsync(disposalId);

        if (disposal.Status != AssetDisposalStatus.PendingApproval)
        {
            throw new InvalidOperationException("Only pending disposals can be rejected.");
        }

        if (CurrentUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Unable to resolve the current approver.");
        }

        if (!await _workflowService.CanUserApproveAsync("AssetDisposal", disposalId, CurrentUserId))
        {
            throw new InvalidOperationException("This asset disposal is assigned to another workflow approver.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("AssetDisposal", disposalId, CurrentUserId, "Reject", comments);
        if (!workflowResult.Success)
        {
            throw new InvalidOperationException(workflowResult.Message ?? "Unable to process asset disposal rejection.");
        }

        if (workflowResult.Status is not (WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed))
        {
            return MapToDto(disposal);
        }

        disposal.Status = AssetDisposalStatus.Rejected;
        disposal.ApprovedById = rejectedById == Guid.Empty ? null : rejectedById;
        disposal.Comments = comments;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        await _context.SaveChangesAsync();
        await RecordDisposalAuditAsync(
            FinanceAuditEvents.FixedAssetDisposalRejected,
            disposal,
            afterValues: new { disposal.Status, disposal.ApprovedById },
            comment: comments);

        return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to reject disposal.");
    }

    public async Task<BulkOperationResultDto<AssetDisposalDto>> RequestBulkDisposalAsync(
        RequestBulkAssetDisposalDto dto,
        Guid requestedById)
    {
        var result = new BulkOperationResultDto<AssetDisposalDto>
        {
            TotalCount = dto.FixedAssetIds.Count
        };

        foreach (var assetId in dto.FixedAssetIds)
        {
            try
            {
                var singleDto = new RequestAssetDisposalDto
                {
                    FixedAssetId = assetId,
                    DisposalDate = dto.DisposalDate,
                    DisposalType = dto.DisposalType,
                    Reason = dto.Reason,
                    BuyerName = dto.BuyerName,
                    SaleProceeds = dto.SaleProceeds,
                    DisposalCost = dto.DisposalCost
                };

                var disposal = await RequestDisposalAsync(singleDto, requestedById);
                result.SuccessfulItems.Add(disposal);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Asset {assetId}: {ex.Message}");
                result.FailureCount++;
            }
        }

        return result;
    }

    private IQueryable<AssetDisposal> BuildDisposalQuery()
        => _context.AssetDisposals
            .Include(d => d.FixedAsset)
            .Include(d => d.RequestedBy)
            .Include(d => d.ApprovedBy)
            .Include(d => d.ProceedsAccount)
            .Include(d => d.FiscalPeriod)
            .Include(d => d.AccountingBook)
            .Include(d => d.JournalEntry)
            .Include(d => d.PostingEvent);

    private async Task<AssetDisposal> LoadDisposalForMutationAsync(Guid disposalId)
        => await _context.AssetDisposals
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.Category)
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.BookValues)
                    .ThenInclude(b => b.AccountingBook)
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.Valuations)
            .Include(d => d.FixedAsset)
                .ThenInclude(a => a.DepreciationSchedules)
            .Include(d => d.RequestedBy)
            .Include(d => d.ApprovedBy)
            .FirstOrDefaultAsync(d => d.TenantId == TenantId && d.Id == disposalId)
            ?? throw new KeyNotFoundException("Disposal request not found.");

    private async Task<FixedAsset?> LoadAssetForDisposalAsync(Guid fixedAssetId)
        => await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(b => b.AccountingBook)
            .Include(a => a.Valuations)
            .Include(a => a.DepreciationSchedules)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == fixedAssetId);

    private async Task ValidateDisposalRequestAsync(FixedAsset asset, RequestAssetDisposalDto dto)
    {
        ValidateAssetStillDisposable(asset, dto);

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new InvalidOperationException("Fixed asset disposal reason is required.");
        }

        if (dto.SaleProceeds < 0m || dto.DisposalCost < 0m)
        {
            throw new InvalidOperationException("Fixed asset disposal proceeds and disposal costs cannot be negative.");
        }

        if (dto.SaleProceeds - dto.DisposalCost < 0m)
        {
            throw new InvalidOperationException("Fixed asset disposal net proceeds cannot be negative in this foundation batch.");
        }

        if (dto.DisposalType != DisposalType.Sale && (dto.SaleProceeds != 0m || dto.DisposalCost != 0m))
        {
            throw new InvalidOperationException("Write-off, scrap, donation, and loss disposals must have zero proceeds in this foundation batch.");
        }

        var functionalCurrency = await GetFunctionalCurrencyAsync();
        var proceedsCurrency = NormalizeCurrency(dto.ProceedsCurrencyCode, functionalCurrency);
        if (!string.Equals(proceedsCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            await RecordBlockedDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetDisposalBlockedInvalidTenantAccountProceeds,
                asset.Id,
                "Foreign-currency disposal proceeds are not supported in the Batch 21C foundation.");
            throw new InvalidOperationException("Foreign-currency disposal proceeds are not supported in the Batch 21C foundation.");
        }
    }

    private static void ValidateAssetStillDisposable(FixedAsset asset, RequestAssetDisposalDto dto)
    {
        if (asset.TenantId == Guid.Empty || asset.Category == null || asset.Category.TenantId != asset.TenantId)
        {
            throw new InvalidOperationException("Fixed asset or category belongs to another tenant.");
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale)
        {
            throw new InvalidOperationException("Disposed, written-off, or held-for-sale assets cannot be disposed again.");
        }

        if (!IsCapitalized(asset))
        {
            throw new InvalidOperationException("Fixed asset must be capitalized before disposal.");
        }

        if (asset.CapitalizationDate.HasValue && dto.DisposalDate.Date < asset.CapitalizationDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset disposal date cannot be before capitalization date.");
        }

        if (asset.PlacedInServiceDate.HasValue && dto.DisposalDate.Date < asset.PlacedInServiceDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset disposal date cannot be before placed-in-service date.");
        }
    }

    private static void ValidateAssetStillDisposable(FixedAsset asset, AssetDisposal disposal)
    {
        if (asset.TenantId != disposal.TenantId || asset.Category?.TenantId != disposal.TenantId)
        {
            throw new InvalidOperationException("Fixed asset or category belongs to another tenant.");
        }

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale)
        {
            throw new InvalidOperationException("Disposed, written-off, or held-for-sale assets cannot be disposed again.");
        }

        if (!IsCapitalized(asset))
        {
            throw new InvalidOperationException("Fixed asset must be capitalized before disposal.");
        }

        if (asset.CapitalizationDate.HasValue && disposal.DisposalDate.Date < asset.CapitalizationDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset disposal date cannot be before capitalization date.");
        }

        if (asset.PlacedInServiceDate.HasValue && disposal.DisposalDate.Date < asset.PlacedInServiceDate.Value.Date)
        {
            throw new InvalidOperationException("Fixed asset disposal date cannot be before placed-in-service date.");
        }
    }

    private async Task<FinalDepreciationPreparation> CalculateFinalDepreciationAsync(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FiscalPeriod fiscalPeriod,
        DateTime disposalDate,
        decimal? productionUnits,
        string? productionEvidenceReference,
        string? productionEvidenceNotes,
        Guid? completingDisposalId = null)
    {
        // A normal full-period schedule and a prorated disposal charge cannot coexist: doing so
        // would over-depreciate a mid-period disposal. A user must reverse the normal schedule and
        // let the disposal create the single authoritative current-period schedule.
        var currentPeriodScheduleExists = await _context.AssetDepreciationSchedules.AnyAsync(schedule =>
            schedule.TenantId == TenantId &&
            schedule.FixedAssetId == asset.Id &&
            schedule.FiscalPeriodId == fiscalPeriod.Id &&
            schedule.BookClassification == bookValue.BookClassification &&
            !schedule.IsDeleted &&
            !schedule.IsReversed &&
            (!completingDisposalId.HasValue || schedule.AssetDisposalId != completingDisposalId.Value));
        if (currentPeriodScheduleExists)
        {
            throw new InvalidOperationException(
                "A current-period depreciation schedule already exists. Reverse it before disposing the asset so depreciation can be calculated through the disposal date.");
        }

        var placedInService = bookValue.PlacedInServiceDate ?? asset.PlacedInServiceDate;
        if (!placedInService.HasValue)
        {
            throw new InvalidOperationException("Fixed asset book value must have a placed-in-service date before disposal depreciation can be calculated.");
        }

        if (placedInService.Value.Date < fiscalPeriod.StartDate.Date)
        {
            var previousPeriod = await _context.FiscalPeriods
                .Where(p => p.TenantId == TenantId && !p.IsDeleted && p.EndDate < fiscalPeriod.StartDate)
                .OrderByDescending(p => p.EndDate)
                .FirstOrDefaultAsync();
            if (previousPeriod != null &&
                (!bookValue.LastDepreciationDate.HasValue ||
                 bookValue.LastDepreciationDate.Value.Date < previousPeriod.EndDate.Date))
            {
                throw new InvalidOperationException("Fixed asset depreciation must be posted through the period before disposal.");
            }
        }

        var disposalDay = disposalDate.Date;
        var startDate = new[] { fiscalPeriod.StartDate.Date, placedInService.Value.Date }
            .Max();
        if (bookValue.LastDepreciationDate.HasValue)
        {
            startDate = new[] { startDate, bookValue.LastDepreciationDate.Value.Date.AddDays(1) }.Max();
        }

        var usage = bookValue.DepreciationMethod == DepreciationMethod.UnitsOfProduction
            ? new FixedAssetProductionUsageDto
            {
                FixedAssetId = asset.Id,
                BookClassification = bookValue.BookClassification,
                UnitsConsumed = productionUnits ?? 0m,
                EvidenceReference = productionEvidenceReference ?? string.Empty,
                EvidenceNotes = productionEvidenceNotes
            }
            : null;
        if (bookValue.DepreciationMethod != DepreciationMethod.UnitsOfProduction &&
            (productionUnits.GetValueOrDefault() != 0m || !string.IsNullOrWhiteSpace(productionEvidenceReference)))
        {
            throw new InvalidOperationException("Final production usage may only be supplied for a units-of-production asset.");
        }

        var calculation = FixedAssetDepreciationCalculator.Calculate(bookValue, usage);
        var periodDays = (fiscalPeriod.EndDate.Date - fiscalPeriod.StartDate.Date).Days + 1;
        var eligibleDays = startDate > disposalDay ? 0 : (disposalDay - startDate).Days + 1;
        var basis = bookValue.DepreciationMethod == DepreciationMethod.UnitsOfProduction
            ? "ProductionUsage"
            : "ActualDaysInclusive";
        var amount = bookValue.DepreciationMethod == DepreciationMethod.UnitsOfProduction
            ? calculation.DepreciationAmount
            : RoundMoney(calculation.DepreciationAmount * eligibleDays / periodDays);
        amount = Math.Min(amount, RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue));

        return new FinalDepreciationPreparation(
            Math.Max(0m, amount),
            eligibleDays == 0 ? null : startDate,
            eligibleDays == 0 ? null : disposalDay,
            periodDays,
            eligibleDays,
            basis,
            calculation);
    }

    private static void ValidateApprovedFinalDepreciation(
        AssetDisposal disposal,
        FinalDepreciationPreparation current,
        DepreciationMethod currentMethod)
    {
        if (disposal.FinalDepreciationAmount != current.Amount ||
            disposal.FinalDepreciationFromDate?.Date != current.FromDate?.Date ||
            disposal.FinalDepreciationToDate?.Date != current.ToDate?.Date ||
            disposal.FinalDepreciationPeriodDays != current.PeriodDays ||
            disposal.FinalDepreciationEligibleDays != current.EligibleDays ||
            disposal.FinalDepreciationProrationBasis != current.ProrationBasis ||
            disposal.FinalDepreciationMethodSnapshot != currentMethod ||
            disposal.FinalDepreciationProductionUnits != current.Calculation.PeriodProductionUnits ||
            disposal.FinalDepreciationDiminishingRatePercent != current.Calculation.EffectiveDiminishingBalanceRatePercent ||
            disposal.FinalDepreciationLifetimeProductionCapacity != current.Calculation.LifetimeProductionCapacity ||
            disposal.FinalDepreciationCumulativeProductionUnitsBefore != current.Calculation.CumulativeProductionUnitsBefore ||
            disposal.FinalDepreciationCumulativeProductionUnitsAfter != current.Calculation.CumulativeProductionUnitsAfter ||
            disposal.FinalDepreciationEvidenceReference != current.Calculation.ProductionEvidenceReference)
        {
            // The request snapshot is the accounting evidence approved by the checker. If a book,
            // usage record, or date policy changed meanwhile, cancel instead of posting a different
            // charge under the old approval.
            throw new StaleDisposalApprovalException(
                "The final disposal-date depreciation calculation changed after approval. Submit a new disposal request.");
        }
    }

    private async Task<FinancePostingRequestDto> BuildDisposalPostingRequestAsync(
        AssetDisposal disposal,
        FixedAsset asset,
        FiscalPeriod fiscalPeriod,
        string functionalCurrency,
        DisposalSnapshot snapshot)
    {
        var category = asset.Category;
        var assetAccount = await ResolveDisposalAccountAsync(category.AssetAccountId, "fixed asset cost/carrying account", AccountType.Asset);
        var depreciationExpenseAccount = snapshot.FinalDepreciation.Amount > 0m
            ? await ResolveDisposalAccountAsync(category.DepreciationExpenseAccountId, "depreciation expense account", AccountType.Expense)
            : null;
        var accumulatedDepreciationAccount = snapshot.AccumulatedDepreciation > 0m
            ? await ResolveDisposalAccountAsync(category.AccumulatedDepreciationAccountId, "accumulated depreciation account", AccountType.Asset)
            : null;
        var accumulatedImpairmentAccount = snapshot.AccumulatedImpairment > 0m
            ? await ResolveDisposalAccountAsync(category.AccumulatedImpairmentAccountId, "accumulated impairment account", AccountType.Asset)
            : null;
        var proceedsAccount = snapshot.NetProceeds > 0m
            ? await ResolveDisposalAccountAsync(snapshot.ProceedsAccountId, "disposal proceeds clearing account", AccountType.Asset)
            : null;
        var lossAccount = snapshot.GainOrLoss < 0m
            ? await ResolveDisposalAccountAsync(category.LossOnDisposalAccountId, "loss on disposal account", AccountType.Expense)
            : null;
        var gainAccount = snapshot.GainOrLoss > 0m
            ? await ResolveDisposalAccountAsync(category.GainOnDisposalAccountId, "gain on disposal account", AccountType.Expense)
            : null;
        var revaluationSurplusAccount = snapshot.RevaluationSurplusBalance > 0m
            ? await ResolveDisposalAccountAsync(disposal.RevaluationSurplusAccountId, "revaluation surplus disposal-transfer account", AccountType.Equity)
            : null;
        var retainedEarningsAccount = snapshot.RevaluationSurplusBalance > 0m
            ? await ResolveDisposalAccountAsync(disposal.RetainedEarningsAccountId, "retained earnings disposal-transfer account", AccountType.Equity)
            : null;

        var reference = BuildDisposalReference(asset, disposal);
        var lineNumber = 1;
        var lines = new List<FinancePostingLineDto>();

        // The final charge and derecognition share one idempotent posting event. This preserves the
        // existing disposal maker-checker decision and prevents an asset from becoming disposed
        // while its final depreciation journal fails (or vice versa).
        AddPostingLine(lines, depreciationExpenseAccount?.Id, snapshot.FinalDepreciation.Amount, 0m, "Final depreciation through disposal date", reference, lineNumber++, "FA-Depreciation", disposal, asset, functionalCurrency);
        AddPostingLine(lines, accumulatedDepreciationAccount?.Id, 0m, snapshot.FinalDepreciation.Amount, "Final accumulated depreciation through disposal date", reference, lineNumber++, "FA-AccumulatedDepreciation", disposal, asset, functionalCurrency);
        AddPostingLine(lines, accumulatedDepreciationAccount?.Id, snapshot.AccumulatedDepreciation, 0m, "Clear accumulated depreciation", reference, lineNumber++, "FA-DisposalAccumulatedDepreciation", disposal, asset, functionalCurrency);
        AddPostingLine(lines, accumulatedImpairmentAccount?.Id, snapshot.AccumulatedImpairment, 0m, "Clear accumulated impairment", reference, lineNumber++, "FA-DisposalAccumulatedImpairment", disposal, asset, functionalCurrency);
        AddPostingLine(lines, proceedsAccount?.Id, snapshot.NetProceeds, 0m, "Record disposal proceeds clearing", reference, lineNumber++, "FA-DisposalProceeds", disposal, asset, functionalCurrency);
        AddPostingLine(lines, lossAccount?.Id, Math.Abs(Math.Min(snapshot.GainOrLoss, 0m)), 0m, "Loss on disposal", reference, lineNumber++, "FA-DisposalLoss", disposal, asset, functionalCurrency);
        AddPostingLine(lines, assetAccount.Id, 0m, snapshot.AssetCarryingAccountAmount, "Derecognize fixed asset carrying account", reference, lineNumber++, "FA-DisposalAsset", disposal, asset, functionalCurrency);
        AddPostingLine(lines, gainAccount?.Id, 0m, Math.Max(snapshot.GainOrLoss, 0m), "Gain on disposal", reference, lineNumber++, "FA-DisposalGain", disposal, asset, functionalCurrency);
        // IAS 16 equity transfer: this does not alter the disposal gain/loss. The remaining
        // asset-specific reserve is debited and retained earnings is credited in the same journal.
        AddPostingLine(lines, revaluationSurplusAccount?.Id, snapshot.RevaluationSurplusBalance, 0m, "Transfer revaluation surplus on derecognition", reference, lineNumber++, "FA-DisposalRevaluationSurplus", disposal, asset, functionalCurrency);
        AddPostingLine(lines, retainedEarningsAccount?.Id, 0m, snapshot.RevaluationSurplusBalance, "Transfer revaluation surplus to retained earnings", reference, lineNumber++, "FA-DisposalRetainedEarnings", disposal, asset, functionalCurrency);

        if (lines.Count < 2)
        {
            throw new InvalidOperationException("Fixed asset disposal posting requires at least two balanced posting lines.");
        }

        return new FinancePostingRequestDto
        {
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = disposal.Id,
            SourceDocumentTenantId = disposal.TenantId,
            PostingAction = PostingAction,
            SourceDocumentReference = reference,
            Description = $"Fixed asset disposal - {asset.AssetCode} - {asset.Name}",
            PostingDate = (disposal.AccountingDate ?? disposal.DisposalDate).Date,
            FiscalPeriodId = fiscalPeriod.Id,
            JournalType = "Fixed Asset Disposal",
            BookClassification = disposal.BookClassification,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = BuildPostingIdempotencyKey(disposal),
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private static void AddPostingLine(
        ICollection<FinancePostingLineDto> lines,
        Guid? accountId,
        decimal debit,
        decimal credit,
        string description,
        string reference,
        int lineNumber,
        string tag,
        AssetDisposal disposal,
        FixedAsset asset,
        string functionalCurrency)
    {
        debit = RoundMoney(debit);
        credit = RoundMoney(credit);
        if (accountId == null || (debit == 0m && credit == 0m))
        {
            return;
        }

        lines.Add(new FinancePostingLineDto
        {
            AccountId = accountId.Value,
            DebitAmount = debit,
            CreditAmount = credit,
            TransactionCurrency = functionalCurrency,
            TransactionDebitAmount = debit,
            TransactionCreditAmount = credit,
            Description = $"{description} - {asset.AssetCode}",
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            SegmentString = asset.CurrentSegmentString,
            Notes = $"FixedAssetId={asset.Id:N};AssetDisposalId={disposal.Id:N};" +
                (disposal.FinalDepreciationScheduleId.HasValue ? $"ScheduleId={disposal.FinalDepreciationScheduleId.Value:N};" : string.Empty) +
                $"Book={disposal.BookClassification}",
            TransactionTag = tag
        });
    }

    private void ApplyPostedDisposal(
        AssetDisposal disposal,
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FinancePostingResultDto postingResult,
        DisposalSnapshot snapshot)
    {
        if (snapshot.FinalDepreciation.Amount > 0m)
        {
            AddPostedFinalDepreciationSchedule(disposal, asset, bookValue, postingResult, snapshot.FinalDepreciation);
        }

        disposal.Status = AssetDisposalStatus.Completed;
        disposal.PostedAt = DateTime.UtcNow;
        disposal.CompletedAt = DateTime.UtcNow;
        disposal.JournalEntryId = postingResult.JournalEntryId;
        disposal.PostingEventId = postingResult.PostingEventId;
        disposal.FailedAt = null;
        disposal.FailureReason = null;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;

        asset.Status = disposal.DisposalType == DisposalType.Sale
            ? FixedAssetStatus.Disposed
            : FixedAssetStatus.WrittenOff;
        asset.DisposalDate = disposal.DisposalDate;
        asset.NetBookValue = 0m;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        bookValue.AccumulatedDepreciation = 0m;
        bookValue.NetBookValue = 0m;
        bookValue.UpdatedAt = DateTime.UtcNow;
        bookValue.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = disposal.FixedAssetId,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            TransactionDate = disposal.DisposalDate,
            TransactionType = disposal.DisposalType == DisposalType.Sale ? "Disposal" : "WriteOff",
            Description = $"Fixed asset {disposal.DisposalType} disposal. Proceeds: {snapshot.NetProceeds:N2}, Gain/Loss: {snapshot.GainOrLoss:N2}",
            Amount = snapshot.NetProceeds,
            ResultingBookValue = 0m,
            RelatedEntityId = disposal.Id,
            PerformedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        });
    }

    private void AddPostedFinalDepreciationSchedule(
        AssetDisposal disposal,
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FinancePostingResultDto postingResult,
        FinalDepreciationPreparation finalDepreciation)
    {
        if (!disposal.FinalDepreciationScheduleId.HasValue)
        {
            throw new InvalidOperationException("Final disposal depreciation is missing its immutable schedule identifier.");
        }

        var accumulatedBefore = RoundMoney(bookValue.AccumulatedDepreciation);
        var netBookValueBefore = RoundMoney(bookValue.NetBookValue);
        var schedule = new AssetDepreciationSchedule
        {
            Id = disposal.FinalDepreciationScheduleId.Value,
            TenantId = disposal.TenantId,
            FixedAssetId = asset.Id,
            AssetDisposalId = disposal.Id,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            FiscalPeriodId = disposal.FiscalPeriodId
                ?? throw new InvalidOperationException("Final disposal depreciation is missing its fiscal period."),
            DepreciationAmount = finalDepreciation.Amount,
            AccumulatedDepreciationBefore = accumulatedBefore,
            AccumulatedDepreciation = RoundMoney(accumulatedBefore + finalDepreciation.Amount),
            NetBookValueBefore = netBookValueBefore,
            NetBookValue = RoundMoney(netBookValueBefore - finalDepreciation.Amount),
            DepreciableAmount = RoundMoney(netBookValueBefore - bookValue.ResidualValue),
            ResidualValueSnapshot = bookValue.ResidualValue,
            UsefulLifeMonthsSnapshot = bookValue.UsefulLifeMonths,
            DepreciationMethodSnapshot = bookValue.DepreciationMethod,
            DiminishingBalanceRatePercentSnapshot = finalDepreciation.Calculation.EffectiveDiminishingBalanceRatePercent,
            LifetimeProductionCapacitySnapshot = finalDepreciation.Calculation.LifetimeProductionCapacity,
            PeriodProductionUnits = finalDepreciation.Calculation.PeriodProductionUnits,
            CumulativeProductionUnitsBefore = finalDepreciation.Calculation.CumulativeProductionUnitsBefore,
            CumulativeProductionUnitsAfter = finalDepreciation.Calculation.CumulativeProductionUnitsAfter,
            ProductionEvidenceReference = finalDepreciation.Calculation.ProductionEvidenceReference,
            ProductionEvidenceNotes = finalDepreciation.Calculation.ProductionEvidenceNotes,
            PlacedInServiceDateSnapshot = bookValue.PlacedInServiceDate ?? asset.PlacedInServiceDate,
            ApprovalStatus = "ApprovedWithDisposal",
            ApprovedAt = disposal.ApprovedAt,
            ApprovedById = disposal.ApprovedById,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            PostingDate = disposal.AccountingDate ?? disposal.DisposalDate,
            JournalEntryId = postingResult.JournalEntryId,
            PostingEventId = postingResult.PostingEventId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };

        _context.AssetDepreciationSchedules.Add(schedule);

        // Although derecognition immediately zeroes the book, these counters remain meaningful
        // historical evidence and make the final schedule consistent with ordinary depreciation.
        bookValue.AccumulatedProductionUnits = finalDepreciation.Calculation.CumulativeProductionUnitsAfter;
        bookValue.LastDepreciationDate = disposal.DisposalDate.Date;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = disposal.TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            TransactionDate = disposal.DisposalDate,
            TransactionType = "Depreciation",
            Description = $"Final depreciation through disposal date ({finalDepreciation.ProrationBasis})",
            Amount = finalDepreciation.Amount,
            ResultingBookValue = schedule.NetBookValue,
            RelatedEntityId = disposal.Id,
            PerformedByUserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        });
    }

    private static void ApplySnapshot(AssetDisposal disposal, DisposalSnapshot snapshot)
    {
        disposal.NetProceeds = snapshot.NetProceeds;
        disposal.ProceedsCurrencyCode = snapshot.ProceedsCurrencyCode;
        disposal.ProceedsAccountId = snapshot.ProceedsAccountId;
        disposal.CostAtDisposal = snapshot.AssetCarryingAccountAmount;
        disposal.AccumulatedDepreciationAtDisposal = snapshot.AccumulatedDepreciation;
        disposal.FinalDepreciationAmount = snapshot.FinalDepreciation.Amount;
        disposal.AccumulatedImpairmentAtDisposal = snapshot.AccumulatedImpairment;
        disposal.RevaluationSurplusAtDisposal = snapshot.RevaluationSurplusBalance;
        disposal.RevaluationSurplusTransferAmount = snapshot.RevaluationSurplusBalance;
        disposal.NetBookValueAtDisposal = snapshot.NetBookValue;
        disposal.GainOrLoss = snapshot.GainOrLoss;
    }

    private async Task<DisposalSnapshot> BuildDisposalSnapshotAsync(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        RequestAssetDisposalDto dto,
        FinalDepreciationPreparation finalDepreciation)
    {
        var functionalCurrency = await GetFunctionalCurrencyAsync();
        var proceedsCurrency = NormalizeCurrency(dto.ProceedsCurrencyCode, functionalCurrency);
        var netProceeds = RoundMoney(dto.SaleProceeds - dto.DisposalCost);
        var accumulatedImpairment = await CalculateAccumulatedImpairmentAsync(asset.Id, bookValue);
        var revaluationAssetAdjustment = await CalculateRevaluationAssetAdjustmentAsync(asset.Id, bookValue);
        var revaluationSurplusBalance = await CalculateRevaluationSurplusBalanceAsync(asset.Id, bookValue);
        var assetCarryingAccountAmount = RoundMoney(bookValue.AcquisitionCost + revaluationAssetAdjustment);
        var netBookValue = RoundMoney(bookValue.NetBookValue - finalDepreciation.Amount);
        var proceedsAccountId = netProceeds > 0m
            ? dto.ProceedsAccountId ?? asset.Category.DisposalProceedsClearingAccountId
            : null;

        return new DisposalSnapshot(
            assetCarryingAccountAmount,
            RoundMoney(bookValue.AccumulatedDepreciation + finalDepreciation.Amount),
            accumulatedImpairment,
            revaluationSurplusBalance,
            netBookValue,
            netProceeds,
            RoundMoney(netProceeds - netBookValue),
            proceedsCurrency,
            proceedsAccountId,
            finalDepreciation);
    }

    private async Task<DisposalSnapshot> BuildDisposalSnapshotAsync(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        AssetDisposal disposal,
        FinalDepreciationPreparation finalDepreciation)
    {
        var accumulatedImpairment = await CalculateAccumulatedImpairmentAsync(asset.Id, bookValue);
        var revaluationAssetAdjustment = await CalculateRevaluationAssetAdjustmentAsync(asset.Id, bookValue);
        var revaluationSurplusBalance = await CalculateRevaluationSurplusBalanceAsync(asset.Id, bookValue);
        var assetCarryingAccountAmount = RoundMoney(bookValue.AcquisitionCost + revaluationAssetAdjustment);
        var netBookValue = RoundMoney(bookValue.NetBookValue - finalDepreciation.Amount);
        var netProceeds = RoundMoney(disposal.SaleProceeds - disposal.DisposalCost);
        var proceedsAccountId = netProceeds > 0m
            ? disposal.ProceedsAccountId ?? asset.Category.DisposalProceedsClearingAccountId
            : null;

        return new DisposalSnapshot(
            assetCarryingAccountAmount,
            RoundMoney(bookValue.AccumulatedDepreciation + finalDepreciation.Amount),
            accumulatedImpairment,
            revaluationSurplusBalance,
            netBookValue,
            netProceeds,
            RoundMoney(netProceeds - netBookValue),
            NormalizeCurrency(disposal.ProceedsCurrencyCode, await GetFunctionalCurrencyAsync()),
            proceedsAccountId,
            finalDepreciation);
    }

    private async Task<decimal> CalculateAccumulatedImpairmentAsync(Guid assetId, FixedAssetBookValue bookValue)
    {
        var amount = await _context.AssetValuations
            .Where(v => v.TenantId == TenantId
                && v.FixedAssetId == assetId
                && !v.IsDeleted
                && v.IsPostedToGL
                && v.BookClassification == bookValue.BookClassification)
            .SumAsync(v => v.ImpairmentLoss - v.ImpairmentReversal);

        return Math.Max(0m, RoundMoney(amount));
    }

    private async Task<decimal> CalculateRevaluationAssetAdjustmentAsync(Guid assetId, FixedAssetBookValue bookValue)
    {
        var amount = await _context.AssetValuations
            .Where(v => v.TenantId == TenantId
                && v.FixedAssetId == assetId
                && !v.IsDeleted
                && v.IsPostedToGL
                && v.ValuationType == ValuationType.Revaluation
                && v.BookClassification == bookValue.BookClassification)
            .SumAsync(v => v.RevaluationSurplus - v.RevaluationDeficit);

        return RoundMoney(amount);
    }

    private async Task<decimal> CalculateRevaluationSurplusBalanceAsync(Guid assetId, FixedAssetBookValue bookValue)
    {
        var amount = await _context.AssetValuations
            .Where(v => v.TenantId == TenantId
                && v.FixedAssetId == assetId
                && !v.IsDeleted
                && v.IsPostedToGL
                && v.ValuationType == ValuationType.Revaluation
                && v.BookClassification == bookValue.BookClassification)
            .SumAsync(v => v.RevaluationSurplus - v.RevaluationSurplusApplied);

        return Math.Max(0m, RoundMoney(amount));
    }

    private FixedAssetBookValue ResolveDefaultBookValue(FixedAsset asset)
    {
        var bookValue = asset.BookValues
            .OrderByDescending(b => b.AccountingBook != null && b.AccountingBook.IsDefault)
            .ThenByDescending(b => b.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return bookValue ?? throw new InvalidOperationException("Fixed asset disposal requires a book-value record.");
    }

    private static FixedAssetBookValue ResolveBookValue(FixedAsset asset, Guid? accountingBookId, string bookClassification)
    {
        var normalizedBook = NormalizeBookClassification(bookClassification);
        var bookValue = asset.BookValues.FirstOrDefault(b =>
            (!accountingBookId.HasValue || b.AccountingBookId == accountingBookId.Value) &&
            b.BookClassification.Equals(normalizedBook, StringComparison.OrdinalIgnoreCase));

        return bookValue ?? throw new InvalidOperationException("Fixed asset disposal book value was not found.");
    }

    private async Task<RevaluationSurplusTransferPreparation> ResolveRevaluationSurplusTransferAsync(
        FixedAsset asset,
        decimal surplusBalance)
    {
        if (RoundMoney(surplusBalance) <= 0m)
        {
            return new RevaluationSurplusTransferPreparation(null, null);
        }

        // TDC default: transfer the entire remaining asset-specific reserve on derecognition.
        // FinanceSettings already owns the tenant retained-earnings account, so this extends the
        // established configuration instead of introducing a second fixed-asset policy register.
        var retainedEarningsAccountId = await _context.FinanceSettings
            .Where(settings => settings.TenantId == TenantId && !settings.IsDeleted)
            .Select(settings => settings.RetainedEarningsAccountId)
            .FirstOrDefaultAsync();
        var surplusAccount = await ResolveDisposalAccountAsync(
            asset.Category.RevaluationSurplusAccountId,
            "revaluation surplus disposal-transfer account",
            AccountType.Equity);
        var retainedEarningsAccount = await ResolveDisposalAccountAsync(
            retainedEarningsAccountId,
            "retained earnings disposal-transfer account",
            AccountType.Equity);

        if (surplusAccount.Id == retainedEarningsAccount.Id)
        {
            throw new InvalidOperationException(
                "Revaluation surplus and retained earnings must use different equity accounts for disposal transfer.");
        }

        return new RevaluationSurplusTransferPreparation(surplusAccount.Id, retainedEarningsAccount.Id);
    }

    private async Task ValidateApprovedRevaluationSurplusTransferAsync(
        AssetDisposal disposal,
        FixedAsset asset,
        DisposalSnapshot currentSnapshot)
    {
        if (RoundMoney(disposal.RevaluationSurplusAtDisposal) != currentSnapshot.RevaluationSurplusBalance)
        {
            throw new StaleDisposalApprovalException(
                "The asset-specific revaluation surplus changed after approval. Submit a new disposal request.");
        }

        var currentPolicy = await ResolveRevaluationSurplusTransferAsync(asset, currentSnapshot.RevaluationSurplusBalance);
        if (disposal.RevaluationSurplusAccountId != currentPolicy.RevaluationSurplusAccountId ||
            disposal.RetainedEarningsAccountId != currentPolicy.RetainedEarningsAccountId)
        {
            // Category and Finance settings are mutable. Posting new account mappings under an old
            // approval would defeat maker-checker, so retire the stale request rather than rebuild it.
            throw new StaleDisposalApprovalException(
                "The revaluation-surplus disposal policy accounts changed after approval. Submit a new disposal request.");
        }
    }

    private async Task<Account> ResolveDisposalAccountAsync(Guid? accountId, string label, params AccountType[] allowedTypes)
    {
        if (!accountId.HasValue || accountId.Value == Guid.Empty)
        {
            throw new InvalidOperationException($"Fixed asset {label} is required.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == accountId.Value && !a.IsDeleted)
            ?? throw new InvalidOperationException($"Fixed asset {label} was not found for this tenant.");

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Fixed asset {label} must be active.");
        }

        if (!account.AllowDirectPosting)
        {
            throw new InvalidOperationException($"Fixed asset {label} must allow direct posting.");
        }

        if (allowedTypes.Length > 0 && !allowedTypes.Contains(account.AccountType))
        {
            throw new InvalidOperationException($"Fixed asset {label} has an invalid account type.");
        }

        return account;
    }

    private async Task<FiscalPeriod?> ResolveFiscalPeriodAsync(DateTime accountingDate)
    {
        var date = accountingDate.Date;
        return await _context.FiscalPeriods
            .FirstOrDefaultAsync(p => p.TenantId == TenantId && !p.IsDeleted && p.StartDate <= date && p.EndDate >= date);
    }

    private async Task<string> GetFunctionalCurrencyAsync()
    {
        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(settings?.BaseCurrency))
        {
            return settings.BaseCurrency.Trim().ToUpperInvariant();
        }

        var tenantCurrency = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == TenantId && !t.IsDeleted)
            .Select(t => t.BaseCurrency)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(tenantCurrency)
            ? "GHS"
            : tenantCurrency.Trim().ToUpperInvariant();
    }

    private async Task RecordBlockedDisposalAuditAsync(string eventType, Guid assetId, string reason)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = assetId,
            Resource = "Finance.FixedAssetDisposal",
            ResourceId = assetId.ToString(),
            Reason = reason,
            Context = new { assetId, reason }
        });
    }

    private Task RecordBlockedDisposalAuditAsync(string eventType, AssetDisposal disposal, string reason)
        => RecordDisposalAuditAsync(
            eventType,
            disposal,
            afterValues: new { disposal.FixedAssetId, disposal.Status, reason },
            reason: reason,
            comment: reason);

    private async Task RecordDisposalAuditAsync(
        string eventType,
        AssetDisposal disposal,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = disposal.TenantId,
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = disposal.Id,
            JournalEntryId = journalEntryId ?? disposal.JournalEntryId,
            PostingEventId = postingEventId ?? disposal.PostingEventId,
            WorkflowInstanceId = disposal.WorkflowInstanceId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = "Finance.FixedAssetDisposal",
            ResourceId = disposal.Id.ToString(),
            Context = new
            {
                disposal.FixedAssetId,
                disposal.DisposalType,
                disposal.Status,
                disposal.ReferenceNumber
            }
        });
    }

    private static object BuildDisposalAuditSnapshot(AssetDisposal disposal)
        => new
        {
            disposal.FixedAssetId,
            disposal.DisposalType,
            disposal.DisposalDate,
            disposal.AccountingDate,
            disposal.FiscalPeriodId,
            disposal.BookClassification,
            disposal.Status,
            disposal.SaleProceeds,
            disposal.DisposalCost,
            disposal.NetProceeds,
            disposal.ProceedsAccountId,
            disposal.CostAtDisposal,
            disposal.AccumulatedDepreciationAtDisposal,
            disposal.FinalDepreciationAmount,
            disposal.FinalDepreciationFromDate,
            disposal.FinalDepreciationToDate,
            disposal.FinalDepreciationPeriodDays,
            disposal.FinalDepreciationEligibleDays,
            disposal.FinalDepreciationProrationBasis,
            disposal.FinalDepreciationMethodSnapshot,
            disposal.FinalDepreciationScheduleId,
            disposal.FinalDepreciationProductionUnits,
            disposal.FinalDepreciationDiminishingRatePercent,
            disposal.FinalDepreciationLifetimeProductionCapacity,
            disposal.FinalDepreciationCumulativeProductionUnitsBefore,
            disposal.FinalDepreciationCumulativeProductionUnitsAfter,
            disposal.FinalDepreciationEvidenceReference,
            disposal.AccumulatedImpairmentAtDisposal,
            disposal.RevaluationSurplusAtDisposal,
            disposal.RevaluationSurplusAccountId,
            disposal.RetainedEarningsAccountId,
            disposal.RevaluationSurplusTransferAmount,
            disposal.NetBookValueAtDisposal,
            disposal.GainOrLoss,
            disposal.JournalEntryId,
            disposal.PostingEventId
        };

    private static bool IsCapitalized(FixedAsset asset)
        => asset.CapitalizedAt.HasValue &&
           asset.PostingEventId.HasValue &&
           asset.Status is FixedAssetStatus.Active or FixedAssetStatus.Capitalized or FixedAssetStatus.FullyDepreciated;

    private static string NormalizeIdempotencyKey(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeBookClassification(string? value)
        => string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string? value, string defaultValue)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new InvalidOperationException("Disposal proceeds currency must be a three-character ISO currency code.");
        }

        return normalized;
    }

    private static string BuildPostingIdempotencyKey(AssetDisposal disposal)
        => string.IsNullOrWhiteSpace(disposal.IdempotencyKey)
            ? $"FA:Disposal:{disposal.TenantId:N}:{disposal.Id:N}:{PostingAction}"
            : $"FA:Disposal:{disposal.TenantId:N}:{disposal.IdempotencyKey}:{PostingAction}";

    private static string BuildDisposalReference(FixedAsset asset, AssetDisposal disposal)
        => !string.IsNullOrWhiteSpace(disposal.ReferenceNumber)
            ? disposal.ReferenceNumber!
            : $"DSP-{asset.AssetCode}-{disposal.DisposalDate:yyyyMMdd}";

    private static decimal RoundMoney(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static AssetDisposalDto MapToDto(AssetDisposal d)
    {
        return new AssetDisposalDto
        {
            Id = d.Id,
            FixedAssetId = d.FixedAssetId,
            FixedAssetName = d.FixedAsset?.Name,
            AssetCode = d.FixedAsset?.AssetCode,
            DisposalDate = d.DisposalDate,
            AccountingDate = d.AccountingDate,
            FiscalPeriodId = d.FiscalPeriodId,
            AccountingBookId = d.AccountingBookId,
            BookClassification = d.BookClassification,
            DisposalType = d.DisposalType,
            Status = d.Status,
            Reason = d.Reason,
            SaleProceeds = d.SaleProceeds,
            DisposalCost = d.DisposalCost,
            NetProceeds = d.NetProceeds,
            ProceedsCurrencyCode = d.ProceedsCurrencyCode,
            ProceedsAccountId = d.ProceedsAccountId,
            CostAtDisposal = d.CostAtDisposal,
            AccumulatedDepreciationAtDisposal = d.AccumulatedDepreciationAtDisposal,
            FinalDepreciationAmount = d.FinalDepreciationAmount,
            FinalDepreciationFromDate = d.FinalDepreciationFromDate,
            FinalDepreciationToDate = d.FinalDepreciationToDate,
            FinalDepreciationPeriodDays = d.FinalDepreciationPeriodDays,
            FinalDepreciationEligibleDays = d.FinalDepreciationEligibleDays,
            FinalDepreciationProrationBasis = d.FinalDepreciationProrationBasis,
            FinalDepreciationMethodSnapshot = d.FinalDepreciationMethodSnapshot,
            FinalDepreciationScheduleId = d.FinalDepreciationScheduleId,
            FinalDepreciationProductionUnits = d.FinalDepreciationProductionUnits,
            FinalDepreciationDiminishingRatePercent = d.FinalDepreciationDiminishingRatePercent,
            FinalDepreciationLifetimeProductionCapacity = d.FinalDepreciationLifetimeProductionCapacity,
            FinalDepreciationCumulativeProductionUnitsBefore = d.FinalDepreciationCumulativeProductionUnitsBefore,
            FinalDepreciationCumulativeProductionUnitsAfter = d.FinalDepreciationCumulativeProductionUnitsAfter,
            FinalDepreciationEvidenceReference = d.FinalDepreciationEvidenceReference,
            FinalDepreciationEvidenceNotes = d.FinalDepreciationEvidenceNotes,
            AccumulatedImpairmentAtDisposal = d.AccumulatedImpairmentAtDisposal,
            RevaluationSurplusAtDisposal = d.RevaluationSurplusAtDisposal,
            RevaluationSurplusAccountId = d.RevaluationSurplusAccountId,
            RetainedEarningsAccountId = d.RetainedEarningsAccountId,
            RevaluationSurplusTransferAmount = d.RevaluationSurplusTransferAmount,
            NetBookValueAtDisposal = d.NetBookValueAtDisposal,
            GainOrLoss = d.GainOrLoss,
            BuyerName = d.BuyerName,
            ReferenceNumber = d.ReferenceNumber,
            RequestedById = d.RequestedById,
            RequestedByName = d.RequestedBy != null ? $"{d.RequestedBy.FirstName} {d.RequestedBy.LastName}" : null,
            ApprovedById = d.ApprovedById,
            ApprovedByName = d.ApprovedBy != null ? $"{d.ApprovedBy.FirstName} {d.ApprovedBy.LastName}" : null,
            ApprovedAt = d.ApprovedAt,
            CompletedAt = d.CompletedAt,
            PostedAt = d.PostedAt,
            FailedAt = d.FailedAt,
            Comments = d.Comments,
            FailureReason = d.FailureReason,
            JournalEntryId = d.JournalEntryId,
            PostingEventId = d.PostingEventId,
            WorkflowInstanceId = d.WorkflowInstanceId,
            IdempotencyKey = d.IdempotencyKey,
            CreatedAt = d.CreatedAt
        };
    }

    private sealed record DisposalSnapshot(
        decimal AssetCarryingAccountAmount,
        decimal AccumulatedDepreciation,
        decimal AccumulatedImpairment,
        decimal RevaluationSurplusBalance,
        decimal NetBookValue,
        decimal NetProceeds,
        decimal GainOrLoss,
        string ProceedsCurrencyCode,
        Guid? ProceedsAccountId,
        FinalDepreciationPreparation FinalDepreciation);

    private sealed record FinalDepreciationPreparation(
        decimal Amount,
        DateTime? FromDate,
        DateTime? ToDate,
        int PeriodDays,
        int EligibleDays,
        string ProrationBasis,
        DepreciationCalculation Calculation);

    private sealed record RevaluationSurplusTransferPreparation(
        Guid? RevaluationSurplusAccountId,
        Guid? RetainedEarningsAccountId);

    /// <summary>
    /// Identifies a checker decision whose reserve balance or account policy no longer matches
    /// current authoritative data. Unlike a transient posting failure, it must not be retried.
    /// </summary>
    private sealed class StaleDisposalApprovalException(string message) : InvalidOperationException(message);
}
