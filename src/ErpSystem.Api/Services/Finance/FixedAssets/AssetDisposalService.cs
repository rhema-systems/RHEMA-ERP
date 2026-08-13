using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.AR;
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
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

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
    private readonly IInvoiceService? _invoiceService;
    private readonly IPaymentService? _paymentService;

    public AssetDisposalService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberingService documentNumberingService,
        IWorkflowService workflowService,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IInvoiceService? invoiceService = null,
        IPaymentService? paymentService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _documentNumberingService = documentNumberingService;
        _workflowService = workflowService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _invoiceService = invoiceService;
        _paymentService = paymentService;
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
                dto.DisposalScope,
                dto.DisposedPortionPercent,
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
                // Completed partial disposals are history, not an active lock on the remaining
                // asset. Only an unfinished maker-checker request blocks another disposal.
                && (d.Status == AssetDisposalStatus.Draft
                    || d.Status == AssetDisposalStatus.PendingApproval
                    || d.Status == AssetDisposalStatus.Approved)
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
            DisposalScope = dto.DisposalScope,
            DisposedPortionPercent = snapshot.DisposedPortionPercent,
            ComponentReference = NormalizeOptionalText(dto.ComponentReference),
            ComponentDescription = NormalizeOptionalText(dto.ComponentDescription),
            AllocationEvidenceReference = NormalizeOptionalText(dto.AllocationEvidenceReference),
            AllocationEvidenceNotes = NormalizeOptionalText(dto.AllocationEvidenceNotes),
            Reason = dto.Reason?.Trim(),
            SaleProceeds = RoundMoney(dto.SaleProceeds),
            DisposalCost = RoundMoney(dto.DisposalCost),
            NetProceeds = snapshot.NetProceeds,
            ProceedsCurrencyCode = snapshot.ProceedsCurrencyCode,
            ProceedsFunctionalAmount = snapshot.ProceedsFunctionalAmount,
            ProceedsExchangeRateId = snapshot.ProceedsExchangeRate.ExchangeRateId,
            ProceedsExchangeRateValue = snapshot.ProceedsExchangeRate.Rate,
            ProceedsExchangeRateSource = snapshot.ProceedsExchangeRate.Source,
            ProceedsExchangeRateDate = snapshot.ProceedsExchangeRate.EffectiveDate,
            ProceedsExchangeRateType = snapshot.ProceedsExchangeRate.RateType,
            ProceedsExchangeRateQuoteSide = snapshot.ProceedsExchangeRate.QuoteSide,
            ProceedsAccountId = snapshot.ProceedsAccountId,
            CostAtDisposal = snapshot.AssetCarryingAccountAmount,
            AcquisitionCostAllocated = snapshot.AcquisitionCostAllocated,
            RevaluationAdjustmentAllocated = snapshot.RevaluationAdjustmentAllocated,
            ResidualValueAllocated = snapshot.ResidualValueAllocated,
            ProductionCapacityAllocated = snapshot.ProductionCapacityAllocated,
            AccumulatedProductionUnitsAllocated = snapshot.AccumulatedProductionUnitsAllocated,
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
            RemainingAcquisitionCostAfterDisposal = snapshot.RemainingAcquisitionCost,
            RemainingAccumulatedDepreciationAfterDisposal = snapshot.RemainingAccumulatedDepreciation,
            RemainingNetBookValueAfterDisposal = snapshot.RemainingNetBookValue,
            BuyerName = dto.BuyerName?.Trim(),
            BuyerBusinessPartnerId = dto.BuyerBusinessPartnerId,
            SettlementMode = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m
                ? dto.SettlementMode
                : AssetDisposalSettlementMode.NotApplicable,
            SettlementStatus = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m
                ? AssetDisposalSettlementStatus.Pending
                : AssetDisposalSettlementStatus.NotApplicable,
            // Normalize non-sale requests at the entity boundary as well as validating the DTO.
            // This prevents harmless API defaults (for example Standard tax treatment) from
            // becoming misleading dormant settlement evidence on write-offs or donations.
            SaleTaxGroupId = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m ? dto.SaleTaxGroupId : null,
            SaleTaxTreatment = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m
                ? dto.SaleTaxTreatment
                : TaxTreatment.OutOfScope,
            SettlementPaymentTermId = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m ? dto.SettlementPaymentTermId : null,
            SettlementPaymentMethodId = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m ? dto.SettlementPaymentMethodId : null,
            SettlementBankAccountId = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m ? dto.SettlementBankAccountId : null,
            SettlementLiquidityAccountId = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m ? dto.SettlementLiquidityAccountId : null,
            SettlementReference = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m
                ? NormalizeOptionalText(dto.SettlementReference)
                : null,
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
        if (disposal.DisposalScope != AssetDisposalScope.WholeAsset)
        {
            await RecordDisposalAuditAsync(
                FinanceAuditEvents.FixedAssetPartialDisposalAllocationCalculated,
                disposal,
                afterValues: BuildDisposalAuditSnapshot(disposal),
                comment: "Partial/component carrying-value allocation calculated for maker-checker approval.");
        }
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
        AssetDisposal disposal = await LoadDisposalForMutationAsync(disposalId);

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
        // SQL Server is configured with retry-on-failure, so EF requires the execution strategy
        // to own every user transaction. Keeping failure evidence outside the retry delegate also
        // prevents a transient first attempt from leaving a misleading permanent failure audit
        // when EF subsequently retries and completes the disposal successfully.
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            return _context.Database.CurrentTransaction != null
                ? await CompleteDisposalAttemptAsync(disposalId)
                : await strategy.ExecuteAsync(() => CompleteDisposalAttemptAsync(disposalId));
        }
        catch (Exception ex)
        {
            await RecordDisposalFailureAsync(disposalId, ex);
            throw;
        }
    }

    private async Task<AssetDisposalDto> CompleteDisposalAttemptAsync(Guid disposalId)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset disposal.");
        }

        AssetDisposal disposal = await LoadDisposalForMutationAsync(disposalId);

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

        IDbContextTransaction? transaction = null;
        try
        {
            // The derecognition, AR statutory invoice, optional receipt allocation, and every GL
            // posting form one accounting command. FinancePostingEngine and UnitOfWork both join
            // an existing DbContext transaction, so none of these durable records can survive if
            // a later hand-off step fails. Focused tests verify composition and the UAT/dry-run
            // release scenario verifies the real SQL Server rollback boundary.
            if (_context.Database.IsRelational() && _context.Database.CurrentTransaction == null)
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            var finalDepreciation = await CalculateFinalDepreciationAsync(
                asset,
                bookValue,
                fiscalPeriod,
                disposal.DisposalDate,
                disposal.DisposalScope,
                disposal.DisposedPortionPercent,
                disposal.FinalDepreciationProductionUnits == 0m ? null : disposal.FinalDepreciationProductionUnits,
                disposal.FinalDepreciationEvidenceReference,
                disposal.FinalDepreciationEvidenceNotes,
                disposal.Id);
            ValidateApprovedFinalDepreciation(disposal, finalDepreciation, bookValue.DepreciationMethod);
            var snapshot = await BuildDisposalSnapshotAsync(asset, bookValue, disposal, finalDepreciation);
            ValidateApprovedProceedsSnapshot(disposal, snapshot);
            await ValidateApprovedRevaluationSurplusTransferAsync(disposal, asset, snapshot);
            ValidateApprovedAllocationSnapshot(disposal, snapshot);
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
                    disposal.ProceedsCurrencyCode,
                    disposal.ProceedsFunctionalAmount,
                    disposal.ProceedsExchangeRateId,
                    disposal.ProceedsExchangeRateValue,
                    disposal.ProceedsExchangeRateSource,
                    disposal.ProceedsExchangeRateDate,
                    disposal.ProceedsExchangeRateType,
                    disposal.ProceedsExchangeRateQuoteSide,
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

            if (RequiresSaleSettlement(disposal))
            {
                await CreateSaleSettlementDocumentsAsync(disposal);
                await _context.SaveChangesAsync();
            }

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

            if (disposal.DisposalScope != AssetDisposalScope.WholeAsset)
            {
                await RecordDisposalAuditAsync(
                    FinanceAuditEvents.FixedAssetPartialDisposalAllocationPosted,
                    disposal,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: BuildDisposalAuditSnapshot(disposal),
                    comment: "Partial/component derecognition posted; the retained carrying basis remains active.");
            }

            if (disposal.DisposalType == DisposalType.Sale && disposal.NetProceeds > 0m)
            {
                await RecordDisposalAuditAsync(
                    FinanceAuditEvents.FixedAssetDisposalSaleProceedsRecorded,
                    disposal,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new
                    {
                        disposal.NetProceeds,
                        disposal.ProceedsCurrencyCode,
                        disposal.ProceedsFunctionalAmount,
                        disposal.ProceedsExchangeRateId,
                        disposal.ProceedsExchangeRateValue,
                        disposal.ProceedsExchangeRateSource,
                        disposal.ProceedsExchangeRateDate,
                        disposal.ProceedsExchangeRateQuoteSide,
                        disposal.ProceedsAccountId
                    },
                    comment: "Fixed asset sale proceeds and their approved functional-currency translation were recorded to the configured clearing account.");
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

            if (transaction != null)
            {
                await transaction.CommitAsync();
                await transaction.DisposeAsync();
                transaction = null;
            }

            return await GetByIdAsync(disposal.Id) ?? throw new InvalidOperationException("Failed to complete disposal.");
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
                await transaction.DisposeAsync();
                transaction = null;
                _context.ChangeTracker.Clear();
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task RecordDisposalFailureAsync(Guid disposalId, Exception exception)
    {
        _context.ChangeTracker.Clear();

        // Reload after rollback before recording durable failure evidence. Reusing the rolled-back
        // graph could accidentally reinsert invoice, receipt or posting artifacts. This method is
        // called only after EF's retry strategy has exhausted transient retries.
        var disposal = await LoadDisposalForMutationAsync(disposalId);
        disposal.Status = exception is StaleDisposalApprovalException
            ? AssetDisposalStatus.Cancelled
            : AssetDisposalStatus.Approved;
        if (RequiresSaleSettlement(disposal))
        {
            disposal.SettlementStatus = AssetDisposalSettlementStatus.Failed;
        }
        disposal.FailedAt = DateTime.UtcNow;
        disposal.FailureReason = exception.Message;
        disposal.UpdatedAt = DateTime.UtcNow;
        disposal.UpdatedBy = UserName;
        await _context.SaveChangesAsync();

        var eventType = exception.Message.Contains("period", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
                ? FinanceAuditEvents.FixedAssetDisposalBlockedClosedPeriod
                : exception.Message.Contains("depreciation", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetDisposalBlockedMissingDepreciation
                    : FinanceAuditEvents.FixedAssetDisposalPostingFailed;

        await RecordDisposalAuditAsync(
            eventType,
            disposal,
            afterValues: new { error = exception.Message },
            reason: exception.Message,
            comment: "Fixed asset disposal posting failed.");
    }

    private static bool RequiresSaleSettlement(AssetDisposal disposal)
        => disposal.DisposalType == DisposalType.Sale
            && disposal.SaleProceeds > 0m
            && disposal.SettlementMode is AssetDisposalSettlementMode.CreditSale or AssetDisposalSettlementMode.ImmediateReceipt;

    private async Task CreateSaleSettlementDocumentsAsync(AssetDisposal disposal)
    {
        if (_invoiceService == null)
        {
            throw new InvalidOperationException("AR invoice service is not configured for fixed-asset sale settlement.");
        }
        if (!disposal.BuyerBusinessPartnerId.HasValue || !disposal.ProceedsAccountId.HasValue)
        {
            throw new InvalidOperationException("Approved disposal buyer and proceeds-clearing account evidence is incomplete.");
        }

        // The positive line is the statutory sale consideration and therefore owns the selected
        // tax treatment. DisposalCost represents a buyer/auctioneer deduction from remitted
        // proceeds: it is a separate out-of-scope contra line so tax remains calculated on gross
        // consideration while the AR journal clears exactly the net amount debited by disposal.
        var invoiceLines = new List<InvoiceLineItemCreateDto>
        {
            new()
            {
                LineItemType = nameof(LineItemType.FixedAssetDisposal),
                GLAccountId = disposal.ProceedsAccountId,
                Description = $"Fixed asset sale - {disposal.FixedAsset.AssetCode} - {disposal.FixedAsset.Name}",
                Quantity = 1m,
                UnitPrice = disposal.SaleProceeds,
                TaxGroupId = disposal.SaleTaxTreatment == TaxTreatment.Standard ? disposal.SaleTaxGroupId : null,
                TaxTreatment = disposal.SaleTaxTreatment,
                Unit = "Disposal"
            }
        };
        if (disposal.DisposalCost > 0m)
        {
            invoiceLines.Add(new InvoiceLineItemCreateDto
            {
                LineItemType = nameof(LineItemType.FixedAssetDisposalAdjustment),
                GLAccountId = disposal.ProceedsAccountId,
                Description = "Buyer/auctioneer deduction from remitted disposal proceeds",
                Quantity = 1m,
                UnitPrice = -disposal.DisposalCost,
                TaxTreatment = TaxTreatment.OutOfScope,
                Unit = "Deduction"
            });
        }

        var invoice = await _invoiceService.CreateAsync(new InvoiceCreateDto
        {
            CustomerId = disposal.BuyerBusinessPartnerId.Value,
            InvoiceDate = disposal.DisposalDate.Date,
            DueDate = disposal.SettlementMode == AssetDisposalSettlementMode.ImmediateReceipt
                ? disposal.DisposalDate.Date
                : null,
            Reference = $"FA-DISPOSAL:{disposal.ReferenceNumber ?? disposal.Id.ToString("N")}",
            Notes = "System-created statutory invoice for an approved fixed-asset disposal. Amend or reverse through the linked disposal workflow; do not edit the posted invoice directly.",
            CurrencyCode = disposal.ProceedsCurrencyCode,
            ExchangeRate = disposal.ProceedsExchangeRateValue,
            PaymentTermId = disposal.SettlementPaymentTermId,
            TaxGroupId = disposal.SaleTaxTreatment == TaxTreatment.Standard ? disposal.SaleTaxGroupId : null,
            LineItems = invoiceLines
        });
        invoice = await _invoiceService.SendInvoiceAsync(invoice.Id);

        disposal.CustomerInvoiceId = invoice.Id;
        disposal.SettlementInvoiceAmount = RoundMoney(invoice.TotalAmount);
        disposal.SettlementTaxAmount = RoundMoney(invoice.TaxAmount);
        disposal.SettlementStatus = AssetDisposalSettlementStatus.Invoiced;

        if (disposal.SettlementMode == AssetDisposalSettlementMode.CreditSale)
        {
            disposal.SettlementCompletedAt = DateTime.UtcNow;
            return;
        }

        if (_paymentService == null)
        {
            throw new InvalidOperationException("AR receipt service is not configured for immediate fixed-asset sale settlement.");
        }

        var receipt = await _paymentService.CreateAsync(new PaymentCreateDto
        {
            CustomerId = disposal.BuyerBusinessPartnerId.Value,
            PaymentDate = disposal.DisposalDate.Date,
            TotalAmount = invoice.TotalAmount,
            PaymentMethod = "Configured",
            PaymentMethodId = disposal.SettlementPaymentMethodId,
            CurrencyCode = disposal.ProceedsCurrencyCode,
            ExchangeRate = disposal.ProceedsExchangeRateValue,
            ExchangeRateId = disposal.ProceedsExchangeRateId,
            BankAccountId = disposal.SettlementBankAccountId,
            LiquidityAccountId = disposal.SettlementLiquidityAccountId,
            TransactionReference = disposal.SettlementReference ?? disposal.ReferenceNumber,
            Notes = $"Immediate receipt for fixed-asset disposal {disposal.ReferenceNumber} and AR invoice {invoice.InvoiceNumber}.",
            Allocations = new List<InvoiceAllocationDto>
            {
                new()
                {
                    InvoiceId = invoice.Id,
                    AllocatedAmount = invoice.TotalAmount,
                    PaymentCurrencyAmount = invoice.TotalAmount,
                    Notes = $"Automatic settlement of fixed-asset disposal {disposal.ReferenceNumber}."
                }
            }
        });
        receipt = await _paymentService.PostAsync(receipt.Id);

        disposal.CustomerPaymentId = receipt.Id;
        disposal.SettlementStatus = AssetDisposalSettlementStatus.Settled;
        disposal.SettlementCompletedAt = DateTime.UtcNow;
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
        // A sale is a legal customer transaction: each asset needs its own buyer, tax decision,
        // invoice and (where applicable) receipt allocation. The legacy bulk DTO contains only a
        // shared proceeds figure, so accepting it would either duplicate that amount per asset or
        // bypass the FIN-LIM-0040 settlement controls. Keep non-sale mass retirements available and
        // direct users to the controlled single-disposal workspace for sales.
        if (dto.DisposalType == DisposalType.Sale)
        {
            throw new InvalidOperationException(
                "Bulk asset sales are not supported. Request each sale separately so buyer, statutory tax, AR invoice and receipt evidence are controlled per asset.");
        }

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
            .Include(d => d.BuyerBusinessPartner)
            .Include(d => d.SaleTaxGroup)
            .Include(d => d.CustomerInvoice)
            .Include(d => d.CustomerPayment)
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

        ValidateDisposalScope(dto);

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

        await ValidateSettlementRequestAsync(dto);

        var functionalCurrency = await GetFunctionalCurrencyAsync();
        var proceedsCurrency = NormalizeCurrency(dto.ProceedsCurrencyCode, functionalCurrency);
        if (string.Equals(proceedsCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            && dto.ProceedsExchangeRateId.HasValue)
        {
            throw new InvalidOperationException("An exchange rate must not be supplied for functional-currency disposal proceeds.");
        }
    }

    private async Task ValidateSettlementRequestAsync(RequestAssetDisposalDto dto)
    {
        var hasSaleProceeds = dto.DisposalType == DisposalType.Sale && dto.SaleProceeds > 0m;
        if (!hasSaleProceeds)
        {
            // Non-sale disposals must not retain dormant customer, tax, or collection fields.
            // Failing closed here prevents a later UI change from accidentally producing an AR
            // document for a write-off, donation, scrap, or loss event.
            if (dto.BuyerBusinessPartnerId.HasValue || dto.SaleTaxGroupId.HasValue ||
                dto.SettlementPaymentTermId.HasValue || dto.SettlementPaymentMethodId.HasValue ||
                dto.SettlementBankAccountId.HasValue || dto.SettlementLiquidityAccountId.HasValue ||
                !string.IsNullOrWhiteSpace(dto.SettlementReference) ||
                dto.SettlementMode != AssetDisposalSettlementMode.NotApplicable)
            {
                throw new InvalidOperationException("AR, tax, and collection details are only valid for a fixed-asset sale with positive proceeds.");
            }

            return;
        }

        if (dto.SettlementMode is not (AssetDisposalSettlementMode.CreditSale or AssetDisposalSettlementMode.ImmediateReceipt))
        {
            throw new InvalidOperationException("Choose either Credit Sale or Immediate Receipt for fixed-asset sale proceeds.");
        }
        if (!dto.BuyerBusinessPartnerId.HasValue)
        {
            throw new InvalidOperationException("A canonical customer/business partner is required for a fixed-asset sale.");
        }

        var buyer = await _context.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == dto.BuyerBusinessPartnerId.Value
                && !item.IsDeleted
                && item.IsActive
                && (item.PartnerType == "Customer" || item.PartnerType == "Both"));
        if (buyer == null)
        {
            throw new InvalidOperationException("The selected disposal buyer must be an active same-tenant customer/business partner.");
        }

        // Freeze the master-data display value used by the checker. The canonical ID remains the
        // source of truth, so a caller cannot substitute a different free-text buyer name.
        dto.BuyerName = buyer.PartnerName;

        if (!Enum.IsDefined(dto.SaleTaxTreatment))
        {
            throw new InvalidOperationException("A valid statutory tax treatment is required for the fixed-asset sale.");
        }

        if (dto.SaleTaxTreatment == TaxTreatment.Standard)
        {
            if (!dto.SaleTaxGroupId.HasValue)
            {
                throw new InvalidOperationException("A sales tax group is required for a standard-rated fixed-asset sale.");
            }

            var taxGroupExists = await _context.TaxGroups.AsNoTracking().AnyAsync(group =>
                group.TenantId == TenantId && group.Id == dto.SaleTaxGroupId.Value && !group.IsDeleted &&
                group.IsActive && (group.Applicability == TaxApplicability.Sales || group.Applicability == TaxApplicability.Both));
            if (!taxGroupExists)
            {
                throw new InvalidOperationException("The selected tax group is not an active same-tenant sales tax group.");
            }
        }
        else if (dto.SaleTaxGroupId.HasValue)
        {
            throw new InvalidOperationException("Exempt, zero-rated, and out-of-scope asset sales must not carry a standard tax group.");
        }

        if (dto.SettlementPaymentTermId.HasValue)
        {
            var validTerm = await _context.PaymentTerms.AsNoTracking().AnyAsync(term =>
                term.TenantId == TenantId && term.Id == dto.SettlementPaymentTermId.Value && !term.IsDeleted && term.IsActive);
            if (!validTerm)
            {
                throw new InvalidOperationException("The selected payment term is not active for this tenant.");
            }
        }

        if (dto.SettlementMode == AssetDisposalSettlementMode.CreditSale)
        {
            if (dto.SettlementPaymentMethodId.HasValue || dto.SettlementBankAccountId.HasValue || dto.SettlementLiquidityAccountId.HasValue)
            {
                throw new InvalidOperationException("Credit-sale disposal requests must not include receipt destination details.");
            }
            return;
        }

        if (!dto.SettlementPaymentMethodId.HasValue)
        {
            throw new InvalidOperationException("Immediate asset-sale settlement requires a configured Finance payment method.");
        }
        if (dto.SettlementBankAccountId.HasValue == dto.SettlementLiquidityAccountId.HasValue)
        {
            throw new InvalidOperationException("Immediate asset-sale settlement requires exactly one bank or liquidity destination.");
        }

        var method = await _context.PaymentMethods.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == dto.SettlementPaymentMethodId.Value && !item.IsDeleted && item.IsActive);
        if (method == null)
        {
            throw new InvalidOperationException("The selected receipt payment method is not active for this tenant.");
        }
        if (method.RequiresReference && string.IsNullOrWhiteSpace(dto.SettlementReference))
        {
            throw new InvalidOperationException("The selected receipt payment method requires a settlement reference.");
        }

        var proceedsCurrency = NormalizeCurrency(dto.ProceedsCurrencyCode, await GetFunctionalCurrencyAsync());
        var isDirectBankMethod = method.Type is PaymentMethodType.EFT
            or PaymentMethodType.DirectDebit
            or PaymentMethodType.StandingOrder
            or PaymentMethodType.BankTransfer;
        if (isDirectBankMethod != dto.SettlementBankAccountId.HasValue)
        {
            throw new InvalidOperationException(isDirectBankMethod
                ? "The selected direct-bank receipt method requires a bank-account destination."
                : "The selected cash, cheque, card or mobile-money method requires a matching liquidity holding account.");
        }

        if (dto.SettlementBankAccountId.HasValue)
        {
            var validBank = await _context.BankAccounts.AsNoTracking().AnyAsync(item =>
                item.TenantId == TenantId && item.Id == dto.SettlementBankAccountId.Value && !item.IsDeleted && item.IsActive);
            if (!validBank)
            {
                throw new InvalidOperationException("The selected settlement bank account is not active for this tenant.");
            }

            var currencyMatches = await _context.BankAccounts.AsNoTracking().AnyAsync(item =>
                item.TenantId == TenantId && item.Id == dto.SettlementBankAccountId.Value && item.Currency == proceedsCurrency);
            if (!currencyMatches)
            {
                throw new InvalidOperationException("Disposal proceeds and receiving bank-account currencies must match.");
            }
        }
        else
        {
            var liquidity = await _context.LiquidityAccounts.AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == dto.SettlementLiquidityAccountId!.Value && !item.IsDeleted && item.IsActive);
            if (liquidity == null)
            {
                throw new InvalidOperationException("The selected settlement liquidity account is not active for this tenant.");
            }
            if (!liquidity.Currency.Equals(proceedsCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Disposal proceeds and receipt holding-account currencies must match.");
            }

            var expectedLiquidityType = method.Type switch
            {
                PaymentMethodType.Cheque => LiquidityAccountType.ChequesAwaitingDeposit,
                PaymentMethodType.Card => LiquidityAccountType.CardSettlementClearing,
                PaymentMethodType.MobileMoney => LiquidityAccountType.MobileMoneyClearing,
                _ => LiquidityAccountType.UndepositedCash
            };
            if (liquidity.AccountType != expectedLiquidityType &&
                liquidity.AccountType != LiquidityAccountType.OtherSettlementClearing &&
                !(method.Type == PaymentMethodType.Cash && liquidity.AccountType == LiquidityAccountType.CashTill))
            {
                throw new InvalidOperationException(
                    $"The selected holding account is not suitable for the {method.Name} receipt method.");
            }
        }

        ValidateTextLength(dto.SettlementReference, 100, "Settlement reference");
    }

    private static void ValidateDisposalScope(RequestAssetDisposalDto dto)
    {
        if (!Enum.IsDefined(dto.DisposalScope))
        {
            throw new InvalidOperationException("A valid whole-asset, partial-portion, or component disposal scope is required.");
        }

        if (dto.DisposalScope == AssetDisposalScope.WholeAsset)
        {
            // The API owns the whole-asset value. Silently accepting a partial percentage while
            // labelling the request whole would create misleading approval evidence.
            if (dto.DisposedPortionPercent != 100m)
            {
                throw new InvalidOperationException("Whole-asset disposal must use 100 percent.");
            }

            return;
        }

        if (dto.DisposedPortionPercent <= 0m || dto.DisposedPortionPercent >= 100m)
        {
            throw new InvalidOperationException("Partial and component disposal percentage must be greater than zero and less than 100.");
        }
        if (string.IsNullOrWhiteSpace(dto.AllocationEvidenceReference))
        {
            throw new InvalidOperationException("Partial and component disposal requires an allocation evidence reference.");
        }
        if (dto.DisposalScope == AssetDisposalScope.Component && string.IsNullOrWhiteSpace(dto.ComponentReference))
        {
            throw new InvalidOperationException("Component disposal requires a component reference.");
        }

        ValidateTextLength(dto.ComponentReference, 100, "Component reference");
        ValidateTextLength(dto.ComponentDescription, 500, "Component description");
        ValidateTextLength(dto.AllocationEvidenceReference, 200, "Allocation evidence reference");
        ValidateTextLength(dto.AllocationEvidenceNotes, 1000, "Allocation evidence notes");
    }

    private static void ValidateTextLength(string? value, int maximumLength, string fieldName)
    {
        if (value?.Trim().Length > maximumLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot exceed {maximumLength} characters.");
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
        AssetDisposalScope disposalScope,
        decimal disposedPortionPercent,
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
            // A prior partial-disposal schedule covers only the portion that left service. It must
            // not block later depreciation of the retained asset in the same fiscal period.
            (schedule.AssetDisposalId == null || schedule.AssetDisposal!.DisposalScope == AssetDisposalScope.WholeAsset) &&
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
        var wholeAssetAmount = bookValue.DepreciationMethod == DepreciationMethod.UnitsOfProduction
            ? calculation.DepreciationAmount
            : RoundMoney(calculation.DepreciationAmount * eligibleDays / periodDays);
        wholeAssetAmount = Math.Min(wholeAssetAmount, RoundMoney(bookValue.NetBookValue - bookValue.ResidualValue));

        // For a partial/component disposal, only the portion leaving service receives final
        // depreciation here. The retained portion remains eligible for its ordinary current-period
        // run, preventing both a skipped charge and double-counting after the disposal date.
        var allocationRate = disposalScope == AssetDisposalScope.WholeAsset
            ? 1m
            : RoundAllocation(disposedPortionPercent) / 100m;
        var amount = disposalScope == AssetDisposalScope.WholeAsset
            ? wholeAssetAmount
            : AllocateMoney(wholeAssetAmount, allocationRate);

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
        AddProceedsPostingLine(lines, proceedsAccount?.Id, snapshot, "Record disposal proceeds clearing", reference, lineNumber++, disposal, asset, functionalCurrency);
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

    private static void AddProceedsPostingLine(
        ICollection<FinancePostingLineDto> lines,
        Guid? accountId,
        DisposalSnapshot snapshot,
        string description,
        string reference,
        int lineNumber,
        AssetDisposal disposal,
        FixedAsset asset,
        string functionalCurrency)
    {
        if (accountId == null || snapshot.ProceedsFunctionalAmount == 0m)
        {
            return;
        }

        var isForeign = !string.Equals(snapshot.ProceedsCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        // The journal balances in functional currency, while transaction debit and FX evidence
        // preserve what the buyer actually paid. The central posting engine independently checks
        // this tenant-owned rate before persisting its own immutable line snapshot.
        lines.Add(new FinancePostingLineDto
        {
            AccountId = accountId.Value,
            DebitAmount = snapshot.ProceedsFunctionalAmount,
            CreditAmount = 0m,
            TransactionCurrency = snapshot.ProceedsCurrencyCode,
            TransactionDebitAmount = snapshot.NetProceeds,
            TransactionCreditAmount = 0m,
            ForeignCurrencyAmount = isForeign ? snapshot.NetProceeds : null,
            ExchangeRateId = isForeign ? snapshot.ProceedsExchangeRate.ExchangeRateId : null,
            ExchangeRate = isForeign ? snapshot.ProceedsExchangeRate.Rate : null,
            ExchangeRateSource = isForeign ? snapshot.ProceedsExchangeRate.Source : null,
            ExchangeRateDate = isForeign ? snapshot.ProceedsExchangeRate.EffectiveDate : null,
            Description = $"{description} - {asset.AssetCode}",
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            SegmentString = asset.CurrentSegmentString,
            Notes = $"FixedAssetId={asset.Id:N};AssetDisposalId={disposal.Id:N};Book={disposal.BookClassification};" +
                $"NativeProceeds={snapshot.NetProceeds:0.00} {snapshot.ProceedsCurrencyCode};" +
                $"FunctionalProceeds={snapshot.ProceedsFunctionalAmount:0.00} {functionalCurrency}",
            TransactionTag = "FA-DisposalProceeds"
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

        if (disposal.DisposalScope == AssetDisposalScope.WholeAsset)
        {
            asset.Status = disposal.DisposalType == DisposalType.Sale
                ? FixedAssetStatus.Disposed
                : FixedAssetStatus.WrittenOff;
            asset.DisposalDate = disposal.DisposalDate;
            asset.NetBookValue = 0m;

            // Preserve historical gross cost on a wholly derecognised asset, matching the existing
            // register convention. Status and zero NBV remove it from the live asset population.
            bookValue.AccumulatedDepreciation = 0m;
            bookValue.NetBookValue = 0m;
        }
        else
        {
            // A partial/component disposal changes the current carrying basis but does not retire
            // the parent asset. It therefore remains available for future depreciation, valuation,
            // transfer, verification, and eventual additional or whole disposal.
            asset.AcquisitionCost = snapshot.RemainingAcquisitionCost;
            asset.NetBookValue = snapshot.RemainingNetBookValue;
            asset.ResidualValue = RoundMoney(Math.Max(0m, bookValue.ResidualValue - snapshot.ResidualValueAllocated));
            asset.LifetimeProductionCapacity = RoundUnits(Math.Max(0m,
                bookValue.LifetimeProductionCapacity - snapshot.ProductionCapacityAllocated));
            asset.AccumulatedProductionUnits = RoundUnits(Math.Max(0m,
                bookValue.AccumulatedProductionUnits - snapshot.AccumulatedProductionUnitsAllocated));

            bookValue.AcquisitionCost = snapshot.RemainingAcquisitionCost;
            bookValue.AccumulatedDepreciation = snapshot.RemainingAccumulatedDepreciation;
            bookValue.NetBookValue = snapshot.RemainingNetBookValue;
            bookValue.ResidualValue = asset.ResidualValue;
            bookValue.LifetimeProductionCapacity = asset.LifetimeProductionCapacity;
            bookValue.AccumulatedProductionUnits = asset.AccumulatedProductionUnits;
        }

        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;
        bookValue.UpdatedAt = DateTime.UtcNow;
        bookValue.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = disposal.FixedAssetId,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            TransactionDate = disposal.DisposalDate,
            TransactionType = disposal.DisposalScope == AssetDisposalScope.WholeAsset
                ? disposal.DisposalType == DisposalType.Sale ? "Disposal" : "WriteOff"
                : "PartialDisposal",
            Description = disposal.DisposalScope == AssetDisposalScope.WholeAsset
                ? $"Fixed asset {disposal.DisposalType} disposal. Proceeds: {snapshot.NetProceeds:N2} {snapshot.ProceedsCurrencyCode}; functional value: {snapshot.ProceedsFunctionalAmount:N2}; gain/loss: {snapshot.GainOrLoss:N2}"
                : $"{disposal.DisposalScope} disposal ({snapshot.DisposedPortionPercent:N4}%). Proceeds: {snapshot.NetProceeds:N2} {snapshot.ProceedsCurrencyCode}; functional value: {snapshot.ProceedsFunctionalAmount:N2}; gain/loss: {snapshot.GainOrLoss:N2}",
            // FixedAssetTransaction is a functional-currency register. Native proceeds remain on
            // AssetDisposal and the journal line, avoiding mixed-currency totals in asset reports.
            Amount = snapshot.ProceedsFunctionalAmount,
            ResultingBookValue = snapshot.RemainingNetBookValue,
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
            // Negative sequences reserve a separate namespace from ordinary runs (0+) while still
            // satisfying the existing unique asset/period/book/sequence constraint.
            CorrectionSequence = ResolveDisposalScheduleSequence(asset, disposal),
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
        disposal.ProceedsFunctionalAmount = snapshot.ProceedsFunctionalAmount;
        disposal.ProceedsExchangeRateId = snapshot.ProceedsExchangeRate.ExchangeRateId;
        disposal.ProceedsExchangeRateValue = snapshot.ProceedsExchangeRate.Rate;
        disposal.ProceedsExchangeRateSource = snapshot.ProceedsExchangeRate.Source;
        disposal.ProceedsExchangeRateDate = snapshot.ProceedsExchangeRate.EffectiveDate;
        disposal.ProceedsExchangeRateType = snapshot.ProceedsExchangeRate.RateType;
        disposal.ProceedsExchangeRateQuoteSide = snapshot.ProceedsExchangeRate.QuoteSide;
        disposal.ProceedsAccountId = snapshot.ProceedsAccountId;
        disposal.CostAtDisposal = snapshot.AssetCarryingAccountAmount;
        disposal.DisposedPortionPercent = snapshot.DisposedPortionPercent;
        disposal.AcquisitionCostAllocated = snapshot.AcquisitionCostAllocated;
        disposal.RevaluationAdjustmentAllocated = snapshot.RevaluationAdjustmentAllocated;
        disposal.ResidualValueAllocated = snapshot.ResidualValueAllocated;
        disposal.ProductionCapacityAllocated = snapshot.ProductionCapacityAllocated;
        disposal.AccumulatedProductionUnitsAllocated = snapshot.AccumulatedProductionUnitsAllocated;
        disposal.AccumulatedDepreciationAtDisposal = snapshot.AccumulatedDepreciation;
        disposal.FinalDepreciationAmount = snapshot.FinalDepreciation.Amount;
        disposal.AccumulatedImpairmentAtDisposal = snapshot.AccumulatedImpairment;
        disposal.RevaluationSurplusAtDisposal = snapshot.RevaluationSurplusBalance;
        disposal.RevaluationSurplusTransferAmount = snapshot.RevaluationSurplusBalance;
        disposal.NetBookValueAtDisposal = snapshot.NetBookValue;
        disposal.GainOrLoss = snapshot.GainOrLoss;
        disposal.RemainingAcquisitionCostAfterDisposal = snapshot.RemainingAcquisitionCost;
        disposal.RemainingAccumulatedDepreciationAfterDisposal = snapshot.RemainingAccumulatedDepreciation;
        disposal.RemainingNetBookValueAfterDisposal = snapshot.RemainingNetBookValue;
    }

    private static int ResolveDisposalScheduleSequence(FixedAsset asset, AssetDisposal disposal)
    {
        var priorDisposalSequences = asset.DepreciationSchedules
            .Where(schedule => schedule.FiscalPeriodId == disposal.FiscalPeriodId
                && schedule.BookClassification == disposal.BookClassification
                && schedule.AssetDisposalId.HasValue)
            .Select(schedule => schedule.CorrectionSequence)
            .ToList();

        return priorDisposalSequences.Count == 0
            ? -1
            : Math.Min(-1, priorDisposalSequences.Min() - 1);
    }

    private static void ValidateApprovedAllocationSnapshot(AssetDisposal disposal, DisposalSnapshot current)
    {
        if (disposal.DisposedPortionPercent != current.DisposedPortionPercent ||
            disposal.CostAtDisposal != current.AssetCarryingAccountAmount ||
            disposal.AcquisitionCostAllocated != current.AcquisitionCostAllocated ||
            disposal.RevaluationAdjustmentAllocated != current.RevaluationAdjustmentAllocated ||
            disposal.ResidualValueAllocated != current.ResidualValueAllocated ||
            disposal.ProductionCapacityAllocated != current.ProductionCapacityAllocated ||
            disposal.AccumulatedProductionUnitsAllocated != current.AccumulatedProductionUnitsAllocated ||
            disposal.AccumulatedDepreciationAtDisposal != current.AccumulatedDepreciation ||
            disposal.AccumulatedImpairmentAtDisposal != current.AccumulatedImpairment ||
            disposal.NetBookValueAtDisposal != current.NetBookValue ||
            disposal.RemainingAcquisitionCostAfterDisposal != current.RemainingAcquisitionCost ||
            disposal.RemainingAccumulatedDepreciationAfterDisposal != current.RemainingAccumulatedDepreciation ||
            disposal.RemainingNetBookValueAfterDisposal != current.RemainingNetBookValue)
        {
            throw new StaleDisposalApprovalException(
                "The approved partial/component allocation no longer matches the asset book. Submit a new disposal request.");
        }
    }

    private static void ValidateApprovedProceedsSnapshot(AssetDisposal disposal, DisposalSnapshot current)
    {
        if (disposal.NetProceeds != current.NetProceeds
            || disposal.ProceedsFunctionalAmount != current.ProceedsFunctionalAmount
            || disposal.ProceedsExchangeRateId != current.ProceedsExchangeRate.ExchangeRateId
            || disposal.ProceedsExchangeRateValue != current.ProceedsExchangeRate.Rate
            || disposal.ProceedsExchangeRateSource != current.ProceedsExchangeRate.Source
            || disposal.ProceedsExchangeRateDate.Date != current.ProceedsExchangeRate.EffectiveDate.Date
            || disposal.ProceedsExchangeRateType != current.ProceedsExchangeRate.RateType
            || disposal.ProceedsExchangeRateQuoteSide != current.ProceedsExchangeRate.QuoteSide)
        {
            // A checker approves both the native proceeds and their functional equivalent. Never
            // let a changed rate record or quote-side policy alter that accounting decision later.
            throw new StaleDisposalApprovalException(
                "The approved disposal-proceeds exchange-rate snapshot is no longer current. Submit a new disposal request.");
        }
    }

    private async Task<ProceedsExchangeRateSnapshot> ResolveProceedsExchangeRateAsync(
        string proceedsCurrency,
        string functionalCurrency,
        DateTime disposalDate,
        Guid? requestedExchangeRateId,
        Guid? proceedsAccountId)
    {
        if (string.Equals(proceedsCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (requestedExchangeRateId.HasValue)
            {
                throw new InvalidOperationException("An exchange rate must not be supplied for functional-currency disposal proceeds.");
            }

            return new ProceedsExchangeRateSnapshot(
                null,
                1m,
                "Functional currency",
                disposalDate.Date,
                ExchangeRateType.Daily,
                ExchangeRateQuoteSide.Mid);
        }

        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && !s.IsDeleted);
        var date = disposalDate.Date;
        if (!proceedsAccountId.HasValue)
        {
            throw new InvalidOperationException("A disposal proceeds clearing account is required for foreign-currency sale proceeds.");
        }

        var proceedsAccount = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(account => account.TenantId == TenantId
                && account.Id == proceedsAccountId.Value
                && !account.IsDeleted
                && account.Status == AccountStatus.Active
                && account.AllowDirectPosting);
        if (proceedsAccount == null)
        {
            throw new InvalidOperationException("The disposal proceeds clearing account must be an active same-tenant direct-posting account.");
        }

        var accountCurrency = NormalizeCurrency(proceedsAccount.CurrencyCode, functionalCurrency);
        AccountCurrencyLink? currencyLink = null;
        if (!string.Equals(accountCurrency, proceedsCurrency, StringComparison.OrdinalIgnoreCase))
        {
            currencyLink = proceedsAccount.IsMultiCurrency
                ? await _context.AccountCurrencyLinks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(link => link.TenantId == TenantId
                        && link.AccountId == proceedsAccount.Id
                        && !link.IsDeleted
                        && link.IsActive
                        && link.LinkedCurrencyCode == proceedsCurrency
                        && link.EffectiveDate.Date <= date
                        && (!link.EffectiveEndDate.HasValue || link.EffectiveEndDate.Value.Date >= date))
                : null;
            if (currencyLink == null)
            {
                // Reject this before workflow submission; otherwise a checker could approve a
                // disposal that the central posting engine is guaranteed to reject later.
                throw new InvalidOperationException(
                    $"The disposal proceeds clearing account is not authorized for {proceedsCurrency} on {date:yyyy-MM-dd}.");
            }
        }

        if (currencyLink != null
            && !string.Equals(
                currencyLink.TransactionRateType?.Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty),
                nameof(ExchangeRateType.Daily),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Foreign-currency disposal proceeds require a Daily transaction-rate policy on the clearing account currency link.");
        }

        // FixedAssets is a normal Finance source. When a currency link exists, its quote side is
        // authoritative; otherwise the tenant default applies. This mirrors FinancePostingEngine
        // so request-time selection and posting-time independent validation cannot disagree.
        var quoteSide = settings?.DirectionalExchangeRatePolicyEnabled == true
            ? currencyLink?.TransactionQuoteSide ?? settings.DefaultTransactionQuoteSide
            : ExchangeRateQuoteSide.Mid;

        IQueryable<ExchangeRate> query = _context.ExchangeRates
            .AsNoTracking()
            .Where(rate => rate.TenantId == TenantId
                && !rate.IsDeleted
                && rate.BaseCurrencyCode == functionalCurrency
                && rate.TargetCurrencyCode == proceedsCurrency
                && rate.RateType == ExchangeRateType.Daily
                && rate.QuoteSide == quoteSide
                && rate.IsActive
                && rate.Rate > 0m
                && (rate.ApprovalStatus == RateApprovalStatus.Approved
                    || rate.ApprovalStatus == RateApprovalStatus.AutoApproved)
                && rate.EffectiveDate.Date <= date
                && (!rate.EndDate.HasValue || rate.EndDate.Value.Date >= date));

        ExchangeRate? rate;
        if (requestedExchangeRateId.HasValue)
        {
            // Keep every policy predicate on an explicitly selected ID; accepting an ID first and
            // validating only its value could leak another tenant's rate into the approval record.
            rate = await query.FirstOrDefaultAsync(candidate => candidate.Id == requestedExchangeRateId.Value);
            if (rate == null)
            {
                throw new InvalidOperationException(
                    $"The selected exchange rate is not an active, approved {quoteSide} daily rate for {proceedsCurrency} to {functionalCurrency} on {date:yyyy-MM-dd}.");
            }
        }
        else
        {
            rate = await query
                .OrderByDescending(candidate => candidate.EffectiveDate)
                .ThenByDescending(candidate => candidate.Priority)
                .ThenByDescending(candidate => candidate.CreatedAt)
                .FirstOrDefaultAsync();
            if (rate == null)
            {
                throw new InvalidOperationException(
                    $"No active, approved {quoteSide} daily exchange rate exists for {proceedsCurrency} to {functionalCurrency} on {date:yyyy-MM-dd}.");
            }
        }

        return new ProceedsExchangeRateSnapshot(
            rate.Id,
            rate.Rate,
            rate.RateSource,
            rate.EffectiveDate.Date,
            rate.RateType,
            rate.QuoteSide);
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
        var proceedsAccountId = netProceeds > 0m
            ? dto.ProceedsAccountId ?? asset.Category.DisposalProceedsClearingAccountId
            : null;
        var exchangeRate = await ResolveProceedsExchangeRateAsync(
            proceedsCurrency,
            functionalCurrency,
            dto.DisposalDate,
            dto.ProceedsExchangeRateId,
            proceedsAccountId);
        var functionalProceeds = RoundMoney(netProceeds * exchangeRate.Rate);
        var accumulatedImpairment = await CalculateAccumulatedImpairmentAsync(asset.Id, bookValue);
        var revaluationAssetAdjustment = await CalculateRevaluationAssetAdjustmentAsync(asset.Id, bookValue);
        var revaluationSurplusBalance = await CalculateRevaluationSurplusBalanceAsync(asset.Id, bookValue);
        return BuildAllocatedDisposalSnapshot(
            bookValue,
            dto.DisposalScope,
            dto.DisposedPortionPercent,
            revaluationAssetAdjustment,
            accumulatedImpairment,
            revaluationSurplusBalance,
            netProceeds,
            functionalProceeds,
            proceedsCurrency,
            exchangeRate,
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
        var netProceeds = RoundMoney(disposal.SaleProceeds - disposal.DisposalCost);
        var functionalCurrency = await GetFunctionalCurrencyAsync();
        var proceedsCurrency = NormalizeCurrency(disposal.ProceedsCurrencyCode, functionalCurrency);
        var proceedsAccountId = netProceeds > 0m
            ? disposal.ProceedsAccountId ?? asset.Category.DisposalProceedsClearingAccountId
            : null;
        // Completion resolves the exact approved rate ID again. This detects deactivation,
        // approval withdrawal, date changes or tenant policy drift before any journal is posted.
        ProceedsExchangeRateSnapshot exchangeRate;
        try
        {
            exchangeRate = await ResolveProceedsExchangeRateAsync(
                proceedsCurrency,
                functionalCurrency,
                disposal.DisposalDate,
                disposal.ProceedsExchangeRateId,
                proceedsAccountId);
        }
        catch (InvalidOperationException ex)
        {
            // At completion, a missing/rejected rate is approval drift rather than a transient
            // posting failure. Cancel the stale checker decision so it cannot later be retried.
            throw new StaleDisposalApprovalException(
                $"The approved disposal-proceeds exchange-rate snapshot is no longer usable: {ex.Message}");
        }
        var functionalProceeds = RoundMoney(netProceeds * exchangeRate.Rate);
        return BuildAllocatedDisposalSnapshot(
            bookValue,
            disposal.DisposalScope,
            disposal.DisposedPortionPercent,
            revaluationAssetAdjustment,
            accumulatedImpairment,
            revaluationSurplusBalance,
            netProceeds,
            functionalProceeds,
            proceedsCurrency,
            exchangeRate,
            proceedsAccountId,
            finalDepreciation);
    }

    private static DisposalSnapshot BuildAllocatedDisposalSnapshot(
        FixedAssetBookValue bookValue,
        AssetDisposalScope scope,
        decimal disposedPortionPercent,
        decimal remainingRevaluationAdjustment,
        decimal remainingAccumulatedImpairment,
        decimal remainingRevaluationSurplus,
        decimal netProceeds,
        decimal proceedsFunctionalAmount,
        string proceedsCurrency,
        ProceedsExchangeRateSnapshot proceedsExchangeRate,
        Guid? proceedsAccountId,
        FinalDepreciationPreparation finalDepreciation)
    {
        var normalizedPercent = scope == AssetDisposalScope.WholeAsset
            ? 100m
            : RoundAllocation(disposedPortionPercent);
        var allocationRate = normalizedPercent / 100m;

        // Final depreciation belongs to the entire asset still in service through the disposal
        // date. Only after that charge is recognised do we allocate the disposed portion of each
        // carrying-value layer. This keeps the retained portion's NBV and future depreciation sound.
        var totalAccumulatedDepreciation = RoundMoney(bookValue.AccumulatedDepreciation + finalDepreciation.Amount);
        var totalNetBookValue = RoundMoney(bookValue.NetBookValue - finalDepreciation.Amount);
        var acquisitionCostAllocated = AllocateMoney(bookValue.AcquisitionCost, allocationRate);
        var revaluationAdjustmentAllocated = AllocateMoney(remainingRevaluationAdjustment, allocationRate);
        // Final depreciation already belongs only to the disposed portion. Allocate the opening
        // reserve, then add that final charge once; multiplying the combined balance again would
        // incorrectly apply the disposal percentage twice.
        var accumulatedDepreciationAllocated = RoundMoney(
            AllocateMoney(bookValue.AccumulatedDepreciation, allocationRate) + finalDepreciation.Amount);
        var accumulatedImpairmentAllocated = AllocateMoney(remainingAccumulatedImpairment, allocationRate);
        var revaluationSurplusAllocated = AllocateMoney(remainingRevaluationSurplus, allocationRate);
        // Derive a partial portion's NBV from the rounded carrying layers that will actually post.
        // This avoids a one-pesewa imbalance when independently rounded percentages differ. Whole
        // disposal retains the authoritative book NBV used by the established workflow.
        var netBookValueAllocated = scope == AssetDisposalScope.WholeAsset
            ? totalNetBookValue
            : RoundMoney(
                acquisitionCostAllocated + revaluationAdjustmentAllocated -
                accumulatedDepreciationAllocated - accumulatedImpairmentAllocated);
        var residualValueAllocated = AllocateMoney(bookValue.ResidualValue, allocationRate);
        var productionCapacityAllocated = AllocateUnits(bookValue.LifetimeProductionCapacity, allocationRate);
        var accumulatedProductionUnitsAllocated = AllocateUnits(
            finalDepreciation.Calculation.CumulativeProductionUnitsAfter > 0m
                ? finalDepreciation.Calculation.CumulativeProductionUnitsAfter
                : bookValue.AccumulatedProductionUnits,
            allocationRate);

        return new DisposalSnapshot(
            RoundMoney(acquisitionCostAllocated + revaluationAdjustmentAllocated),
            accumulatedDepreciationAllocated,
            accumulatedImpairmentAllocated,
            revaluationSurplusAllocated,
            netBookValueAllocated,
            netProceeds,
            proceedsFunctionalAmount,
            RoundMoney(proceedsFunctionalAmount - netBookValueAllocated),
            proceedsCurrency,
            proceedsExchangeRate,
            proceedsAccountId,
            finalDepreciation,
            normalizedPercent,
            acquisitionCostAllocated,
            revaluationAdjustmentAllocated,
            residualValueAllocated,
            productionCapacityAllocated,
            accumulatedProductionUnitsAllocated,
            RoundMoney(bookValue.AcquisitionCost - acquisitionCostAllocated),
            RoundMoney(totalAccumulatedDepreciation - accumulatedDepreciationAllocated),
            RoundMoney(totalNetBookValue - netBookValueAllocated));
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

        var alreadyDerecognised = await _context.AssetDisposals
            .Where(d => d.TenantId == TenantId && d.FixedAssetId == assetId
                && d.BookClassification == bookValue.BookClassification
                && d.Status == AssetDisposalStatus.Completed && !d.IsDeleted)
            .SumAsync(d => d.AccumulatedImpairmentAtDisposal);

        return Math.Max(0m, RoundMoney(amount - alreadyDerecognised));
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

        var alreadyDerecognised = await _context.AssetDisposals
            .Where(d => d.TenantId == TenantId && d.FixedAssetId == assetId
                && d.BookClassification == bookValue.BookClassification
                && d.Status == AssetDisposalStatus.Completed && !d.IsDeleted)
            .SumAsync(d => d.RevaluationAdjustmentAllocated);

        return RoundMoney(amount - alreadyDerecognised);
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

        var alreadyTransferred = await _context.AssetDisposals
            .Where(d => d.TenantId == TenantId && d.FixedAssetId == assetId
                && d.BookClassification == bookValue.BookClassification
                && d.Status == AssetDisposalStatus.Completed && !d.IsDeleted)
            .SumAsync(d => d.RevaluationSurplusTransferAmount);

        return Math.Max(0m, RoundMoney(amount - alreadyTransferred));
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
            disposal.DisposalScope,
            disposal.DisposedPortionPercent,
            disposal.ComponentReference,
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
            disposal.DisposalScope,
            disposal.DisposedPortionPercent,
            disposal.ComponentReference,
            disposal.ComponentDescription,
            disposal.AllocationEvidenceReference,
            disposal.AllocationEvidenceNotes,
            disposal.DisposalDate,
            disposal.AccountingDate,
            disposal.FiscalPeriodId,
            disposal.BookClassification,
            disposal.Status,
            disposal.SaleProceeds,
            disposal.DisposalCost,
            disposal.NetProceeds,
            disposal.ProceedsCurrencyCode,
            disposal.ProceedsFunctionalAmount,
            disposal.ProceedsExchangeRateId,
            disposal.ProceedsExchangeRateValue,
            disposal.ProceedsExchangeRateSource,
            disposal.ProceedsExchangeRateDate,
            disposal.ProceedsExchangeRateType,
            disposal.ProceedsExchangeRateQuoteSide,
            disposal.ProceedsAccountId,
            disposal.CostAtDisposal,
            disposal.AcquisitionCostAllocated,
            disposal.RevaluationAdjustmentAllocated,
            disposal.ResidualValueAllocated,
            disposal.ProductionCapacityAllocated,
            disposal.AccumulatedProductionUnitsAllocated,
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
            disposal.RemainingAcquisitionCostAfterDisposal,
            disposal.RemainingAccumulatedDepreciationAfterDisposal,
            disposal.RemainingNetBookValueAfterDisposal,
            // Preserve the complete statutory and settlement chain in the disposal audit event.
            // Reviewers can therefore prove which customer, tax decision, invoice and receipt
            // were approved without reconstructing mutable master-data labels after the fact.
            disposal.BuyerBusinessPartnerId,
            disposal.BuyerName,
            disposal.SettlementMode,
            disposal.SettlementStatus,
            disposal.SaleTaxGroupId,
            disposal.SaleTaxTreatment,
            disposal.SettlementPaymentTermId,
            disposal.SettlementPaymentMethodId,
            disposal.SettlementBankAccountId,
            disposal.SettlementLiquidityAccountId,
            disposal.SettlementReference,
            disposal.CustomerInvoiceId,
            disposal.CustomerPaymentId,
            disposal.SettlementInvoiceAmount,
            disposal.SettlementTaxAmount,
            disposal.SettlementCompletedAt,
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

    private static string? NormalizeOptionalText(string? value)
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

    private static decimal RoundUnits(decimal amount)
        => Math.Round(amount, 4, MidpointRounding.AwayFromZero);

    private static decimal RoundAllocation(decimal amount)
        => Math.Round(amount, 4, MidpointRounding.AwayFromZero);

    private static decimal AllocateMoney(decimal amount, decimal allocationRate)
        => RoundMoney(amount * allocationRate);

    private static decimal AllocateUnits(decimal amount, decimal allocationRate)
        => RoundUnits(amount * allocationRate);

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
            DisposalScope = d.DisposalScope,
            DisposedPortionPercent = d.DisposedPortionPercent,
            ComponentReference = d.ComponentReference,
            ComponentDescription = d.ComponentDescription,
            AllocationEvidenceReference = d.AllocationEvidenceReference,
            AllocationEvidenceNotes = d.AllocationEvidenceNotes,
            Reason = d.Reason,
            SaleProceeds = d.SaleProceeds,
            DisposalCost = d.DisposalCost,
            NetProceeds = d.NetProceeds,
            ProceedsCurrencyCode = d.ProceedsCurrencyCode,
            ProceedsFunctionalAmount = d.ProceedsFunctionalAmount,
            ProceedsExchangeRateId = d.ProceedsExchangeRateId,
            ProceedsExchangeRateValue = d.ProceedsExchangeRateValue,
            ProceedsExchangeRateSource = d.ProceedsExchangeRateSource,
            ProceedsExchangeRateDate = d.ProceedsExchangeRateDate,
            ProceedsExchangeRateType = d.ProceedsExchangeRateType,
            ProceedsExchangeRateQuoteSide = d.ProceedsExchangeRateQuoteSide,
            ProceedsAccountId = d.ProceedsAccountId,
            CostAtDisposal = d.CostAtDisposal,
            AcquisitionCostAllocated = d.AcquisitionCostAllocated,
            RevaluationAdjustmentAllocated = d.RevaluationAdjustmentAllocated,
            ResidualValueAllocated = d.ResidualValueAllocated,
            ProductionCapacityAllocated = d.ProductionCapacityAllocated,
            AccumulatedProductionUnitsAllocated = d.AccumulatedProductionUnitsAllocated,
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
            RemainingAcquisitionCostAfterDisposal = d.RemainingAcquisitionCostAfterDisposal,
            RemainingAccumulatedDepreciationAfterDisposal = d.RemainingAccumulatedDepreciationAfterDisposal,
            RemainingNetBookValueAfterDisposal = d.RemainingNetBookValueAfterDisposal,
            BuyerName = d.BuyerName,
            BuyerBusinessPartnerId = d.BuyerBusinessPartnerId,
            SettlementMode = d.SettlementMode,
            SettlementStatus = d.SettlementStatus,
            SaleTaxGroupId = d.SaleTaxGroupId,
            SaleTaxTreatment = d.SaleTaxTreatment,
            SettlementPaymentTermId = d.SettlementPaymentTermId,
            SettlementPaymentMethodId = d.SettlementPaymentMethodId,
            SettlementBankAccountId = d.SettlementBankAccountId,
            SettlementLiquidityAccountId = d.SettlementLiquidityAccountId,
            SettlementReference = d.SettlementReference,
            CustomerInvoiceId = d.CustomerInvoiceId,
            CustomerPaymentId = d.CustomerPaymentId,
            SettlementInvoiceAmount = d.SettlementInvoiceAmount,
            SettlementTaxAmount = d.SettlementTaxAmount,
            SettlementCompletedAt = d.SettlementCompletedAt,
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
        decimal ProceedsFunctionalAmount,
        decimal GainOrLoss,
        string ProceedsCurrencyCode,
        ProceedsExchangeRateSnapshot ProceedsExchangeRate,
        Guid? ProceedsAccountId,
        FinalDepreciationPreparation FinalDepreciation,
        decimal DisposedPortionPercent,
        decimal AcquisitionCostAllocated,
        decimal RevaluationAdjustmentAllocated,
        decimal ResidualValueAllocated,
        decimal ProductionCapacityAllocated,
        decimal AccumulatedProductionUnitsAllocated,
        decimal RemainingAcquisitionCost,
        decimal RemainingAccumulatedDepreciation,
        decimal RemainingNetBookValue);

    private sealed record ProceedsExchangeRateSnapshot(
        Guid? ExchangeRateId,
        decimal Rate,
        string Source,
        DateTime EffectiveDate,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide);

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
