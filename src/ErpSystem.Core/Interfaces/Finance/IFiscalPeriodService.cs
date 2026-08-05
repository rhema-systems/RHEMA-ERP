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

        /// <summary>
        /// Updates safe fiscal-year metadata (name/notes). Close state and dates are excluded.
        /// </summary>
        Task<FiscalYearDto> UpdateFiscalYearAsync(Guid id, UpdateFiscalYearDto dto, CancellationToken cancellationToken = default);

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
        /// Submits a controlled request to reopen a closed fiscal period. The period remains
        /// closed until an independent higher-tier reviewer approves the request.
        /// </summary>
        Task<FinancePeriodReopenRequestDto> RequestPeriodReopenAsync(
            PeriodReopenRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the independent reopen decision. Approval revalidates the affected-period
        /// snapshot, reopens the period, supersedes cycle N, and creates cycle N+1 atomically.
        /// </summary>
        Task<FinancePeriodReopenRequestDto> ReviewPeriodReopenAsync(
            Guid periodId,
            Guid requestId,
            PeriodReopenReviewDto request,
            CancellationToken cancellationToken = default);

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

        /// <summary>
        /// Evaluates and returns the persisted Finance close workspace for a period.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> EvaluatePeriodCloseWorkspaceAsync(Guid periodId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs the preparer declaration after every mandatory automated check passes.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> PreparePeriodCloseAsync(
            Guid periodId,
            PeriodClosePreparationRequestDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists every retained close-template version for the tenant. Superseded versions remain
        /// visible because historical cycles may refer to them.
        /// </summary>
        Task<IReadOnlyList<FinanceCloseTemplateDto>> GetFinanceCloseTemplatesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new draft version. An approved version is never edited in place.
        /// </summary>
        Task<FinanceCloseTemplateDto> CreateFinanceCloseTemplateVersionAsync(
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates task design and metadata while a template version remains Draft.
        /// </summary>
        Task<FinanceCloseTemplateDto> UpdateFinanceCloseTemplateDraftAsync(
            Guid templateId,
            SaveFinanceCloseTemplateVersionDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Approves and activates a draft through maker-checker; the author cannot approve it.
        /// </summary>
        Task<FinanceCloseTemplateDto> ApproveFinanceCloseTemplateAsync(
            Guid templateId,
            ApproveFinanceCloseTemplateDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Assigns or completes a manual task in the active cycle with retained evidence.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> UpdateFinanceCloseTaskAsync(
            Guid periodId,
            Guid taskId,
            UpdateFinanceCloseTaskDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Links a controlled tenant upload to an active close task as retained evidence.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> LinkFinanceCloseEvidenceAsync(
            Guid periodId,
            Guid taskId,
            LinkFinanceCloseEvidenceDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an unused evidence link while the close cycle remains in progress. The shared
        /// uploaded object is retained until its normal file-retention operation runs.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> RemoveFinanceCloseEvidenceAsync(
            Guid periodId,
            Guid taskId,
            Guid attachmentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests controlled acceptance of the exact failed/warning check snapshot.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> RequestFinanceCloseExceptionWaiverAsync(
            Guid periodId,
            Guid snapshotId,
            RequestFinanceCloseWaiverDto request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records a second-person waiver decision and re-evaluates approved evidence immediately.
        /// </summary>
        Task<FinanceCloseWorkspaceDto> ReviewFinanceCloseExceptionWaiverAsync(
            Guid periodId,
            Guid waiverId,
            ReviewFinanceCloseWaiverDto request,
            CancellationToken cancellationToken = default);

        // Module-Level Locking Operations
        
        /// <summary>
        /// Locks a period for a specific module.
        /// </summary>
        Task<FiscalPeriodDto> LockPeriodForModuleAsync(Guid periodId, string moduleCode, string reason, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks a period for a specific module.
        /// </summary>
        Task<FiscalPeriodDto> UnlockPeriodForModuleAsync(
            Guid periodId,
            string moduleCode,
            string reason,
            DateTime reopenUntilUtc,
            CancellationToken cancellationToken = default);

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
