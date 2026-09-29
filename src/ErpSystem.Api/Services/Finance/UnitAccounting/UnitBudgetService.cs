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
    /// Service implementation for Unit Account Budget operations.
    /// </summary>
    public class UnitBudgetService : IUnitBudgetService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UnitBudgetService> _logger;

        public UnitBudgetService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<UnitBudgetService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<UnitAccountBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var budgets = await _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(b => b.TenantId == TenantId && !b.IsDeleted)
                .Include(b => b.UnitAccount)
                    .ThenInclude(a => a!.UnitType)
                .Include(b => b.FiscalPeriod)
                .OrderByDescending(b => b.FiscalPeriod!.StartDate)
                .ThenBy(b => b.UnitAccount!.AccountNumber)
                .ToListAsync(cancellationToken);

            return budgets.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitAccountBudgetDto>> GetByAccountAsync(Guid unitAccountId, CancellationToken cancellationToken = default)
        {
            var budgets = await _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(b => b.TenantId == TenantId && b.UnitAccountId == unitAccountId && !b.IsDeleted)
                .Include(b => b.UnitAccount)
                    .ThenInclude(a => a!.UnitType)
                .Include(b => b.FiscalPeriod)
                .OrderByDescending(b => b.FiscalPeriod!.StartDate)
                .ToListAsync(cancellationToken);

            return budgets.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitAccountBudgetDto>> GetByPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default)
        {
            var budgets = await _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(b => b.TenantId == TenantId && b.FiscalPeriodId == fiscalPeriodId && !b.IsDeleted)
                .Include(b => b.UnitAccount)
                    .ThenInclude(a => a!.UnitType)
                .Include(b => b.FiscalPeriod)
                .OrderBy(b => b.UnitAccount!.AccountNumber)
                .ToListAsync(cancellationToken);

            return budgets.Select(MapToDto).ToList();
        }

        public async Task<UnitAccountBudgetDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var budget = await _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted)
                .Include(b => b.UnitAccount)
                    .ThenInclude(a => a!.UnitType)
                .Include(b => b.FiscalPeriod)
                .FirstOrDefaultAsync(cancellationToken);

            return budget == null ? null : MapToDto(budget);
        }

        public async Task<UnitAccountBudgetDto> CreateAsync(CreateUnitAccountBudgetDto dto, CancellationToken cancellationToken = default)
        {
            // Validate unit account exists
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(a => a.Id == dto.UnitAccountId && a.TenantId == TenantId && a.IsActive && !a.IsDeleted);
            if (account == null)
                throw new ArgumentException($"Active unit account with ID '{dto.UnitAccountId}' not found.");

            var period = await _unitOfWork.Repository<FiscalPeriod>()
                .FirstOrDefaultAsync(p => p.Id == dto.FiscalPeriodId && p.TenantId == TenantId && !p.IsDeleted);
            if (period == null)
                throw new ArgumentException($"Fiscal period with ID '{dto.FiscalPeriodId}' not found.");
            if (period.FiscalYearId != dto.FiscalYearId)
                throw new InvalidOperationException("The selected fiscal period does not belong to the selected fiscal year.");

            var budgetVersion = string.IsNullOrWhiteSpace(dto.BudgetVersion)
                ? "Original"
                : dto.BudgetVersion.Trim();

            // Check for duplicate budget entry
            var existing = await _unitOfWork.Repository<UnitAccountBudget>()
                .FirstOrDefaultAsync(b => 
                    b.TenantId == TenantId && 
                    b.UnitAccountId == dto.UnitAccountId && 
                    b.FiscalPeriodId == dto.FiscalPeriodId &&
                    b.BudgetVersion == budgetVersion &&
                    !b.IsDeleted);
            if (existing != null)
                throw new InvalidOperationException("A budget entry already exists for this account and period.");

            var budget = new UnitAccountBudget
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                UnitAccountId = dto.UnitAccountId,
                FiscalYearId = dto.FiscalYearId,
                FiscalPeriodId = dto.FiscalPeriodId,
                BudgetQuantity = dto.BudgetQuantity,
                Notes = dto.Notes,
                BudgetVersion = budgetVersion,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<UnitAccountBudget>().AddAsync(budget);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit budget created for account {AccountId} period {PeriodId}", 
                dto.UnitAccountId, dto.FiscalPeriodId);

            return (await GetByIdAsync(budget.Id, cancellationToken))!;
        }

        public async Task<UnitAccountBudgetDto> UpdateAsync(Guid id, UpdateUnitAccountBudgetDto dto, CancellationToken cancellationToken = default)
        {
            var budget = await _unitOfWork.Repository<UnitAccountBudget>()
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted);

            if (budget == null)
                throw new ArgumentException($"Unit budget with ID '{id}' not found.");

            budget.BudgetQuantity = dto.BudgetQuantity;
            budget.Notes = dto.Notes;
            budget.IsActive = dto.IsActive;
            budget.UpdatedAt = DateTime.UtcNow;
            budget.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitAccountBudget>().UpdateAsync(budget);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit budget {Id} updated", id);

            return (await GetByIdAsync(id, cancellationToken))!;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var budget = await _unitOfWork.Repository<UnitAccountBudget>()
                .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == TenantId && !b.IsDeleted);

            if (budget == null)
                return;

            budget.IsDeleted = true;
            budget.DeletedAt = DateTime.UtcNow;
            budget.DeletedBy = UserName;

            await _unitOfWork.Repository<UnitAccountBudget>().UpdateAsync(budget);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit budget {Id} deleted", id);
        }

        public async Task<IReadOnlyList<BudgetVarianceDto>> GetVariancesAsync(Guid? fiscalPeriodId = null, CancellationToken cancellationToken = default)
        {
            IQueryable<UnitAccountBudget> query = _unitOfWork.Repository<UnitAccountBudget>()
                .GetQueryable(b => b.TenantId == TenantId && b.IsActive && !b.IsDeleted)
                .Include(b => b.UnitAccount)
                    .ThenInclude(a => a!.UnitType)
                .Include(b => b.FiscalPeriod);

            if (fiscalPeriodId.HasValue)
                query = query.Where(b => b.FiscalPeriodId == fiscalPeriodId.Value);

            var budgets = await query.ToListAsync(cancellationToken);

            var variances = new List<BudgetVarianceDto>();
            foreach (var budget in budgets)
            {
                var account = budget.UnitAccount;
                if (account == null) continue;

                var actualQuantity = await _unitOfWork.Repository<UnitAccountBalance>()
                    .GetQueryable(balance => balance.TenantId == TenantId
                        && balance.UnitAccountId == budget.UnitAccountId
                        && balance.FiscalPeriodId == budget.FiscalPeriodId
                        && !balance.IsDeleted)
                    .Select(balance => (decimal?)balance.PeriodActivity)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0m;

                var budgetQuantity = budget.BudgetQuantity;
                var variance = actualQuantity - budgetQuantity;
                var variancePercent = budgetQuantity != 0 ? (variance / budgetQuantity) * 100 : 0;

                variances.Add(new BudgetVarianceDto(
                    budget.UnitAccountId,
                    account.AccountNumber,
                    account.Name,
                    account.UnitType?.Code ?? "",
                    budget.FiscalPeriod?.PeriodName ?? "",
                    budgetQuantity,
                    actualQuantity,
                    variance,
                    Math.Round(variancePercent, 2),
                    variance >= 0 // IsFavorable depends on context, simplified here
                ));
            }

            return variances;
        }

        private static UnitAccountBudgetDto MapToDto(UnitAccountBudget budget)
        {
            return new UnitAccountBudgetDto(
                budget.Id,
                budget.UnitAccountId,
                budget.UnitAccount?.AccountNumber ?? "",
                budget.UnitAccount?.Name ?? "",
                budget.UnitAccount?.UnitType?.Code ?? "",
                budget.FiscalYearId,
                budget.FiscalPeriodId,
                budget.FiscalPeriod?.PeriodName ?? "",
                budget.BudgetQuantity,
                budget.Notes,
                budget.BudgetVersion,
                budget.IsActive,
                budget.CreatedAt
            );
        }
    }
}
