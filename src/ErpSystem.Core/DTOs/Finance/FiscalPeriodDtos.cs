using System;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// READ DTO: Represents a Fiscal Period as returned by the Finance API.
    /// Used for period management and transaction date validation.
    /// </summary>
    public class FiscalPeriodDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        
        /// <summary>Reference to the fiscal year this period belongs to</summary>
        public Guid FiscalYearId { get; set; }
        
        /// <summary>Period name (e.g., "January 2025", "Q1-2025")</summary>
        public string PeriodName { get; set; } = string.Empty;
        
        /// <summary>Period code (e.g., "2025-01", "2025-Q1")</summary>
        public string PeriodCode { get; set; } = string.Empty;
        
        /// <summary>Period number within fiscal year (1-12 for monthly)</summary>
        public int PeriodNumber { get; set; }
        
        /// <summary>Period type: Monthly, Quarterly, Weekly, Daily</summary>
        public PeriodType PeriodType { get; set; }
        
        /// <summary>Period start date</summary>
        public DateTime StartDate { get; set; }
        
        /// <summary>Period end date</summary>
        public DateTime EndDate { get; set; }
        
        /// <summary>Number of days in this period</summary>
        public int PeriodDays { get; set; }
        
        /// <summary>Status: "Future", "Open", "Closed", "Locked"</summary>
        public string PeriodStatus { get; set; } = "Future";
        
        /// <summary>TRUE if period allows transaction posting</summary>
        public bool IsOpen { get; set; }
        
        /// <summary>TRUE if period is permanently locked</summary>
        public bool IsLocked { get; set; }

        /// <summary>TRUE when a global lock is temporarily represented by module locks.</summary>
        public bool IsGlobalLockSuspended { get; set; }

        /// <summary>TRUE when at least one lockable module is locked while the period is open.</summary>
        public bool IsPartiallyLocked { get; set; }
        
        /// <summary>Date when period was locked</summary>
        public DateTime? LockedDate { get; set; }
        
        /// <summary>TRUE if period close initiated</summary>
        public bool IsCloseInitiated { get; set; }
        
        /// <summary>Date when close was initiated</summary>
        public DateTime? CloseInitiatedDate { get; set; }
        
        /// <summary>TRUE if period has been closed</summary>
        public bool IsClosed { get; set; }
        
        /// <summary>Date when period was closed</summary>
        public DateTime? ClosedDate { get; set; }

        /// <summary>
        /// Latest approved numbered close cycle that can be rendered as a signed evidence pack.
        /// This remains populated after a reopen so Finance can retrieve the superseded historical
        /// certificate while the next close cycle is still being worked.
        /// </summary>
        public Guid? LatestClosePackCycleId { get; set; }

        /// <summary>
        /// Latest controlled reopen decision. The period list exposes it so makers cannot create
        /// duplicate requests and higher-tier reviewers can act from the existing Finance screen.
        /// </summary>
        public FinancePeriodReopenRequestDto? LatestReopenRequest { get; set; }
        
        /// <summary>TRUE if trial balance validated</summary>
        public bool TrialBalanceValidated { get; set; }
        
        /// <summary>TRUE if bank reconciliation complete</summary>
        public bool BankReconciliationComplete { get; set; }
        
        /// <summary>TRUE if currency revaluation complete</summary>
        public bool CurrencyRevaluationComplete { get; set; }
        
        /// <summary>TRUE if depreciation complete</summary>
        public bool DepreciationComplete { get; set; }
        
        /// <summary>TRUE if inventory valuation complete</summary>
        public bool InventoryValuationComplete { get; set; }
        
        /// <summary>TRUE if accruals complete</summary>
        public bool AccrualsComplete { get; set; }
        
        /// <summary>TRUE if period has been reopened after close</summary>
        public bool HasBeenReopened { get; set; }
        
        /// <summary>Count of times period has been reopened</summary>
        public int ReopenCount { get; set; }
        
        /// <summary>Date when period was last reopened</summary>
        public DateTime? LastReopenedDate { get; set; }
        
        /// <summary>TRUE if this is the final period of fiscal year</summary>
        public bool IsYearEnd { get; set; }
        
        /// <summary>TRUE if year-end close complete</summary>
        public bool YearEndCloseComplete { get; set; }
        
        /// <summary>Total journal entries in this period</summary>
        public int TotalJournalEntries { get; set; }
        
        /// <summary>Total transaction lines in this period</summary>
        public int TotalTransactionLines { get; set; }
        
        /// <summary>Total debits for the period</summary>
        public decimal TotalDebits { get; set; }
        
        /// <summary>Total credits for the period</summary>
        public decimal TotalCredits { get; set; }
        
        /// <summary>Balance difference (should be 0.00)</summary>
        public decimal BalanceDifference { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Module-specific lock statuses.
        /// </summary>
        public List<PeriodModuleLockDto> ModuleLocks { get; set; } = new List<PeriodModuleLockDto>();
    }

    /// <summary>
    /// DTO for module-level period lock status.
    /// </summary>
    public class PeriodModuleLockDto
    {
        public Guid Id { get; set; }
        public Guid FiscalPeriodId { get; set; }
        public Guid ModuleDefinitionId { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public DateTime? LockedDate { get; set; }
        public string? LockedByUserName { get; set; }
        public string? LockReason { get; set; }
        public DateTime? UnlockedDate { get; set; }
        public string? UnlockedByUserName { get; set; }
        public string? UnlockReason { get; set; }
        public DateTime? ReopenExpiresAtUtc { get; set; }
        public DateTime? AutoRelockedDate { get; set; }
        public bool IsTemporaryReopening { get; set; }
    }

    /// <summary>
    /// DTO for module definitions.
    /// </summary>
    public class ModuleDefinitionDto
    {
        public Guid Id { get; set; }
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }

    public class ModuleLockRequestDto
    {
        [Required]
        public string ModuleCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string? Reason { get; set; }

        /// <summary>
        /// Required when reopening a module. Must be in the future and no more than
        /// 24 hours from the request time.
        /// </summary>
        public DateTime? ReopenUntilUtc { get; set; }
    }
}
