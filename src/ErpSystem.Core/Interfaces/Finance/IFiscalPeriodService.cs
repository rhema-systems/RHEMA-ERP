using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Service contract for managing Fiscal Years and Fiscal Periods.
    /// Handles period management, close/reopen operations, and date validation.
    /// </summary>
    public interface IFiscalPeriodService
    {
        // Fiscal Year Operations
        
        /// <summary>
        /// Retrieves all fiscal years for the current tenant.
        /// </summary>
        Task<IReadOnlyList<FiscalYearDto>> GetFiscalYearsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single fiscal year by ID.
        /// </summary>
        Task<FiscalYearDto?> GetFiscalYearByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new fiscal year with periods.
        /// </summary>
        Task<FiscalYearDto> CreateFiscalYearAsync(CreateFiscalYearDto dto, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a fiscal year if it has no transactions.
        /// </summary>
        Task DeleteFiscalYearAsync(Guid id, CancellationToken cancellationToken = default);

        // Fiscal Period Operations
        
        /// <summary>
        /// Retrieves fiscal periods, optionally filtered by fiscal year.
        /// </summary>
        Task<IReadOnlyList<FiscalPeriodDto>> GetFiscalPeriodsAsync(
            Guid? fiscalYearId = null,
            string? status = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a single fiscal period by ID.
        /// </summary>
        Task<FiscalPeriodDto?> GetFiscalPeriodByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the fiscal period for a given transaction date.
        /// Used for transaction date validation.
        /// </summary>
        Task<FiscalPeriodDto?> GetPeriodForDateAsync(DateTime transactionDate, CancellationToken cancellationToken = default);

        // Period Close/Reopen Operations
        
        /// <summary>
        /// Closes a fiscal period.
        /// </summary>
        Task<PeriodCloseResultDto> ClosePeriodAsync(PeriodCloseRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reopens a closed fiscal period.
        /// </summary>
        Task<FiscalPeriodDto> ReopenPeriodAsync(PeriodReopenRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Locks a fiscal period (permanent lock).
        /// </summary>
        Task<FiscalPeriodDto> LockPeriodAsync(PeriodLockRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks a locked fiscal period (requires admin permission).
        /// </summary>
        Task<FiscalPeriodDto> UnlockPeriodAsync(Guid periodId, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates if a period can be closed.
        /// </summary>
        Task<PeriodCloseValidationDto> ValidatePeriodCloseAsync(Guid periodId, CancellationToken cancellationToken = default);

        // Module-Level Locking Operations
        
        /// <summary>
        /// Locks a period for a specific module.
        /// </summary>
        Task<FiscalPeriodDto> LockPeriodForModuleAsync(Guid periodId, string moduleCode, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks a period for a specific module.
        /// </summary>
        Task<FiscalPeriodDto> UnlockPeriodForModuleAsync(Guid periodId, string moduleCode, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a period is locked for a specific module (checks both global and module lock).
        /// </summary>
        Task<bool> IsPeriodLockedForModuleAsync(Guid periodId, string moduleCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all available module definitions.
        /// </summary>
        Task<IReadOnlyList<ModuleDefinitionDto>> GetModuleDefinitionsAsync(CancellationToken cancellationToken = default);
    }
}
