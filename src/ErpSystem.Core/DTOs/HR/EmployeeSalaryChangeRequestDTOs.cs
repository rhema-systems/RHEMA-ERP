using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>A salary change request as the Salary tab and the approver read it (round 3, lane S).</summary>
public class SalaryChangeRequestDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public SalaryChangeKind Kind { get; set; }
    public string KindName => Kind.ToString();
    public SalaryChangeRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public PayBasis CurrentPayBasis { get; set; }
    public Guid? CurrentGradeId { get; set; }
    public string? CurrentGradeCode { get; set; }
    public Guid? CurrentLevelId { get; set; }
    public Guid? CurrentNotchId { get; set; }
    public string? CurrentNotchNumber { get; set; }
    public decimal? CurrentAmount { get; set; }

    public PayBasis? ProposedPayBasis { get; set; }
    public Guid? ProposedGradeId { get; set; }
    public string? ProposedGradeCode { get; set; }
    public string? ProposedGradeName { get; set; }
    public Guid? ProposedLevelId { get; set; }
    public string? ProposedLevelCode { get; set; }
    public Guid? ProposedNotchId { get; set; }
    public string? ProposedNotchNumber { get; set; }
    /// <summary>The notch's amount when a notch is named — what the person will be paid.</summary>
    public decimal? ProposedNotchAmount { get; set; }
    public decimal? ProposedAmount { get; set; }
    public string? ProposedCurrencyCode { get; set; }

    /// <summary>The figure the change resolves to (notch amount or the negotiated amount), for the approver's card.</summary>
    public decimal? ResultingMonthlyBasicPay { get; set; }

    public DateTime EffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;

    public Guid RequestedById { get; set; }
    public string? RequestedByName { get; set; }
    public Guid? SourceProposalId { get; set; }

    public string? RejectionReason { get; set; }
    public DateTime? HrAppliedOn { get; set; }
    public DateTime? AppliedOn { get; set; }
    public Guid? AppliedPlacementId { get; set; }
    public string? ApplyFailure { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Body of <c>POST api/hr/salary-change-requests</c>.</summary>
public class CreateSalaryChangeRequestDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public SalaryChangeKind Kind { get; set; }

    public PayBasis? ProposedPayBasis { get; set; }
    public Guid? ProposedGradeId { get; set; }
    public Guid? ProposedLevelId { get; set; }
    public Guid? ProposedNotchId { get; set; }

    [Range(0, 999999999)]
    public decimal? ProposedAmount { get; set; }

    [MaxLength(10)]
    public string? ProposedCurrencyCode { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? SourceProposalId { get; set; }
}

/// <summary>Body of <c>PUT api/hr/salary-change-requests/{id}</c>. Draft or Rejected only.</summary>
public class UpdateSalaryChangeRequestDto
{
    [Required]
    public SalaryChangeKind Kind { get; set; }

    public PayBasis? ProposedPayBasis { get; set; }
    public Guid? ProposedGradeId { get; set; }
    public Guid? ProposedLevelId { get; set; }
    public Guid? ProposedNotchId { get; set; }

    [Range(0, 999999999)]
    public decimal? ProposedAmount { get; set; }

    [MaxLength(10)]
    public string? ProposedCurrencyCode { get; set; }

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Body of the reject action.</summary>
public class RejectSalaryChangeRequestDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}
