using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>DTO used to record a new utilization/claim against an enrollment's coverage limit.</summary>
public class CreateBenefitUtilizationDto
{
    [Required]
    public Guid EnrollmentId { get; set; }

    /// <summary>Optional dependent the expense is attributed to; null = the employee directly.</summary>
    public Guid? EmployeeDependentId { get; set; }

    [Required]
    public DateOnly ClaimDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    public BenefitUtilizationType Type { get; set; } = BenefitUtilizationType.Expense;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }
}

/// <summary>DTO used to transition a utilization/claim's status (approve, reject, pay, cancel).</summary>
public class ClaimStatusChangeDto
{
    [Required]
    public BenefitClaimStatus Status { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>Read DTO for a utilization/claim row.</summary>
public class BenefitUtilizationDto : BaseDto
{
    public Guid EnrollmentId { get; set; }

    public Guid? EmployeeDependentId { get; set; }

    /// <summary>Display name of the attributed dependent, if any.</summary>
    public string? DependentName { get; set; }

    public DateOnly ClaimDate { get; set; }

    public decimal Amount { get; set; }

    public BenefitUtilizationType Type { get; set; }

    public BenefitClaimStatus Status { get; set; }

    public string? Description { get; set; }

    public string? ReferenceNumber { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? RejectionReason { get; set; }
}

/// <summary>
/// The computed coverage balance for an enrollment in its current usage period. The amounts reflect
/// the lazy periodic reset (Monthly/Annual per the policy's LimitPeriod; Lifetime never resets).
/// </summary>
public class EnrollmentBalanceDto
{
    public Guid EnrollmentId { get; set; }

    /// <summary>Resolved coverage ceiling (policy or grade/level value).</summary>
    public decimal CoverageLimit { get; set; }

    /// <summary>Approved/Paid amount consumed within the current period.</summary>
    public decimal UtilizedAmount { get; set; }

    /// <summary>CoverageLimit − UtilizedAmount (never negative).</summary>
    public decimal RemainingAmount { get; set; }

    public BenefitLimitPeriod LimitPeriod { get; set; }

    /// <summary>Start of the current usage window (null for Lifetime).</summary>
    public DateTime? PeriodStart { get; set; }

    /// <summary>End of the current usage window (null for Lifetime / open-ended).</summary>
    public DateTime? PeriodEnd { get; set; }

    public string Currency { get; set; } = "GHS";
}
