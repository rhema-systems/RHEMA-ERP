using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service interface for Unit Account Budget operations.
    /// </summary>
    public interface IUnitBudgetService
    {
        /// <summary>
        /// Get all unit account budgets for the current tenant.
        /// </summary>
        Task<IReadOnlyList<UnitAccountBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all budgets for a specific unit account.
        /// </summary>
        Task<IReadOnlyList<UnitAccountBudgetDto>> GetByAccountAsync(Guid unitAccountId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get budgets for a specific fiscal period.
        /// </summary>
        Task<IReadOnlyList<UnitAccountBudgetDto>> GetByPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a specific budget by ID.
        /// </summary>
        Task<UnitAccountBudgetDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a new unit account budget.
        /// </summary>
        Task<UnitAccountBudgetDto> CreateAsync(CreateUnitAccountBudgetDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update an existing budget.
        /// </summary>
        Task<UnitAccountBudgetDto> UpdateAsync(Guid id, UpdateUnitAccountBudgetDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a budget entry.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get budget vs actual variances for a period.
        /// </summary>
        Task<IReadOnlyList<BudgetVarianceDto>> GetVariancesAsync(Guid? fiscalPeriodId = null, CancellationToken cancellationToken = default);
    }
}
