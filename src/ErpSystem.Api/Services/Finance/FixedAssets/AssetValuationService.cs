using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public partial class AssetValuationService : IAssetValuationService
{
    private const string SourceModule = "FixedAssets";
    private const string SourceDocumentType = "FixedAssetValuation";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountingBookService? _accountingBookService;
    private readonly IFinancePostingEngine? _financePostingEngine;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IWorkflowService? _workflowService;
    private readonly IFinanceReversalPolicyService? _financeReversalPolicyService;

    public AssetValuationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IJournalEntryService? journalEntryService = null,
        IAccountingBookService? accountingBookService = null,
        IFinancePostingEngine? financePostingEngine = null,
        IFinanceAuditService? financeAuditService = null,
        IWorkflowService? workflowService = null,
        IFinanceReversalPolicyService? financeReversalPolicyService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _accountingBookService = accountingBookService;
        _financePostingEngine = financePostingEngine;
        _financeAuditService = financeAuditService;
        _workflowService = workflowService;
        _financeReversalPolicyService = financeReversalPolicyService;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid? UserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : null;
    private string UserName => _currentUser.UserName ?? "system";

    public async Task<AssetValuationDto> CreateValuationAsync(
        CreateAssetValuationDto dto, Guid performedByUserId)
    {
        var asset = await LoadAssetForValuationAsync(dto.FixedAssetId)
            ?? throw new KeyNotFoundException("Fixed asset not found.");

        var bookValue = ResolveDefaultBookValue(asset);
        var fiscalPeriod = await ResolveFiscalPeriodAsync(dto.ValuationDate);
        var valuation = await BuildValuationAsync(asset, bookValue, fiscalPeriod, dto, performedByUserId);

        var existing = await _context.AssetValuations
            .Include(v => v.FixedAsset)
            .FirstOrDefaultAsync(v => v.TenantId == TenantId && v.IdempotencyKey == valuation.IdempotencyKey);
        if (existing != null)
        {
            return MapToDto(existing, existing.FixedAsset);
        }

        if (_workflowService != null)
        {
            valuation.Status = "PendingApproval";
        }

        _context.AssetValuations.Add(valuation);
        await _context.SaveChangesAsync();

        await RecordValuationAuditAsync(
            GetCalculatedAuditEvent(dto.ValuationType),
            valuation,
            afterValues: BuildValuationAuditSnapshot(valuation),
            comment: $"{dto.ValuationType} calculated for fixed asset.",
            cancellationToken: CancellationToken.None);

        if (_workflowService != null)
        {
            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("AssetValuation", valuation.Id);
            if (!workflowResult.Success)
            {
                valuation.Status = "Failed";
                valuation.FailedAt = DateTime.UtcNow;
                valuation.FailureReason = workflowResult.Message;
                await _context.SaveChangesAsync();
                await RecordValuationAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                    valuation,
                    afterValues: new { workflowResult.Status, workflowResult.Message },
                    reason: workflowResult.Message,
                    comment: "Fixed asset valuation workflow submission failed.",
                    cancellationToken: CancellationToken.None);
                throw new InvalidOperationException(workflowResult.Message ?? "Fixed asset valuation workflow could not be started.");
            }

            await RecordValuationAuditAsync(
                FinanceAuditEvents.FinanceWorkflowSubmitted,
                valuation,
                afterValues: new { valuation.Status, workflowResult.WorkflowInstanceId },
                comment: $"{dto.ValuationType} submitted for workflow approval.",
                cancellationToken: CancellationToken.None);
        }

        return MapToDto(valuation, asset);
    }

    public async Task<BulkOperationResultDto<AssetValuationDto>> CreateBulkValuationAsync(
        CreateBulkAssetValuationDto dto, Guid performedByUserId)
    {
        if (dto.ValuationType == ValuationType.ImpairmentReversal)
        {
            throw new InvalidOperationException(
                "Impairment reversals must be created individually so each reversal identifies its source impairment and IAS 36 ceiling.");
        }

        var result = new BulkOperationResultDto<AssetValuationDto>
        {
            TotalCount = dto.FixedAssetIds.Count
        };

        var assets = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(b => b.AccountingBook)
            .Where(a => a.TenantId == TenantId && dto.FixedAssetIds.Contains(a.Id))
            .ToListAsync();

        var fiscalPeriod = await ResolveFiscalPeriodAsync(dto.ValuationDate);
        var createdValuations = new List<AssetValuation>();
        foreach (var asset in assets)
        {
            try
            {
                var bookValue = ResolveDefaultBookValue(asset);
                var fairValue = Math.Round(bookValue.NetBookValue * (1 + dto.IndexPercentage / 100m), 2);
                var singleDto = new CreateAssetValuationDto
                {
                    FixedAssetId = asset.Id,
                    ValuationDate = dto.ValuationDate,
                    ValuationType = dto.ValuationType,
                    FairValue = fairValue,
                    ValuerName = dto.ValuerName,
                    ValuationMethod = dto.ValuationMethod,
                    ValuationReportReference = dto.ValuationReportReference,
                    Reason = dto.Reason ?? $"Bulk revaluation index adjustment {dto.IndexPercentage:N2}%.",
                    Notes = dto.Notes
                };

                var valuation = await BuildValuationAsync(asset, bookValue, fiscalPeriod, singleDto, performedByUserId);
                var exists = await _context.AssetValuations
                    .AnyAsync(v => v.TenantId == TenantId && v.IdempotencyKey == valuation.IdempotencyKey);
                if (!exists)
                {
                    if (_workflowService != null)
                    {
                        valuation.Status = "PendingApproval";
                    }

                    _context.AssetValuations.Add(valuation);
                    createdValuations.Add(valuation);
                    result.SuccessfulItems.Add(MapToDto(valuation, asset));
                    result.SuccessCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Asset {asset.AssetCode}: {ex.Message}");
                result.FailureCount++;
            }
        }

        var processedIds = assets.Select(a => a.Id).ToHashSet();
        foreach (var missingId in dto.FixedAssetIds.Where(id => !processedIds.Contains(id)))
        {
            result.Errors.Add($"Asset {missingId}: Not found for this tenant");
            result.FailureCount++;
        }

        await _context.SaveChangesAsync();

        if (_workflowService != null)
        {
            foreach (var valuation in createdValuations)
            {
                var workflowResult = await _workflowService.StartApprovalWorkflowAsync("AssetValuation", valuation.Id);
                if (!workflowResult.Success)
                {
                    valuation.Status = "Failed";
                    valuation.FailedAt = DateTime.UtcNow;
                    valuation.FailureReason = workflowResult.Message;
                    result.Errors.Add($"Valuation {valuation.Id}: {workflowResult.Message}");
                    result.FailureCount++;
                    continue;
                }

                await RecordValuationAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowSubmitted,
                    valuation,
                    afterValues: new { valuation.Status, workflowResult.WorkflowInstanceId },
                    comment: $"{valuation.ValuationType} submitted for workflow approval.",
                    cancellationToken: CancellationToken.None);
            }

            await _context.SaveChangesAsync();
        }

        return result;
    }

    public async Task<IEnumerable<AssetValuationDto>> GetValuationsByAssetAsync(Guid assetId)
    {
        var asset = await _context.FixedAssets
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == assetId);

        if (asset == null)
        {
            throw new KeyNotFoundException("Fixed asset not found.");
        }

        var valuations = await _context.AssetValuations
            .Where(v => v.TenantId == TenantId && v.FixedAssetId == assetId)
            .OrderByDescending(v => v.ValuationDate)
            .ToListAsync();

        return valuations.Select(v => MapToDto(v, asset)).ToList();
    }

    public async Task<AssetValuationDto> PostValuationToGLAsync(Guid valuationId)
    {
        if (_financePostingEngine == null)
        {
            throw new InvalidOperationException("Central finance posting engine is not configured for fixed asset valuation.");
        }

        var valuation = await _context.AssetValuations
            .Include(v => v.FixedAsset)
                .ThenInclude(a => a.Category)
            .Include(v => v.FixedAsset)
                .ThenInclude(a => a.BookValues)
                    .ThenInclude(b => b.AccountingBook)
            .FirstOrDefaultAsync(v => v.TenantId == TenantId && v.Id == valuationId)
            ?? throw new KeyNotFoundException("Valuation not found.");

        var asset = valuation.FixedAsset;
        EnsureValuationTenant(asset);
        if (valuation.IsPostedToGL)
        {
            await RecordValuationAuditAsync(
                FinanceAuditEvents.DuplicatePostingAttempt,
                valuation,
                afterValues: new { valuation.JournalEntryId, valuation.PostingEventId },
                comment: "Fixed asset valuation posting retried after it was already posted.",
                cancellationToken: CancellationToken.None);
            return MapToDto(valuation, asset);
        }

        if (_workflowService != null && !string.Equals(valuation.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            var rejected = string.Equals(valuation.Status, "Rejected", StringComparison.OrdinalIgnoreCase);
            await RecordValuationAuditAsync(
                rejected
                    ? FinanceAuditEvents.FinancePostingBlockedAfterRejection
                    : FinanceAuditEvents.FinancePostingBlockedPendingApproval,
                valuation,
                afterValues: new { valuation.Status },
                reason: rejected
                    ? "Fixed asset valuation was rejected by workflow."
                    : "Fixed asset valuation has not been approved.",
                comment: "Fixed asset valuation posting blocked by workflow status.",
                cancellationToken: CancellationToken.None);
            throw new InvalidOperationException(rejected
                ? "Rejected fixed asset valuations cannot be posted."
                : "Fixed asset valuation must be approved before posting.");
        }

        var bookValue = ResolveBookValue(asset, valuation.AccountingBookId, valuation.BookClassification);
        var fiscalPeriod = await ResolveFiscalPeriodAsync(valuation.AccountingDate == default ? valuation.ValuationDate : valuation.AccountingDate);
        if (fiscalPeriod == null)
        {
            throw new InvalidOperationException("No fiscal period covers the valuation accounting date.");
        }

        var functionalCurrency = await GetFunctionalCurrencyAsync();
        try
        {
            var postingRequest = await BuildPostingRequestAsync(valuation, asset, fiscalPeriod, functionalCurrency);

            await RecordValuationAuditAsync(
                FinanceAuditEvents.FixedAssetValuationConfigurationUsed,
                valuation,
                afterValues: new
                {
                    asset.FixedAssetCategoryId,
                    asset.Category.AssetAccountId,
                    asset.Category.RevaluationSurplusAccountId,
                    asset.Category.RevaluationLossAccountId,
                    asset.Category.ImpairmentLossAccountId,
                    asset.Category.AccumulatedImpairmentAccountId,
                    asset.Category.ImpairmentReversalAccountId
                },
                comment: "Fixed asset valuation account mappings used for posting.",
                cancellationToken: CancellationToken.None);

            var postingResult = await _financePostingEngine.PostAsync(postingRequest);
            ApplyPostedValuation(valuation, asset, bookValue, postingResult);

            await _context.SaveChangesAsync();
            await RecordValuationAuditAsync(
                GetPostedAuditEvent(valuation.ValuationType),
                valuation,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: BuildValuationAuditSnapshot(valuation),
                comment: $"{valuation.ValuationType} posted through the central posting engine.",
                cancellationToken: CancellationToken.None);

            return MapToDto(valuation, asset);
        }
        catch (Exception ex)
        {
            valuation.Status = "Failed";
            valuation.FailedAt = DateTime.UtcNow;
            valuation.FailureReason = ex.Message;
            valuation.UpdatedAt = DateTime.UtcNow;
            valuation.UpdatedBy = UserName;
            await _context.SaveChangesAsync();

            var eventType = ex.Message.Contains("period is not open", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
                    ? FinanceAuditEvents.FixedAssetValuationBlockedClosedPeriod
                    : GetFailedAuditEvent(valuation.ValuationType);

            await RecordValuationAuditAsync(
                eventType,
                valuation,
                afterValues: new { error = ex.Message },
                reason: ex.Message,
                comment: "Fixed asset valuation posting failed.",
                cancellationToken: CancellationToken.None);
            throw;
        }
    }

    private async Task<FixedAsset?> LoadAssetForValuationAsync(Guid assetId)
    {
        var asset = await _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.BookValues)
                .ThenInclude(b => b.AccountingBook)
            .Include(a => a.Valuations)
            .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == assetId);

        if (_accountingBookService != null)
        {
            await _accountingBookService.EnsureTenantDefaultsAsync(CancellationToken.None);
        }

        return asset;
    }

    private async Task<AssetValuation> BuildValuationAsync(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FiscalPeriod? fiscalPeriod,
        CreateAssetValuationDto dto,
        Guid performedByUserId)
    {
        EnsureAssetEligibleForValuation(asset, dto);

        if (dto.FairValue < 0m)
        {
            throw new InvalidOperationException("Valuation amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new InvalidOperationException("A valuation reason is required.");
        }

        var carryingBefore = RoundMoney(bookValue.NetBookValue);
        var carryingAfter = RoundMoney(dto.FairValue);
        if (carryingBefore == carryingAfter)
        {
            throw new InvalidOperationException("Valuation does not change the asset carrying amount.");
        }

        decimal surplus = 0m;
        decimal deficit = 0m;
        decimal impairmentLoss = 0m;
        decimal impairmentReversal = 0m;
        decimal outstandingImpairmentBefore = 0m;
        decimal unimpairedCarryingAmountCap = 0m;
        AssetValuation? sourceImpairment = null;
        decimal surplusApplied = 0m;
        decimal revaluationLossRecognized = 0m;

        if (dto.ValuationType == ValuationType.Revaluation)
        {
            if (carryingAfter > carryingBefore)
            {
                surplus = RoundMoney(carryingAfter - carryingBefore);
            }
            else
            {
                deficit = RoundMoney(carryingBefore - carryingAfter);
                var availableSurplus = await GetAvailableRevaluationSurplusAsync(asset.Id, bookValue.BookClassification);
                surplusApplied = Math.Min(deficit, availableSurplus);
                revaluationLossRecognized = RoundMoney(deficit - surplusApplied);
            }
        }
        else if (dto.ValuationType == ValuationType.Impairment)
        {
            if (carryingAfter >= carryingBefore)
            {
                throw new InvalidOperationException("Recoverable amount must be less than carrying amount for impairment.");
            }

            impairmentLoss = RoundMoney(carryingBefore - carryingAfter);
        }
        else if (dto.ValuationType == ValuationType.ImpairmentReversal)
        {
            // IAS 36 does not permit an arbitrary upward valuation to masquerade as an
            // impairment reversal. The request must identify one posted impairment, and the
            // server derives its still-unreversed balance from immutable valuation history.
            sourceImpairment = await ResolveSourceImpairmentAsync(asset, bookValue, dto);
            outstandingImpairmentBefore = await GetOutstandingImpairmentAsync(sourceImpairment);
            if (outstandingImpairmentBefore <= 0m)
                throw new InvalidOperationException("The selected impairment has already been fully reversed.");
            if (carryingAfter <= carryingBefore)
                throw new InvalidOperationException("Recoverable amount must exceed the current carrying amount for impairment reversal.");
            if (!dto.UnimpairedCarryingAmountCap.HasValue)
                throw new InvalidOperationException("The no-prior-impairment carrying amount is required for impairment reversal.");

            unimpairedCarryingAmountCap = RoundMoney(dto.UnimpairedCarryingAmountCap.Value);
            if (unimpairedCarryingAmountCap <= carryingBefore)
                throw new InvalidOperationException("The no-prior-impairment carrying amount must exceed the current carrying amount.");

            // The lower of these independent ceilings is the maximum lawful post-reversal NBV:
            // (1) the remaining source loss and (2) IAS 36's hypothetical no-impairment NBV.
            var maximumCarryingAmount = Math.Min(
                RoundMoney(carryingBefore + outstandingImpairmentBefore),
                unimpairedCarryingAmountCap);
            if (carryingAfter > maximumCarryingAmount)
            {
                throw new InvalidOperationException(
                    $"Impairment reversal cannot increase carrying amount above {maximumCarryingAmount:N2}.");
            }

            if (string.IsNullOrWhiteSpace(dto.ValuationReportReference) || string.IsNullOrWhiteSpace(dto.ValuationMethod))
            {
                throw new InvalidOperationException(
                    "Impairment reversal requires valuation method and report-reference evidence.");
            }

            impairmentReversal = RoundMoney(carryingAfter - carryingBefore);
        }

        var accountingDate = dto.ValuationDate.Date;
        return new AssetValuation
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            FiscalPeriodId = fiscalPeriod?.Id,
            ValuationDate = dto.ValuationDate.Date,
            AccountingDate = accountingDate,
            ValuationType = dto.ValuationType,
            CarryingAmountBefore = carryingBefore,
            AccumulatedDepreciationBefore = RoundMoney(bookValue.AccumulatedDepreciation),
            NetBookValueBefore = carryingBefore,
            FairValue = carryingAfter,
            CarryingAmountAfter = carryingAfter,
            RevaluationSurplus = surplus,
            RevaluationDeficit = deficit,
            ImpairmentLoss = impairmentLoss,
            ImpairmentReversal = impairmentReversal,
            SourceImpairmentValuationId = sourceImpairment?.Id,
            OutstandingImpairmentBefore = outstandingImpairmentBefore,
            UnimpairedCarryingAmountCap = unimpairedCarryingAmountCap,
            AdjustmentAmount = RoundMoney(carryingAfter - carryingBefore),
            RevaluationSurplusApplied = surplusApplied,
            RevaluationLossRecognized = revaluationLossRecognized,
            RevisedUsefulLifeMonths = dto.RevisedUsefulLifeMonths,
            UsefulLifeMonthsBefore = bookValue.UsefulLifeMonths,
            RemainingUsefulLifeMonthsBefore = bookValue.RemainingUsefulLifeMonths,
            ValuerName = dto.ValuerName,
            ValuationMethod = dto.ValuationMethod,
            ValuationReportReference = dto.ValuationReportReference,
            Reason = dto.Reason.Trim(),
            Notes = dto.Notes,
            Status = "Calculated",
            IdempotencyKey = BuildValuationIdempotencyKey(asset.TenantId, asset.Id, bookValue.BookClassification, dto),
            PerformedByUserId = performedByUserId == Guid.Empty ? UserId ?? Guid.Empty : performedByUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        };
    }

    private async Task<FinancePostingRequestDto> BuildPostingRequestAsync(
        AssetValuation valuation,
        FixedAsset asset,
        FiscalPeriod fiscalPeriod,
        string functionalCurrency)
    {
        var category = asset.Category;
        var reference = $"VAL-{asset.AssetCode}-{valuation.ValuationDate:yyyyMMdd}";
        var lineNumber = 1;
        var lines = new List<FinancePostingLineDto>();

        if (valuation.ValuationType == ValuationType.Revaluation)
        {
            var assetAccount = await ResolveValuationAccountAsync(category.AssetAccountId, "fixed asset carrying amount account", AccountType.Asset);

            if (valuation.RevaluationSurplus > 0m)
            {
                var surplusAccount = await ResolveValuationAccountAsync(category.RevaluationSurplusAccountId, "revaluation surplus account", AccountType.Equity);
                lines.Add(CreatePostingLine(assetAccount.Id, valuation.RevaluationSurplus, 0m, "Revaluation increase - asset carrying amount", reference, lineNumber++, "FA-RevaluationAsset", valuation, functionalCurrency));
                lines.Add(CreatePostingLine(surplusAccount.Id, 0m, valuation.RevaluationSurplus, "Revaluation surplus", reference, lineNumber++, "FA-RevaluationSurplus", valuation, functionalCurrency));
            }
            else if (valuation.RevaluationDeficit > 0m)
            {
                lines.Add(CreatePostingLine(assetAccount.Id, 0m, valuation.RevaluationDeficit, "Revaluation decrease - asset carrying amount", reference, lineNumber++, "FA-RevaluationAsset", valuation, functionalCurrency));

                if (valuation.RevaluationSurplusApplied > 0m)
                {
                    var surplusAccount = await ResolveValuationAccountAsync(category.RevaluationSurplusAccountId, "revaluation surplus account", AccountType.Equity);
                    lines.Add(CreatePostingLine(surplusAccount.Id, valuation.RevaluationSurplusApplied, 0m, "Revaluation surplus utilized", reference, lineNumber++, "FA-RevaluationSurplusApplied", valuation, functionalCurrency));
                }

                if (valuation.RevaluationLossRecognized > 0m)
                {
                    var lossAccount = await ResolveValuationAccountAsync(category.RevaluationLossAccountId, "revaluation loss account", AccountType.Expense);
                    lines.Add(CreatePostingLine(lossAccount.Id, valuation.RevaluationLossRecognized, 0m, "Revaluation loss", reference, lineNumber++, "FA-RevaluationLoss", valuation, functionalCurrency));
                }
            }
        }
        else if (valuation.ValuationType == ValuationType.Impairment)
        {
            var lossAccount = await ResolveValuationAccountAsync(category.ImpairmentLossAccountId, "impairment loss account", AccountType.Expense);
            var allowanceAccount = await ResolveValuationAccountAsync(category.AccumulatedImpairmentAccountId, "accumulated impairment account", AccountType.Asset);
            lines.Add(CreatePostingLine(lossAccount.Id, valuation.ImpairmentLoss, 0m, "Impairment loss", reference, lineNumber++, "FA-ImpairmentLoss", valuation, functionalCurrency));
            lines.Add(CreatePostingLine(allowanceAccount.Id, 0m, valuation.ImpairmentLoss, "Accumulated impairment", reference, lineNumber++, "FA-AccumulatedImpairment", valuation, functionalCurrency));
        }
        else if (valuation.ValuationType == ValuationType.ImpairmentReversal)
        {
            var allowanceAccount = await ResolveValuationAccountAsync(category.AccumulatedImpairmentAccountId, "accumulated impairment account", AccountType.Asset);
            var reversalAccount = await ResolveValuationAccountAsync(category.ImpairmentReversalAccountId, "impairment reversal account", AccountType.Revenue);
            lines.Add(CreatePostingLine(allowanceAccount.Id, valuation.ImpairmentReversal, 0m, "Release accumulated impairment", reference, lineNumber++, "FA-ImpairmentRelease", valuation, functionalCurrency));
            lines.Add(CreatePostingLine(reversalAccount.Id, 0m, valuation.ImpairmentReversal, "Impairment reversal income", reference, lineNumber++, "FA-ImpairmentReversal", valuation, functionalCurrency));
        }

        if (lines.Count < 2)
        {
            throw new InvalidOperationException("Valuation posting requires at least two balanced posting lines.");
        }

        return new FinancePostingRequestDto
        {
            SourceModule = SourceModule,
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = valuation.Id,
            SourceDocumentTenantId = valuation.TenantId,
            PostingAction = valuation.ValuationType.ToString(),
            SourceDocumentReference = reference,
            Description = $"{valuation.ValuationType} for {asset.AssetCode} - {asset.Name}",
            PostingDate = valuation.AccountingDate == default ? valuation.ValuationDate.Date : valuation.AccountingDate.Date,
            FiscalPeriodId = fiscalPeriod.Id,
            JournalType = valuation.ValuationType switch
            {
                ValuationType.Revaluation => "Fixed Asset Revaluation",
                ValuationType.Impairment => "Fixed Asset Impairment",
                _ => "Fixed Asset Impairment Reversal"
            },
            BookClassification = valuation.BookClassification,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FA:Valuation:{valuation.TenantId:N}:{valuation.Id:N}:{valuation.ValuationType}",
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private static FinancePostingLineDto CreatePostingLine(
        Guid accountId,
        decimal debit,
        decimal credit,
        string description,
        string reference,
        int lineNumber,
        string tag,
        AssetValuation valuation,
        string functionalCurrency)
    {
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            DebitAmount = RoundMoney(debit),
            CreditAmount = RoundMoney(credit),
            TransactionCurrency = functionalCurrency,
            TransactionDebitAmount = RoundMoney(debit),
            TransactionCreditAmount = RoundMoney(credit),
            Description = description,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            Notes = $"FixedAssetId={valuation.FixedAssetId:N};ValuationId={valuation.Id:N};Book={valuation.BookClassification}",
            TransactionTag = tag
        };
    }

    private void ApplyPostedValuation(
        AssetValuation valuation,
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        FinancePostingResultDto postingResult)
    {
        valuation.IsPostedToGL = true;
        valuation.Status = "Posted";
        valuation.PostedDate = DateTime.UtcNow;
        valuation.PostedAt = DateTime.UtcNow;
        valuation.JournalEntryId = postingResult.JournalEntryId;
        valuation.PostingEventId = postingResult.PostingEventId;
        valuation.FailureReason = null;
        valuation.FailedAt = null;
        valuation.UpdatedAt = DateTime.UtcNow;
        valuation.UpdatedBy = UserName;

        bookValue.NetBookValue = valuation.CarryingAmountAfter;
        if (valuation.RevisedUsefulLifeMonths.HasValue)
        {
            bookValue.RemainingUsefulLifeMonths = valuation.RevisedUsefulLifeMonths.Value;
            bookValue.UsefulLifeMonths = valuation.RevisedUsefulLifeMonths.Value;
        }
        bookValue.UpdatedAt = DateTime.UtcNow;
        bookValue.UpdatedBy = UserName;

        if (bookValue.AccountingBook?.IsDefault == true ||
            bookValue.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
        {
            asset.NetBookValue = valuation.CarryingAmountAfter;
            if (valuation.RevisedUsefulLifeMonths.HasValue)
            {
                asset.UsefulLifeMonths = valuation.RevisedUsefulLifeMonths.Value;
            }
        }

        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = UserName;

        _context.AssetTransactions.Add(new AssetTransaction
        {
            TenantId = TenantId,
            FixedAssetId = asset.Id,
            AccountingBookId = bookValue.AccountingBookId,
            BookClassification = bookValue.BookClassification,
            TransactionDate = valuation.AccountingDate == default ? valuation.ValuationDate : valuation.AccountingDate,
            TransactionType = valuation.ValuationType.ToString(),
            Description = $"{valuation.ValuationType} posted through finance posting engine",
            Amount = valuation.AdjustmentAmount,
            ResultingBookValue = valuation.CarryingAmountAfter,
            RelatedEntityId = postingResult.PostingEventId,
            PerformedByUserId = UserId ?? valuation.PerformedByUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName
        });
    }

    private void EnsureAssetEligibleForValuation(FixedAsset asset, CreateAssetValuationDto dto)
    {
        EnsureValuationTenant(asset);

        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff or FixedAssetStatus.HeldForSale)
        {
            throw new InvalidOperationException("Disposed, written-off, or held-for-sale assets cannot be revalued or impaired.");
        }

        if (!IsCapitalized(asset))
        {
            throw new InvalidOperationException("Fixed asset must be capitalized before revaluation or impairment.");
        }

        var capitalizationDate = asset.CapitalizationDate ?? asset.CapitalizedAt ?? asset.PurchaseDate;
        if (dto.ValuationDate.Date < capitalizationDate.Date)
        {
            throw new InvalidOperationException("Valuation date cannot be before the asset capitalization date.");
        }
    }

    private void EnsureValuationTenant(FixedAsset asset)
    {
        if (asset.TenantId != TenantId || asset.Category.TenantId != TenantId)
        {
            throw new InvalidOperationException("Fixed asset or category belongs to another tenant.");
        }
    }

    private static bool IsCapitalized(FixedAsset asset)
        => asset.CapitalizationDate.HasValue
            || asset.CapitalizedAt.HasValue
            || asset.JournalEntryId.HasValue
            || asset.PostingEventId.HasValue
            || asset.Status is FixedAssetStatus.Capitalized or FixedAssetStatus.Active or FixedAssetStatus.FullyDepreciated;

    private FixedAssetBookValue ResolveDefaultBookValue(FixedAsset asset)
    {
        var bookValue = asset.BookValues
            .Where(b => b.TenantId == TenantId && !b.IsDeleted)
            .OrderByDescending(b => b.AccountingBook != null && b.AccountingBook.IsDefault)
            .ThenByDescending(b => b.BookClassification.Equals("IFRS", StringComparison.OrdinalIgnoreCase))
            .ThenBy(b => b.BookClassification)
            .FirstOrDefault();

        return bookValue ?? throw new InvalidOperationException("Fixed asset requires a book-value record before valuation.");
    }

    private FixedAssetBookValue ResolveBookValue(FixedAsset asset, Guid? accountingBookId, string bookClassification)
    {
        var normalized = NormalizeBookClassification(bookClassification);
        var bookValue = asset.BookValues.FirstOrDefault(b =>
            b.TenantId == TenantId &&
            !b.IsDeleted &&
            (!accountingBookId.HasValue || b.AccountingBookId == accountingBookId.Value) &&
            b.BookClassification.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        return bookValue ?? throw new InvalidOperationException("Fixed asset valuation book value was not found for this tenant.");
    }

    private async Task<decimal> GetAvailableRevaluationSurplusAsync(Guid assetId, string bookClassification)
    {
        var normalized = NormalizeBookClassification(bookClassification);
        var postedRevaluations = await _context.AssetValuations
            .AsNoTracking()
            .Where(v => v.TenantId == TenantId
                && v.FixedAssetId == assetId
                && v.BookClassification == normalized
                && v.ValuationType == ValuationType.Revaluation
                && v.IsPostedToGL
                && !v.IsDeleted)
            .ToListAsync();

        return RoundMoney(postedRevaluations.Sum(v => v.RevaluationSurplus - v.RevaluationSurplusApplied));
    }

    private async Task<AssetValuation> ResolveSourceImpairmentAsync(
        FixedAsset asset,
        FixedAssetBookValue bookValue,
        CreateAssetValuationDto dto)
    {
        if (!dto.SourceImpairmentValuationId.HasValue || dto.SourceImpairmentValuationId == Guid.Empty)
            throw new InvalidOperationException("A posted source impairment is required for impairment reversal.");

        var source = await _context.AssetValuations.FirstOrDefaultAsync(v =>
            v.TenantId == TenantId && v.Id == dto.SourceImpairmentValuationId.Value && !v.IsDeleted);
        if (source == null || source.FixedAssetId != asset.Id ||
            source.AccountingBookId != bookValue.AccountingBookId ||
            !source.BookClassification.Equals(bookValue.BookClassification, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Source impairment must belong to the same tenant, fixed asset, and accounting book.");
        }

        if (source.ValuationType != ValuationType.Impairment || !source.IsPostedToGL || source.IsCorrected)
            throw new InvalidOperationException("Only an active posted impairment can be reversed.");

        return source;
    }

    private async Task<decimal> GetOutstandingImpairmentAsync(AssetValuation source)
    {
        var reversedAmount = await _context.AssetValuations
            .AsNoTracking()
            .Where(v => v.TenantId == TenantId &&
                v.SourceImpairmentValuationId == source.Id &&
                v.ValuationType == ValuationType.ImpairmentReversal &&
                v.IsPostedToGL && !v.IsCorrected && !v.IsDeleted)
            .SumAsync(v => (decimal?)v.ImpairmentReversal) ?? 0m;

        return Math.Max(0m, RoundMoney(source.ImpairmentLoss - reversedAmount));
    }

    private async Task<Account> ResolveValuationAccountAsync(Guid? accountId, string label, params AccountType[] allowedTypes)
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

    private static string BuildValuationIdempotencyKey(
        Guid tenantId,
        Guid fixedAssetId,
        string bookClassification,
        CreateAssetValuationDto dto)
        => $"FA:Valuation:{tenantId:N}:{fixedAssetId:N}:{NormalizeBookClassification(bookClassification)}:{dto.ValuationDate:yyyyMMdd}:{dto.ValuationType}:{RoundMoney(dto.FairValue):0.00}";

    private static string NormalizeBookClassification(string? value)
        => string.IsNullOrWhiteSpace(value) ? "IFRS" : value.Trim().ToUpperInvariant();

    private static decimal RoundMoney(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string GetCalculatedAuditEvent(ValuationType type) => type switch
    {
        ValuationType.Revaluation => FinanceAuditEvents.FixedAssetRevaluationCalculated,
        ValuationType.Impairment => FinanceAuditEvents.FixedAssetImpairmentCalculated,
        _ => FinanceAuditEvents.FixedAssetImpairmentReversalCalculated
    };

    private static string GetPostedAuditEvent(ValuationType type) => type switch
    {
        ValuationType.Revaluation => FinanceAuditEvents.FixedAssetRevaluationPosted,
        ValuationType.Impairment => FinanceAuditEvents.FixedAssetImpairmentPosted,
        _ => FinanceAuditEvents.FixedAssetImpairmentReversalPosted
    };

    private static string GetFailedAuditEvent(ValuationType type) => type switch
    {
        ValuationType.Revaluation => FinanceAuditEvents.FixedAssetRevaluationPostingFailed,
        ValuationType.Impairment => FinanceAuditEvents.FixedAssetImpairmentPostingFailed,
        _ => FinanceAuditEvents.FixedAssetImpairmentReversalPostingFailed
    };

    private static object BuildValuationAuditSnapshot(AssetValuation valuation)
        => new
        {
            valuation.FixedAssetId,
            valuation.AccountingBookId,
            valuation.BookClassification,
            valuation.FiscalPeriodId,
            valuation.ValuationDate,
            valuation.AccountingDate,
            valuation.ValuationType,
            valuation.CarryingAmountBefore,
            valuation.CarryingAmountAfter,
            valuation.RevaluationSurplus,
            valuation.RevaluationDeficit,
            valuation.RevaluationSurplusApplied,
            valuation.RevaluationLossRecognized,
            valuation.ImpairmentLoss,
            valuation.ImpairmentReversal,
            valuation.SourceImpairmentValuationId,
            valuation.OutstandingImpairmentBefore,
            valuation.UnimpairedCarryingAmountCap,
            valuation.AdjustmentAmount,
            valuation.JournalEntryId,
            valuation.PostingEventId,
            valuation.Status
        };

    private async Task RecordValuationAuditAsync(
        string eventType,
        AssetValuation valuation,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        object? beforeValues = null,
        object? afterValues = null,
        string? reason = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = valuation.TenantId,
            SourceModule = "FA",
            SourceDocumentType = SourceDocumentType,
            SourceDocumentId = valuation.Id,
            PostingEventId = postingEventId ?? valuation.PostingEventId,
            JournalEntryId = journalEntryId ?? valuation.JournalEntryId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Reason = reason,
            Comment = comment,
            Resource = "Finance.FixedAssetValuation",
            ResourceId = valuation.Id.ToString()
        }, cancellationToken);
    }

    private static AssetValuationDto MapToDto(AssetValuation v, FixedAsset? asset)
    {
        return new AssetValuationDto
        {
            Id = v.Id,
            FixedAssetId = v.FixedAssetId,
            AssetCode = asset?.AssetCode,
            AssetName = asset?.Name,
            AccountingBookId = v.AccountingBookId,
            BookClassification = v.BookClassification,
            FiscalPeriodId = v.FiscalPeriodId,
            ValuationDate = v.ValuationDate,
            AccountingDate = v.AccountingDate,
            ValuationType = v.ValuationType,
            CarryingAmountBefore = v.CarryingAmountBefore,
            AccumulatedDepreciationBefore = v.AccumulatedDepreciationBefore,
            NetBookValueBefore = v.NetBookValueBefore,
            FairValue = v.FairValue,
            CarryingAmountAfter = v.CarryingAmountAfter,
            RevaluationSurplus = v.RevaluationSurplus,
            RevaluationDeficit = v.RevaluationDeficit,
            ImpairmentLoss = v.ImpairmentLoss,
            ImpairmentReversal = v.ImpairmentReversal,
            SourceImpairmentValuationId = v.SourceImpairmentValuationId,
            OutstandingImpairmentBefore = v.OutstandingImpairmentBefore,
            UnimpairedCarryingAmountCap = v.UnimpairedCarryingAmountCap,
            AdjustmentAmount = v.AdjustmentAmount,
            RevaluationSurplusApplied = v.RevaluationSurplusApplied,
            RevaluationLossRecognized = v.RevaluationLossRecognized,
            RevisedUsefulLifeMonths = v.RevisedUsefulLifeMonths,
            UsefulLifeMonthsBefore = v.UsefulLifeMonthsBefore,
            RemainingUsefulLifeMonthsBefore = v.RemainingUsefulLifeMonthsBefore,
            ValuerName = v.ValuerName,
            ValuationMethod = v.ValuationMethod,
            ValuationReportReference = v.ValuationReportReference,
            Reason = v.Reason,
            Notes = v.Notes,
            IsPostedToGL = v.IsPostedToGL,
            JournalEntryId = v.JournalEntryId,
            PostingEventId = v.PostingEventId,
            IsCorrected = v.IsCorrected,
            CorrectionId = v.CorrectionId,
            CorrectedAt = v.CorrectedAt,
            Status = v.Status,
            IdempotencyKey = v.IdempotencyKey,
            PostedDate = v.PostedDate,
            PostedAt = v.PostedAt,
            FailedAt = v.FailedAt,
            FailureReason = v.FailureReason,
            CreatedAt = v.CreatedAt
        };
    }
}
