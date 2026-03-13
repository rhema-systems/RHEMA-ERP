using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class EmployeeMovementListDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public MovementType MovementType { get; set; }
    public string MovementTypeName { get; set; }
    public string CurrentPosition { get; set; }
    public string NewPosition { get; set; }
    public string CurrentDepartment { get; set; }
    public string NewDepartment { get; set; }
    public DateTime EffectiveDate { get; set; }
    public MovementStatus Status { get; set; }
    public string StatusName { get; set; }
    public decimal? SalaryIncreasePercentage { get; set; }
}

// Detail DTO
public class EmployeeMovementDetailDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }

    // Movement Details
    public MovementType MovementType { get; set; }
    public string MovementTypeName { get; set; }
    public MovementCategory Category { get; set; }
    public string CategoryName { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Reason { get; set; }
    public string Justification { get; set; }

    // Current Position Details
    public Guid CurrentPositionId { get; set; }
    public string CurrentPositionName { get; set; }
    public Guid CurrentDepartmentId { get; set; }
    public string CurrentDepartmentName { get; set; }
    public Guid? CurrentSectionId { get; set; }
    public string CurrentSectionName { get; set; }
    public Guid? CurrentStationId { get; set; }
    public string CurrentStationName { get; set; }
    public Guid? CurrentSupervisorId { get; set; }
    public string CurrentSupervisorName { get; set; }
    public string CurrentGrade { get; set; }
    public decimal CurrentSalary { get; set; }

    // New Position Details
    public Guid NewPositionId { get; set; }
    public string NewPositionName { get; set; }
    public Guid NewDepartmentId { get; set; }
    public string NewDepartmentName { get; set; }
    public Guid? NewSectionId { get; set; }
    public string NewSectionName { get; set; }
    public Guid? NewStationId { get; set; }
    public string NewStationName { get; set; }
    public Guid? NewSupervisorId { get; set; }
    public string NewSupervisorName { get; set; }
    public string NewGrade { get; set; }
    public decimal NewSalary { get; set; }
    public decimal SalaryIncreaseAmount { get; set; }
    public decimal SalaryIncreasePercentage { get; set; }

    // Temporary Arrangement (for Secondment/Acting)
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryStartDate { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public string TemporaryArrangementDetails { get; set; }

    // Status
    public MovementStatus Status { get; set; }
    public string StatusName { get; set; }

    // Requesting Info
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; }
    public DateTime RequestDate { get; set; }

    // Approval Workflow
    public DateTime? CurrentSupervisorApprovalDate { get; set; }
    public Guid? CurrentSupervisorApprovedById { get; set; }
    public string CurrentSupervisorApprovedByName { get; set; }
    public string CurrentSupervisorComments { get; set; }

    public DateTime? NewSupervisorApprovalDate { get; set; }
    public Guid? NewSupervisorApprovedById { get; set; }
    public string NewSupervisorApprovedByName { get; set; }
    public string NewSupervisorComments { get; set; }

    public DateTime? CurrentHodApprovalDate { get; set; }
    public Guid? CurrentHodApprovedById { get; set; }
    public string CurrentHodApprovedByName { get; set; }
    public string CurrentHodComments { get; set; }

    public DateTime? NewHodApprovalDate { get; set; }
    public Guid? NewHodApprovedById { get; set; }
    public string NewHodApprovedByName { get; set; }
    public string NewHodComments { get; set; }

    public DateTime? HrApprovalDate { get; set; }
    public Guid? HrApprovedById { get; set; }
    public string HrApprovedByName { get; set; }
    public string HrComments { get; set; }

    public DateTime? ManagementApprovalDate { get; set; }
    public Guid? ManagementApprovedById { get; set; }
    public string ManagementApprovedByName { get; set; }
    public string ManagementComments { get; set; }

    // Rejection
    public DateTime? RejectionDate { get; set; }
    public Guid? RejectedById { get; set; }
    public string RejectedByName { get; set; }
    public string RejectionReason { get; set; }

    // Employee Acceptance
    public bool RequiresEmployeeAcceptance { get; set; }
    public bool EmployeeAccepted { get; set; }
    public DateTime? EmployeeResponseDate { get; set; }
    public string EmployeeComments { get; set; }

    // Implementation
    public bool IsImplemented { get; set; }
    public DateTime? ImplementationDate { get; set; }
    public Guid? ImplementedById { get; set; }
    public string ImplementedByName { get; set; }
    public string HandoverNotes { get; set; }

    // Additional Details
    public string BenefitChanges { get; set; }
    public string AllowanceChanges { get; set; }
    public string AdditionalNotes { get; set; }
    public bool RequiresRelocation { get; set; }
    public string InternalCommunication { get; set; }
    public string ExternalCommunication { get; set; }

    // Collections
    public List<MovementAttachmentDto> Attachments { get; set; }
    public List<MovementChecklistItemDto> ChecklistItems { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateEmployeeMovementDto
{
    public Guid EmployeeId { get; set; }
    public MovementType MovementType { get; set; }
    public MovementCategory Category { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Reason { get; set; }
    public string Justification { get; set; }

    // New Position Details
    public Guid NewPositionId { get; set; }
    public Guid NewDepartmentId { get; set; }
    public Guid? NewSectionId { get; set; }
    public Guid? NewStationId { get; set; }
    public Guid? NewSupervisorId { get; set; }
    public string NewGrade { get; set; }
    public decimal NewSalary { get; set; }

    // Temporary Details (if applicable)
    public bool IsTemporary { get; set; }
    public DateTime? TemporaryStartDate { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public string TemporaryArrangementDetails { get; set; }

    public bool RequiresEmployeeAcceptance { get; set; }
    public bool RequiresRelocation { get; set; }
    public string BenefitChanges { get; set; }
    public string AllowanceChanges { get; set; }
    public string AdditionalNotes { get; set; }
}

// Update DTO
public class UpdateEmployeeMovementDto
{
    public Guid Id { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Reason { get; set; }
    public string Justification { get; set; }
    public decimal NewSalary { get; set; }
    public string BenefitChanges { get; set; }
    public string AllowanceChanges { get; set; }
    public string AdditionalNotes { get; set; }
}

// Approval DTO
public class ApproveMovementDto
{
    public Guid Id { get; set; }
    public string Comments { get; set; }
}

// Reject DTO
public class RejectMovementDto
{
    public Guid Id { get; set; }
    public string RejectionReason { get; set; }
}

// Employee Response DTO
public class EmployeeMovementResponseDto
{
    public Guid Id { get; set; }
    public bool Accepted { get; set; }
    public string Comments { get; set; }
}

// Implement DTO
public class ImplementMovementDto
{
    public Guid Id { get; set; }
    public DateTime ImplementationDate { get; set; }
    public string HandoverNotes { get; set; }
}

// Supporting DTOs
public class MovementAttachmentDto
{
    public Guid Id { get; set; }
    public MovementAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class MovementChecklistItemDto
{
    public Guid Id { get; set; }
    public ChecklistCategory Category { get; set; }
    public string CategoryName { get; set; }
    public string TaskDescription { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string ResponsiblePersonName { get; set; }
    public string CompletionNotes { get; set; }
    public int DisplayOrder { get; set; }
}

// Promotion Specific DTO
public class PromotionDetailDto
{
    public Guid Id { get; set; }
    public Guid MovementId { get; set; }
    public PromotionType PromotionType { get; set; }
    public string PromotionTypeName { get; set; }
    public bool IsActingPromotion { get; set; }
    public string ActingConditions { get; set; }
    public string AdditionalResponsibilities { get; set; }
    public string NewAuthorities { get; set; }
    public string RequiredTraining { get; set; }
}

// Transfer Specific DTO
public class TransferDetailDto
{
    public Guid Id { get; set; }
    public Guid MovementId { get; set; }
    public StaffTransferType TransferType { get; set; }
    public string TransferTypeName { get; set; }
    public TransferReason TransferReason { get; set; }
    public string TransferReasonName { get; set; }
    public bool RequiresRelocation { get; set; }
    public decimal? RelocationAllowance { get; set; }
    public string RelocationDetails { get; set; }
    public string HousingDetails { get; set; }
    public Guid? ReplacementEmployeeId { get; set; }
    public string ReplacementEmployeeName { get; set; }
    public string KnowledgeTransferPlan { get; set; }
}

// Acting Appointment DTO
public class ActingAppointmentDto
{
    public Guid Id { get; set; }
    public string AppointmentNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string CurrentPosition { get; set; }
    public string ActingPositionName { get; set; }
    public ActingReason Reason { get; set; }
    public string ReasonName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal ActingAllowance { get; set; }
    public ActingStatus Status { get; set; }
    public string StatusName { get; set; }
    public Guid? ActingForEmployeeId { get; set; }
    public string ActingForEmployeeName { get; set; }
}

// Career Path DTO
public class EmployeeCareerPathDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public DateTime JoinDate { get; set; }
    public List<CareerPathItemDto> CareerHistory { get; set; }
}

public class CareerPathItemDto
{
    public Guid Id { get; set; }
    public string PositionName { get; set; }
    public string DepartmentName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DurationMonths { get; set; }
    public bool IsCurrent { get; set; }
    public MovementType? MovementType { get; set; }
    public string MovementTypeName { get; set; }
    public string Achievements { get; set; }
    public string KeyProjects { get; set; }
}

// Dashboard DTO
public class CareerMovementDashboardDto
{
    public int TotalMovements { get; set; }
    public int PendingApproval { get; set; }
    public int PendingImplementation { get; set; }
    public int CompletedThisYear { get; set; }
    public Dictionary<MovementType, int> MovementsByType { get; set; }
    public List<EmployeeMovementListDto> RecentMovements { get; set; }
    public List<EmployeeMovementListDto> PendingMyApproval { get; set; }
    public List<ActingAppointmentDto> ActiveActingAppointments { get; set; }
}