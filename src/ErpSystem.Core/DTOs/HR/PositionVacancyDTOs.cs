using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// POSITION VACANCY DTOs
// ============================================================================

#region Position Vacancy

/// <summary>Full detail of a single tracked position vacancy.</summary>
public class PositionVacancyDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? PositionCode { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    public Guid? VacatedByEmployeeId { get; set; }
    public string? VacatedByEmployeeName { get; set; }

    public VacancyReason Reason { get; set; }
    public string ReasonName => Reason.ToString();

    public DateTime VacatedDate { get; set; }

    public bool IsAnticipated { get; set; }
    public DateTime? ExpectedVacancyDate { get; set; }

    public PositionVacancyStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public VacancyClassification Classification { get; set; }
    public string ClassificationName => Classification.ToString();

    public int ExpectedHeadcount { get; set; }
    public int ActiveHeadcountAtDetection { get; set; }

    /// <summary>Live count of active occupants on the position right now (may differ from the detection snapshot).</summary>
    public int CurrentActiveHeadcount { get; set; }

    public Guid? StaffRequisitionId { get; set; }
    public string? StaffRequisitionNumber { get; set; }

    public string? Notes { get; set; }

    public DateTime? ClosedDate { get; set; }
    public string? ClosedReason { get; set; }

    /// <summary>Days the vacancy has been open (to closed date, or to now if still open).</summary>
    public int AgeInDays { get; set; }
}

/// <summary>List-row projection of a position vacancy.</summary>
public class PositionVacancySummaryDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? OrganizationUnitName { get; set; }
    public string? VacatedByEmployeeName { get; set; }
    public VacancyReason Reason { get; set; }
    public string ReasonName => Reason.ToString();
    public DateTime VacatedDate { get; set; }
    public bool IsAnticipated { get; set; }
    public DateTime? ExpectedVacancyDate { get; set; }
    public PositionVacancyStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public VacancyClassification Classification { get; set; }
    public string ClassificationName => Classification.ToString();
    public Guid? StaffRequisitionId { get; set; }
    public string? StaffRequisitionNumber { get; set; }
    public int AgeInDays { get; set; }
}

/// <summary>
/// Establishment view of a single position for the "all positions — filled &amp; vacant" grid the
/// stakeholder asked for. Counts are computed live; the vacancy link surfaces any open tracking row.
/// </summary>
public class PositionEstablishmentDto
{
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public string? PositionCode { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    /// <summary>
    /// The stored headcount — <b>meaningless unless <see cref="IsEstablished"/></b>. 186 of 231
    /// live positions carry the column default of 1 (measured 2026-09-10); only
    /// <c>EstablishmentApprovedOn</c> makes the number an authorisation.
    /// </summary>
    public int ExpectedHeadcount { get; set; }
    public int FilledCount { get; set; }

    /// <summary>
    /// Round 2b, R4a. ⚠ Before this the grid reported a "gap" of 0 of 1 for every unestablished
    /// post — the opposite of the rule every enforcement path uses — and reconcile opened a
    /// vacancy for each one with nobody in it (38 phantom gaps on the live tenant, none on an
    /// established post). A gap can only be stated where a headcount was authorised.
    /// </summary>
    public bool IsEstablished { get; set; }
    public DateTime? EstablishmentApprovedOn { get; set; }
    public Guid? EstablishmentSourceBudgetId { get; set; }
    public string? EstablishmentSourceBudgetNumber { get; set; }

    /// <summary>Whether a gap can be stated at all — the same as <see cref="IsEstablished"/>, named for the reader.</summary>
    public bool GapKnown => IsEstablished;
    public int VacantCount => IsEstablished ? Math.Max(0, ExpectedHeadcount - FilledCount) : 0;
    public bool IsFullyFilled => IsEstablished && VacantCount == 0;
    public bool IsOverEstablishment => IsEstablished && FilledCount > ExpectedHeadcount;

    /// <summary>The open tracking vacancy for this position, if one has been logged.</summary>
    public Guid? OpenVacancyId { get; set; }
    public PositionVacancyStatus? OpenVacancyStatus { get; set; }
    public string? OpenVacancyStatusName => OpenVacancyStatus?.ToString();
    public DateTime? OldestOpenVacancyDate { get; set; }
}

/// <summary>Counts for the vacant-positions dashboard summary cards.</summary>
public class PositionVacancyStatsDto
{
    public int TotalOpen { get; set; }
    public int Anticipated { get; set; }
    public int UnderReview { get; set; }
    public int RequisitionRaised { get; set; }
    public int WithinEstablishment { get; set; }
    public int NoShortfallOrOver { get; set; }

    public int TotalPositions { get; set; }
    public int PositionsWithVacancy { get; set; }
}

public class UpdatePositionVacancyStatusDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    public PositionVacancyStatus NewStatus { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class ClosePositionVacancyDto
{
    [Required]
    public Guid VacancyId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Optional overrides when raising a staff requisition from a vacancy. Everything is pre-filled from
/// the vacancy/position; the requester can tune these before the requisition form opens.
/// </summary>
public class RaiseRequisitionFromVacancyDto
{
    public int? NumberOfPositions { get; set; }
    public DateTime? DesiredStartDate { get; set; }

    [MaxLength(200)]
    public string? RequisitionTitle { get; set; }

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }

    public StaffRequisitionPriority? Priority { get; set; }
}

/// <summary>Result of raising a requisition — carries the new requisition id so the UI can navigate to edit it.</summary>
public class RaiseRequisitionResultDto
{
    public Guid RequisitionId { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public Guid VacancyId { get; set; }
}

/// <summary>Result of a reconcile run.</summary>
public class ReconcileVacanciesResultDto
{
    public int Opened { get; set; }
    public int Closed { get; set; }
    public int Scanned { get; set; }
}

#endregion
