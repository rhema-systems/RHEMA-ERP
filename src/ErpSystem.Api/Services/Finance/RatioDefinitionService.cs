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

namespace ErpSystem.Api.Services.Finance
{
    /// <summary>
    /// Service implementation for managing Ratio Definitions and performing calculations.
    /// Ratios combine financial accounts, unit accounts, and constants to calculate KPIs.
    /// </summary>
    public class RatioDefinitionService : IRatioDefinitionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<RatioDefinitionService> _logger;

        public RatioDefinitionService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<RatioDefinitionService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.TenantId ?? Guid.Empty;
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<RatioDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var ratios = await _unitOfWork.Repository<RatioDefinition>()
                .GetQueryable(r => r.TenantId == TenantId && !r.IsDeleted)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return ratios.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<RatioDefinitionDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var ratios = await _unitOfWork.Repository<RatioDefinition>()
                .GetQueryable(r => r.TenantId == TenantId && r.IsActive && !r.IsDeleted)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return ratios.Select(MapToDto).ToList();
        }

        public async Task<RatioDefinitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            return ratio == null ? null : MapToDto(ratio);
        }

        public async Task<RatioDefinitionDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Code == code && r.TenantId == TenantId && !r.IsDeleted);

            return ratio == null ? null : MapToDto(ratio);
        }

        public async Task<RatioDefinitionDto> CreateAsync(CreateRatioDefinitionDto dto, CancellationToken cancellationToken = default)
        {
            var exists = await _unitOfWork.Repository<RatioDefinition>()
                .GetQueryable(r => r.TenantId == TenantId && r.Code == dto.Code && !r.IsDeleted)
                .AnyAsync(cancellationToken);

            if (exists)
                throw new InvalidOperationException($"Ratio definition with code '{dto.Code}' already exists.");

            var now = DateTime.UtcNow;
            var ratio = new RatioDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code.ToUpper(),
                Name = dto.Name,
                Description = dto.Description,
                NumeratorType = ParseComponentType(dto.NumeratorType),
                NumeratorAccountId = dto.NumeratorAccountId ?? dto.NumeratorUnitAccountId,
                NumeratorConstant = dto.NumeratorConstantValue,
                DenominatorType = ParseComponentType(dto.DenominatorType),
                DenominatorAccountId = dto.DenominatorAccountId ?? dto.DenominatorUnitAccountId,
                DenominatorConstant = dto.DenominatorConstantValue,
                ResultFormat = ParseResultFormat(dto.ResultFormat),
                DecimalPlaces = dto.FormatPrecision,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<RatioDefinition>().AddAsync(ratio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ratio definition {Code} created by {User}", ratio.Code, UserName);

            return MapToDto(ratio);
        }

        public async Task<RatioDefinitionDto> UpdateAsync(Guid id, UpdateRatioDefinitionDto dto, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{id}' not found.");

            if (!string.IsNullOrEmpty(dto.Name))
                ratio.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Description))
                ratio.Description = dto.Description;

            ratio.UpdatedAt = DateTime.UtcNow;
            ratio.UpdatedBy = UserName;

            await _unitOfWork.Repository<RatioDefinition>().UpdateAsync(ratio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ratio definition {Code} updated by {User}", ratio.Code, UserName);

            return MapToDto(ratio);
        }

        public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{id}' not found.");

            ratio.IsActive = true;
            ratio.UpdatedAt = DateTime.UtcNow;
            ratio.UpdatedBy = UserName;

            await _unitOfWork.Repository<RatioDefinition>().UpdateAsync(ratio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ratio definition {Code} activated by {User}", ratio.Code, UserName);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{id}' not found.");

            ratio.IsActive = false;
            ratio.UpdatedAt = DateTime.UtcNow;
            ratio.UpdatedBy = UserName;

            await _unitOfWork.Repository<RatioDefinition>().UpdateAsync(ratio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ratio definition {Code} deactivated by {User}", ratio.Code, UserName);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                return;

            ratio.IsDeleted = true;
            ratio.DeletedAt = DateTime.UtcNow;
            ratio.DeletedBy = UserName;

            await _unitOfWork.Repository<RatioDefinition>().UpdateAsync(ratio);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Ratio definition {Code} deleted by {User}", ratio.Code, UserName);
        }

        public async Task<RatioCalculationResultDto> CalculateAsync(
            Guid ratioId,
            Guid fiscalPeriodId,
            CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == ratioId && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{ratioId}' not found.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(p => p.Id == fiscalPeriodId && !p.IsDeleted);

            if (period == null)
                throw new ArgumentException($"Fiscal period with ID '{fiscalPeriodId}' not found.");

            var numerator = await GetComponentValue(ratio.NumeratorType, ratio.NumeratorAccountId, 
                ratio.NumeratorConstant, fiscalPeriodId, cancellationToken);
            
            var denominator = await GetComponentValue(ratio.DenominatorType, ratio.DenominatorAccountId, 
                ratio.DenominatorConstant, fiscalPeriodId, cancellationToken);

            decimal result = denominator != 0 ? numerator / denominator : 0;

            return new RatioCalculationResultDto
            {
                RatioId = ratio.Id,
                RatioCode = ratio.Code,
                RatioName = ratio.Name,
                FiscalPeriodId = fiscalPeriodId,
                PeriodName = period.PeriodName,
                Numerator = numerator,
                Denominator = denominator,
                Result = result,
                FormattedResult = FormatResult(result, ratio.ResultFormat, ratio.DecimalPlaces),
                CalculatedAt = DateTime.UtcNow
            };
        }

        public async Task<RatioCalculationResultDto> CalculateForDateRangeAsync(
            Guid ratioId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == ratioId && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{ratioId}' not found.");

            var numerator = await GetComponentValueForRange(ratio.NumeratorType, ratio.NumeratorAccountId, 
                ratio.NumeratorConstant, startDate, endDate, cancellationToken);
            
            var denominator = await GetComponentValueForRange(ratio.DenominatorType, ratio.DenominatorAccountId, 
                ratio.DenominatorConstant, startDate, endDate, cancellationToken);

            decimal result = denominator != 0 ? numerator / denominator : 0;

            return new RatioCalculationResultDto
            {
                RatioId = ratio.Id,
                RatioCode = ratio.Code,
                RatioName = ratio.Name,
                PeriodName = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
                Numerator = numerator,
                Denominator = denominator,
                Result = result,
                FormattedResult = FormatResult(result, ratio.ResultFormat, ratio.DecimalPlaces),
                CalculatedAt = DateTime.UtcNow
            };
        }

        public async Task<IReadOnlyList<RatioCalculationResultDto>> CalculateAllAsync(
            Guid fiscalPeriodId,
            CancellationToken cancellationToken = default)
        {
            var activeRatios = await GetActiveAsync(cancellationToken);
            var results = new List<RatioCalculationResultDto>();

            foreach (var ratio in activeRatios)
            {
                try
                {
                    var result = await CalculateAsync(ratio.Id, fiscalPeriodId, cancellationToken);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to calculate ratio {Code}", ratio.Code);
                }
            }

            return results;
        }

        public async Task<RatioTrendResultDto> GetTrendAsync(
            Guid ratioId,
            Guid fiscalYearId,
            CancellationToken cancellationToken = default)
        {
            var ratio = await _unitOfWork.Repository<RatioDefinition>()
                .FirstOrDefaultAsync(r => r.Id == ratioId && r.TenantId == TenantId && !r.IsDeleted);

            if (ratio == null)
                throw new ArgumentException($"Ratio definition with ID '{ratioId}' not found.");

            var periods = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(p => p.FiscalYearId == fiscalYearId && !p.IsDeleted)
                .OrderBy(p => p.PeriodNumber)
                .ToListAsync(cancellationToken);

            var dataPoints = new List<RatioTrendDataPointDto>();
            foreach (var period in periods)
            {
                try
                {
                    var result = await CalculateAsync(ratioId, period.Id, cancellationToken);
                    dataPoints.Add(new RatioTrendDataPointDto
                    {
                        PeriodId = period.Id,
                        PeriodName = period.PeriodName,
                        Value = result.Result
                    });
                }
                catch
                {
                    dataPoints.Add(new RatioTrendDataPointDto
                    {
                        PeriodId = period.Id,
                        PeriodName = period.PeriodName,
                        Value = 0
                    });
                }
            }

            return new RatioTrendResultDto
            {
                RatioId = ratio.Id,
                RatioCode = ratio.Code,
                RatioName = ratio.Name,
                FiscalYearId = fiscalYearId,
                DataPoints = dataPoints
            };
        }

        private async Task<decimal> GetComponentValue(
            RatioComponentType componentType, Guid? accountId, 
            decimal? constantValue, Guid fiscalPeriodId, CancellationToken cancellationToken)
        {
            switch (componentType)
            {
                case RatioComponentType.FinancialAccount:
                    if (!accountId.HasValue) return 0;
                    var glBalance = await _unitOfWork.Repository<AccountBalance>()
                        .FirstOrDefaultAsync(b => b.AccountId == accountId.Value && b.FiscalPeriodId == fiscalPeriodId);
                    return glBalance?.ClosingBalance ?? 0;

                case RatioComponentType.UnitAccount:
                    if (!accountId.HasValue) return 0;
                    var unitBalance = await _unitOfWork.Repository<UnitAccountBalance>()
                        .FirstOrDefaultAsync(b => b.UnitAccountId == accountId.Value && b.FiscalPeriodId == fiscalPeriodId);
                    return unitBalance?.ClosingBalance ?? 0;

                case RatioComponentType.Constant:
                    return constantValue ?? 0;

                default:
                    return 0;
            }
        }

        private async Task<decimal> GetComponentValueForRange(
            RatioComponentType componentType, Guid? accountId, 
            decimal? constantValue, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        {
            switch (componentType)
            {
                case RatioComponentType.FinancialAccount:
                    if (!accountId.HasValue) return 0;
                    var transactions = await _unitOfWork.Repository<AccountTransaction>()
                        .GetQueryable(t => t.AccountId == accountId.Value 
                            && t.TransactionDate >= startDate 
                            && t.TransactionDate <= endDate 
                            && !t.IsDeleted)
                        .SumAsync(t => t.DebitAmount - t.CreditAmount, cancellationToken);
                    return transactions;

                case RatioComponentType.UnitAccount:
                    if (!accountId.HasValue) return 0;
                    var account = await _unitOfWork.Repository<UnitAccount>()
                        .FirstOrDefaultAsync(a => a.Id == accountId.Value && !a.IsDeleted);
                    return account?.CurrentBalance ?? 0;

                case RatioComponentType.Constant:
                    return constantValue ?? 0;

                default:
                    return 0;
            }
        }

        private string FormatResult(decimal result, RatioResultFormat format, int precision)
        {
            return format switch
            {
                RatioResultFormat.Percentage => $"{result * 100:N}{precision}%",
                RatioResultFormat.Currency => result.ToString($"C{precision}"),
                _ => result.ToString($"N{precision}")
            };
        }

        private RatioComponentType ParseComponentType(string typeString)
        {
            return typeString switch
            {
                "FinancialAccount" => RatioComponentType.FinancialAccount,
                "UnitAccount" => RatioComponentType.UnitAccount,
                "Constant" => RatioComponentType.Constant,
                _ => RatioComponentType.FinancialAccount
            };
        }

        private RatioResultFormat ParseResultFormat(string formatString)
        {
            return formatString switch
            {
                "Percentage" => RatioResultFormat.Percentage,
                "Currency" => RatioResultFormat.Currency,
                _ => RatioResultFormat.Decimal
            };
        }

        private RatioDefinitionDto MapToDto(RatioDefinition ratio)
        {
            return new RatioDefinitionDto
            {
                Id = ratio.Id,
                Code = ratio.Code,
                Name = ratio.Name,
                Description = ratio.Description,
                RatioType = "Standard",
                NumeratorType = ratio.NumeratorType.ToString(),
                NumeratorAccountId = ratio.NumeratorType == RatioComponentType.FinancialAccount ? ratio.NumeratorAccountId : null,
                NumeratorUnitAccountId = ratio.NumeratorType == RatioComponentType.UnitAccount ? ratio.NumeratorAccountId : null,
                NumeratorConstantValue = ratio.NumeratorConstant,
                DenominatorType = ratio.DenominatorType.ToString(),
                DenominatorAccountId = ratio.DenominatorType == RatioComponentType.FinancialAccount ? ratio.DenominatorAccountId : null,
                DenominatorUnitAccountId = ratio.DenominatorType == RatioComponentType.UnitAccount ? ratio.DenominatorAccountId : null,
                DenominatorConstantValue = ratio.DenominatorConstant,
                ResultFormat = ratio.ResultFormat.ToString(),
                FormatPrecision = ratio.DecimalPlaces,
                IsActive = ratio.IsActive,
                CreatedAt = ratio.CreatedAt,
                CreatedBy = ratio.CreatedBy,
                UpdatedAt = ratio.UpdatedAt,
                UpdatedBy = ratio.UpdatedBy
            };
        }
    }
}
