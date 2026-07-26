using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Request DTO for closing a fiscal period
    /// </summary>
    public class PeriodCloseRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Skip validation checks (admin override)
        /// </summary>
        public bool SkipValidation { get; set; } = false;
        
        /// <summary>
        /// Notes documenting the period close
        /// </summary>
        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }
    }

    /// <summary>
    /// Validation results before closing a period
    /// </summary>
    public class PeriodCloseValidationDto
    {
        public bool CanClose { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        
        // Trial balance summary
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal Difference { get; set; }
        public bool IsBalanced => Math.Abs(Difference) < 0.01m;
        
        // Period info
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalJournalEntries { get; set; }
        public int TotalTransactionLines { get; set; }
    }

    /// <summary>
    /// Result of period close operation
    /// </summary>
    public class PeriodCloseResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime? ClosedDate { get; set; }
        public string? ClosedByUserName { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// Request DTO for reopening a closed fiscal period
    /// </summary>
    public class PeriodReopenRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Mandatory reason for reopening the period
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for locking a fiscal period
    /// </summary>
    public class PeriodLockRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Mandatory reason for locking the period
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string LockReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for unlocking a locked fiscal period.
    /// </summary>
    public class PeriodUnlockRequestDto
    {
        /// <summary>
        /// Mandatory reason for unlocking the period.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for year-end close
    /// </summary>
    public class YearEndCloseRequestDto
    {
        [Required]
        public Guid FiscalYearId { get; set; }

        [Required]
        public Guid RetainedEarningsAccountId { get; set; }

        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }
    }

    /// <summary>
    /// API request body for closing a fiscal year. The retained earnings account is optional
    /// here because the controller defaults it from Finance Settings when not supplied.
    /// </summary>
    public class CloseFiscalYearRequestDto
    {
        public Guid? RetainedEarningsAccountId { get; set; }

        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }
    }

    /// <summary>
    /// API request body for reopening a closed fiscal year.
    /// </summary>
    public class FiscalYearReopenRequestDto
    {
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// API request body for updating safe fiscal-year metadata. Dates, period structure, and
    /// close state are intentionally excluded; those change through dedicated operations.
    /// </summary>
    public class UpdateFiscalYearDto
    {
        [MaxLength(100)]
        public string? FiscalYearName { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
