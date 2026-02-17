using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.PromotionTransfer;

/// <summary>
/// Base class for all employee career movements
/// </summary>
public class EmployeeMovement : TenantEntity
{
    public string MovementNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Movement Type
    public MovementType MovementType { get; set; } // Promotion, Transfer, Demotion, Lateral, Secondment
    public MovementCategory Category { get; set; } // Voluntary, Involuntary, Organizational

    // Current Position (Before Movement)
    public Guid CurrentPositionId { get; set; }
    public EmployeePosition CurrentPosition { get; set; } = null!;

    public Guid CurrentDepartmentId { get; set; }
    public Department CurrentDepartment { get; set; } = null!;

    public Guid? CurrentSectionId { get; set; }
    public Section? CurrentSection { get; set; }

    public Guid? CurrentStationId { get; set; }
    public WorkStation? CurrentStation { get; set; }

    public Guid? CurrentSupervisorId { get; set; }
    public Employee? CurrentSupervisor { get; set; }

    public decimal CurrentSalary { get; set; }
    public string? CurrentGrade { get; set; }

    // New Position (After Movement)
    public Guid NewPositionId { get; set; }
    public EmployeePosition NewPosition { get; set; } = null!;

    public Guid NewDepartmentId { get; set; }
    public Department NewDepartment { get; set; } = null!;

    public Guid? NewSectionId { get; set; }
    public Section? NewSection { get; set; }

    public Guid? NewStationId { get; set; }
    public WorkStation? NewStation { get; set; }

    public Guid? NewSupervisorId { get; set; }
    public Employee? NewSupervisor { get; set; }

    public decimal NewSalary { get; set; }
    public string? NewGrade { get; set; }
    public decimal? SalaryIncreaseAmount { get; set; }
    public decimal? SalaryIncreasePercentage { get; set; }

    // Reason & Justification
    public string Reason { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public bool IsReorganization { get; set; }
    public bool IsSuccessionPlan { get; set; }
    public Guid? SuccessionPlanId { get; set; }

    // Dates
    public DateTime RequestDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? AnnouncementDate { get; set; }

    // For Temporary Movements (Secondment, Acting)
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public string? TemporaryArrangementDetails { get; set; }

    // Requested By
    public Guid RequestedById { get; set; }
    public Employee RequestedBy { get; set; } = null!;

    // Approval Workflow
    public MovementStatus Status { get; set; }

    public Guid? CurrentSupervisorApprovedById { get; set; }
    public Employee? CurrentSupervisorApprovedBy { get; set; }
    public DateTime? CurrentSupervisorApprovalDate { get; set; }
    public string? CurrentSupervisorComments { get; set; }

    public Guid? NewSupervisorApprovedById { get; set; }
    public Employee? NewSupervisorApprovedBy { get; set; }
    public DateTime? NewSupervisorApprovalDate { get; set; }
    public string? NewSupervisorComments { get; set; }

    public Guid? CurrentHodApprovedById { get; set; }
    public Employee? CurrentHodApprovedBy { get; set; }
    public DateTime? CurrentHodApprovalDate { get; set; }
    public string? CurrentHodComments { get; set; }

    public Guid? NewHodApprovedById { get; set; }
    public Employee? NewHodApprovedBy { get; set; }
    public DateTime? NewHodApprovalDate { get; set; }
    public string? NewHodComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    public Employee? HrApprovedBy { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? HrComments { get; set; }

    public Guid? ManagementApprovedById { get; set; }
    public Employee? ManagementApprovedBy { get; set; }
    public DateTime? ManagementApprovalDate { get; set; }
    public string? ManagementComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public Guid? RejectedById { get; set; }
    public Employee? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    // Employee Acceptance
    public bool RequiresEmployeeAcceptance { get; set; }
    public bool? EmployeeAccepted { get; set; }
    public DateTime? EmployeeResponseDate { get; set; }
    public string? EmployeeComments { get; set; }

    // Implementation
    public bool IsImplemented { get; set; }
    public DateTime? ImplementationDate { get; set; }
    public Guid? ImplementedById { get; set; }
    public Employee? ImplementedBy { get; set; }

    // Handover (for transfers)
    public bool RequiresHandover { get; set; }
    public DateTime? HandoverCompletionDate { get; set; }
    public string? HandoverNotes { get; set; }

    // Benefits/Compensation Changes
    public bool AffectsBenefits { get; set; }
    public string? BenefitChanges { get; set; }
    public bool AffectsAllowances { get; set; }
    public string? AllowanceChanges { get; set; }

    // Performance Basis
    public Guid? BasedOnAppraisalId { get; set; }
    public Performance.PerformanceAppraisal? BasedOnAppraisal { get; set; }

    // Additional Notes
    public string? AdditionalNotes { get; set; }
    public string? InternalCommunication { get; set; }
    public string? ExternalCommunication { get; set; }

    public ICollection<MovementAttachment> Attachments { get; set; } = new List<MovementAttachment>();
    public ICollection<MovementChecklistItem> ChecklistItems { get; set; } = new List<MovementChecklistItem>();
}

public class MovementAttachment : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MovementAttachmentType Type { get; set; } // Approval Letter, Performance Review, Justification
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class MovementChecklistItem : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public string TaskDescription { get; set; } = string.Empty;
    public ChecklistCategory Category { get; set; } // HR Tasks, IT Tasks, Finance Tasks, Department Tasks
    public bool IsRequired { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public Employee? ResponsiblePerson { get; set; }

    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Promotion-specific details
/// </summary>
public class Promotion : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public PromotionType Type { get; set; } // Merit-based, Seniority, Acting, Competitive
    public int GradeLevelIncrease { get; set; }

    public bool IsActingPromotion { get; set; }
    public DateTime? ActingPeriodEndDate { get; set; }
    public string? ActingConditions { get; set; }

    // Selection Process
    public bool WasCompetitiveProcess { get; set; }
    public int? NumberOfApplicants { get; set; }
    public bool InterviewConducted { get; set; }
    public DateTime? InterviewDate { get; set; }

    // Probation in New Role
    public bool HasProbationPeriod { get; set; }
    public int? ProbationMonths { get; set; }
    public DateTime? ProbationEndDate { get; set; }

    // Additional Responsibilities
    public string? AdditionalResponsibilities { get; set; }
    public string? NewAuthorities { get; set; }

    // Training Requirements
    public bool RequiresTraining { get; set; }
    public string? RequiredTraining { get; set; }
}

/// <summary>
/// Transfer-specific details
/// </summary>
public class Transfer : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public StaffTransferType Type { get; set; } // Interdepartmental, Inter-station, Cross-functional
    public TransferReason Reason { get; set; }

    // Relocation
    public bool RequiresRelocation { get; set; }
    public bool RelocationAssistanceProvided { get; set; }
    public decimal? RelocationAllowance { get; set; }
    public string? RelocationDetails { get; set; }

    // Housing
    public bool HousingAssistanceProvided { get; set; }
    public string? HousingDetails { get; set; }

    // Transition Period
    public int TransitionPeriodDays { get; set; }
    public DateTime? TransitionStartDate { get; set; }
    public DateTime? TransitionEndDate { get; set; }

    // Knowledge Transfer
    public bool RequiresKnowledgeTransfer { get; set; }
    public string? KnowledgeTransferPlan { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }
    public Employee? ReplacementEmployee { get; set; }
}

/// <summary>
/// Demotion-specific details
/// </summary>
public class Demotion : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public DemotionReason Reason { get; set; }
    public int GradeLevelDecrease { get; set; }

    // Disciplinary Related
    public bool IsDisciplinaryAction { get; set; }
    public Guid? DisciplinaryActionId { get; set; }
    public DisciplinaryAction? DisciplinaryAction { get; set; }

    // Performance Related
    public bool IsPerformanceRelated { get; set; }
    public Guid? PerformanceImprovementPlanId { get; set; }
    public Performance.PerformanceImprovementPlan? PerformanceImprovementPlan { get; set; }

    // Alternative to Termination
    public bool IsAlternativeToTermination { get; set; }
    public string? TerminationAlternativeNotes { get; set; }

    // Employee Rights
    public bool EmployeeNotified { get; set; }
    public DateTime? NotificationDate { get; set; }
    public bool RightToAppeal { get; set; }
    public DateTime? AppealDeadline { get; set; }

    // Support Plan
    public string? SupportPlan { get; set; }
    public bool CoachingProvided { get; set; }
    public string? CoachingDetails { get; set; }
}

/// <summary>
/// Secondment (temporary assignment to another department/organization)
/// </summary>
public class Secondment : TenantEntity
{
    public Guid MovementId { get; set; }
    public EmployeeMovement Movement { get; set; } = null!;

    public SecondmentType Type { get; set; } // Internal, External, Government
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationMonths { get; set; }

    // External Secondment
    public bool IsExternal { get; set; }
    public string? HostOrganization { get; set; }
    public string? HostOrganizationContact { get; set; }

    // Terms
    public string TermsAndConditions { get; set; } = string.Empty;
    public bool SalaryPaidByHomeOrganization { get; set; }
    public bool AllowancesPaidByHostOrganization { get; set; }
    public decimal? SecondmentAllowance { get; set; }

    // Objectives
    public string Objectives { get; set; } = string.Empty;
    public string? ExpectedOutcomes { get; set; }

    // Return
    public bool ReturnGuaranteed { get; set; }
    public string? ReturnArrangements { get; set; }

    // Extensions
    public bool ExtensionAllowed { get; set; }
    public int? MaxExtensionMonths { get; set; }
}

/// <summary>
/// Acting appointment (temporary elevation)
/// </summary>
public class ActingAppointment : TenantEntity
{
    public string AppointmentNumber { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid ActingPositionId { get; set; }
    public EmployeePosition ActingPosition { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ActingReason Reason { get; set; }

    // Acting for specific person
    public Guid? ActingForEmployeeId { get; set; }
    public Employee? ActingForEmployee { get; set; }

    // Compensation
    public bool ReceivesActingAllowance { get; set; }
    public decimal? ActingAllowance { get; set; }
    public AllowanceCalculationMethod? AllowanceCalculation { get; set; }

    // Status
    public ActingStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }

    // Conversion to Permanent
    public bool ConvertedToPermanent { get; set; }
    public DateTime? ConversionDate { get; set; }
    public Guid? ConversionMovementId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Career path tracking
/// </summary>
public class EmployeeCareerPath : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; }

    public Guid? MovementId { get; set; }
    public EmployeeMovement? Movement { get; set; }

    public string? Achievements { get; set; }
    public string? KeyProjects { get; set; }
}