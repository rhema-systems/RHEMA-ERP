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
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.UnitAccounting
{
    /// <summary>
    /// Service implementation for Allocation Rule operations.
    /// </summary>
    public class AllocationService : IAllocationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<AllocationService> _logger;

        public AllocationService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<AllocationService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<AllocationRuleDto>> GetAllRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return rules.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<AllocationRuleDto>> GetActiveRulesAsync(CancellationToken cancellationToken = default)
        {
            var rules = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.TenantId == TenantId && r.IsActive && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);

            return rules.Select(MapToDto).ToList();
        }

        public async Task<AllocationRuleDto?> GetRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetDriverUnitAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return rule == null ? null : MapToDto(rule);
        }

        public async Task<AllocationRuleDto?> GetRuleByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Code == code && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return rule == null ? null : MapToDto(rule);
        }

        public async Task<AllocationRuleDto> CreateRuleAsync(CreateAllocationRuleDto dto, CancellationToken cancellationToken = default)
        {
            // Check for duplicate code
            var existing = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Code == dto.Code && r.TenantId == TenantId && !r.IsDeleted);
            if (existing != null)
                throw new InvalidOperationException($"An allocation rule with code '{dto.Code}' already exists.");

            await ValidateAllocationReferencesAsync(
                dto.SourceAccountId,
                dto.DriverUnitAccountId,
                dto.Targets,
                cancellationToken);

            var rule = new AllocationRule
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = dto.Code,
                Name = dto.Name,
                Description = dto.Description,
                SourceAccountId = dto.SourceAccountId,
                AllocationType = Enum.Parse<AllocationType>(dto.AllocationType),
                DriverUnitAccountId = dto.DriverUnitAccountId,
                IsActive = true,
                AutoReverse = dto.AutoReverse,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<AllocationRule>().AddAsync(rule);

            // Add targets
            foreach (var targetDto in dto.Targets)
            {
                var target = new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AllocationRuleId = rule.Id,
                    TargetAccountId = targetDto.TargetAccountId,
                    FixedPercentage = targetDto.FixedPercentage,
                    TargetDriverUnitAccountId = targetDto.TargetDriverUnitAccountId,
                    CostCenterCode = targetDto.CostCenterCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<AllocationTarget>().AddAsync(target);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} created", dto.Code);

            return (await GetRuleByIdAsync(rule.Id, cancellationToken))!;
        }

        public async Task<AllocationRuleDto> UpdateRuleAsync(Guid id, UpdateAllocationRuleDto dto, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.Targets)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            await ValidateAllocationReferencesAsync(
                dto.SourceAccountId,
                dto.DriverUnitAccountId,
                dto.Targets,
                cancellationToken);

            rule.Name = dto.Name;
            rule.Description = dto.Description;
            rule.SourceAccountId = dto.SourceAccountId;
            rule.AllocationType = Enum.Parse<AllocationType>(dto.AllocationType);
            rule.DriverUnitAccountId = dto.DriverUnitAccountId;
            rule.IsActive = dto.IsActive;
            rule.AutoReverse = dto.AutoReverse;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);

            // Remove existing targets
            foreach (var target in rule.Targets.ToList())
            {
                target.IsDeleted = true;
                target.DeletedAt = DateTime.UtcNow;
                target.DeletedBy = UserName;
                await _unitOfWork.Repository<AllocationTarget>().UpdateAsync(target);
            }

            // Add new targets
            foreach (var targetDto in dto.Targets)
            {
                var target = new AllocationTarget
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    AllocationRuleId = rule.Id,
                    TargetAccountId = targetDto.TargetAccountId,
                    FixedPercentage = targetDto.FixedPercentage,
                    TargetDriverUnitAccountId = targetDto.TargetDriverUnitAccountId,
                    CostCenterCode = targetDto.CostCenterCode,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName
                };
                await _unitOfWork.Repository<AllocationTarget>().AddAsync(target);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} updated", rule.Code);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                return;

            rule.IsDeleted = true;
            rule.DeletedAt = DateTime.UtcNow;
            rule.DeletedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} deleted", rule.Code);
        }

        public async Task<AllocationRuleDto> ActivateRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            rule.IsActive = true;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationRuleDto> DeactivateRuleAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == TenantId && !r.IsDeleted);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{id}' not found.");

            rule.IsActive = false;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;

            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (await GetRuleByIdAsync(id, cancellationToken))!;
        }

        public async Task<AllocationResultDto> RunAllocationAsync(RunAllocationDto dto, CancellationToken cancellationToken = default)
        {
            var rule = await _unitOfWork.Repository<AllocationRule>()
                .GetQueryable(r => r.Id == dto.AllocationRuleId && r.TenantId == TenantId && !r.IsDeleted)
                .Include(r => r.SourceAccount)
                .Include(r => r.DriverUnitAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetAccount)
                .Include(r => r.Targets)
                    .ThenInclude(t => t.TargetDriverUnitAccount)
                .FirstOrDefaultAsync(cancellationToken);

            if (rule == null)
                throw new ArgumentException($"Allocation rule with ID '{dto.AllocationRuleId}' not found.");

            if (!rule.IsActive)
                throw new InvalidOperationException("Cannot run an inactive allocation rule.");

            var fiscalPeriodExists = await _unitOfWork.Repository<FiscalPeriod>()
                .GetQueryable(p => p.TenantId == TenantId && p.Id == dto.FiscalPeriodId && !p.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!fiscalPeriodExists)
                throw new ArgumentException($"Fiscal period with ID '{dto.FiscalPeriodId}' not found.");

            // Get source account balance (simplified - would need to get actual balance)
            var sourceBalance = rule.SourceAccount?.Balance ?? 0;
            
            // Calculate allocation based on type
            var lines = new List<AllocationLineResultDto>();
            decimal totalAllocated = 0;

            var activeTargets = rule.Targets.Where(t => t.TenantId == TenantId && !t.IsDeleted).ToList();

            switch (rule.AllocationType)
            {
                case AllocationType.FixedPercentage:
                    foreach (var target in activeTargets)
                    {
                        var percent = target.FixedPercentage ?? 0;
                        var amount = sourceBalance * (percent / 100);
                        lines.Add(new AllocationLineResultDto(
                            target.TargetAccountId,
                            target.TargetAccount?.AccountNumber ?? "",
                            target.TargetAccount?.AccountName ?? "",
                            percent,
                            percent,
                            amount
                        ));
                        totalAllocated += amount;
                    }
                    break;

                case AllocationType.EqualDistribution:
                    var equalPercent = activeTargets.Count > 0 ? 100m / activeTargets.Count : 0;
                    var equalAmount = activeTargets.Count > 0 ? sourceBalance / activeTargets.Count : 0;
                    foreach (var target in activeTargets)
                    {
                        lines.Add(new AllocationLineResultDto(
                            target.TargetAccountId,
                            target.TargetAccount?.AccountNumber ?? "",
                            target.TargetAccount?.AccountName ?? "",
                            equalAmount,
                            equalPercent,
                            equalAmount
                        ));
                        totalAllocated += equalAmount;
                    }
                    break;

                case AllocationType.UnitAccountBased:
                    // Get driver values for each target
                    decimal totalDriverValue = 0;
                    var targetDriverValues = new Dictionary<Guid, decimal>();
                    
                    foreach (var target in activeTargets)
                    {
                        decimal driverValue = 0;
                        if (target.TargetDriverUnitAccountId.HasValue)
                        {
                            var driverAccount = target.TargetDriverUnitAccount;
                            driverValue = driverAccount?.CurrentBalance ?? 0;
                        }
                        targetDriverValues[target.Id] = driverValue;
                        totalDriverValue += driverValue;
                    }

                    foreach (var target in activeTargets)
                    {
                        var driverValue = targetDriverValues[target.Id];
                        var percent = totalDriverValue > 0 ? (driverValue / totalDriverValue) * 100 : 0;
                        var amount = totalDriverValue > 0 ? sourceBalance * (driverValue / totalDriverValue) : 0;
                        lines.Add(new AllocationLineResultDto(
                            target.TargetAccountId,
                            target.TargetAccount?.AccountNumber ?? "",
                            target.TargetAccount?.AccountName ?? "",
                            driverValue,
                            Math.Round(percent, 2),
                            amount
                        ));
                        totalAllocated += amount;
                    }
                    break;
            }

            // Update last run date
            rule.LastRunDate = DateTime.UtcNow;
            rule.UpdatedAt = DateTime.UtcNow;
            rule.UpdatedBy = UserName;
            await _unitOfWork.Repository<AllocationRule>().UpdateAsync(rule);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Allocation rule {Code} run with total {Amount}", rule.Code, totalAllocated);

            // In a full implementation, this would create a journal entry
            return new AllocationResultDto(
                rule.Id,
                rule.Code,
                rule.Name,
                DateTime.UtcNow,
                Guid.Empty, // Would be actual journal entry ID
                "ALLOC-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
                totalAllocated,
                lines
            );
        }

        private static AllocationRuleDto MapToDto(AllocationRule rule)
        {
            return new AllocationRuleDto(
                rule.Id,
                rule.Code,
                rule.Name,
                rule.Description,
                rule.SourceAccountId,
                rule.SourceAccount?.AccountNumber ?? "",
                rule.SourceAccount?.AccountName ?? "",
                rule.AllocationType.ToString(),
                rule.DriverUnitAccountId,
                rule.DriverUnitAccount?.AccountNumber,
                rule.DriverUnitAccount?.Name,
                rule.IsActive,
                rule.AutoReverse,
                rule.LastRunDate,
                rule.CreatedAt,
                rule.Targets.Where(t => !t.IsDeleted).Select(t => new AllocationTargetDto(
                    t.Id,
                    t.TargetAccountId,
                    t.TargetAccount?.AccountNumber ?? "",
                    t.TargetAccount?.AccountName ?? "",
                    t.FixedPercentage,
                    t.TargetDriverUnitAccountId,
                    t.TargetDriverUnitAccount?.AccountNumber,
                    t.CostCenterCode
                )).ToList()
            );
        }

        private async Task ValidateAllocationReferencesAsync(
            Guid sourceAccountId,
            Guid? driverUnitAccountId,
            IReadOnlyCollection<CreateAllocationTargetDto> targets,
            CancellationToken cancellationToken)
        {
            var sourceAccountExists = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && a.Id == sourceAccountId && !a.IsDeleted)
                .AnyAsync(cancellationToken);
            if (!sourceAccountExists)
                throw new ArgumentException($"Source account with ID '{sourceAccountId}' not found.");

            if (driverUnitAccountId.HasValue)
            {
                var driverExists = await _unitOfWork.Repository<UnitAccount>()
                    .GetQueryable(a => a.TenantId == TenantId && a.Id == driverUnitAccountId.Value && !a.IsDeleted)
                    .AnyAsync(cancellationToken);
                if (!driverExists)
                    throw new ArgumentException($"Driver unit account with ID '{driverUnitAccountId.Value}' not found.");
            }

            var targetAccountIds = targets.Select(t => t.TargetAccountId).Distinct().ToList();
            var validTargetAccountCount = await _unitOfWork.Repository<Account>()
                .GetQueryable(a => a.TenantId == TenantId && targetAccountIds.Contains(a.Id) && !a.IsDeleted)
                .CountAsync(cancellationToken);
            if (validTargetAccountCount != targetAccountIds.Count)
                throw new ArgumentException("One or more target accounts were not found for the current tenant.");

            var targetDriverIds = targets
                .Where(t => t.TargetDriverUnitAccountId.HasValue)
                .Select(t => t.TargetDriverUnitAccountId!.Value)
                .Distinct()
                .ToList();
            if (targetDriverIds.Count == 0)
                return;

            var validTargetDriverCount = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(a => a.TenantId == TenantId && targetDriverIds.Contains(a.Id) && !a.IsDeleted)
                .CountAsync(cancellationToken);
            if (validTargetDriverCount != targetDriverIds.Count)
                throw new ArgumentException("One or more target driver unit accounts were not found for the current tenant.");
        }
    }
}
