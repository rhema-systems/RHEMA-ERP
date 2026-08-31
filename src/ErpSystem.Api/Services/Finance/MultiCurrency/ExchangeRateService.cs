using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.MultiCurrency
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<ExchangeRateService> _logger;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IWorkflowService? _workflowService;

        public ExchangeRateService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            ILogger<ExchangeRateService> logger,
            IFinanceAuditService? financeAuditService = null,
            IWorkflowService? workflowService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
            _financeAuditService = financeAuditService;
            _workflowService = workflowService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";
        private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : null;

        public async Task<IReadOnlyList<ExchangeRateDto>> GetExchangeRatesAsync(
            string? baseCurrency = null,
            string? targetCurrency = null,
            DateTime? effectiveDate = null,
            string? rateType = null,
            string? quoteSide = null,
            CancellationToken cancellationToken = default)
        {
            var query = _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId);

            if (!string.IsNullOrEmpty(baseCurrency))
                query = query.Where(r => r.BaseCurrencyCode == baseCurrency);

            if (!string.IsNullOrEmpty(targetCurrency))
                query = query.Where(r => r.TargetCurrencyCode == targetCurrency);

            if (effectiveDate.HasValue)
                query = query.Where(r => r.EffectiveDate.Date == effectiveDate.Value.Date);

            if (!string.IsNullOrEmpty(rateType))
                query = query.Where(r => r.RateType == ParseRateType(rateType));

            if (!string.IsNullOrEmpty(quoteSide))
                query = query.Where(r => r.QuoteSide == ParseQuoteSide(quoteSide));

            var rates = await query
                .OrderByDescending(r => r.EffectiveDate)
                .ThenBy(r => r.TargetCurrencyCode)
                .ToListAsync(cancellationToken);

            return rates.Select(MapToDto).ToList();
        }

        public async Task<ExchangeRateDto?> GetExchangeRateByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == id);

            return rate == null ? null : MapToDto(rate);
        }

        public async Task<ExchangeRateDto?> GetCurrentRateAsync(
            string targetCurrencyCode,
            string? baseCurrencyCode = null,
            DateTime? effectiveDate = null,
            string? rateType = null,
            string? quoteSide = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(baseCurrencyCode))
            {
                baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            }

            baseCurrencyCode = NormalizeCurrency(baseCurrencyCode, "Base currency");
            targetCurrencyCode = NormalizeCurrency(targetCurrencyCode, "Target currency");
            var date = effectiveDate?.Date ?? DateTime.UtcNow.Date;
            var requestedRateType = string.IsNullOrWhiteSpace(rateType)
                ? ExchangeRateType.Daily
                : ParseRateType(rateType);
            var requestedQuoteSide = string.IsNullOrWhiteSpace(quoteSide)
                ? ExchangeRateQuoteSide.Mid
                : ParseQuoteSide(quoteSide);

            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && r.BaseCurrencyCode == baseCurrencyCode
                    && r.TargetCurrencyCode == targetCurrencyCode
                    && r.RateType == requestedRateType
                    && r.QuoteSide == requestedQuoteSide
                    && r.Rate > 0
                    && r.EffectiveDate <= date
                    && r.IsActive
                    && (r.ApprovalStatus == RateApprovalStatus.Approved || r.ApprovalStatus == RateApprovalStatus.AutoApproved)
                    && (r.EndDate == null || r.EndDate >= date))
                .OrderByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return rate == null ? null : MapToDto(rate);
        }

        public async Task<IReadOnlyList<TrendAnalysisDto>> GetTrendsAsync(
            string? baseCurrencyCode,
            string targetCurrencyCode,
            DateTime startDate,
            DateTime endDate,
            string interval = "daily",
            int movingAverageWindow = 7,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(baseCurrencyCode))
            {
                baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            }

            baseCurrencyCode = NormalizeCurrency(baseCurrencyCode, "Base currency");
            targetCurrencyCode = NormalizeCurrency(targetCurrencyCode, "Target currency");

            var start = startDate.Date;
            var end = endDate.Date;
            if (end < start)
            {
                throw new InvalidOperationException("Trend end date cannot be before the start date.");
            }

            if (movingAverageWindow <= 0)
            {
                throw new InvalidOperationException("Moving average window must be greater than zero.");
            }

            var endExclusive = end.AddDays(1);
            var rawRates = await _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && r.BaseCurrencyCode == baseCurrencyCode
                    && r.TargetCurrencyCode == targetCurrencyCode
                    && r.QuoteSide == ExchangeRateQuoteSide.Mid
                    && r.Rate > 0
                    && r.EffectiveDate >= start
                    && r.EffectiveDate < endExclusive)
                .OrderBy(r => r.EffectiveDate)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.CreatedDate)
                .ToListAsync(cancellationToken);

            var rates = rawRates
                .GroupBy(r => r.EffectiveDate.Date)
                .Select(g => g
                    .OrderByDescending(r => r.Priority)
                    .ThenByDescending(r => r.CreatedDate)
                    .First())
                .OrderBy(r => r.EffectiveDate)
                .ToList();

            if (rates.Count == 0)
            {
                return Array.Empty<TrendAnalysisDto>();
            }

            var periodMin = rates.Min(r => r.Rate);
            var periodMax = rates.Max(r => r.Rate);
            var result = new List<TrendAnalysisDto>(rates.Count);

            for (var i = 0; i < rates.Count; i++)
            {
                var rate = rates[i];
                var previousRate = i == 0 ? (decimal?)null : rates[i - 1].Rate;
                var changeAmount = previousRate.HasValue ? rate.Rate - previousRate.Value : (decimal?)null;
                var changePercentage = previousRate is > 0
                    ? changeAmount / previousRate.Value * 100
                    : null;

                result.Add(new TrendAnalysisDto
                {
                    Date = rate.EffectiveDate.Date,
                    SourceCurrency = baseCurrencyCode,
                    TargetCurrency = targetCurrencyCode,
                    Rate = rate.Rate,
                    PreviousRate = previousRate,
                    ChangeAmount = changeAmount,
                    ChangePercentage = changePercentage,
                    MovingAverage = CalculateMovingAverage(rates, i, movingAverageWindow),
                    Volatility = CalculateVolatility(rates, i, movingAverageWindow),
                    MinRate = periodMin,
                    MaxRate = periodMax
                });
            }

            return result;
        }

        public async Task<ExchangeRateDto> CreateExchangeRateAsync(CreateExchangeRateDto dto, CancellationToken cancellationToken = default)
        {
            var baseCurrencyCode = NormalizeCurrency(dto.BaseCurrencyCode, "Base currency");
            var targetCurrencyCode = NormalizeCurrency(dto.TargetCurrencyCode, "Target currency");
            var rateType = ParseRateType(dto.RateType);
            EnsureOperationalRateType(rateType);
            var quoteSide = ParseQuoteSide(dto.QuoteSide);
            var requestedApprovalStatus = ParseApprovalStatus(dto.ApprovalStatus);
            var approvalStatus = _workflowService == null
                ? requestedApprovalStatus
                : RateApprovalStatus.Pending;
            ValidateRateWindow(dto.Rate, dto.EffectiveDate, dto.ExpiryDate);
            await ValidateClosingRateDateAsync(rateType, dto.EffectiveDate, cancellationToken);

            var baseCurrencyExists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == baseCurrencyCode)
                .AnyAsync(cancellationToken);

            if (!baseCurrencyExists)
                throw new ArgumentException($"Base currency '{baseCurrencyCode}' not found.");

            var targetCurrencyExists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == targetCurrencyCode)
                .AnyAsync(cancellationToken);

            if (!targetCurrencyExists)
                throw new ArgumentException($"Target currency '{targetCurrencyCode}' not found.");

            await EnsureNoOverlappingRateAsync(
                baseCurrencyCode,
                targetCurrencyCode,
                rateType,
                quoteSide,
                dto.EffectiveDate.Date,
                dto.ExpiryDate?.Date,
                excludeId: null,
                cancellationToken);

            var now = DateTime.UtcNow;
            var rate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BaseCurrencyCode = baseCurrencyCode,
                TargetCurrencyCode = targetCurrencyCode,
                Rate = dto.Rate,
                InverseRate = 1 / dto.Rate,
                EffectiveDate = dto.EffectiveDate.Date,
                EndDate = dto.ExpiryDate?.Date,
                RateType = rateType,
                QuoteSide = quoteSide,
                RateSource = dto.RateSource ?? "Manual Entry",
                IsManualEntry = true,
                APIResponseMetadata = dto.SourceReference,
                IsActive = dto.IsActive,
                ApprovalStatus = approvalStatus,
                ApprovalDate = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? now : null,
                ApprovedByUserId = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? CurrentUserId : null,
                CreatedByUserId = CurrentUserId ?? Guid.Empty,
                CreatedDate = now
            };

            await _unitOfWork.Repository<ExchangeRate>().AddAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} @ {Rate} created by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, rate.Rate, UserName);

            await RecordExchangeRateAuditAsync(FinanceAuditEvents.ExchangeRateCreated, rate, afterValues: BuildRateAuditSnapshot(rate), cancellationToken: cancellationToken);
            await StartExchangeRateWorkflowIfRequiredAsync(rate, requestedApprovalStatus, cancellationToken);

            return MapToDto(rate);
        }

        public async Task<ExchangeRateDto> UpdateExchangeRateAsync(Guid id, UpdateExchangeRateDto dto, CancellationToken cancellationToken = default)
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == id);

            if (rate == null)
                throw new ArgumentException($"Exchange rate with ID '{id}' not found.");

            if (await IsRateUsedAsync(rate, cancellationToken))
            {
                await RecordExchangeRateAuditAsync(
                    FinanceAuditEvents.ExchangeRateEditRejectedAfterUse,
                    rate,
                    afterValues: new { dto.Rate, dto.ExpiryDate, dto.RateType, dto.RateSource, dto.IsActive },
                    reason: "Exchange rate has already been used in posted accounting.",
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException("Cannot update exchange rate that has been used in transactions.");
            }

            var rateType = ParseRateType(dto.RateType);
            if (rateType != rate.RateType)
                EnsureOperationalRateType(rateType);
            var quoteSide = ParseQuoteSide(dto.QuoteSide);
            var requestedApprovalStatus = ParseApprovalStatus(dto.ApprovalStatus);
            var approvalStatus = _workflowService == null
                ? requestedApprovalStatus
                : RateApprovalStatus.Pending;
            ValidateRateWindow(dto.Rate, rate.EffectiveDate, dto.ExpiryDate);
            await ValidateClosingRateDateAsync(rateType, rate.EffectiveDate, cancellationToken);
            await EnsureNoOverlappingRateAsync(
                rate.BaseCurrencyCode,
                rate.TargetCurrencyCode,
                rateType,
                quoteSide,
                rate.EffectiveDate.Date,
                dto.ExpiryDate?.Date,
                excludeId: rate.Id,
                cancellationToken);

            var before = BuildRateAuditSnapshot(rate);

            rate.Rate = dto.Rate;
            rate.InverseRate = 1 / dto.Rate;
            rate.EndDate = dto.ExpiryDate?.Date;
            rate.RateType = rateType;
            rate.QuoteSide = quoteSide;
            rate.RateSource = dto.RateSource ?? "Manual Entry";
            rate.APIResponseMetadata = dto.SourceReference;
            rate.IsActive = dto.IsActive;
            rate.ApprovalStatus = approvalStatus;
            rate.ApprovalDate = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? DateTime.UtcNow : null;
            rate.ApprovedByUserId = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? CurrentUserId : null;
            rate.ModifiedDate = DateTime.UtcNow;
            rate.ModifiedByUserId = CurrentUserId;

            await _unitOfWork.Repository<ExchangeRate>().UpdateAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} updated by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, UserName);

            await RecordExchangeRateAuditAsync(FinanceAuditEvents.ExchangeRateUpdated, rate, beforeValues: before, afterValues: BuildRateAuditSnapshot(rate), cancellationToken: cancellationToken);
            await StartExchangeRateWorkflowIfRequiredAsync(rate, requestedApprovalStatus, cancellationToken);

            return MapToDto(rate);
        }

        public async Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == id);

            if (rate == null)
                return;

            if (await IsRateUsedAsync(rate, cancellationToken))
            {
                await RecordExchangeRateAuditAsync(
                    FinanceAuditEvents.ExchangeRateEditRejectedAfterUse,
                    rate,
                    reason: "Exchange rate has already been used in posted accounting.",
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException("Cannot delete exchange rate that has been used in transactions.");
            }

            var before = BuildRateAuditSnapshot(rate);

            await _unitOfWork.Repository<ExchangeRate>().DeleteAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} deleted by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, UserName);

            await RecordExchangeRateAuditAsync(FinanceAuditEvents.ExchangeRateDeactivated, rate, beforeValues: before, cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<ExchangeRateDto>> BulkUploadRatesAsync(
            List<CreateExchangeRateDto> rates,
            CancellationToken cancellationToken = default)
        {
            var createdRates = new List<ExchangeRate>();
            var errors = new List<string>();

            foreach (var dto in rates)
            {
                try
                {
                    var baseCurrencyCode = NormalizeCurrency(dto.BaseCurrencyCode, "Base currency");
                    var targetCurrencyCode = NormalizeCurrency(dto.TargetCurrencyCode, "Target currency");
                    var rateType = ParseRateType(dto.RateType);
                    EnsureOperationalRateType(rateType);
                    var quoteSide = ParseQuoteSide(dto.QuoteSide);
                    var requestedApprovalStatus = ParseApprovalStatus(dto.ApprovalStatus);
                    var approvalStatus = _workflowService == null
                        ? requestedApprovalStatus
                        : RateApprovalStatus.Pending;
                    ValidateRateWindow(dto.Rate, dto.EffectiveDate, dto.ExpiryDate);
                    await ValidateClosingRateDateAsync(rateType, dto.EffectiveDate, cancellationToken);
                    await EnsureNoOverlappingRateAsync(
                        baseCurrencyCode,
                        targetCurrencyCode,
                        rateType,
                        quoteSide,
                        dto.EffectiveDate.Date,
                        dto.ExpiryDate?.Date,
                        excludeId: null,
                        cancellationToken);

                    var now = DateTime.UtcNow;
                    var rate = new ExchangeRate
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        BaseCurrencyCode = baseCurrencyCode,
                        TargetCurrencyCode = targetCurrencyCode,
                        Rate = dto.Rate,
                        InverseRate = 1 / dto.Rate,
                        EffectiveDate = dto.EffectiveDate.Date,
                        EndDate = dto.ExpiryDate?.Date,
                        RateType = rateType,
                        QuoteSide = quoteSide,
                        RateSource = dto.RateSource ?? "Manual Entry",
                        IsManualEntry = true,
                        APIResponseMetadata = dto.SourceReference,
                        IsActive = dto.IsActive,
                        ApprovalStatus = approvalStatus,
                        ApprovalDate = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? now : null,
                        ApprovedByUserId = approvalStatus is RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved ? CurrentUserId : null,
                        CreatedByUserId = CurrentUserId ?? Guid.Empty,
                        CreatedDate = now
                    };

                    createdRates.Add(rate);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error processing {dto.BaseCurrencyCode}/{dto.TargetCurrencyCode}: {ex.Message}");
                }
            }

            if (createdRates.Any())
            {
                foreach (var rate in createdRates)
                {
                    await _unitOfWork.Repository<ExchangeRate>().AddAsync(rate);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                foreach (var rate in createdRates)
                {
                    await StartExchangeRateWorkflowIfRequiredAsync(rate, RateApprovalStatus.Pending, cancellationToken);
                }

                _logger.LogInformation("{Count} exchange rates bulk uploaded by {User}",
                    createdRates.Count, UserName);
            }

            if (errors.Any())
            {
                _logger.LogWarning("Bulk upload had {Count} errors: {Errors}",
                    errors.Count, string.Join("; ", errors));
            }

            return createdRates.Select(MapToDto).ToList();
        }

        private static decimal? CalculateMovingAverage(IReadOnlyList<ExchangeRate> rates, int index, int window)
        {
            if (index + 1 < window)
            {
                return null;
            }

            return rates
                .Skip(index + 1 - window)
                .Take(window)
                .Average(r => r.Rate);
        }

        private static decimal? CalculateVolatility(IReadOnlyList<ExchangeRate> rates, int index, int window)
        {
            if (index + 1 < window)
            {
                return null;
            }

            var windowRates = rates
                .Skip(index + 1 - window)
                .Take(window)
                .Select(r => r.Rate)
                .ToList();
            var average = windowRates.Average();
            var variance = windowRates.Average(rate => Math.Pow((double)(rate - average), 2));
            return (decimal)Math.Sqrt(variance);
        }

        private ExchangeRateDto MapToDto(ExchangeRate rate)
        {
            var usageCount = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == rate.TenantId
                    && t.ExchangeRateId == rate.Id
                    && !t.IsDeleted)
                .Count();

            var firstUsed = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == rate.TenantId
                    && t.ExchangeRateId == rate.Id
                    && !t.IsDeleted)
                .OrderBy(t => t.TransactionDate)
                .Select(t => (DateTime?)t.TransactionDate)
                .FirstOrDefault();

            var lastUsed = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == rate.TenantId
                    && t.ExchangeRateId == rate.Id
                    && !t.IsDeleted)
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => (DateTime?)t.TransactionDate)
                .FirstOrDefault();

            return new ExchangeRateDto
            {
                Id = rate.Id,
                TenantId = rate.TenantId,
                BaseCurrencyCode = rate.BaseCurrencyCode,
                TargetCurrencyCode = rate.TargetCurrencyCode,
                Rate = rate.Rate,
                InverseRate = rate.InverseRate,
                EffectiveDate = rate.EffectiveDate,
                ExpiryDate = rate.EndDate,
                RateType = rate.RateType.ToString(),
                QuoteSide = rate.QuoteSide.ToString(),
                RateSource = rate.RateSource,
                SourceName = rate.RateSource,
                SourceReference = rate.APIResponseMetadata,
                IsActive = rate.IsActive,
                HasBeenUsed = rate.HasBeenUsedInTransactions || usageCount > 0,
                UsageLocked = rate.HasBeenUsedInTransactions || usageCount > 0,
                ApprovalStatus = rate.ApprovalStatus.ToString(),
                UsageCount = usageCount,
                FirstUsedDate = firstUsed,
                LastUsedDate = lastUsed,
                CreatedAt = rate.CreatedDate,
                UpdatedAt = rate.ModifiedDate,
                CreatedBy = rate.CreatedByUserId.ToString(), // TODO: Resolve username
                UpdatedBy = rate.ModifiedByUserId?.ToString() // TODO: Resolve username
            };
        }

        private async Task<bool> IsRateUsedAsync(ExchangeRate rate, CancellationToken cancellationToken)
        {
            if (rate.HasBeenUsedInTransactions)
            {
                return true;
            }

            if (await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.TenantId == rate.TenantId && t.ExchangeRateId == rate.Id && !t.IsDeleted)
                .AnyAsync(cancellationToken))
            {
                return true;
            }

            return await _unitOfWork.Repository<FixedAsset>()
                .GetQueryable(asset =>
                    asset.TenantId == rate.TenantId &&
                    asset.CapitalizationApprovalExchangeRateId == rate.Id &&
                    asset.CapitalizationApprovalInvalidatedAt == null &&
                    (asset.Status == FixedAssetStatus.PendingApproval ||
                     asset.Status == FixedAssetStatus.Acquired) &&
                    !asset.IsDeleted)
                .AnyAsync(cancellationToken);
        }

        private async Task EnsureNoOverlappingRateAsync(
            string baseCurrencyCode,
            string targetCurrencyCode,
            ExchangeRateType rateType,
            ExchangeRateQuoteSide quoteSide,
            DateTime effectiveDate,
            DateTime? expiryDate,
            Guid? excludeId,
            CancellationToken cancellationToken)
        {
            var start = effectiveDate.Date;
            var end = expiryDate?.Date ?? DateTime.MaxValue.Date;
            var overlapExists = await _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && !r.IsDeleted
                    && (!excludeId.HasValue || r.Id != excludeId.Value)
                    && r.BaseCurrencyCode == baseCurrencyCode
                    && r.TargetCurrencyCode == targetCurrencyCode
                    && r.RateType == rateType
                    && r.QuoteSide == quoteSide
                    && r.EffectiveDate.Date <= end
                    && (r.EndDate == null || r.EndDate.Value.Date >= start))
                .AnyAsync(cancellationToken);

            if (overlapExists)
            {
                throw new InvalidOperationException(
                    $"Exchange rate range overlaps an existing {baseCurrencyCode}/{targetCurrencyCode} {rateType}/{quoteSide} rate for this tenant.");
            }
        }

        private static void ValidateRateWindow(decimal rate, DateTime effectiveDate, DateTime? expiryDate)
        {
            if (rate <= 0)
            {
                throw new InvalidOperationException("Exchange rate must be greater than zero.");
            }

            if (effectiveDate == default)
            {
                throw new InvalidOperationException("Exchange rate effective date is required.");
            }

            if (expiryDate.HasValue && expiryDate.Value.Date < effectiveDate.Date)
            {
                throw new InvalidOperationException("Exchange rate expiry date cannot be before the effective date.");
            }
        }

        private static string NormalizeCurrency(string? currencyCode, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                throw new InvalidOperationException($"{fieldName} is required.");
            }

            var normalized = currencyCode.Trim().ToUpperInvariant();
            if (normalized.Length != 3)
            {
                throw new InvalidOperationException($"{fieldName} must be a three-character ISO currency code.");
            }

            return normalized;
        }

        private static ExchangeRateType ParseRateType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse<ExchangeRateType>(value.Trim(), ignoreCase: true, out var rateType))
            {
                throw new InvalidOperationException("Exchange rate type is invalid.");
            }

            return rateType;
        }

        private static void EnsureOperationalRateType(ExchangeRateType rateType)
        {
            if (rateType is ExchangeRateType.Budget or ExchangeRateType.Spot)
            {
                throw new InvalidOperationException(
                    $"{rateType} exchange rates are reserved for future governed workflows and cannot be created yet.");
            }
        }

        private async Task ValidateClosingRateDateAsync(
            ExchangeRateType rateType,
            DateTime effectiveDate,
            CancellationToken cancellationToken)
        {
            var date = effectiveDate.Date;
            if (rateType == ExchangeRateType.MonthEnd)
            {
                var calendarMonthEnd = new DateTime(date.Year, date.Month, 1).AddMonths(1).AddDays(-1);
                if (date != calendarMonthEnd)
                {
                    throw new InvalidOperationException(
                        $"MonthEnd exchange rates must be dated on the actual calendar month-end ({calendarMonthEnd:yyyy-MM-dd}).");
                }

                return;
            }

            if (rateType == ExchangeRateType.QuarterEnd)
            {
                var isFiscalQuarterEnd = await _unitOfWork.Repository<FiscalPeriod>()
                    .GetQueryable(period => period.TenantId == TenantId
                        && !period.IsDeleted
                        && period.EndDate.Date == date)
                    .AnyAsync(period => period.PeriodType == PeriodType.Quarterly
                        || (period.PeriodType == PeriodType.Monthly && period.PeriodNumber % 3 == 0),
                        cancellationToken);
                if (!isFiscalQuarterEnd)
                {
                    throw new InvalidOperationException(
                        $"QuarterEnd exchange rates must be dated on a configured fiscal quarter-end; {date:yyyy-MM-dd} is not one.");
                }

                return;
            }

            if (rateType == ExchangeRateType.YearEnd)
            {
                var isFiscalYearEnd = await _unitOfWork.Repository<FiscalYear>()
                    .GetQueryable(year => year.TenantId == TenantId
                        && !year.IsDeleted
                        && year.EndDate.Date == date)
                    .AnyAsync(cancellationToken);
                if (!isFiscalYearEnd)
                {
                    throw new InvalidOperationException(
                        $"YearEnd exchange rates must be dated on a configured fiscal year-end; {date:yyyy-MM-dd} is not one.");
                }
            }
        }

        private static RateApprovalStatus ParseApprovalStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return RateApprovalStatus.Approved;
            }

            if (!Enum.TryParse<RateApprovalStatus>(value.Trim(), ignoreCase: true, out var approvalStatus))
            {
                throw new InvalidOperationException("Exchange rate approval status is invalid.");
            }

            return approvalStatus;
        }

        private static ExchangeRateQuoteSide ParseQuoteSide(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !Enum.TryParse<ExchangeRateQuoteSide>(value.Trim(), ignoreCase: true, out var quoteSide))
            {
                throw new InvalidOperationException("Exchange-rate quote side must be Mid, Buying, or Selling.");
            }

            return quoteSide;
        }

        private async Task StartExchangeRateWorkflowIfRequiredAsync(
            ExchangeRate rate,
            RateApprovalStatus requestedApprovalStatus,
            CancellationToken cancellationToken)
        {
            if (_workflowService == null)
            {
                return;
            }

            var workflowResult = await _workflowService.StartApprovalWorkflowAsync("ExchangeRate", rate.Id);
            if (!workflowResult.Success)
            {
                rate.ApprovalStatus = RateApprovalStatus.Rejected;
                rate.Comments = string.IsNullOrWhiteSpace(rate.Comments)
                    ? workflowResult.Message
                    : $"{rate.Comments}{Environment.NewLine}{workflowResult.Message}";
                rate.ModifiedDate = DateTime.UtcNow;
                rate.ModifiedByUserId = CurrentUserId;
                await _unitOfWork.Repository<ExchangeRate>().UpdateAsync(rate);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await RecordExchangeRateAuditAsync(
                    FinanceAuditEvents.FinanceWorkflowApprovalFailed,
                    rate,
                    afterValues: new { workflowResult.Status, workflowResult.Message, requestedApprovalStatus },
                    reason: workflowResult.Message,
                    cancellationToken: cancellationToken);
                throw new InvalidOperationException(workflowResult.Message ?? "Exchange rate approval workflow could not be started.");
            }

            await RecordExchangeRateAuditAsync(
                FinanceAuditEvents.FinanceWorkflowSubmitted,
                rate,
                afterValues: new
                {
                    rate.ApprovalStatus,
                    workflowResult.WorkflowInstanceId,
                    requestedApprovalStatus
                },
                cancellationToken: cancellationToken);
        }

        private async Task RecordExchangeRateAuditAsync(
            string eventType,
            ExchangeRate rate,
            object? beforeValues = null,
            object? afterValues = null,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            if (_financeAuditService == null)
            {
                return;
            }

            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = eventType,
                TenantId = rate.TenantId,
                SourceModule = "FX",
                SourceDocumentType = "ExchangeRate",
                SourceDocumentId = rate.Id,
                BeforeValues = beforeValues,
                AfterValues = afterValues,
                Reason = reason,
                Resource = "Finance.ExchangeRate",
                ResourceId = rate.Id.ToString()
            }, cancellationToken);
        }

        private static object BuildRateAuditSnapshot(ExchangeRate rate)
        {
            return new
            {
                rate.Id,
                rate.TenantId,
                rate.BaseCurrencyCode,
                rate.TargetCurrencyCode,
                rate.Rate,
                rate.InverseRate,
                rate.EffectiveDate,
                rate.EndDate,
                rate.RateType,
                rate.QuoteSide,
                rate.RateSource,
                rate.IsActive,
                rate.ApprovalStatus,
                rate.HasBeenUsedInTransactions,
                rate.TransactionCount
            };
        }
    }
}
