using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.MultiCurrency;

/// <summary>
/// Central FX accounting service. Realized settlement FX and unrealized revaluation journals
/// are posted only through <see cref="IFinancePostingEngine"/>.
/// </summary>
public sealed class CurrencyRevaluationService : ICurrencyRevaluationService, IFxAccountingService
{
    private const string PostedStatus = "Posted";
    private const string PostingRecoveryRequiredStatus = "PostingRecoveryRequired";
    private const string SourceModuleFx = "FX";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly IFinancePostingEngine _financePostingEngine;
    private readonly ILogger<CurrencyRevaluationService> _logger;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IFinancePaymentDimensionAdapter? _paymentDimensions;

    public CurrencyRevaluationService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITenantSettingsService tenantSettingsService,
        IFinancePostingEngine financePostingEngine,
        ILogger<CurrencyRevaluationService> logger,
        IFinanceAuditService? financeAuditService = null,
        IFinancePaymentDimensionAdapter? paymentDimensions = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _tenantSettingsService = tenantSettingsService;
        _financePostingEngine = financePostingEngine;
        _logger = logger;
        _financeAuditService = financeAuditService;
        _paymentDimensions = paymentDimensions;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<JournalEntry> RunCurrencyRevaluationAsync(
        RevaluationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batch = await RunUnrealizedRevaluationAsync(request, cancellationToken);
        if (batch.JournalEntryId.HasValue)
        {
            var journal = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(
                    j => j.TenantId == TenantId && j.Id == batch.JournalEntryId.Value && !j.IsDeleted,
                    cancellationToken);

            if (journal != null)
            {
                return journal;
            }
        }

        return BuildPreviewJournal(batch, request);
    }

    public async Task<CurrencyRevaluationPreviewDto> PreviewCurrencyRevaluationAsync(
        RevaluationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var previewRequest = new RevaluationRequestDto
        {
            RevaluationDate = request.RevaluationDate,
            RevaluationType = request.RevaluationType,
            AccountingBookCode = request.AccountingBookCode,
            CurrencyCode = request.CurrencyCode,
            UnrealizedGainLossAccountId = request.UnrealizedGainLossAccountId,
            ExpectedPreviewFingerprint = null,
            PreviewOnly = true
        };
        var batch = await RunUnrealizedRevaluationAsync(previewRequest, cancellationToken);
        var accountIds = batch.Lines.Select(line => line.AccountId).Distinct().ToArray();
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(account => account.TenantId == TenantId && accountIds.Contains(account.Id))
            .Select(account => new { account.Id, account.AccountNumber, account.AccountName })
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        return new CurrencyRevaluationPreviewDto
        {
            BatchNumber = batch.BatchNumber,
            RevaluationDate = batch.RevaluationDate,
            FunctionalCurrencyCode = batch.FunctionalCurrencyCode,
            AccountingBookId = batch.AccountingBookId,
            AccountingBookCode = batch.AccountingBookCode,
            AccountingBookName = batch.AccountingBook?.Name ?? batch.AccountingBookCode,
            TotalGainAmount = batch.TotalGainAmount,
            TotalLossAmount = batch.TotalLossAmount,
            NetGainLossAmount = batch.NetGainLossAmount,
            ExposureCount = batch.Lines.Count,
            PreviewFingerprint = BuildPreviewFingerprint(batch),
            Lines = batch.Lines
                .OrderBy(line => line.SourceModule)
                .ThenBy(line => line.AccountId)
                .ThenBy(line => line.TransactionCurrency)
                .Select(line =>
                {
                    accounts.TryGetValue(line.AccountId, out var account);
                    return new CurrencyRevaluationPreviewLineDto
                    {
                        AccountId = line.AccountId,
                        AccountNumber = account?.AccountNumber ?? line.AccountId.ToString("N")[..8],
                        AccountName = account?.AccountName ?? line.SourceModule,
                        AccountAccountingBookId = line.AccountAccountingBookId,
                        AccountBookCurrencyPolicyId = line.AccountBookCurrencyPolicyId,
                        AccountClassificationId = line.AccountClassificationId,
                        AccountClassificationCode = line.AccountClassificationCode,
                        AccountClassificationName = line.AccountClassificationName,
                        CoreAccountType = line.CoreAccountType,
                        NormalBalanceLabel = line.CoreAccountType is "Liability" or "Equity" or "Revenue" ? "Credit" : "Debit",
                        ClassificationDefault = line.ClassificationDefault,
                        RevaluationOverride = line.RevaluationOverride,
                        EffectiveRevaluationRequired = line.EffectiveRevaluationRequired,
                        EffectivePolicySource = line.EffectivePolicySource,
                        HasGovernanceWarning = line.HasGovernanceWarning,
                        GovernanceWarning = line.GovernanceWarning,
                        SourceModule = line.SourceModule,
                        TransactionCurrency = line.TransactionCurrency,
                        FunctionalCurrencyCode = line.FunctionalCurrencyCode,
                        ForeignCurrencyBalance = line.ForeignCurrencyBalance,
                        CarryingFunctionalAmount = line.CarryingFunctionalAmount,
                        PriorUnreversedAdjustment = line.PriorUnreversedAdjustment,
                        PreviousRate = line.ForeignCurrencyBalance == 0m
                            ? 0m
                            : decimal.Round(line.CarryingFunctionalAmount / line.ForeignCurrencyBalance, 6, MidpointRounding.AwayFromZero),
                        ClosingExchangeRate = line.ClosingExchangeRate,
                        RevaluedFunctionalAmount = line.RevaluedFunctionalAmount,
                        GainLossAmount = line.GainLossAmount,
                        GainLossType = line.GainLossType,
                        RevaluationFrequency = line.Notes?.Split('|').ElementAtOrDefault(1)?.Trim() ?? string.Empty,
                        RateType = line.ClosingRateType,
                        QuoteSide = line.ClosingQuoteSide,
                        ClosingExchangeRateId = line.ClosingExchangeRateId,
                        ClosingRateDate = line.ClosingRateDate
                    };
                })
                .ToList()
        };
    }

    public async Task<IReadOnlyList<JournalEntry>> GetRevaluationHistoryAsync(
        DateTime startDate,
        DateTime endDate,
        string? currencyCode = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var normalizedCurrency = NormalizeOptionalCurrency(currencyCode);

        var query = _context.FxRevaluationBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && !b.IsDeleted
                && b.RevaluationDate.Date >= startDate.Date
                && b.RevaluationDate.Date <= endDate.Date
                && b.JournalEntryId.HasValue)
            .Include(b => b.Lines)
            .Include(b => b.JournalEntry)
                .ThenInclude(j => j!.Transactions)
            .OrderByDescending(b => b.RevaluationDate);

        var batches = await query.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            batches = batches
                .Where(b => b.Lines.Any(l => l.TransactionCurrency == normalizedCurrency))
                .ToList();
        }

        return batches
            .Select(b => b.JournalEntry)
            .Where(j => j != null)
            .Cast<JournalEntry>()
            .ToList();
    }

    public async Task<IReadOnlyList<FxRevaluationBatchSummaryDto>> GetRevaluationBatchesAsync(
        DateTime startDate,
        DateTime endDate,
        string? currencyCode = null,
        CancellationToken cancellationToken = default)
    {
        if (endDate.Date < startDate.Date)
        {
            throw new ArgumentException("History end date cannot be before the start date.");
        }

        var tenantId = TenantId;
        var normalizedCurrency = NormalizeOptionalCurrency(currencyCode);
        var batches = await _context.FxRevaluationBatches
            .AsNoTracking()
            .Where(batch => batch.TenantId == tenantId
                && !batch.IsDeleted
                && batch.RevaluationDate.Date >= startDate.Date
                && batch.RevaluationDate.Date <= endDate.Date)
            .Include(batch => batch.Lines)
            .Include(batch => batch.JournalEntry)
            .Include(batch => batch.ReversalJournalEntry)
            .OrderByDescending(batch => batch.RevaluationDate)
            .ThenByDescending(batch => batch.PostedAt)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            batches = batches.Where(batch => batch.Lines.Any(line => line.TransactionCurrency == normalizedCurrency)).ToList();
        }

        return batches.Select(batch => new FxRevaluationBatchSummaryDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            RevaluationDate = batch.RevaluationDate,
            Status = batch.Status,
            FunctionalCurrencyCode = batch.FunctionalCurrencyCode,
            AccountingBookId = batch.AccountingBookId,
            AccountingBookCode = batch.AccountingBookCode,
            Currencies = batch.Lines.Select(line => line.TransactionCurrency).Distinct().OrderBy(code => code).ToList(),
            ExposureCount = batch.Lines.Count,
            NonstandardPolicyCount = batch.Lines.Count(line => line.HasGovernanceWarning),
            TotalGainAmount = batch.TotalGainAmount,
            TotalLossAmount = batch.TotalLossAmount,
            NetGainLossAmount = batch.NetGainLossAmount,
            JournalEntryId = batch.JournalEntryId,
            JournalEntryNumber = batch.JournalEntry?.JournalEntryNumber,
            ReversalJournalEntryId = batch.ReversalJournalEntryId,
            ReversalJournalEntryNumber = batch.ReversalJournalEntry?.JournalEntryNumber,
            PostedAt = batch.PostedAt,
            ReversedAt = batch.ReversedAt
        }).ToList();
    }

    public async Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForApPaymentAsync(
        Guid vendorPaymentId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var payment = await _context.Set<VendorPayment>()
            .Include(p => p.Allocations)
                .ThenInclude(a => a.VendorInvoice)
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Id == vendorPaymentId && !p.IsDeleted,
                cancellationToken);

        if (payment == null)
        {
            throw new KeyNotFoundException($"Vendor payment with Id '{vendorPaymentId}' was not found.");
        }

        var settings = await GetFinanceSettingsAsync(tenantId, cancellationToken);
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, await _tenantSettingsService.GetBaseCurrencyAsync());
        var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
        var requiresFx = !IsFunctionalCurrency(paymentCurrency, functionalCurrency)
            || payment.Allocations.Any(a => !a.IsReversal &&
                (a.IsCrossCurrency || !IsFunctionalCurrency(
                    NormalizeCurrency(a.VendorInvoice?.CurrencyCode, functionalCurrency),
                    functionalCurrency)));
        if (!requiresFx)
        {
            return Array.Empty<FxRealizedSettlement>();
        }

        if (!payment.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("AP payment must be posted before realized FX can be calculated.");
        }

        var results = new List<FxRealizedSettlement>();
        foreach (var allocation in payment.Allocations.Where(a => !a.IsReversal).OrderBy(a => a.AllocationDate).ThenBy(a => a.Id))
        {
            var existing = await LoadExistingRealizedSettlementAsync(
                tenantId,
                "VendorPayment",
                payment.Id,
                allocation.Id,
                cancellationToken);
            if (existing != null)
            {
                results.Add(existing);
                continue;
            }

            try
            {
                var settlement = await PostApAllocationRealizedFxAsync(
                    tenantId,
                    settings,
                    functionalCurrency,
                    payment,
                    allocation,
                    paymentCurrency,
                    cancellationToken);

                if (settlement != null)
                {
                    results.Add(settlement);
                }
            }
            catch (Exception ex)
            {
                await RecordFxAuditAsync(
                    FinanceAuditEvents.RealizedFxPostingFailed,
                    tenantId,
                    "AP",
                    "VendorPaymentAllocation",
                    allocation.Id,
                    reason: ex.Message,
                    afterValues: new { PaymentId = payment.Id, AllocationId = allocation.Id, error = ex.Message },
                    cancellationToken: cancellationToken);
                throw;
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForArReceiptAsync(
        Guid customerPaymentId,
        CancellationToken cancellationToken = default)
    {
        return await PostRealizedFxForArReceiptAsync(
            customerPaymentId,
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceArCustomerPayment),
            cancellationToken);
    }

    public async Task<IReadOnlyList<FxRealizedSettlement>> PostRealizedFxForArReceiptAsync(
        Guid customerPaymentId,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.Definition.Id is not (
            FinanceDimensionRouteId.FinanceArCustomerPayment or
            FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleReceipt))
        {
            throw new InvalidOperationException(
                $"Route '{producer.Definition.SourceRoute}' is not authorized for AR realized FX posting.");
        }

        var tenantId = TenantId;
        var payment = await _context.Set<CustomerPayment>()
            .Include(p => p.Allocations)
                .ThenInclude(a => a.Invoice)
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.Id == customerPaymentId && !p.IsDeleted,
                cancellationToken);

        if (payment == null)
        {
            throw new KeyNotFoundException($"Customer payment with Id '{customerPaymentId}' was not found.");
        }

        if (payment.IsCreditNote)
        {
            return Array.Empty<FxRealizedSettlement>();
        }

        var settings = await GetFinanceSettingsAsync(tenantId, cancellationToken);
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, await _tenantSettingsService.GetBaseCurrencyAsync());
        var paymentCurrency = NormalizeCurrency(payment.CurrencyCode, functionalCurrency);
        var requiresFx = !IsFunctionalCurrency(paymentCurrency, functionalCurrency)
            || payment.Allocations.Any(a => !a.IsReversal &&
                (a.IsCrossCurrency || !IsFunctionalCurrency(
                    NormalizeCurrency(a.Invoice?.CurrencyCode, functionalCurrency),
                    functionalCurrency)));
        if (!requiresFx)
        {
            return Array.Empty<FxRealizedSettlement>();
        }

        if (!payment.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("AR receipt must be posted before realized FX can be calculated.");
        }

        var results = new List<FxRealizedSettlement>();
        foreach (var allocation in payment.Allocations.Where(a => !a.IsReversal).OrderBy(a => a.AllocationDate).ThenBy(a => a.Id))
        {
            var existing = await LoadExistingRealizedSettlementAsync(
                tenantId,
                "CustomerPayment",
                payment.Id,
                allocation.Id,
                cancellationToken);
            if (existing != null)
            {
                results.Add(existing);
                continue;
            }

            try
            {
                var settlement = await PostArAllocationRealizedFxAsync(
                    tenantId,
                    settings,
                    functionalCurrency,
                    payment,
                    allocation,
                    paymentCurrency,
                    producer,
                    cancellationToken);

                if (settlement != null)
                {
                    results.Add(settlement);
                }
            }
            catch (Exception ex)
            {
                await RecordFxAuditAsync(
                    FinanceAuditEvents.RealizedFxPostingFailed,
                    tenantId,
                    "AR",
                    "PaymentAllocation",
                    allocation.Id,
                    reason: ex.Message,
                    afterValues: new { PaymentId = payment.Id, AllocationId = allocation.Id, error = ex.Message },
                    cancellationToken: cancellationToken);
                throw;
            }
        }

        return results;
    }

    public async Task<FxRevaluationBatch> RunUnrealizedRevaluationAsync(
        RevaluationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = TenantId;
        var revaluationDate = request.RevaluationDate.Date;
        var settings = await GetFinanceSettingsAsync(tenantId, cancellationToken);
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, await _tenantSettingsService.GetBaseCurrencyAsync());
        var fiscalPeriod = await ResolveFiscalPeriodAsync(tenantId, revaluationDate, cancellationToken);
        var scope = ResolveRevaluationScope(request);
        var accountingBook = await ResolveRevaluationBookAsync(tenantId, request.AccountingBookCode, cancellationToken);
        var expectedPreviewFingerprint = request.ExpectedPreviewFingerprint;

        if (!request.PreviewOnly)
        {
            var existing = await _context.FxRevaluationBatches
                .Include(b => b.Lines)
                .FirstOrDefaultAsync(
                    b => b.TenantId == tenantId
                        && !b.IsDeleted
                        && b.AccountingBookId == accountingBook.Id
                        && b.Scope == scope
                        && b.RevaluationDate.Date == revaluationDate
                        && b.FiscalPeriodId == fiscalPeriod.Id,
                    cancellationToken);

            if (existing != null)
            {
                ValidateFrozenPreviewFingerprint(
                    existing,
                    RequireCanonicalPreviewFingerprint(expectedPreviewFingerprint));
                if (existing.Status is PostedStatus or "NoAdjustment" or "Reversed")
                {
                    return existing;
                }

                return await FinalizePersistedRevaluationAsync(existing, cancellationToken);
            }
        }

        var unrealizedGainAccount = await ResolveRequiredFxAccountAsync(
            tenantId,
            settings.UnrealizedFxGainAccountId ?? settings.UnrealizedGainLossAccountId,
            "unrealized FX gain account",
            FinanceAuditEvents.FxPostingBlockedInvalidConfiguration,
            cancellationToken);
        var unrealizedLossAccount = await ResolveRequiredFxAccountAsync(
            tenantId,
            settings.UnrealizedFxLossAccountId,
            "unrealized FX loss account",
            FinanceAuditEvents.FxPostingBlockedInvalidConfiguration,
            cancellationToken);

        var exposures = await BuildPostedExposureBasisAsync(
            tenantId,
            functionalCurrency,
            request.CurrencyCode,
            revaluationDate,
            request.RevaluationType,
            accountingBook,
            settings,
            cancellationToken);

        var batch = new FxRevaluationBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = $"FXR-{revaluationDate:yyyyMMdd}-{DateTime.UtcNow:HHmmssfff}",
            RevaluationDate = revaluationDate,
            FiscalPeriodId = fiscalPeriod.Id,
            AccountingBookId = accountingBook.Id,
            AccountingBookCode = accountingBook.Code,
            AccountingBook = accountingBook,
            Scope = scope,
            FunctionalCurrencyCode = functionalCurrency,
            Status = request.PreviewOnly ? "Preview" : "Calculated",
            IdempotencyKey = $"FX:Revaluation:{tenantId:N}:{accountingBook.Id:N}:{scope}:{revaluationDate:yyyyMMdd}:{fiscalPeriod.Id:N}",
            AutoReverseNextPeriod = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName
        };

        foreach (var exposure in exposures)
        {
            var closingRate = await ResolveClosingRateAsync(
                tenantId,
                functionalCurrency,
                exposure.TransactionCurrency,
                revaluationDate,
                exposure.RateType,
                exposure.QuoteSide,
                cancellationToken);

            var calculation = FxRevaluationMath.Calculate(
                exposure.ForeignCurrencyBalance,
                exposure.CarryingFunctionalAmount,
                closingRate.Rate,
                exposure.PriorUnreversedAdjustment);
            var revaluedFunctionalAmount = calculation.TargetFunctionalValue;
            var gainLossAmount = calculation.Delta;
            if (gainLossAmount == 0m)
            {
                continue;
            }

            var gainLossType = calculation.GainLossType;
            var gainLossAccountId = gainLossType == "Gain"
                ? unrealizedGainAccount.Id
                : unrealizedLossAccount.Id;

            batch.Lines.Add(new FxRevaluationLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SourceModule = exposure.SourceModule,
                SourceDocumentType = exposure.SourceDocumentType,
                SourceDocumentId = exposure.SourceDocumentId,
                AccountId = exposure.AccountId,
                AccountAccountingBookId = exposure.AccountAccountingBookId,
                AccountBookCurrencyPolicyId = exposure.AccountBookCurrencyPolicyId,
                AccountClassificationId = exposure.AccountClassificationId,
                AccountClassificationCode = exposure.AccountClassificationCode,
                AccountClassificationName = exposure.AccountClassificationName,
                CoreAccountType = exposure.CoreAccountType.ToString(),
                ClassificationDefault = exposure.ClassificationDefault.ToString(),
                RevaluationOverride = exposure.RevaluationOverride,
                EffectiveRevaluationRequired = true,
                EffectivePolicySource = exposure.EffectivePolicySource,
                HasGovernanceWarning = exposure.HasGovernanceWarning,
                GovernanceWarning = exposure.GovernanceWarning,
                TransactionCurrency = exposure.TransactionCurrency,
                FunctionalCurrencyCode = functionalCurrency,
                ForeignCurrencyBalance = exposure.ForeignCurrencyBalance,
                CarryingFunctionalAmount = exposure.CarryingFunctionalAmount,
                PriorUnreversedAdjustment = exposure.PriorUnreversedAdjustment,
                ClosingExchangeRateId = closingRate.Id,
                ClosingExchangeRate = closingRate.Rate,
                ClosingRateDate = closingRate.EffectiveDate,
                ClosingRateType = closingRate.RateType.ToString(),
                ClosingQuoteSide = closingRate.QuoteSide.ToString(),
                RevaluedFunctionalAmount = revaluedFunctionalAmount,
                GainLossAmount = gainLossAmount,
                GainLossType = gainLossType,
                GainLossAccountId = gainLossAccountId,
                ClosingExchangeRateRecord = closingRate,
                Notes = $"Closing rate {closingRate.Rate} effective {closingRate.EffectiveDate:yyyy-MM-dd} | {exposure.Frequency}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserName
            });
        }

        UpdateBatchTotals(batch);

        var previewFingerprint = BuildPreviewFingerprint(batch);
        batch.PreviewFingerprint = previewFingerprint;
        if (!request.PreviewOnly
            && !FingerprintsMatch(
                previewFingerprint,
                RequireCanonicalPreviewFingerprint(expectedPreviewFingerprint)))
        {
            throw new InvalidOperationException("Revaluation exposures or closing rates changed after preview. Run preview again before posting.");
        }

        if (request.PreviewOnly)
        {
            return batch;
        }

        _context.FxRevaluationBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordFxAuditAsync(
            FinanceAuditEvents.UnrealizedRevaluationBatchCreated,
            tenantId,
            SourceModuleFx,
            "FxRevaluationBatch",
            batch.Id,
            afterValues: BuildRevaluationAuditSnapshot(batch),
            cancellationToken: cancellationToken);

        await RecordFxAuditAsync(
            FinanceAuditEvents.UnrealizedRevaluationCalculated,
            tenantId,
            SourceModuleFx,
            "FxRevaluationBatch",
            batch.Id,
            afterValues: BuildRevaluationAuditSnapshot(batch),
            cancellationToken: cancellationToken);

        if (!batch.Lines.Any())
        {
            batch.Status = "NoAdjustment";
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUserService.UserName;
            await _context.SaveChangesAsync(cancellationToken);
            return batch;
        }

        return await FinalizePersistedRevaluationAsync(batch, cancellationToken);
    }

    public async Task<FxRevaluationBatch> ReverseRevaluationBatchAsync(
        Guid batchId,
        DateTime reversalDate,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reversal reason is required.", nameof(reason));
        }

        var tenantId = TenantId;
        var batch = await _context.FxRevaluationBatches
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(
                b => b.TenantId == tenantId && b.Id == batchId && !b.IsDeleted,
                cancellationToken);

        if (batch == null)
        {
            throw new KeyNotFoundException($"FX revaluation batch with Id '{batchId}' was not found.");
        }

        if (batch.ReversalPostingEventId.HasValue && batch.ReversalJournalEntryId.HasValue)
        {
            return batch;
        }

        if (batch.Status != PostedStatus || !batch.PostingEventId.HasValue || !batch.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Only posted FX revaluation batches can be reversed.");
        }

        var plan = await _financePostingEngine.GetReversalPlanAsync(
            batch.PostingEventId.Value,
            reason,
            reversalDate.Date,
            cancellationToken);

        var postingRequest = new FinancePostingRequestV2Dto
        {
            SourceModule = SourceModuleFx,
            SourceDocumentType = "FxRevaluationBatch",
            SourceDocumentId = batch.Id,
            SourceDocumentTenantId = batch.TenantId,
            PostingAction = "ReverseUnrealizedRevaluation",
            SourceDocumentReference = batch.BatchNumber,
            Description = $"Reverse FX revaluation {batch.BatchNumber}",
            PostingDate = reversalDate.Date,
            JournalType = "FX Revaluation Reversal",
            AccountingBookCode = batch.AccountingBookCode,
            FunctionalCurrencyCode = batch.FunctionalCurrencyCode,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = reason.Trim(),
            ReversalType = "Automatic",
            IdempotencyKey = $"FX:Revaluation:{tenantId:N}:{batch.Id:N}:Reverse:{reversalDate:yyyyMMdd}",
            ReturnExistingOnDuplicate = true,
            Lines = plan.ReversalLines
        };

        var result = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);
        batch.ReversalJournalEntryId = result.JournalEntryId;
        batch.ReversalPostingEventId = result.PostingEventId;
        batch.ReversedAt = DateTime.UtcNow;
        batch.Status = "Reversed";
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = _currentUserService.UserName;
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFxAuditAsync(
            FinanceAuditEvents.UnrealizedRevaluationReversed,
            tenantId,
            SourceModuleFx,
            "FxRevaluationBatch",
            batch.Id,
            postingEventId: result.PostingEventId,
            journalEntryId: result.JournalEntryId,
            reason: reason,
            afterValues: new
            {
                batch.Id,
                batch.JournalEntryId,
                batch.PostingEventId,
                batch.ReversalJournalEntryId,
                batch.ReversalPostingEventId
            },
            cancellationToken: cancellationToken);

        return batch;
    }

    private async Task<FxRealizedSettlement?> PostApAllocationRealizedFxAsync(
        Guid tenantId,
        FinanceSettings settings,
        string functionalCurrency,
        VendorPayment payment,
        VendorPaymentAllocation allocation,
        string paymentCurrency,
        CancellationToken cancellationToken)
    {
        if (allocation.VendorInvoice == null || allocation.VendorInvoice.TenantId != tenantId)
        {
            throw new InvalidOperationException("AP payment allocation references an invoice from another tenant.");
        }

        if (!allocation.VendorInvoice.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("AP payment allocation references an unposted invoice.");
        }

        var invoiceCurrency = NormalizeCurrency(allocation.VendorInvoice.CurrencyCode, paymentCurrency);
        var settledForeignAmount = RoundMoney(allocation.AllocatedAmount + allocation.DiscountAmount + allocation.WithholdingTaxAmount);
        if (settledForeignAmount <= 0m)
        {
            return null;
        }

        // A functional-currency invoice deliberately has no foreign-rate columns on its posted
        // control line. It can still be settled from a foreign bank account, however, and the
        // difference between the functional liability and cash surrendered is a real FX result.
        // Resolve that control basis at historical rate 1 instead of sending it through the
        // foreign-exposure lookup, which correctly requires rate snapshots.
        var basis = IsFunctionalCurrency(invoiceCurrency, functionalCurrency)
            ? await ResolveFunctionalCurrencySettlementBasisAsync(
                tenantId,
                invoiceCurrency,
                allocation.SettlementFunctionalAmount,
                settledForeignAmount,
                invoiceJournalEntryId: allocation.VendorInvoice.JournalEntryId.Value,
                invoiceDocumentType: "VendorInvoice",
                invoiceDocumentId: allocation.VendorInvoiceId,
                invoiceDebitSide: false,
                settlementJournalEntryId: payment.JournalEntryId!.Value,
                settlementDocumentType: "VendorPayment",
                settlementDocumentId: payment.Id,
                settlementDebitSide: true,
                cancellationToken)
            : await ResolveSettlementBasisAsync(
                tenantId,
                invoiceCurrency,
                invoiceJournalEntryId: allocation.VendorInvoice.JournalEntryId.Value,
                invoiceDocumentType: "VendorInvoice",
                invoiceDocumentId: allocation.VendorInvoiceId,
                invoiceDebitSide: false,
                settlementJournalEntryId: payment.JournalEntryId!.Value,
                settlementDocumentType: "VendorPayment",
                settlementDocumentId: payment.Id,
                settlementDebitSide: true,
                cancellationToken);

        var historicalFunctionalAmount = RoundMoney(settledForeignAmount * basis.HistoricalRate);
        // Cross-currency settlement cannot be reconstructed by multiplying the invoice amount by
        // the payment rate. The allocation freezes the actual functional value of cash plus
        // deductions, which is the correct comparison against the historical AP carrying value.
        var settlementFunctionalAmount = allocation.SettlementFunctionalAmount > 0m
            ? RoundMoney(allocation.SettlementFunctionalAmount)
            : RoundMoney(settledForeignAmount * basis.SettlementRate);
        var delta = RoundMoney(settlementFunctionalAmount - historicalFunctionalAmount);
        if (delta == 0m)
        {
            return null;
        }

        var gainLossType = delta > 0m ? "Loss" : "Gain";
        var gainLossAccount = gainLossType == "Gain"
            ? await ResolveRequiredFxAccountAsync(tenantId, settings.RealizedFxGainAccountId, "realized FX gain account", FinanceAuditEvents.FxPostingBlockedInvalidConfiguration, cancellationToken)
            : await ResolveRequiredFxAccountAsync(tenantId, settings.RealizedFxLossAccountId, "realized FX loss account", FinanceAuditEvents.FxPostingBlockedInvalidConfiguration, cancellationToken);

        var amount = Math.Abs(delta);
        var dimensionEvidence = await RequireApRealizedFxDimensionEvidenceAsync(
            payment.Id, allocation.Id, delta, cancellationToken);
        var postingLines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        if (gainLossType == "Gain")
            postingLines.Add(BuildFunctionalPostingLine(
                basis.ControlAccountId, $"AP realized FX gain - {payment.PaymentNumber}",
                amount, 0m, lineNumber++, "FX-AP-Control"));
        foreach (var component in dimensionEvidence)
        {
            var componentAmount = Math.Abs(component.FunctionalAmount);
            var dimensions = _paymentDimensions is null
                ? Array.Empty<FinancePostingDimensionValueDto>()
                : await _paymentDimensions.ResolveVendorPostingDimensionsAsync(
                    component.Id, gainLossAccount.Id, payment.PaymentDate, cancellationToken);
            postingLines.Add(BuildFunctionalPostingLine(
                gainLossAccount.Id,
                $"AP realized FX {gainLossType.ToLowerInvariant()} - {payment.PaymentNumber}",
                gainLossType == "Loss" ? componentAmount : 0m,
                gainLossType == "Gain" ? componentAmount : 0m,
                lineNumber++,
                gainLossType == "Loss" ? "FX-Realized-Loss" : "FX-Realized-Gain",
                component.OriginatingSourceLineId,
                dimensions));
        }
        if (gainLossType == "Loss")
            postingLines.Add(BuildFunctionalPostingLine(
                basis.ControlAccountId, $"AP realized FX loss - {payment.PaymentNumber}",
                0m, amount, lineNumber, "FX-AP-Control"));

        await RecordFxAuditAsync(
            FinanceAuditEvents.RealizedFxCalculated,
            tenantId,
            "AP",
            "VendorPaymentAllocation",
            allocation.Id,
            afterValues: new
            {
                allocation.Id,
                settledForeignAmount,
                historicalFunctionalAmount,
                settlementFunctionalAmount,
                gainLossType,
                amount
            },
            cancellationToken: cancellationToken);

        var accountingBookCode = await ResolvePostedJournalBookCodeAsync(
            tenantId, allocation.VendorInvoice.JournalEntryId.Value, cancellationToken);

        var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = SourceModuleFx,
            SourceDocumentType = "VendorPaymentAllocation",
            SourceDocumentId = allocation.Id,
            SourceDocumentTenantId = tenantId,
            PostingAction = "RealizedFx",
            SourceDocumentReference = payment.PaymentNumber,
            Description = $"AP realized FX {gainLossType.ToLowerInvariant()} for {payment.PaymentNumber}",
            PostingDate = payment.PaymentDate,
            JournalType = "Realized FX",
            AccountingBookCode = accountingBookCode,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FX:Realized:AP:{tenantId:N}:{allocation.Id:N}",
            ReturnExistingOnDuplicate = true,
            Lines = postingLines
        }, cancellationToken);

        var settlement = new FxRealizedSettlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "AP",
            SettlementDocumentType = "VendorPayment",
            SettlementDocumentId = payment.Id,
            SettlementAllocationId = allocation.Id,
            InvoiceDocumentType = "VendorInvoice",
            InvoiceDocumentId = allocation.VendorInvoiceId,
            ControlAccountId = basis.ControlAccountId,
            TransactionCurrency = invoiceCurrency,
            FunctionalCurrencyCode = functionalCurrency,
            SettledForeignAmount = settledForeignAmount,
            PaymentCurrencyCode = NormalizeCurrency(allocation.PaymentCurrencyCode, paymentCurrency),
            PaymentCurrencyAmount = allocation.PaymentCurrencyAmount > 0m
                ? allocation.PaymentCurrencyAmount
                : allocation.AllocatedAmount,
            PaymentExchangeRateId = allocation.PaymentExchangeRateId,
            PaymentExchangeRate = allocation.PaymentExchangeRate > 0m
                ? allocation.PaymentExchangeRate
                : NormalizeExchangeRate(payment.ExchangeRate),
            IsCrossCurrency = allocation.IsCrossCurrency,
            HistoricalExchangeRate = basis.HistoricalRate,
            HistoricalExchangeRateId = basis.HistoricalRateId,
            SettlementExchangeRate = settledForeignAmount > 0m
                ? decimal.Round(settlementFunctionalAmount / settledForeignAmount, 6, MidpointRounding.AwayFromZero)
                : basis.SettlementRate,
            // A blended third-currency value has no single rate-master row. Payment and invoice
            // rate ids are retained separately on the allocation/FX event instead.
            SettlementExchangeRateId = allocation.IsCrossCurrency ? null : basis.SettlementRateId,
            HistoricalFunctionalAmount = historicalFunctionalAmount,
            SettlementFunctionalAmount = settlementFunctionalAmount,
            GainLossAmount = amount,
            GainLossType = gainLossType,
            GainLossAccountId = gainLossAccount.Id,
            JournalEntryId = postingResult.JournalEntryId,
            PostingEventId = postingResult.PostingEventId,
            SettlementDate = payment.PaymentDate.Date,
            PostedAt = DateTime.UtcNow,
            Status = PostedStatus,
            IdempotencyKey = $"FX:Realized:AP:{tenantId:N}:{allocation.Id:N}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName
        };

        _context.FxRealizedSettlements.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFxAuditAsync(
            FinanceAuditEvents.RealizedFxPosted,
            tenantId,
            "AP",
            "VendorPaymentAllocation",
            allocation.Id,
            postingEventId: postingResult.PostingEventId,
            journalEntryId: postingResult.JournalEntryId,
            afterValues: BuildRealizedAuditSnapshot(settlement),
            cancellationToken: cancellationToken);

        return settlement;
    }

    private async Task<FxRealizedSettlement?> PostArAllocationRealizedFxAsync(
        Guid tenantId,
        FinanceSettings settings,
        string functionalCurrency,
        CustomerPayment payment,
        PaymentAllocation allocation,
        string paymentCurrency,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken)
    {
        if (allocation.Invoice == null || allocation.Invoice.TenantId != tenantId)
        {
            throw new InvalidOperationException("AR receipt allocation references an invoice from another tenant.");
        }

        if (!allocation.Invoice.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("AR receipt allocation references an unposted invoice.");
        }

        var invoiceCurrency = NormalizeCurrency(allocation.Invoice.CurrencyCode, paymentCurrency);
        var settledForeignAmount = RoundMoney(
            allocation.AllocatedAmount + allocation.DiscountAmount +
            allocation.WithholdingTaxAmount + allocation.VatWithholdingAmount);
        if (settledForeignAmount <= 0m)
        {
            return null;
        }

        // See the AP equivalent above. Functional AR has a fixed historical basis of one, but a
        // foreign receipt can still create a settlement difference that must clear AR and reach
        // realized FX. Do not require nonexistent foreign-rate metadata from the invoice line.
        var basis = IsFunctionalCurrency(invoiceCurrency, functionalCurrency)
            ? await ResolveFunctionalCurrencySettlementBasisAsync(
                tenantId,
                invoiceCurrency,
                allocation.SettlementFunctionalAmount,
                settledForeignAmount,
                invoiceJournalEntryId: allocation.Invoice.JournalEntryId.Value,
                invoiceDocumentType: "CustomerInvoice",
                invoiceDocumentId: allocation.InvoiceId,
                invoiceDebitSide: true,
                settlementJournalEntryId: payment.JournalEntryId!.Value,
                settlementDocumentType: "CustomerPayment",
                settlementDocumentId: payment.Id,
                settlementDebitSide: false,
                cancellationToken)
            : await ResolveSettlementBasisAsync(
                tenantId,
                invoiceCurrency,
                invoiceJournalEntryId: allocation.Invoice.JournalEntryId.Value,
                invoiceDocumentType: "CustomerInvoice",
                invoiceDocumentId: allocation.InvoiceId,
                invoiceDebitSide: true,
                settlementJournalEntryId: payment.JournalEntryId!.Value,
                settlementDocumentType: "CustomerPayment",
                settlementDocumentId: payment.Id,
                settlementDebitSide: false,
                cancellationToken);

        var historicalFunctionalAmount = RoundMoney(settledForeignAmount * basis.HistoricalRate);
        var settlementFunctionalAmount = allocation.SettlementFunctionalAmount > 0m
            ? RoundMoney(allocation.SettlementFunctionalAmount)
            : RoundMoney(settledForeignAmount * basis.SettlementRate);
        var delta = RoundMoney(settlementFunctionalAmount - historicalFunctionalAmount);
        if (delta == 0m)
        {
            return null;
        }

        var gainLossType = delta > 0m ? "Gain" : "Loss";
        var gainLossAccount = gainLossType == "Gain"
            ? await ResolveRequiredFxAccountAsync(tenantId, settings.RealizedFxGainAccountId, "realized FX gain account", FinanceAuditEvents.FxPostingBlockedInvalidConfiguration, cancellationToken)
            : await ResolveRequiredFxAccountAsync(tenantId, settings.RealizedFxLossAccountId, "realized FX loss account", FinanceAuditEvents.FxPostingBlockedInvalidConfiguration, cancellationToken);

        var amount = Math.Abs(delta);
        var dimensionEvidence = await RequireArRealizedFxDimensionEvidenceAsync(
            payment.Id, allocation.Id, delta, producer, cancellationToken);
        var postingLines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        if (gainLossType == "Gain")
            postingLines.Add(BuildFunctionalPostingLine(
                basis.ControlAccountId, $"AR realized FX gain - {payment.PaymentNumber}",
                amount, 0m, lineNumber++, "FX-AR-Control"));
        foreach (var component in dimensionEvidence)
        {
            var componentAmount = Math.Abs(component.FunctionalAmount);
            var dimensions = _paymentDimensions is null
                ? Array.Empty<FinancePostingDimensionValueDto>()
                : await _paymentDimensions.ResolveCustomerPostingDimensionsAsync(
                    component.Id, gainLossAccount.Id, payment.PaymentDate, producer, cancellationToken);
            postingLines.Add(BuildFunctionalPostingLine(
                gainLossAccount.Id,
                $"AR realized FX {gainLossType.ToLowerInvariant()} - {payment.PaymentNumber}",
                gainLossType == "Loss" ? componentAmount : 0m,
                gainLossType == "Gain" ? componentAmount : 0m,
                lineNumber++,
                gainLossType == "Loss" ? "FX-Realized-Loss" : "FX-Realized-Gain",
                component.OriginatingSourceLineId,
                dimensions));
        }
        if (gainLossType == "Loss")
            postingLines.Add(BuildFunctionalPostingLine(
                basis.ControlAccountId, $"AR realized FX loss - {payment.PaymentNumber}",
                0m, amount, lineNumber, "FX-AR-Control"));

        await RecordFxAuditAsync(
            FinanceAuditEvents.RealizedFxCalculated,
            tenantId,
            "AR",
            "PaymentAllocation",
            allocation.Id,
            afterValues: new
            {
                allocation.Id,
                settledForeignAmount,
                historicalFunctionalAmount,
                settlementFunctionalAmount,
                gainLossType,
                amount
            },
            cancellationToken: cancellationToken);

        var accountingBookCode = await ResolvePostedJournalBookCodeAsync(
            tenantId, allocation.Invoice.JournalEntryId.Value, cancellationToken);

        var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = SourceModuleFx,
            SourceDocumentType = "PaymentAllocation",
            SourceDocumentId = allocation.Id,
            SourceDocumentTenantId = tenantId,
            PostingAction = "RealizedFx",
            SourceDocumentReference = payment.PaymentNumber,
            Description = $"AR realized FX {gainLossType.ToLowerInvariant()} for {payment.PaymentNumber}",
            PostingDate = payment.PaymentDate,
            JournalType = "Realized FX",
            AccountingBookCode = accountingBookCode,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"FX:Realized:AR:{tenantId:N}:{allocation.Id:N}",
            ReturnExistingOnDuplicate = true,
            Lines = postingLines
        }, cancellationToken);

        var settlement = new FxRealizedSettlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = "AR",
            SettlementDocumentType = "CustomerPayment",
            SettlementDocumentId = payment.Id,
            SettlementAllocationId = allocation.Id,
            InvoiceDocumentType = "CustomerInvoice",
            InvoiceDocumentId = allocation.InvoiceId,
            ControlAccountId = basis.ControlAccountId,
            TransactionCurrency = invoiceCurrency,
            FunctionalCurrencyCode = functionalCurrency,
            SettledForeignAmount = settledForeignAmount,
            PaymentCurrencyCode = NormalizeCurrency(allocation.PaymentCurrencyCode, paymentCurrency),
            PaymentCurrencyAmount = allocation.PaymentCurrencyAmount > 0m
                ? allocation.PaymentCurrencyAmount
                : allocation.AllocatedAmount,
            PaymentExchangeRateId = allocation.PaymentExchangeRateId,
            PaymentExchangeRate = allocation.PaymentExchangeRate > 0m
                ? allocation.PaymentExchangeRate
                : NormalizeExchangeRate(payment.ExchangeRate),
            IsCrossCurrency = allocation.IsCrossCurrency,
            HistoricalExchangeRate = basis.HistoricalRate,
            HistoricalExchangeRateId = basis.HistoricalRateId,
            SettlementExchangeRate = settledForeignAmount > 0m
                ? decimal.Round(settlementFunctionalAmount / settledForeignAmount, 6, MidpointRounding.AwayFromZero)
                : basis.SettlementRate,
            SettlementExchangeRateId = allocation.IsCrossCurrency ? null : basis.SettlementRateId,
            HistoricalFunctionalAmount = historicalFunctionalAmount,
            SettlementFunctionalAmount = settlementFunctionalAmount,
            GainLossAmount = amount,
            GainLossType = gainLossType,
            GainLossAccountId = gainLossAccount.Id,
            JournalEntryId = postingResult.JournalEntryId,
            PostingEventId = postingResult.PostingEventId,
            SettlementDate = payment.PaymentDate.Date,
            PostedAt = DateTime.UtcNow,
            Status = PostedStatus,
            IdempotencyKey = $"FX:Realized:AR:{tenantId:N}:{allocation.Id:N}",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserName
        };

        _context.FxRealizedSettlements.Add(settlement);
        await _context.SaveChangesAsync(cancellationToken);

        await RecordFxAuditAsync(
            FinanceAuditEvents.RealizedFxPosted,
            tenantId,
            "AR",
            "PaymentAllocation",
            allocation.Id,
            postingEventId: postingResult.PostingEventId,
            journalEntryId: postingResult.JournalEntryId,
            afterValues: BuildRealizedAuditSnapshot(settlement),
            cancellationToken: cancellationToken);

        return settlement;
    }

    /// <summary>
    /// Resolves AP/AR control-account identity for a functional-currency invoice settled with
    /// foreign cash. Functional journal lines intentionally omit exchange-rate metadata, so they
    /// must not be queried through the foreign-exposure resolver. Historical rate is exactly one;
    /// the effective settlement rate comes from the immutable allocation functional value.
    /// </summary>
    private async Task<SettlementBasis> ResolveFunctionalCurrencySettlementBasisAsync(
        Guid tenantId,
        string transactionCurrency,
        decimal settlementFunctionalAmount,
        decimal settledTransactionAmount,
        Guid invoiceJournalEntryId,
        string invoiceDocumentType,
        Guid invoiceDocumentId,
        bool invoiceDebitSide,
        Guid settlementJournalEntryId,
        string settlementDocumentType,
        Guid settlementDocumentId,
        bool settlementDebitSide,
        CancellationToken cancellationToken)
    {
        var invoiceLines = await LoadControlCandidateLinesAsync(
            tenantId,
            invoiceJournalEntryId,
            invoiceDocumentType,
            invoiceDocumentId,
            transactionCurrency,
            invoiceDebitSide,
            requireExchangeRateSnapshot: false,
            cancellationToken);
        var settlementLines = await LoadControlCandidateLinesAsync(
            tenantId,
            settlementJournalEntryId,
            settlementDocumentType,
            settlementDocumentId,
            transactionCurrency,
            settlementDebitSide,
            requireExchangeRateSnapshot: false,
            cancellationToken);

        var invoiceLine = invoiceLines
            .FirstOrDefault(candidate => settlementLines.Any(line => line.AccountId == candidate.AccountId))
            ?? invoiceLines.FirstOrDefault();
        var settlementLine = invoiceLine == null
            ? null
            : settlementLines.FirstOrDefault(line => line.AccountId == invoiceLine.AccountId)
              ?? settlementLines.FirstOrDefault();
        if (invoiceLine == null || settlementLine == null)
        {
            throw new InvalidOperationException(
                "Posted functional-currency control-account lines were not found for cross-currency settlement.");
        }

        var effectiveSettlementRate = settledTransactionAmount > 0m && settlementFunctionalAmount > 0m
            ? decimal.Round(settlementFunctionalAmount / settledTransactionAmount, 6, MidpointRounding.AwayFromZero)
            : 1m;
        return new SettlementBasis(
            invoiceLine.AccountId,
            HistoricalRate: 1m,
            HistoricalRateId: null,
            SettlementRate: effectiveSettlementRate,
            SettlementRateId: null);
    }

    private async Task<SettlementBasis> ResolveSettlementBasisAsync(
        Guid tenantId,
        string transactionCurrency,
        Guid invoiceJournalEntryId,
        string invoiceDocumentType,
        Guid invoiceDocumentId,
        bool invoiceDebitSide,
        Guid settlementJournalEntryId,
        string settlementDocumentType,
        Guid settlementDocumentId,
        bool settlementDebitSide,
        CancellationToken cancellationToken)
    {
        var invoiceLines = await LoadControlCandidateLinesAsync(
            tenantId,
            invoiceJournalEntryId,
            invoiceDocumentType,
            invoiceDocumentId,
            transactionCurrency,
            invoiceDebitSide,
            requireExchangeRateSnapshot: true,
            cancellationToken);
        var settlementLines = await LoadControlCandidateLinesAsync(
            tenantId,
            settlementJournalEntryId,
            settlementDocumentType,
            settlementDocumentId,
            transactionCurrency,
            settlementDebitSide,
            requireExchangeRateSnapshot: true,
            cancellationToken);

        var invoiceLine = invoiceLines
            .FirstOrDefault(i => settlementLines.Any(s => s.AccountId == i.AccountId))
            ?? invoiceLines.FirstOrDefault();
        var settlementLine = invoiceLine == null
            ? null
            : settlementLines.FirstOrDefault(s => s.AccountId == invoiceLine.AccountId) ?? settlementLines.FirstOrDefault();

        if (invoiceLine == null || settlementLine == null)
        {
            throw new InvalidOperationException("Posted foreign-currency control-account snapshots were not found for FX settlement.");
        }

        if (!invoiceLine.ExchangeRate.HasValue || invoiceLine.ExchangeRate.Value <= 0m ||
            !settlementLine.ExchangeRate.HasValue || settlementLine.ExchangeRate.Value <= 0m)
        {
            throw new InvalidOperationException("Posted foreign-currency control-account lines are missing exchange-rate snapshots.");
        }

        await EnsureExchangeRateSnapshotBelongsToTenantAsync(tenantId, invoiceLine.ExchangeRateId, "invoice", cancellationToken);
        await EnsureExchangeRateSnapshotBelongsToTenantAsync(tenantId, settlementLine.ExchangeRateId, "settlement", cancellationToken);

        return new SettlementBasis(
            invoiceLine.AccountId,
            invoiceLine.ExchangeRate.Value,
            invoiceLine.ExchangeRateId,
            settlementLine.ExchangeRate.Value,
            settlementLine.ExchangeRateId);
    }

    private async Task<List<AccountTransaction>> LoadControlCandidateLinesAsync(
        Guid tenantId,
        Guid journalEntryId,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string transactionCurrency,
        bool debitSide,
        bool requireExchangeRateSnapshot,
        CancellationToken cancellationToken)
    {
        var query = _context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && !t.IsDeleted
                && t.JournalEntryId == journalEntryId
                && t.SourceDocumentType == sourceDocumentType
                && t.SourceDocumentId == sourceDocumentId
                && t.PostingStatus == PostedStatus
                && t.TransactionCurrency == transactionCurrency
                && (debitSide ? t.DebitAmount > 0m : t.CreditAmount > 0m));
        if (requireExchangeRateSnapshot)
        {
            // Foreign exposure reconstruction must never infer a rate from functional totals;
            // only the dedicated functional-invoice path above may accept rate-less lines.
            query = query.Where(transaction => transaction.ExchangeRate.HasValue);
        }

        var lines = await query
            .OrderBy(t => t.LineNumber)
            .ToListAsync(cancellationToken);

        return lines;
    }

    private async Task EnsureExchangeRateSnapshotBelongsToTenantAsync(
        Guid tenantId,
        Guid? exchangeRateId,
        string snapshotRole,
        CancellationToken cancellationToken)
    {
        if (!exchangeRateId.HasValue)
        {
            return;
        }

        var exists = await _context.ExchangeRates
            .AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId && r.Id == exchangeRateId.Value && !r.IsDeleted, cancellationToken);

        if (!exists)
        {
            await RecordFxAuditAsync(
                FinanceAuditEvents.FxPostingBlockedInvalidConfiguration,
                tenantId,
                SourceModuleFx,
                "ExchangeRate",
                exchangeRateId,
                reason: $"Posted {snapshotRole} exchange-rate snapshot does not belong to this tenant.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"Posted {snapshotRole} exchange-rate snapshot does not belong to this tenant.");
        }
    }

    private async Task<List<PostedExposure>> BuildPostedExposureBasisAsync(
        Guid tenantId,
        string functionalCurrency,
        string? currencyFilter,
        DateTime revaluationDate,
        string revaluationType,
        AccountingBook accountingBook,
        FinanceSettings settings,
        CancellationToken cancellationToken)
    {
        var normalizedCurrencyFilter = NormalizeOptionalCurrency(currencyFilter);
        var mappings = await _context.AccountAccountingBooks
            .AsNoTracking()
            .Include(mapping => mapping.Account)
            .Include(mapping => mapping.AccountClassification)
            .Where(mapping => mapping.TenantId == tenantId
                && mapping.AccountingBookId == accountingBook.Id
                && mapping.IsEnabled && !mapping.IsDeleted
                && !mapping.Account.IsDeleted && mapping.Account.Status == AccountStatus.Active
                && mapping.AccountClassificationId.HasValue
                && mapping.AccountClassification!.TenantId == tenantId
                && mapping.AccountClassification.AccountingBookId == accountingBook.Id
                && mapping.AccountClassification.Status == AccountClassificationStatus.Active
                && mapping.AccountClassification.IsPostingClassification
                && !mapping.AccountClassification.IsDeleted)
            .ToListAsync(cancellationToken);
        var accountIds = mappings.Select(mapping => mapping.AccountId).Distinct().ToArray();
        var links = await _context.AccountCurrencyLinks.AsNoTracking()
            .Where(link => link.TenantId == tenantId && accountIds.Contains(link.AccountId)
                && !link.IsDeleted && link.IsActive
                && link.RevaluationFrequency != RevaluationFrequency.None
                && link.EffectiveDate.Date <= revaluationDate.Date
                && (link.EffectiveEndDate == null || link.EffectiveEndDate.Value.Date >= revaluationDate.Date))
            .ToListAsync(cancellationToken);
        var mappingIds = mappings.Select(mapping => mapping.Id).ToArray();
        var linkIds = links.Select(link => link.Id).ToArray();
        var overrides = await _context.AccountBookCurrencyPolicies.AsNoTracking()
            .Where(policy => policy.TenantId == tenantId && !policy.IsDeleted
                && mappingIds.Contains(policy.AccountAccountingBookId)
                && linkIds.Contains(policy.AccountCurrencyLinkId))
            .ToDictionaryAsync(policy => (policy.AccountAccountingBookId, policy.AccountCurrencyLinkId), cancellationToken);
        var bankByAccount = await _context.BankAccounts.AsNoTracking()
            .Where(bank => bank.TenantId == tenantId && !bank.IsDeleted && bank.IsActive
                && bank.GLAccountId.HasValue && accountIds.Contains(bank.GLAccountId.Value))
            .GroupBy(bank => bank.GLAccountId!.Value)
            .Select(group => group.OrderBy(bank => bank.Id).First())
            .ToDictionaryAsync(bank => bank.GLAccountId!.Value, cancellationToken);
        var policyMap = new Dictionary<(Guid AccountId, string Currency), ExposurePolicy>();
        foreach (var mapping in mappings)
        {
            foreach (var link in links.Where(link => link.AccountId == mapping.AccountId
                         && IsFrequencyEligible(link.RevaluationFrequency, revaluationType)))
            {
                var currency = NormalizeCurrency(link.LinkedCurrencyCode, "Currency");
                if (IsFunctionalCurrency(currency, functionalCurrency)
                    || (!string.IsNullOrWhiteSpace(normalizedCurrencyFilter) && currency != normalizedCurrencyFilter))
                    continue;
                overrides.TryGetValue((mapping.Id, link.Id), out var policy);
                var inherited = mapping.AccountClassification!.DefaultRevaluationTreatment == RevaluationTreatment.Include;
                var effective = policy?.RevaluationOverride ?? inherited;
                if (!effective) continue;
                var coreType = mapping.AccountClassification.CoreAccountType;
                var nonstandard = coreType is AccountType.Equity or AccountType.Revenue or AccountType.Expense;
                var role = mapping.AccountClassification.SystemRole;
                var sourceModule = mapping.AccountId == settings.ControlAccountArId
                        || role == AccountClassificationSystemRole.ReceivableControl ? "AR"
                    : mapping.AccountId == settings.ControlAccountApId
                        || role == AccountClassificationSystemRole.PayableControl ? "AP"
                    : bankByAccount.ContainsKey(mapping.AccountId)
                        || role is AccountClassificationSystemRole.Cash or AccountClassificationSystemRole.Bank ? "BankCash"
                    : "GL";
                var bank = bankByAccount.GetValueOrDefault(mapping.AccountId);
                policyMap[(mapping.AccountId, currency)] = new ExposurePolicy(
                    mapping.Id,
                    policy?.Id,
                    mapping.AccountClassification.Id,
                    mapping.AccountClassification.Code,
                    mapping.AccountClassification.Name,
                    coreType,
                    mapping.AccountClassification.DefaultRevaluationTreatment,
                    policy?.RevaluationOverride,
                    policy?.RevaluationOverride.HasValue == true ? "CurrencyOverride" : "Classification",
                    nonstandard,
                    nonstandard ? "Non-standard revaluation policy: this Equity, Revenue or Expense exposure is included in closing revaluation." : null,
                    sourceModule,
                    ResolveRevaluationRateType(link.RevaluationRateType),
                    link.RevaluationQuoteSide,
                    link.RevaluationFrequency,
                    bank == null ? null : "BankAccount",
                    bank?.Id);
            }
        }

        if (policyMap.Count == 0)
        {
            return new List<PostedExposure>();
        }

        var policyAccountIds = policyMap.Keys.Select(key => key.AccountId).Distinct().ToList();
        var transactions = await _context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && !t.IsDeleted
                && t.PostingStatus == PostedStatus
                && t.BookClassification == accountingBook.Code
                && t.TransactionDate.Date <= revaluationDate.Date
                && policyAccountIds.Contains(t.AccountId)
                && t.TransactionCurrency != null
                && t.TransactionCurrency != functionalCurrency)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(normalizedCurrencyFilter))
        {
            transactions = transactions
                .Where(t => t.TransactionCurrency == normalizedCurrencyFilter)
                .ToList();
        }

        var priorRevaluations = await _context.FxRevaluationLines
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && !l.IsDeleted
                && l.Batch.AccountingBookId == accountingBook.Id
                && l.Batch.Status == PostedStatus
                && !l.Batch.ReversalPostingEventId.HasValue
                && l.Batch.RevaluationDate.Date <= revaluationDate.Date
                && policyAccountIds.Contains(l.AccountId))
            .ToListAsync(cancellationToken);

        var exposures = new List<PostedExposure>();
        foreach (var group in transactions.GroupBy(t => new { t.AccountId, Currency = NormalizeCurrency(t.TransactionCurrency, functionalCurrency) }))
        {
            if (!policyMap.TryGetValue((group.Key.AccountId, group.Key.Currency), out var exposureAccount))
            {
                continue;
            }
            var foreignBalance = group.Sum(t =>
                t.TransactionDebitAmount.GetValueOrDefault() - t.TransactionCreditAmount.GetValueOrDefault());
            var carryingFunctional = group.Sum(t => t.DebitAmount - t.CreditAmount);

            var priorAdjustment = priorRevaluations
                .Where(l => l.AccountId == group.Key.AccountId && l.TransactionCurrency == group.Key.Currency)
                .Sum(l => l.GainLossAmount);
            carryingFunctional = RoundMoney(carryingFunctional);
            foreignBalance = RoundMoney(foreignBalance);

            if (foreignBalance == 0m)
            {
                continue;
            }

            exposures.Add(new PostedExposure(
                group.Key.AccountId,
                exposureAccount.AccountAccountingBookId,
                exposureAccount.AccountBookCurrencyPolicyId,
                exposureAccount.AccountClassificationId,
                exposureAccount.AccountClassificationCode,
                exposureAccount.AccountClassificationName,
                exposureAccount.CoreAccountType,
                exposureAccount.ClassificationDefault,
                exposureAccount.RevaluationOverride,
                exposureAccount.EffectivePolicySource,
                exposureAccount.HasGovernanceWarning,
                exposureAccount.GovernanceWarning,
                exposureAccount.SourceModule,
                exposureAccount.SourceDocumentType,
                exposureAccount.SourceDocumentId,
                group.Key.Currency,
                foreignBalance,
                carryingFunctional,
                priorAdjustment,
                exposureAccount.RateType,
                exposureAccount.QuoteSide,
                exposureAccount.Frequency));
        }

        return exposures;
    }

    private async Task<ExchangeRate> ResolveClosingRateAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        DateTime revaluationDate,
        ExchangeRateType rateType,
        ExchangeRateQuoteSide closingQuoteSide,
        CancellationToken cancellationToken)
    {
        var rate = await _context.ExchangeRates
            .Where(r => r.TenantId == tenantId
                && !r.IsDeleted
                && r.BaseCurrencyCode == functionalCurrency
                && r.TargetCurrencyCode == transactionCurrency
                && r.RateType == rateType
                && r.QuoteSide == closingQuoteSide
                && r.IsActive
                && r.Rate > 0m
                && (r.ApprovalStatus == RateApprovalStatus.Approved || r.ApprovalStatus == RateApprovalStatus.AutoApproved)
                && r.EffectiveDate.Date <= revaluationDate.Date
                && (r.EndDate == null || r.EndDate.Value.Date >= revaluationDate.Date))
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (rate == null)
        {
            await RecordFxAuditAsync(
                FinanceAuditEvents.FxPostingBlockedInvalidConfiguration,
                tenantId,
                SourceModuleFx,
                "ExchangeRate",
                null,
                reason: $"Missing {closingQuoteSide} {rateType} closing rate for {transactionCurrency}/{functionalCurrency} on {revaluationDate:yyyy-MM-dd}.",
                afterValues: new { transactionCurrency, functionalCurrency, revaluationDate, rateType, closingQuoteSide },
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"No approved {closingQuoteSide} {rateType} exchange rate exists for {transactionCurrency} to {functionalCurrency} on {revaluationDate:yyyy-MM-dd}.");
        }

        return rate;
    }

    private async Task<FxRevaluationBatch> FinalizePersistedRevaluationAsync(
        FxRevaluationBatch batch,
        CancellationToken cancellationToken)
    {
        Guid? committedPostingEventId = null;
        Guid? committedJournalEntryId = null;
        try
        {
            // FinancePostingEngine owns and commits the accounting transaction. Everything below
            // is deliberately a recoverable evidence-finalization boundary, not a distributed
            // transaction pretending that the journal and this workflow share one commit.
            var postingResult = await _financePostingEngine.PostAsync(
                BuildRevaluationPostingRequest(batch),
                cancellationToken);
            committedPostingEventId = postingResult.PostingEventId;
            committedJournalEntryId = postingResult.JournalEntryId;

            ApplyCommittedPostingEvidence(batch, postingResult.PostingEventId, postingResult.JournalEntryId);
            batch.Status = PostingRecoveryRequiredStatus;
            await MarkClosingRatesUsedAsync(batch, postingResult.PostingEventId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Build audit evidence for the intended terminal state without exposing that state
            // to an audit service SaveChanges call before all ancillary evidence succeeds.
            batch.Status = PostedStatus;
            var postedAuditSnapshot = BuildRevaluationAuditSnapshot(batch);
            batch.Status = PostingRecoveryRequiredStatus;
            await RecordRevaluationPostingEvidenceAsync(
                batch,
                postingResult.PostingEventId,
                postingResult.JournalEntryId,
                postedAuditSnapshot,
                cancellationToken);

            batch.Status = PostedStatus;
            batch.Notes = null;
            batch.PostedAt ??= DateTime.UtcNow;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUserService.UserName;
            await _context.SaveChangesAsync(cancellationToken);
            return batch;
        }
        catch (Exception ex)
        {
            var committed = committedPostingEventId.HasValue && committedJournalEntryId.HasValue
                ? (committedPostingEventId.Value, committedJournalEntryId.Value)
                : await FindCommittedRevaluationPostingAsync(batch, cancellationToken);

            if (committed != null)
            {
                ApplyCommittedPostingEvidence(batch, committed.Value.PostingEventId, committed.Value.JournalEntryId);
                batch.Status = PostingRecoveryRequiredStatus;
                batch.Notes = $"Central posting committed; evidence finalization requires retry. {ex.Message}";
            }
            else
            {
                batch.Status = "Failed";
                batch.Notes = ex.Message;
            }

            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUserService.UserName;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await RecordFxAuditAsync(
                    FinanceAuditEvents.UnrealizedRevaluationPostingFailed,
                    batch.TenantId,
                    SourceModuleFx,
                    "FxRevaluationBatch",
                    batch.Id,
                    postingEventId: committed?.PostingEventId,
                    journalEntryId: committed?.JournalEntryId,
                    reason: ex.Message,
                    afterValues: new
                    {
                        batch.Id,
                        error = ex.Message,
                        centralPostingCommitted = committed != null,
                        recoveryStatus = batch.Status
                    },
                    cancellationToken: cancellationToken);
            }
            catch (Exception recoveryError)
            {
                _logger.LogError(
                    recoveryError,
                    "Failed to persist FX revaluation recovery evidence for batch {BatchId}; the deterministic posting key remains {IdempotencyKey}.",
                    batch.Id,
                    batch.IdempotencyKey);
            }

            throw;
        }
    }

    private async Task<(Guid PostingEventId, Guid JournalEntryId)?> FindCommittedRevaluationPostingAsync(
        FxRevaluationBatch batch,
        CancellationToken cancellationToken)
    {
        var posting = await _context.FinancePostingEvents
            .AsNoTracking()
            .Where(item => item.TenantId == batch.TenantId
                && item.IdempotencyKey == batch.IdempotencyKey
                && item.SourceModule == SourceModuleFx
                && item.SourceDocumentType == "FxRevaluationBatch"
                && item.SourceDocumentId == batch.Id
                && item.PostingAction == "UnrealizedRevaluation"
                && item.PostingStatus == PostedStatus
                && item.JournalEntryId.HasValue)
            .Select(item => new { PostingEventId = item.Id, JournalEntryId = item.JournalEntryId!.Value })
            .SingleOrDefaultAsync(cancellationToken);

        return posting == null ? null : (posting.PostingEventId, posting.JournalEntryId);
    }

    private void ApplyCommittedPostingEvidence(
        FxRevaluationBatch batch,
        Guid postingEventId,
        Guid journalEntryId)
    {
        batch.JournalEntryId = journalEntryId;
        batch.PostingEventId = postingEventId;
        batch.PostedAt ??= DateTime.UtcNow;
        batch.UpdatedAt = DateTime.UtcNow;
        batch.UpdatedBy = _currentUserService.UserName;
        foreach (var line in batch.Lines)
        {
            line.JournalEntryId = journalEntryId;
            line.PostingEventId = postingEventId;
        }
    }

    private async Task RecordRevaluationPostingEvidenceAsync(
        FxRevaluationBatch batch,
        Guid postingEventId,
        Guid journalEntryId,
        object postedAuditSnapshot,
        CancellationToken cancellationToken)
    {
        await RecordFxAuditAsync(
            FinanceAuditEvents.UnrealizedRevaluationPosted,
            batch.TenantId,
            SourceModuleFx,
            "FxRevaluationBatch",
            batch.Id,
            postingEventId: postingEventId,
            journalEntryId: journalEntryId,
            afterValues: postedAuditSnapshot,
            cancellationToken: cancellationToken);

        foreach (var rateId in batch.Lines.Select(line => line.ClosingExchangeRateId).Distinct())
        {
            await RecordFxAuditAsync(
                FinanceAuditEvents.ExchangeRateUsedForRevaluation,
                batch.TenantId,
                SourceModuleFx,
                "ExchangeRate",
                rateId,
                postingEventId: postingEventId,
                journalEntryId: journalEntryId,
                afterValues: new { batch.Id, rateId, batch.RevaluationDate, batch.FunctionalCurrencyCode },
                cancellationToken: cancellationToken);
        }

        if (batch.Lines.Any(line => line.SourceModule == "BankCash"))
        {
            await RecordFxAuditAsync(
                FinanceAuditEvents.ForeignBankRevaluationPosted,
                batch.TenantId,
                SourceModuleFx,
                "FxRevaluationBatch",
                batch.Id,
                postingEventId: postingEventId,
                journalEntryId: journalEntryId,
                afterValues: postedAuditSnapshot,
                cancellationToken: cancellationToken);
        }
    }

    private FinancePostingRequestV2Dto BuildRevaluationPostingRequest(FxRevaluationBatch batch)
    {
        var lines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        foreach (var line in batch.Lines.OrderBy(l => l.SourceModule).ThenBy(l => l.AccountId).ThenBy(l => l.TransactionCurrency))
        {
            var amount = Math.Abs(line.GainLossAmount);
            if (line.GainLossType == "Gain")
            {
                lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation gain - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Revaluation-Control"));
                lines.Add(BuildFunctionalPostingLine(line.GainLossAccountId, $"FX revaluation gain - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Unrealized-Gain"));
            }
            else
            {
                lines.Add(BuildFunctionalPostingLine(line.GainLossAccountId, $"FX revaluation loss - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Unrealized-Loss"));
                lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation loss - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Revaluation-Control"));
            }
        }

        return new FinancePostingRequestV2Dto
        {
            SourceModule = SourceModuleFx,
            SourceDocumentType = "FxRevaluationBatch",
            SourceDocumentId = batch.Id,
            SourceDocumentTenantId = batch.TenantId,
            PostingAction = "UnrealizedRevaluation",
            SourceDocumentReference = batch.BatchNumber,
            Description = $"FX revaluation {batch.BatchNumber}",
            PostingDate = batch.RevaluationDate,
            FiscalPeriodId = batch.FiscalPeriodId,
            JournalType = "FX Revaluation",
            AccountingBookCode = batch.AccountingBookCode,
            FunctionalCurrencyCode = batch.FunctionalCurrencyCode,
            IdempotencyKey = batch.IdempotencyKey,
            ReturnExistingOnDuplicate = true,
            Lines = lines
        };
    }

    private async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>>
        RequireApRealizedFxDimensionEvidenceAsync(
            Guid paymentId,
            Guid allocationId,
            decimal expectedSignedAmount,
            CancellationToken cancellationToken)
    {
        if (_paymentDimensions is null)
            return new[]
            {
                new FinanceSettlementDimensionComponentDto
                {
                    SettlementSourceLineId = allocationId,
                    OriginatingSourceLineId = allocationId,
                    ComponentType = FinanceSettlementComponentType.RealizedFx,
                    FunctionalAmount = expectedSignedAmount,
                    TransactionCurrencyCode = "FX",
                    ExchangeRate = 1m
                }
            };
        var rows = (await _paymentDimensions.GetVendorPaymentAsync(paymentId, cancellationToken))
            .Where(item => item.SettlementSourceLineId == allocationId
                && item.ComponentType == FinanceSettlementComponentType.RealizedFx)
            .OrderBy(item => item.OriginatingSourceLineId)
            .ToArray();
        RequireRealizedFxEvidence(rows, expectedSignedAmount, allocationId);
        return rows;
    }

    private async Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>>
        RequireArRealizedFxDimensionEvidenceAsync(
            Guid paymentId,
            Guid allocationId,
            decimal expectedSignedAmount,
            FinancePostingProducerContext producer,
            CancellationToken cancellationToken)
    {
        if (_paymentDimensions is null)
            return new[]
            {
                new FinanceSettlementDimensionComponentDto
                {
                    SettlementSourceLineId = allocationId,
                    OriginatingSourceLineId = allocationId,
                    ComponentType = FinanceSettlementComponentType.RealizedFx,
                    FunctionalAmount = expectedSignedAmount,
                    TransactionCurrencyCode = "FX",
                    ExchangeRate = 1m
                }
            };
        var rows = (await _paymentDimensions.GetCustomerPaymentAsync(
                paymentId, producer, cancellationToken))
            .Where(item => item.SettlementSourceLineId == allocationId
                && item.ComponentType == FinanceSettlementComponentType.RealizedFx)
            .OrderBy(item => item.OriginatingSourceLineId)
            .ToArray();
        RequireRealizedFxEvidence(rows, expectedSignedAmount, allocationId);
        return rows;
    }

    private static void RequireRealizedFxEvidence(
        IReadOnlyCollection<FinanceSettlementDimensionComponentDto> evidence,
        decimal expectedSignedAmount,
        Guid allocationId)
    {
        if (evidence.Count == 0)
            throw new InvalidOperationException(
                $"Realized FX dimension evidence is missing for settlement allocation {allocationId}.");
        if (RoundMoney(evidence.Sum(item => item.FunctionalAmount)) != RoundMoney(expectedSignedAmount))
            throw new InvalidOperationException(
                $"Realized FX dimension evidence is stale for settlement allocation {allocationId}.");
    }

    private static FinancePostingLineDto BuildFunctionalPostingLine(
        Guid accountId,
        string description,
        decimal debitAmount,
        decimal creditAmount,
        int lineNumber,
        string tag,
        Guid? sourceDocumentLineId = null,
        IReadOnlyList<FinancePostingDimensionValueDto>? dimensions = null)
    {
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            SourceDocumentLineId = sourceDocumentLineId,
            Description = description,
            DebitAmount = RoundMoney(debitAmount),
            CreditAmount = RoundMoney(creditAmount),
            LineNumber = lineNumber,
            TransactionTag = tag,
            Dimensions = dimensions ?? Array.Empty<FinancePostingDimensionValueDto>()
        };
    }

    private async Task<FinanceSettings> GetFinanceSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _context.FinanceSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

        return settings ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
    }

    private async Task<Account> ResolveRequiredFxAccountAsync(
        Guid tenantId,
        Guid? accountId,
        string accountPurpose,
        string blockedAuditEvent,
        CancellationToken cancellationToken)
    {
        if (!accountId.HasValue || accountId.Value == Guid.Empty)
        {
            await RecordFxAuditAsync(
                blockedAuditEvent,
                tenantId,
                SourceModuleFx,
                "FinanceSettings",
                null,
                reason: $"{accountPurpose} is not configured.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"{accountPurpose} is not configured for this tenant.");
        }

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId.Value && !a.IsDeleted, cancellationToken);

        if (account == null)
        {
            await RecordFxAuditAsync(
                blockedAuditEvent,
                tenantId,
                SourceModuleFx,
                "Account",
                accountId,
                reason: $"{accountPurpose} does not belong to this tenant.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"{accountPurpose} was not found for this tenant.");
        }

        if (account.Status != AccountStatus.Active)
        {
            await RecordFxAuditAsync(
                blockedAuditEvent,
                tenantId,
                SourceModuleFx,
                "Account",
                accountId,
                reason: $"{accountPurpose} is inactive.",
                cancellationToken: cancellationToken);
            throw new InvalidOperationException($"{accountPurpose} is inactive.");
        }

        return account;
    }

    private async Task<FxRealizedSettlement?> LoadExistingRealizedSettlementAsync(
        Guid tenantId,
        string settlementDocumentType,
        Guid settlementDocumentId,
        Guid allocationId,
        CancellationToken cancellationToken)
    {
        return await _context.FxRealizedSettlements
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                && !s.IsDeleted
                && s.SettlementDocumentType == settlementDocumentType
                && s.SettlementDocumentId == settlementDocumentId
                && s.SettlementAllocationId == allocationId,
                cancellationToken);
    }

    private async Task<FiscalPeriod> ResolveFiscalPeriodAsync(
        Guid tenantId,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var period = await _context.FiscalPeriods
            .FirstOrDefaultAsync(p => p.TenantId == tenantId
                && !p.IsDeleted
                && p.StartDate <= date
                && p.EndDate >= date,
                cancellationToken);

        if (period == null)
        {
            throw new InvalidOperationException("No fiscal period covers the FX posting date for this tenant.");
        }

        if (!period.IsOpen || period.IsClosed || period.IsLocked)
        {
            throw new InvalidOperationException("FX posting period is not open.");
        }

        return period;
    }

    private async Task<AccountingBook> ResolveRevaluationBookAsync(
        Guid tenantId, string? accountingBookCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountingBookCode))
            throw new InvalidOperationException("AccountingBookCode is required for revaluation; Finance will not guess a default book.");
        var code = accountingBookCode.Trim().ToUpperInvariant();
        var books = await _context.AccountingBooks
            .Where(book => book.TenantId == tenantId && book.Code == code && !book.IsDeleted)
            .ToListAsync(cancellationToken);
        if (books.Count != 1)
            throw new InvalidOperationException($"Accounting book '{code}' is unavailable or ambiguous for this tenant.");
        var book = books[0];
        if (!book.IsActive || !book.AllowsPosting)
            throw new InvalidOperationException($"Accounting book '{code}' is inactive or does not allow posting.");
        return book;
    }

    private async Task<string> ResolvePostedJournalBookCodeAsync(
        Guid tenantId, Guid journalEntryId, CancellationToken cancellationToken)
    {
        var code = await _context.JournalEntries.AsNoTracking()
            .Where(journal => journal.TenantId == tenantId && journal.Id == journalEntryId
                && journal.PostingStatus == PostedStatus && !journal.IsDeleted)
            .Select(journal => journal.BookClassification)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("The original posted journal has no accounting-book authority.");
        return (await ResolveRevaluationBookAsync(tenantId, code, cancellationToken)).Code;
    }

    private async Task MarkClosingRatesUsedAsync(
        FxRevaluationBatch batch,
        Guid postingEventId,
        CancellationToken cancellationToken)
    {
        var rateIds = batch.Lines
            .Select(l => l.ClosingExchangeRateId)
            .Distinct()
            .ToList();

        var rates = await _context.ExchangeRates
            .Where(r => r.TenantId == batch.TenantId && rateIds.Contains(r.Id) && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var rate in rates)
        {
            rate.HasBeenUsedInTransactions = true;
            rate.TransactionCount += batch.Lines.Count(l => l.ClosingExchangeRateId == rate.Id);
            rate.FirstUsedDate ??= DateTime.UtcNow;
            rate.LastUsedDate = DateTime.UtcNow;
            rate.ModifiedDate = DateTime.UtcNow;
            rate.ModifiedByUserId = GetCurrentUserGuid();
        }
    }

    private async Task RecordFxAuditAsync(
        string eventType,
        Guid tenantId,
        string? sourceModule,
        string? sourceDocumentType,
        Guid? sourceDocumentId,
        Guid? journalEntryId = null,
        Guid? postingEventId = null,
        object? beforeValues = null,
        object? afterValues = null,
        object? context = null,
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
            TenantId = tenantId,
            SourceModule = sourceModule,
            SourceDocumentType = sourceDocumentType,
            SourceDocumentId = sourceDocumentId,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEventId,
            BeforeValues = beforeValues,
            AfterValues = afterValues,
            Context = context,
            Reason = reason,
            Comment = comment,
            Resource = sourceDocumentType,
            ResourceId = sourceDocumentId?.ToString()
        }, cancellationToken);
    }

    private static void UpdateBatchTotals(FxRevaluationBatch batch)
    {
        batch.TotalGainAmount = RoundMoney(batch.Lines.Where(l => l.GainLossType == "Gain").Sum(l => Math.Abs(l.GainLossAmount)));
        batch.TotalLossAmount = RoundMoney(batch.Lines.Where(l => l.GainLossType == "Loss").Sum(l => Math.Abs(l.GainLossAmount)));
        batch.NetGainLossAmount = RoundMoney(batch.TotalGainAmount - batch.TotalLossAmount);
    }

    private static string BuildPreviewFingerprint(FxRevaluationBatch batch)
    {
        var evidence = new StringBuilder()
            .Append("RHEMA:FX-REVALUATION-PREVIEW:V1|")
            .Append(batch.TenantId.ToString("N")).Append('|')
            .Append(batch.AccountingBookId.ToString("N")).Append('|')
            .Append(batch.AccountingBookCode).Append('|')
            .Append(batch.RevaluationDate.ToString("yyyyMMdd")).Append('|')
            .Append(batch.FiscalPeriodId.ToString("N")).Append('|')
            .Append(batch.Scope).Append('|')
            .Append(batch.FunctionalCurrencyCode).Append('|')
            .Append(batch.AutoReverseNextPeriod).Append('|')
            .Append(batch.IdempotencyKey);
        foreach (var line in batch.Lines
                     .OrderBy(line => line.AccountId)
                     .ThenBy(line => line.TransactionCurrency)
                     .ThenBy(line => line.SourceModule)
                     .ThenBy(line => line.SourceDocumentType)
                     .ThenBy(line => line.SourceDocumentId))
        {
            evidence.Append('|').Append(line.TenantId.ToString("N"))
                .Append(':').Append(line.AccountId.ToString("N"))
                .Append(':').Append(line.SourceModule)
                .Append(':').Append(line.SourceDocumentType)
                .Append(':').Append(line.SourceDocumentId?.ToString("N") ?? "NONE")
                .Append(':').Append(line.AccountAccountingBookId.ToString("N"))
                .Append(':').Append(line.AccountBookCurrencyPolicyId?.ToString("N") ?? "INHERITED")
                .Append(':').Append(line.AccountClassificationId.ToString("N"))
                .Append(':').Append(line.AccountClassificationCode)
                .Append(':').Append(line.AccountClassificationName)
                .Append(':').Append(line.CoreAccountType)
                .Append(':').Append(line.ClassificationDefault)
                .Append(':').Append(line.RevaluationOverride?.ToString() ?? "NULL")
                .Append(':').Append(line.EffectiveRevaluationRequired)
                .Append(':').Append(line.EffectivePolicySource)
                .Append(':').Append(line.HasGovernanceWarning)
                .Append(':').Append(line.GovernanceWarning ?? "NONE")
                .Append(':').Append(line.TransactionCurrency)
                .Append(':').Append(line.FunctionalCurrencyCode)
                .Append(':').Append(CanonicalDecimal(line.ForeignCurrencyBalance))
                .Append(':').Append(CanonicalDecimal(line.CarryingFunctionalAmount))
                .Append(':').Append(CanonicalDecimal(line.PriorUnreversedAdjustment))
                .Append(':').Append(line.ClosingExchangeRateId.ToString("N"))
                .Append(':').Append(CanonicalDecimal(line.ClosingExchangeRate))
                .Append(':').Append(line.ClosingRateDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))
                .Append(':').Append(line.ClosingRateType)
                .Append(':').Append(line.ClosingQuoteSide)
                .Append(':').Append(CanonicalDecimal(line.RevaluedFunctionalAmount))
                .Append(':').Append(CanonicalDecimal(line.GainLossAmount))
                .Append(':').Append(line.GainLossType)
                .Append(':').Append(line.GainLossAccountId.ToString("N"))
                .Append(':').Append(line.Notes ?? "NONE");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.ToString())))
            .ToLowerInvariant();
    }

    private static string RequireCanonicalPreviewFingerprint(string? value)
    {
        var fingerprint = value?.Trim();
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new InvalidOperationException(
                "A preview fingerprint is required before posting an FX revaluation. Run preview and submit its 64-character fingerprint.");
        }

        if (fingerprint.Length != 64 || fingerprint.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                "The FX revaluation preview fingerprint is malformed. Run preview again and submit the canonical 64-character SHA-256 fingerprint.");
        }

        return fingerprint.ToLowerInvariant();
    }

    private static void ValidateFrozenPreviewFingerprint(FxRevaluationBatch batch, string expectedFingerprint)
    {
        var storedFingerprint = RequireCanonicalPreviewFingerprint(batch.PreviewFingerprint);
        var recomputedFingerprint = BuildPreviewFingerprint(batch);
        if (!FingerprintsMatch(storedFingerprint, recomputedFingerprint))
        {
            throw new InvalidOperationException(
                "Stored FX revaluation evidence no longer matches its preview fingerprint. Posting is blocked; investigate possible evidence tampering.");
        }

        if (!FingerprintsMatch(storedFingerprint, expectedFingerprint))
        {
            throw new InvalidOperationException(
                "The supplied preview fingerprint does not match the persisted FX revaluation plan. Reload preview before retrying.");
        }
    }

    private static bool FingerprintsMatch(string first, string second) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(first.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(second.ToLowerInvariant()));

    private static string CanonicalDecimal(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static ExchangeRateType ResolveRevaluationRateType(string? revaluationType)
    {
        if (revaluationType?.Contains("Year", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ExchangeRateType.YearEnd;
        }

        if (revaluationType?.Contains("Quarter", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ExchangeRateType.QuarterEnd;
        }

        return ExchangeRateType.MonthEnd;
    }

    private static bool IsFrequencyEligible(RevaluationFrequency frequency, string? revaluationType)
    {
        if (frequency == RevaluationFrequency.None)
        {
            return false;
        }
        if (revaluationType?.Contains("Ad", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        if (revaluationType?.Contains("Year", StringComparison.OrdinalIgnoreCase) == true)
        {
            return frequency is RevaluationFrequency.Monthly or RevaluationFrequency.Quarterly or RevaluationFrequency.Annually;
        }
        if (revaluationType?.Contains("Quarter", StringComparison.OrdinalIgnoreCase) == true)
        {
            return frequency is RevaluationFrequency.Monthly or RevaluationFrequency.Quarterly;
        }
        return frequency == RevaluationFrequency.Monthly;
    }

    private static string ResolveRevaluationScope(RevaluationRequestDto request)
    {
        return string.IsNullOrWhiteSpace(request.CurrencyCode)
            ? "Combined"
            : $"Currency:{NormalizeCurrency(request.CurrencyCode, "Currency")}";
    }

    private static string? NormalizeOptionalCurrency(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizeCurrency(value, "Currency");
    }

    private static string NormalizeCurrency(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new InvalidOperationException("Currency code is required.");
        }

        var normalized = candidate.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new InvalidOperationException($"Currency code '{candidate}' must be a three-letter ISO code.");
        }

        return normalized;
    }

    private static bool IsFunctionalCurrency(string currency, string functionalCurrency)
    {
        return string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeExchangeRate(decimal value)
    {
        return value <= 0m ? 1m : decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private Guid? GetCurrentUserGuid()
    {
        return Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;
    }

    private static object BuildRealizedAuditSnapshot(FxRealizedSettlement settlement)
    {
        return new
        {
            settlement.Id,
            settlement.SourceModule,
            settlement.SettlementDocumentType,
            settlement.SettlementDocumentId,
            settlement.SettlementAllocationId,
            settlement.InvoiceDocumentType,
            settlement.InvoiceDocumentId,
            settlement.TransactionCurrency,
            settlement.FunctionalCurrencyCode,
            settlement.SettledForeignAmount,
            settlement.HistoricalExchangeRate,
            settlement.SettlementExchangeRate,
            settlement.HistoricalFunctionalAmount,
            settlement.SettlementFunctionalAmount,
            settlement.GainLossAmount,
            settlement.GainLossType,
            settlement.GainLossAccountId,
            settlement.JournalEntryId,
            settlement.PostingEventId
        };
    }

    private static object BuildRevaluationAuditSnapshot(FxRevaluationBatch batch)
    {
        return new
        {
            batch.Id,
            batch.BatchNumber,
            batch.RevaluationDate,
            batch.FiscalPeriodId,
            batch.AccountingBookId,
            batch.AccountingBookCode,
            batch.Status,
            batch.Scope,
            batch.FunctionalCurrencyCode,
            batch.TotalGainAmount,
            batch.TotalLossAmount,
            batch.NetGainLossAmount,
            batch.JournalEntryId,
            batch.PostingEventId,
            batch.PreviewFingerprint,
            NonstandardPolicyCount = batch.Lines.Count(line => line.HasGovernanceWarning),
            LineCount = batch.Lines.Count
        };
    }

    private static JournalEntry BuildPreviewJournal(FxRevaluationBatch batch, RevaluationRequestDto request)
    {
        return new JournalEntry
        {
            Id = batch.JournalEntryId ?? Guid.NewGuid(),
            TenantId = batch.TenantId,
            JournalEntryNumber = batch.BatchNumber,
            EntryDate = batch.RevaluationDate,
            Description = $"FX revaluation {request.RevaluationType} {batch.RevaluationDate:yyyy-MM-dd}",
            ReferenceNumber = batch.BatchNumber,
            PostingStatus = request.PreviewOnly ? "Draft" : batch.Status,
            BookClassification = batch.AccountingBookCode,
            PrimaryCurrency = batch.FunctionalCurrencyCode,
            IsMultiCurrency = batch.Lines.Any(),
            FiscalPeriodId = batch.FiscalPeriodId,
            IsBalanced = true,
            TotalDebitAmount = RoundMoney(batch.TotalGainAmount + batch.TotalLossAmount),
            TotalCreditAmount = RoundMoney(batch.TotalGainAmount + batch.TotalLossAmount),
            Transactions = new List<AccountTransaction>()
        };
    }

    private sealed record SettlementBasis(
        Guid ControlAccountId,
        decimal HistoricalRate,
        Guid? HistoricalRateId,
        decimal SettlementRate,
        Guid? SettlementRateId);

    private sealed record ExposurePolicy(
        Guid AccountAccountingBookId,
        Guid? AccountBookCurrencyPolicyId,
        Guid AccountClassificationId,
        string AccountClassificationCode,
        string AccountClassificationName,
        AccountType CoreAccountType,
        RevaluationTreatment ClassificationDefault,
        bool? RevaluationOverride,
        string EffectivePolicySource,
        bool HasGovernanceWarning,
        string? GovernanceWarning,
        string SourceModule,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        RevaluationFrequency Frequency,
        string? SourceDocumentType = null,
        Guid? SourceDocumentId = null);

    private sealed record PostedExposure(
        Guid AccountId,
        Guid AccountAccountingBookId,
        Guid? AccountBookCurrencyPolicyId,
        Guid AccountClassificationId,
        string AccountClassificationCode,
        string AccountClassificationName,
        AccountType CoreAccountType,
        RevaluationTreatment ClassificationDefault,
        bool? RevaluationOverride,
        string EffectivePolicySource,
        bool HasGovernanceWarning,
        string? GovernanceWarning,
        string SourceModule,
        string? SourceDocumentType,
        Guid? SourceDocumentId,
        string TransactionCurrency,
        decimal ForeignCurrencyBalance,
        decimal CarryingFunctionalAmount,
        decimal PriorUnreversedAdjustment,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        RevaluationFrequency Frequency);

}
