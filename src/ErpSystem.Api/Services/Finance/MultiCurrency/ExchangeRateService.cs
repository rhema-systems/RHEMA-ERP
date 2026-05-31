using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.Finance.MultiCurrency
{
    public class ExchangeRateService : IExchangeRateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITenantSettingsService _tenantSettingsService;
        private readonly ILogger<ExchangeRateService> _logger;

        public ExchangeRateService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ITenantSettingsService tenantSettingsService,
            ILogger<ExchangeRateService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _tenantSettingsService = tenantSettingsService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";
        private Guid? CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : null;

        public async Task<IReadOnlyList<ExchangeRateDto>> GetExchangeRatesAsync(
            string? baseCurrency = null,
            string? targetCurrency = null,
            DateTime? effectiveDate = null,
            string? rateType = null,
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
                query = query.Where(r => r.RateType == (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), rateType));

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
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(baseCurrencyCode))
            {
                baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
            }

            var date = effectiveDate?.Date ?? DateTime.UtcNow.Date;

            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && r.BaseCurrencyCode == baseCurrencyCode
                    && r.TargetCurrencyCode == targetCurrencyCode
                    && r.EffectiveDate <= date
                    && r.IsActive
                    && (r.EndDate == null || r.EndDate >= date))
                .OrderByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            return rate == null ? null : MapToDto(rate);
        }

        public async Task<ExchangeRateDto> CreateExchangeRateAsync(CreateExchangeRateDto dto, CancellationToken cancellationToken = default)
        {
            var baseCurrencyExists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == dto.BaseCurrencyCode)
                .AnyAsync(cancellationToken);

            if (!baseCurrencyExists)
                throw new ArgumentException($"Base currency '{dto.BaseCurrencyCode}' not found.");

            var targetCurrencyExists = await _unitOfWork.Repository<Currency>()
                .GetQueryable(c => c.TenantId == TenantId && c.CurrencyCode == dto.TargetCurrencyCode)
                .AnyAsync(cancellationToken);

            if (!targetCurrencyExists)
                throw new ArgumentException($"Target currency '{dto.TargetCurrencyCode}' not found.");

            var duplicate = await _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && r.BaseCurrencyCode == dto.BaseCurrencyCode
                    && r.TargetCurrencyCode == dto.TargetCurrencyCode
                    && r.EffectiveDate.Date == dto.EffectiveDate.Date
                    && r.RateType == (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), dto.RateType))
                .AnyAsync(cancellationToken);

            if (duplicate)
                throw new InvalidOperationException(
                    $"Exchange rate for {dto.BaseCurrencyCode}/{dto.TargetCurrencyCode} " +
                    $"on {dto.EffectiveDate:yyyy-MM-dd} with type '{dto.RateType}' already exists.");

            var now = DateTime.UtcNow;
            var rate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BaseCurrencyCode = dto.BaseCurrencyCode.ToUpper(),
                TargetCurrencyCode = dto.TargetCurrencyCode.ToUpper(),
                Rate = dto.Rate,
                InverseRate = dto.Rate > 0 ? 1 / dto.Rate : 0,
                EffectiveDate = dto.EffectiveDate.Date,
                EndDate = dto.ExpiryDate?.Date,
                RateType = (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), dto.RateType),
                RateSource = dto.RateSource ?? "Manual Entry",
                IsManualEntry = true,
                APIResponseMetadata = dto.SourceReference,
                IsActive = dto.IsActive,
                CreatedByUserId = CurrentUserId ?? Guid.Empty,
                CreatedDate = now
            };

            await _unitOfWork.Repository<ExchangeRate>().AddAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} @ {Rate} created by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, rate.Rate, UserName);

            return MapToDto(rate);
        }

        public async Task<ExchangeRateDto> UpdateExchangeRateAsync(Guid id, UpdateExchangeRateDto dto, CancellationToken cancellationToken = default)
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == id);

            if (rate == null)
                throw new ArgumentException($"Exchange rate with ID '{id}' not found.");

            var hasBeenUsed = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.ExchangeRate == rate.Rate
                    && t.TransactionCurrency == rate.TargetCurrencyCode
                    && t.TransactionDate.Date == rate.EffectiveDate.Date
                    && !t.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasBeenUsed)
                throw new InvalidOperationException("Cannot update exchange rate that has been used in transactions.");

            rate.Rate = dto.Rate;
            rate.InverseRate = dto.Rate > 0 ? 1 / dto.Rate : 0;
            rate.EndDate = dto.ExpiryDate?.Date;
            rate.RateType = (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), dto.RateType);
            rate.RateSource = dto.RateSource ?? "Manual Entry";
            rate.APIResponseMetadata = dto.SourceReference;
            rate.IsActive = dto.IsActive;
            rate.ModifiedDate = DateTime.UtcNow;
            rate.ModifiedByUserId = CurrentUserId;

            await _unitOfWork.Repository<ExchangeRate>().UpdateAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} updated by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, UserName);

            return MapToDto(rate);
        }

        public async Task DeleteExchangeRateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rate = await _unitOfWork.Repository<ExchangeRate>()
                .FirstOrDefaultAsync(r => r.TenantId == TenantId && r.Id == id);

            if (rate == null)
                return;

            var hasBeenUsed = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.ExchangeRate == rate.Rate
                    && t.TransactionCurrency == rate.TargetCurrencyCode
                    && t.TransactionDate.Date == rate.EffectiveDate.Date
                    && !t.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasBeenUsed)
                throw new InvalidOperationException("Cannot delete exchange rate that has been used in transactions.");

            await _unitOfWork.Repository<ExchangeRate>().DeleteAsync(rate);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Exchange rate {Base}/{Target} deleted by {User}",
                rate.BaseCurrencyCode, rate.TargetCurrencyCode, UserName);
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
                    var duplicate = await _unitOfWork.Repository<ExchangeRate>()
                        .GetQueryable(r => r.TenantId == TenantId
                            && r.BaseCurrencyCode == dto.BaseCurrencyCode
                            && r.TargetCurrencyCode == dto.TargetCurrencyCode
                            && r.EffectiveDate.Date == dto.EffectiveDate.Date
                            && r.RateType == (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), dto.RateType))
                        .AnyAsync(cancellationToken);

                    if (duplicate)
                    {
                        errors.Add($"Duplicate rate: {dto.BaseCurrencyCode}/{dto.TargetCurrencyCode} on {dto.EffectiveDate:yyyy-MM-dd}");
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    var rate = new ExchangeRate
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        BaseCurrencyCode = dto.BaseCurrencyCode.ToUpper(),
                        TargetCurrencyCode = dto.TargetCurrencyCode.ToUpper(),
                        Rate = dto.Rate,
                        InverseRate = dto.Rate > 0 ? 1 / dto.Rate : 0,
                        EffectiveDate = dto.EffectiveDate.Date,
                        EndDate = dto.ExpiryDate?.Date,
                        RateType = (ExchangeRateType)Enum.Parse(typeof(ExchangeRateType), dto.RateType),
                        RateSource = dto.RateSource ?? "Manual Entry",
                        IsManualEntry = true,
                        APIResponseMetadata = dto.SourceReference,
                        IsActive = dto.IsActive,
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

        private ExchangeRateDto MapToDto(ExchangeRate rate)
        {
            var usageCount = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.ExchangeRate == rate.Rate
                    && t.TransactionCurrency == rate.TargetCurrencyCode
                    && t.TransactionDate.Date == rate.EffectiveDate.Date
                    && !t.IsDeleted)
                .Count();

            var firstUsed = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.ExchangeRate == rate.Rate
                    && t.TransactionCurrency == rate.TargetCurrencyCode
                    && t.TransactionDate.Date == rate.EffectiveDate.Date
                    && !t.IsDeleted)
                .OrderBy(t => t.TransactionDate)
                .Select(t => (DateTime?)t.TransactionDate)
                .FirstOrDefault();

            var lastUsed = _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.ExchangeRate == rate.Rate
                    && t.TransactionCurrency == rate.TargetCurrencyCode
                    && t.TransactionDate.Date == rate.EffectiveDate.Date
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
                RateSource = rate.RateSource,
                SourceName = rate.RateSource,
                SourceReference = rate.APIResponseMetadata,
                IsActive = rate.IsActive,
                HasBeenUsed = usageCount > 0,
                UsageCount = usageCount,
                FirstUsedDate = firstUsed,
                LastUsedDate = lastUsed,
                CreatedAt = rate.CreatedDate,
                UpdatedAt = rate.ModifiedDate,
                CreatedBy = rate.CreatedByUserId.ToString(), // TODO: Resolve username
                UpdatedBy = rate.ModifiedByUserId?.ToString() // TODO: Resolve username
            };
        }

        private async Task<Dictionary<Guid, string>> GetUsersDictionaryAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken)
        {
            var userIds = new HashSet<Guid>();
            foreach (var r in rates)
            {
                userIds.Add(r.CreatedByUserId);
                if (r.ModifiedByUserId.HasValue) userIds.Add(r.ModifiedByUserId.Value);
                if (r.ApprovedByUserId.HasValue) userIds.Add(r.ApprovedByUserId.Value);
            }

            userIds.Remove(Guid.Empty);
            if (!userIds.Any()) return new Dictionary<Guid, string>();

            var users = await _userService.GetUsersByIdsAsync(userIds);
            return users.ToDictionary(u => u.Id, u => !string.IsNullOrEmpty(u.UserName) ? u.UserName : (!string.IsNullOrEmpty(u.Email) ? u.Email : "Unknown user"));
        }

        public async Task<IReadOnlyList<TrendAnalysisDto>> GetTrendsAsync(
            string? sourceCurrency,
            string targetCurrency,
            DateTime startDate,
            DateTime endDate,
            string groupBy = "daily",
            int movingAverageWindow = 7,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sourceCurrency))
            {
                sourceCurrency = await _tenantSettingsService.GetBaseCurrencyAsync();
            }

            var query = _unitOfWork.Repository<ExchangeRate>()
                .GetQueryable(r => r.TenantId == TenantId
                    && r.BaseCurrencyCode == sourceCurrency
                    && r.TargetCurrencyCode == targetCurrency
                    && r.EffectiveDate >= startDate.Date
                    && r.EffectiveDate <= endDate.Date)
                .OrderBy(r => r.EffectiveDate);

            var rates = await query.ToListAsync(cancellationToken);

            var groupedRates = rates
                .GroupBy(r => groupBy.ToLower() switch
                {
                    "weekly" => r.EffectiveDate.AddDays(-(int)r.EffectiveDate.DayOfWeek).Date,
                    "monthly" => new DateTime(r.EffectiveDate.Year, r.EffectiveDate.Month, 1),
                    _ => r.EffectiveDate.Date
                })
                .Select(g => new
                {
                    Date = g.Key,
                    Rate = g.Average(r => r.Rate),
                    MinRate = g.Min(r => r.Rate),
                    MaxRate = g.Max(r => r.Rate),
                    Rates = g.Select(r => r.Rate).ToList()
                })
                .OrderBy(g => g.Date)
                .ToList();

            var trends = new List<TrendAnalysisDto>();
            var allRates = rates.Select(r => r.Rate).ToList();

            for (int i = 0; i < groupedRates.Count; i++)
            {
                var current = groupedRates[i];
                decimal? previousRate = i > 0 ? groupedRates[i - 1].Rate : null;
                decimal? changeAmount = previousRate.HasValue ? current.Rate - previousRate.Value : null;
                decimal? changePercentage = previousRate.HasValue && previousRate.Value != 0
                    ? (changeAmount / previousRate.Value) * 100m
                    : null;

                // Moving Average
                decimal? movingAverage = null;
                if (i >= movingAverageWindow - 1)
                {
                    movingAverage = groupedRates
                        .Skip(i - movingAverageWindow + 1)
                        .Take(movingAverageWindow)
                        .Average(g => g.Rate);
                }

                // Volatility (Standard Deviation)
                decimal? volatility = null;
                if (current.Rates.Count > 1)
                {
                    var avg = current.Rates.Average();
                    var sumOfSquaresOfDifferences = current.Rates.Select(val => (val - avg) * (val - avg)).Sum();
                    volatility = (decimal)Math.Sqrt((double)(sumOfSquaresOfDifferences / (current.Rates.Count - 1)));
                }

                trends.Add(new TrendAnalysisDto
                {
                    Date = current.Date,
                    SourceCurrency = sourceCurrency,
                    TargetCurrency = targetCurrency,
                    Rate = current.Rate,
                    PreviousRate = previousRate,
                    ChangeAmount = changeAmount,
                    ChangePercentage = changePercentage,
                    MovingAverage = movingAverage,
                    Volatility = volatility,
                    MinRate = current.MinRate,
                    MaxRate = current.MaxRate
                });
            }

            return trends;
        }
    }
}
