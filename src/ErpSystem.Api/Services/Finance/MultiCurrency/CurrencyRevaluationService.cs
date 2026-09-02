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
                        SourceModule = line.SourceModule,
                        TransactionCurrency = line.TransactionCurrency,
                        FunctionalCurrencyCode = line.FunctionalCurrencyCode,
                        ForeignCurrencyBalance = line.ForeignCurrencyBalance,
                        CarryingFunctionalAmount = line.CarryingFunctionalAmount,
                        PreviousRate = line.ForeignCurrencyBalance == 0m
                            ? 0m
                            : decimal.Round(line.CarryingFunctionalAmount / line.ForeignCurrencyBalance, 6, MidpointRounding.AwayFromZero),
                        ClosingExchangeRate = line.ClosingExchangeRate,
                        RevaluedFunctionalAmount = line.RevaluedFunctionalAmount,
                        GainLossAmount = line.GainLossAmount,
                        GainLossType = line.GainLossType,
                        RevaluationFrequency = line.Notes?.Split('|').ElementAtOrDefault(1)?.Trim() ?? string.Empty,
                        RateType = line.ClosingExchangeRateRecord?.RateType.ToString() ?? string.Empty,
                        QuoteSide = line.ClosingExchangeRateRecord?.QuoteSide.ToString() ?? string.Empty
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
            Currencies = batch.Lines.Select(line => line.TransactionCurrency).Distinct().OrderBy(code => code).ToList(),
            ExposureCount = batch.Lines.Count,
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

        if (!request.PreviewOnly)
        {
            var existing = await _context.FxRevaluationBatches
                .Include(b => b.Lines)
                .FirstOrDefaultAsync(
                    b => b.TenantId == tenantId
                        && !b.IsDeleted
                        && b.Scope == scope
                        && b.RevaluationDate.Date == revaluationDate
                        && b.FiscalPeriodId == fiscalPeriod.Id,
                    cancellationToken);

            if (existing != null)
            {
                return existing;
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
            settings,
            cancellationToken);

        var batch = new FxRevaluationBatch
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchNumber = $"FXR-{revaluationDate:yyyyMMdd}-{DateTime.UtcNow:HHmmssfff}",
            RevaluationDate = revaluationDate,
            FiscalPeriodId = fiscalPeriod.Id,
            Scope = scope,
            FunctionalCurrencyCode = functionalCurrency,
            Status = request.PreviewOnly ? "Preview" : "Calculated",
            IdempotencyKey = $"FX:Revaluation:{tenantId:N}:{scope}:{revaluationDate:yyyyMMdd}:{fiscalPeriod.Id:N}",
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

            var revaluedFunctionalAmount = RoundMoney(exposure.ForeignCurrencyBalance * closingRate.Rate);
            var gainLossAmount = RoundMoney(revaluedFunctionalAmount - exposure.CarryingFunctionalAmount);
            if (gainLossAmount == 0m)
            {
                continue;
            }

            var gainLossType = ResolveUnrealizedGainLossType(exposure.Kind, gainLossAmount);
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
                TransactionCurrency = exposure.TransactionCurrency,
                FunctionalCurrencyCode = functionalCurrency,
                ForeignCurrencyBalance = exposure.ForeignCurrencyBalance,
                CarryingFunctionalAmount = exposure.CarryingFunctionalAmount,
                ClosingExchangeRateId = closingRate.Id,
                ClosingExchangeRate = closingRate.Rate,
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
        if (!request.PreviewOnly
            && !string.IsNullOrWhiteSpace(request.ExpectedPreviewFingerprint)
            && !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(previewFingerprint),
                Encoding.UTF8.GetBytes(request.ExpectedPreviewFingerprint.Trim().ToLowerInvariant())))
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

        try
        {
            var postingRequest = BuildRevaluationPostingRequest(
                batch,
                unrealizedGainAccount.Id,
                unrealizedLossAccount.Id);
            var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);

            batch.JournalEntryId = postingResult.JournalEntryId;
            batch.PostingEventId = postingResult.PostingEventId;
            batch.Status = PostedStatus;
            batch.PostedAt = DateTime.UtcNow;
            batch.UpdatedAt = DateTime.UtcNow;
            batch.UpdatedBy = _currentUserService.UserName;
            foreach (var line in batch.Lines)
            {
                line.JournalEntryId = postingResult.JournalEntryId;
                line.PostingEventId = postingResult.PostingEventId;
            }

            await UpdateCurrencyLinkRevaluationStateAsync(batch, cancellationToken);

            await MarkClosingRatesUsedAsync(batch, postingResult.PostingEventId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await RecordFxAuditAsync(
                FinanceAuditEvents.UnrealizedRevaluationPosted,
                tenantId,
                SourceModuleFx,
                "FxRevaluationBatch",
                batch.Id,
                postingEventId: postingResult.PostingEventId,
                journalEntryId: postingResult.JournalEntryId,
                afterValues: BuildRevaluationAuditSnapshot(batch),
                cancellationToken: cancellationToken);

            foreach (var rateId in batch.Lines.Select(l => l.ClosingExchangeRateId).Distinct())
            {
                await RecordFxAuditAsync(
                    FinanceAuditEvents.ExchangeRateUsedForRevaluation,
                    tenantId,
                    SourceModuleFx,
                    "ExchangeRate",
                    rateId,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: new { batch.Id, rateId, batch.RevaluationDate, batch.FunctionalCurrencyCode },
                    cancellationToken: cancellationToken);
            }

            if (batch.Lines.Any(l => l.SourceModule == "BankCash"))
            {
                await RecordFxAuditAsync(
                    FinanceAuditEvents.ForeignBankRevaluationPosted,
                    tenantId,
                    SourceModuleFx,
                    "FxRevaluationBatch",
                    batch.Id,
                    postingEventId: postingResult.PostingEventId,
                    journalEntryId: postingResult.JournalEntryId,
                    afterValues: BuildRevaluationAuditSnapshot(batch),
                    cancellationToken: cancellationToken);
            }

            return batch;
        }
        catch (Exception ex)
        {
            var failedBatch = await _context.FxRevaluationBatches
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.Id == batch.Id, cancellationToken);
            if (failedBatch != null)
            {
                failedBatch.Status = "Failed";
                failedBatch.Notes = ex.Message;
                failedBatch.UpdatedAt = DateTime.UtcNow;
                failedBatch.UpdatedBy = _currentUserService.UserName;
                await _context.SaveChangesAsync(cancellationToken);
            }

            await RecordFxAuditAsync(
                FinanceAuditEvents.UnrealizedRevaluationPostingFailed,
                tenantId,
                SourceModuleFx,
                "FxRevaluationBatch",
                batch.Id,
                reason: ex.Message,
                afterValues: new { batch.Id, error = ex.Message },
                cancellationToken: cancellationToken);

            throw;
        }
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

        var postingRequest = new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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
        await RefreshCurrencyLinkRevaluationStateAfterReversalAsync(batch, cancellationToken);
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

    private async Task RefreshCurrencyLinkRevaluationStateAfterReversalAsync(
        FxRevaluationBatch reversedBatch,
        CancellationToken cancellationToken)
    {
        var keys = reversedBatch.Lines.Select(line => new { line.AccountId, Currency = line.TransactionCurrency }).Distinct().ToList();
        var accountIds = keys.Select(key => key.AccountId).Distinct().ToArray();
        var currencies = keys.Select(key => key.Currency).Distinct().ToArray();
        var links = await _context.AccountCurrencyLinks
            .Where(link => link.TenantId == reversedBatch.TenantId
                && accountIds.Contains(link.AccountId)
                && currencies.Contains(link.LinkedCurrencyCode)
                && !link.IsDeleted)
            .ToListAsync(cancellationToken);

        var priorLines = await _context.FxRevaluationLines
            .AsNoTracking()
            .Include(line => line.Batch)
            .Where(line => line.TenantId == reversedBatch.TenantId
                && accountIds.Contains(line.AccountId)
                && currencies.Contains(line.TransactionCurrency)
                && line.FxRevaluationBatchId != reversedBatch.Id
                && line.Batch.Status == PostedStatus
                && !line.Batch.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var link in links)
        {
            var matching = priorLines
                .Where(line => line.AccountId == link.AccountId && line.TransactionCurrency == link.LinkedCurrencyCode)
                .OrderByDescending(line => line.Batch.RevaluationDate)
                .ThenByDescending(line => line.Batch.PostedAt)
                .ToList();
            var latest = matching.FirstOrDefault();
            link.LastRevaluationDate = latest?.Batch.RevaluationDate;
            link.CurrentExchangeRate = latest?.ClosingExchangeRate ?? 0m;
            link.RateEffectiveDate = latest?.Batch.RevaluationDate;
            link.LastRevaluationAdjustment = latest?.GainLossAmount ?? 0m;
            link.CumulativeRevaluationAdjustment = matching.Sum(line => line.GainLossAmount);
            link.UpdatedAt = DateTime.UtcNow;
            link.UpdatedBy = _currentUserService.UserName;
        }
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

        var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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
            payment.Id, allocation.Id, delta, cancellationToken);
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
                    component.Id, gainLossAccount.Id, payment.PaymentDate, cancellationToken);
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

        var postingResult = await _financePostingEngine.PostAsync(new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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
        FinanceSettings settings,
        CancellationToken cancellationToken)
    {
        var normalizedCurrencyFilter = NormalizeOptionalCurrency(currencyFilter);

        var foreignBankAccounts = await _context.BankAccounts
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && !b.IsDeleted
                && b.GLAccountId.HasValue
                && b.IsActive
                && b.Currency != functionalCurrency)
            .ToListAsync(cancellationToken);

        var bankByAccount = foreignBankAccounts
            .GroupBy(bank => bank.GLAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var links = await _context.AccountCurrencyLinks
            .AsNoTracking()
            .Include(link => link.Account)
            .Where(link => link.TenantId == tenantId
                && !link.IsDeleted
                && link.IsActive
                && link.RevaluationRequired
                && link.RevaluationFrequency != RevaluationFrequency.None
                && link.EffectiveDate.Date <= revaluationDate.Date
                && (link.EffectiveEndDate == null || link.EffectiveEndDate.Value.Date >= revaluationDate.Date)
                && !link.Account.IsDeleted
                && link.Account.Status == AccountStatus.Active
                && (link.Account.AccountType == AccountType.Asset || link.Account.AccountType == AccountType.Liability))
            .ToListAsync(cancellationToken);

        var policyMap = new Dictionary<(Guid AccountId, string Currency), ExposurePolicy>();
        foreach (var link in links.Where(link => IsFrequencyEligible(link.RevaluationFrequency, revaluationType)))
        {
            var currency = NormalizeCurrency(link.LinkedCurrencyCode, "Currency");
            if (IsFunctionalCurrency(currency, functionalCurrency) ||
                (!string.IsNullOrWhiteSpace(normalizedCurrencyFilter) && currency != normalizedCurrencyFilter))
            {
                continue;
            }

            var sourceModule = settings.ControlAccountArId == link.AccountId
                ? "AR"
                : settings.ControlAccountApId == link.AccountId
                    ? "AP"
                    : bankByAccount.ContainsKey(link.AccountId) ? "BankCash" : "GL";
            var bank = bankByAccount.GetValueOrDefault(link.AccountId);
            policyMap[(link.AccountId, currency)] = new ExposurePolicy(
                sourceModule,
                link.Account.AccountType == AccountType.Liability ? MonetaryExposureKind.Liability : MonetaryExposureKind.Asset,
                ResolveRevaluationRateType(link.RevaluationRateType),
                link.RevaluationQuoteSide,
                link.RevaluationFrequency,
                bank == null ? null : "BankAccount",
                bank?.Id);
        }

        // Foreign bank accounts are inherently monetary. Preserve their implicit
        // primary-currency policy even though primary currencies are not stored as links.
        foreach (var bank in foreignBankAccounts)
        {
            var currency = NormalizeCurrency(bank.Currency, "Currency");
            if (!string.IsNullOrWhiteSpace(normalizedCurrencyFilter) && currency != normalizedCurrencyFilter)
            {
                continue;
            }
            policyMap.TryAdd(
                (bank.GLAccountId!.Value, currency),
                new ExposurePolicy("BankCash", MonetaryExposureKind.Asset,
                    ResolveRevaluationRateType(revaluationType), settings.ClosingQuoteSide,
                    RevaluationFrequency.Monthly, "BankAccount", bank.Id));
        }

        if (policyMap.Count == 0)
        {
            return new List<PostedExposure>();
        }

        var accountIds = policyMap.Keys.Select(key => key.AccountId).Distinct().ToList();
        var transactions = await _context.AccountTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && !t.IsDeleted
                && t.PostingStatus == PostedStatus
                && t.TransactionDate.Date <= revaluationDate.Date
                && accountIds.Contains(t.AccountId)
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
                && l.Batch.Status == PostedStatus
                && l.Batch.RevaluationDate.Date <= revaluationDate.Date
                && accountIds.Contains(l.AccountId))
            .ToListAsync(cancellationToken);

        var exposures = new List<PostedExposure>();
        foreach (var group in transactions.GroupBy(t => new { t.AccountId, Currency = NormalizeCurrency(t.TransactionCurrency, functionalCurrency) }))
        {
            if (!policyMap.TryGetValue((group.Key.AccountId, group.Key.Currency), out var exposureAccount))
            {
                continue;
            }
            var foreignBalance = exposureAccount.Kind == MonetaryExposureKind.Liability
                ? group.Sum(t => t.TransactionCreditAmount.GetValueOrDefault() - t.TransactionDebitAmount.GetValueOrDefault())
                : group.Sum(t => t.TransactionDebitAmount.GetValueOrDefault() - t.TransactionCreditAmount.GetValueOrDefault());
            var carryingFunctional = exposureAccount.Kind == MonetaryExposureKind.Liability
                ? group.Sum(t => t.CreditAmount - t.DebitAmount)
                : group.Sum(t => t.DebitAmount - t.CreditAmount);

            var priorAdjustment = priorRevaluations
                .Where(l => l.AccountId == group.Key.AccountId && l.TransactionCurrency == group.Key.Currency)
                .Sum(l => l.GainLossAmount);
            carryingFunctional = RoundMoney(carryingFunctional + priorAdjustment);
            foreignBalance = RoundMoney(foreignBalance);

            if (foreignBalance == 0m)
            {
                continue;
            }

            var effectiveKind = foreignBalance < 0m
                ? exposureAccount.Kind == MonetaryExposureKind.Asset ? MonetaryExposureKind.Liability : MonetaryExposureKind.Asset
                : exposureAccount.Kind;

            exposures.Add(new PostedExposure(
                group.Key.AccountId,
                exposureAccount.SourceModule,
                exposureAccount.SourceDocumentType,
                exposureAccount.SourceDocumentId,
                effectiveKind,
                group.Key.Currency,
                foreignBalance,
                carryingFunctional,
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

    private FinancePostingRequestDto BuildRevaluationPostingRequest(
        FxRevaluationBatch batch,
        Guid unrealizedGainAccountId,
        Guid unrealizedLossAccountId)
    {
        var lines = new List<FinancePostingLineDto>();
        var lineNumber = 1;
        foreach (var line in batch.Lines.OrderBy(l => l.SourceModule).ThenBy(l => l.AccountId).ThenBy(l => l.TransactionCurrency))
        {
            var amount = Math.Abs(line.GainLossAmount);
            var isAssetExposure = line.SourceModule is "AR" or "BankCash";
            if (line.GainLossType == "Gain")
            {
                if (isAssetExposure)
                {
                    lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation gain - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Revaluation-Control"));
                    lines.Add(BuildFunctionalPostingLine(unrealizedGainAccountId, $"FX revaluation gain - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Unrealized-Gain"));
                }
                else
                {
                    lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation gain - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Revaluation-Control"));
                    lines.Add(BuildFunctionalPostingLine(unrealizedGainAccountId, $"FX revaluation gain - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Unrealized-Gain"));
                }
            }
            else
            {
                if (isAssetExposure)
                {
                    lines.Add(BuildFunctionalPostingLine(unrealizedLossAccountId, $"FX revaluation loss - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Unrealized-Loss"));
                    lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation loss - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Revaluation-Control"));
                }
                else
                {
                    lines.Add(BuildFunctionalPostingLine(unrealizedLossAccountId, $"FX revaluation loss - {line.TransactionCurrency}", amount, 0m, lineNumber++, "FX-Unrealized-Loss"));
                    lines.Add(BuildFunctionalPostingLine(line.AccountId, $"FX revaluation loss - {line.TransactionCurrency}", 0m, amount, lineNumber++, "FX-Revaluation-Control"));
                }
            }
        }

        return new FinancePostingRequestDto
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
            BookClassification = "IFRS",
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
        var rows = (await _paymentDimensions.GetCustomerPaymentAsync(paymentId, cancellationToken))
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

    private async Task UpdateCurrencyLinkRevaluationStateAsync(
        FxRevaluationBatch batch,
        CancellationToken cancellationToken)
    {
        var accountIds = batch.Lines.Select(line => line.AccountId).Distinct().ToArray();
        var currencies = batch.Lines.Select(line => line.TransactionCurrency).Distinct().ToArray();
        var links = await _context.AccountCurrencyLinks
            .Where(link => link.TenantId == batch.TenantId
                && accountIds.Contains(link.AccountId)
                && currencies.Contains(link.LinkedCurrencyCode)
                && !link.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var link in links)
        {
            var line = batch.Lines.FirstOrDefault(candidate =>
                candidate.AccountId == link.AccountId
                && candidate.TransactionCurrency == link.LinkedCurrencyCode);
            if (line == null)
            {
                continue;
            }

            var economicAdjustment = line.GainLossType == "Gain"
                ? Math.Abs(line.GainLossAmount)
                : -Math.Abs(line.GainLossAmount);
            link.ForeignCurrencyBalance = line.ForeignCurrencyBalance;
            link.BaseCurrencyEquivalent = line.RevaluedFunctionalAmount;
            link.CurrentExchangeRate = line.ClosingExchangeRate;
            link.RateEffectiveDate = batch.RevaluationDate;
            link.LastRevaluationDate = batch.RevaluationDate;
            link.LastRevaluationAdjustment = economicAdjustment;
            link.CumulativeRevaluationAdjustment = RoundMoney(link.CumulativeRevaluationAdjustment + economicAdjustment);
            link.UpdatedAt = DateTime.UtcNow;
            link.UpdatedBy = _currentUserService.UserName;
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
            .Append(batch.TenantId.ToString("N")).Append('|')
            .Append(batch.RevaluationDate.ToString("yyyyMMdd")).Append('|')
            .Append(batch.Scope).Append('|')
            .Append(batch.FunctionalCurrencyCode);
        foreach (var line in batch.Lines
                     .OrderBy(line => line.AccountId)
                     .ThenBy(line => line.TransactionCurrency))
        {
            evidence.Append('|').Append(line.AccountId.ToString("N"))
                .Append(':').Append(line.TransactionCurrency)
                .Append(':').Append(line.ForeignCurrencyBalance)
                .Append(':').Append(line.CarryingFunctionalAmount)
                .Append(':').Append(line.ClosingExchangeRateId.ToString("N"))
                .Append(':').Append(line.ClosingExchangeRate)
                .Append(':').Append(line.GainLossAmount);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence.ToString())))
            .ToLowerInvariant();
    }

    private static string ResolveUnrealizedGainLossType(MonetaryExposureKind kind, decimal gainLossAmount)
    {
        if (kind == MonetaryExposureKind.Asset)
        {
            return gainLossAmount > 0m ? "Gain" : "Loss";
        }

        return gainLossAmount > 0m ? "Loss" : "Gain";
    }

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
            batch.Status,
            batch.Scope,
            batch.FunctionalCurrencyCode,
            batch.TotalGainAmount,
            batch.TotalLossAmount,
            batch.NetGainLossAmount,
            batch.JournalEntryId,
            batch.PostingEventId,
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
            BookClassification = "IFRS",
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
        string SourceModule,
        MonetaryExposureKind Kind,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        RevaluationFrequency Frequency,
        string? SourceDocumentType = null,
        Guid? SourceDocumentId = null);

    private sealed record PostedExposure(
        Guid AccountId,
        string SourceModule,
        string? SourceDocumentType,
        Guid? SourceDocumentId,
        MonetaryExposureKind Kind,
        string TransactionCurrency,
        decimal ForeignCurrencyBalance,
        decimal CarryingFunctionalAmount,
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        RevaluationFrequency Frequency);

    private enum MonetaryExposureKind
    {
        Asset,
        Liability
    }
}
