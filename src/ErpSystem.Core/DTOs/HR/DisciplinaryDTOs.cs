using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class DisciplinaryActionListDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public OffenseCategory OffenseCategory { get; set; }
    public string OffenseCategoryName { get; set; }
    public OffenseSeverity Severity { get; set; }
    public string SeverityName { get; set; }
    public DisciplinaryStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime IncidentDate { get; set; }
    public DateTime ReportedDate { get; set; }
    public string ReportedByName { get; set; }
    public DisciplinaryActionType? ActionTaken { get; set; }
    public string ActionTakenName { get; set; }
}

// Detail DTO
public class DisciplinaryActionDetailDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Incident Details
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public string IncidentLocation { get; set; }
    public OffenseCategory OffenseCategory { get; set; }
    public string OffenseCategoryName { get; set; }
    public OffenseSeverity Severity { get; set; }
    public string SeverityName { get; set; }
    public string IncidentDescription { get; set; }

    // Reporting
    public DateTime ReportedDate { get; set; }
    public Guid ReportedById { get; set; }
    public string ReportedByName { get; set; }

    // Investigation
    public Guid? InvestigatorId { get; set; }
    public string InvestigatorName { get; set; }
    public DateTime? InvestigationStartDate { get; set; }
    public DateTime? InvestigationEndDate { get; set; }
    public string InvestigationFindings { get; set; }
    public string EvidenceCollected { get; set; }

    // Hearing
    public Guid? HearingOfficerId { get; set; }
    public string HearingOfficerName { get; set; }
    public DateTime? HearingDate { get; set; }
    public string HearingVenue { get; set; }
    public string HearingNotes { get; set; }
    public string EmployeeStatement { get; set; }
    public bool EmployeeHadRepresentation { get; set; }
    public string RepresentativeName { get; set; }

    // Decision
    public DisciplinaryActionType? ActionTaken { get; set; }
    public string ActionTakenName { get; set; }
    public string ActionDetails { get; set; }
    public DateTime? ActionEffectiveDate { get; set; }
    public Guid? DecisionById { get; set; }
    public string DecisionByName { get; set; }
    public DateTime? DecisionDate { get; set; }
    public string DecisionRationale { get; set; }

    // Warning/Suspension
    public WarningType? WarningType { get; set; }
    public string WarningTypeName { get; set; }
    public DateTime? WarningExpiryDate { get; set; }
    public int? SuspensionDays { get; set; }
    public bool IsSuspensionPaid { get; set; }

    // Fine
    public decimal? FineAmount { get; set; }
    public DateTime? FinePaymentDueDate { get; set; }
    public bool FinePaid { get; set; }

    // Appeal
    public bool HasAppeal { get; set; }
    public AppealStatus? AppealStatus { get; set; }
    public string AppealStatusName { get; set; }
    public DateTime? AppealDate { get; set; }
    public string AppealReason { get; set; }
    public DateTime? AppealHearingDate { get; set; }
    public DateTime? AppealDecisionDate { get; set; }
    public string AppealOutcome { get; set; }

    // Status
    public DisciplinaryStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? ClosedDate { get; set; }
    public string ClosureNotes { get; set; }

    // Collections
    public List<DisciplinaryWitnessDto> Witnesses { get; set; }
    public List<DisciplinaryDocumentDto> Documents { get; set; }
    public List<DisciplinaryNoteDto> Notes { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateDisciplinaryActionDto
{
    public Guid EmployeeId { get; set; }
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public string IncidentLocation { get; set; }
    public OffenseCategory OffenseCategory { get; set; }
    public OffenseSeverity Severity { get; set; }
    public string IncidentDescription { get; set; }
    public Guid? InvestigatorId { get; set; }
    public List<CreateDisciplinaryWitnessDto> Witnesses { get; set; }
}

// Update DTO
public class UpdateDisciplinaryActionDto
{
    public Guid Id { get; set; }
    public DateTime IncidentDate { get; set; }
    public TimeSpan? IncidentTime { get; set; }
    public string IncidentLocation { get; set; }
    public OffenseCategory OffenseCategory { get; set; }
    public OffenseSeverity Severity { get; set; }
    public string IncidentDescription { get; set; }
    public string InvestigationFindings { get; set; }
    public string EvidenceCollected { get; set; }
}

// Schedule Hearing DTO
public class ScheduleHearingDto
{
    public Guid Id { get; set; }
    public Guid HearingOfficerId { get; set; }
    public DateTime HearingDate { get; set; }
    public string HearingVenue { get; set; }
}

// Record Decision DTO
public class RecordDisciplinaryDecisionDto
{
    public Guid Id { get; set; }
    public DisciplinaryActionType ActionTaken { get; set; }
    public string ActionDetails { get; set; }
    public DateTime ActionEffectiveDate { get; set; }
    public string DecisionRationale { get; set; }

    // Warning specific
    public WarningType? WarningType { get; set; }
    public DateTime? WarningExpiryDate { get; set; }

    // Suspension specific
    public int? SuspensionDays { get; set; }
    public bool IsSuspensionPaid { get; set; }

    // Fine specific
    public decimal? FineAmount { get; set; }
    public DateTime? FinePaymentDueDate { get; set; }
}

// Appeal DTO
public class FileDisciplinaryAppealDto
{
    public Guid Id { get; set; }
    public DateTime AppealDate { get; set; }
    public string AppealReason { get; set; }
}

// Supporting DTOs
public class DisciplinaryWitnessDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string ContactInfo { get; set; }
    public string Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class CreateDisciplinaryWitnessDto
{
    public string Name { get; set; }
    public Guid? EmployeeId { get; set; }
    public string ContactInfo { get; set; }
    public string Statement { get; set; }
    public DateTime? StatementDate { get; set; }
}

public class DisciplinaryDocumentDto
{
    public Guid Id { get; set; }
    public DocumentCategory Category { get; set; }
    public string CategoryName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class DisciplinaryNoteDto
{
    public Guid Id { get; set; }
    public string Note { get; set; }
    public Guid CreatedByEmployeeId { get; set; }
    public string CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Warning History DTO
public class EmployeeWarningHistoryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public WarningType WarningType { get; set; }
    public string WarningTypeName { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; }
    public Guid? DisciplinaryActionId { get; set; }
    public string CaseNumber { get; set; }
    public string IssuedByName { get; set; }
}

// Dashboard DTO
public class DisciplinaryDashboardDto
{
    public int TotalCases { get; set; }
    public int OpenCases { get; set; }
    public int UnderInvestigation { get; set; }
    public int PendingHearing { get; set; }
    public int CompletedCases { get; set; }
    public Dictionary<OffenseCategory, int> CasesByCategory { get; set; }
    public Dictionary<OffenseSeverity, int> CasesBySeverity { get; set; }
    public List<DisciplinaryActionListDto> RecentCases { get; set; }
}