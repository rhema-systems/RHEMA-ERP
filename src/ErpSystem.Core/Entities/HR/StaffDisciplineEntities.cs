using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffDiscipline;

/// <summary>
/// Disciplinary case/action against an employee
/// </summary>
public class DisciplinaryAction : TenantEntity
{
    public string CaseNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Incident Details
    public DateTime IncidentDate { get; set; }
    public string IncidentDescription { get; set; } = string.Empty;
    public OffenseCategory Category { get; set; }
    public OffenseSeverity Severity { get; set; }

    // Reported By
    public Guid ReportedById { get; set; }
    public Employee ReportedBy { get; set; } = null!;
    public DateTime ReportedDate { get; set; }

    // Investigation
    public bool RequiresInvestigation { get; set; }
    public Guid? InvestigatorId { get; set; }
    public Employee? Investigator { get; set; }

    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationEndDate { get; set; }
    public string? InvestigationFindings { get; set; }
    public string? EvidenceCollected { get; set; }

    // Hearing
    public bool HearingRequired { get; set; }
    public DateTime? HearingDate { get; set; }
    public string? HearingVenue { get; set; }
    public string? HearingNotes { get; set; }

    public Guid? HearingOfficerId { get; set; }
    public Employee? HearingOfficer { get; set; }

    public bool EmployeeAttendedHearing { get; set; }
    public string? EmployeeStatement { get; set; }
    public bool EmployeeHadRepresentation { get; set; }
    public string? RepresentativeName { get; set; }

    // Decision
    public DisciplinaryStatus Status { get; set; }
    public DisciplinaryAction? ActionTaken { get; set; }
    public string? ActionDetails { get; set; }

    public DateTime? DecisionDate { get; set; }
    public Guid? DecisionById { get; set; }
    public Employee? DecisionBy { get; set; }
    public string? DecisionRationale { get; set; }

    // Penalties
    public bool IsWarning { get; set; }
    public WarningType? WarningType { get; set; }
    public DateTime? WarningExpiryDate { get; set; }

    public bool IsSuspension { get; set; }
    public DateTime? SuspensionStartDate { get; set; }
    public DateTime? SuspensionEndDate { get; set; }
    public bool SuspensionWithPay { get; set; }

    public bool IsDemotion { get; set; }
    public Guid? DemotionFromPositionId { get; set; }
    public Guid? DemotionToPositionId { get; set; }

    public bool IsTermination { get; set; }
    public DateTime? TerminationEffectiveDate { get; set; }

    public bool IsFinePenalty { get; set; }
    public decimal? FineAmount { get; set; }

    // Appeal
    public bool AppealFiled { get; set; }
    public DateTime? AppealDate { get; set; }
    public string? AppealReason { get; set; }
    public AppealStatus? AppealStatus { get; set; }
    public DateTime? AppealHearingDate { get; set; }
    public string? AppealOutcome { get; set; }

    // Closure
    public DateTime? ClosedDate { get; set; }
    public string? ClosureNotes { get; set; }

    // Relations
    public ICollection<DisciplinaryWitness> Witnesses { get; set; } = new List<DisciplinaryWitness>();
    public ICollection<DisciplinaryDocument> Documents { get; set; } = new List<DisciplinaryDocument>();
    public ICollection<DisciplinaryNote> Notes { get; set; } = new List<DisciplinaryNote>();
}

public class DisciplinaryWitness : TenantEntity
{
    public Guid DisciplinaryActionId { get; set; }
    public DisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public bool IsEmployee { get; set; }
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? ContactInfo { get; set; }
    public string? Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class DisciplinaryDocument : TenantEntity
{
    public Guid DisciplinaryActionId { get; set; }
    public DisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; } // Evidence, Statement, Report
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class DisciplinaryNote : TenantEntity
{
    public Guid DisciplinaryActionId { get; set; }
    public DisciplinaryAction DisciplinaryAction { get; set; } = null!;

    public Guid CreatedByEmployeeId { get; set; }
    public Employee CreatedByEmployee { get; set; } = null!;

    public string Note { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public DateTime NoteDate { get; set; }
}

/// <summary>
/// Employee warning history tracker
/// </summary>
public class EmployeeWarningHistory : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid? DisciplinaryActionId { get; set; }
    public DisciplinaryAction? DisciplinaryAction { get; set; }

    public WarningType WarningType { get; set; }
    public DateTime WarningDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public Guid IssuedById { get; set; }
    public Employee IssuedBy { get; set; } = null!;
}