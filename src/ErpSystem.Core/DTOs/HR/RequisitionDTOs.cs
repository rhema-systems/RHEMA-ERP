using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class StaffRequisitionListDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; }
    public string PositionName { get; set; }
    public string Department { get; set; }
    public RequisitionType Type { get; set; }
    public string TypeName { get; set; }
    public int NumberOfPositions { get; set; }
    public RequisitionPriority Priority { get; set; }
    public string PriorityName { get; set; }
    public RequisitionStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime RequestDate { get; set; }
    public string RequestedByName { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public int DaysOpen { get; set; }
}

// Detail DTO
public class StaffRequisitionDetailDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; }

    // Position Info
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid? StationId { get; set; }
    public string StationName { get; set; }

    // Requisition Details
    public RequisitionType Type { get; set; }
    public string TypeName { get; set; }
    public int NumberOfPositions { get; set; }
    public RequisitionPriority Priority { get; set; }
    public string PriorityName { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string EmploymentTypeName { get; set; }
    public string ContractDuration { get; set; }

    // Replacement Info
    public Guid? ReplacementForEmployeeId { get; set; }
    public string ReplacementForEmployeeName { get; set; }
    public ReplacementReason? ReplacementReason { get; set; }
    public string ReplacementReasonName { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }

    // Justification
    public string BusinessJustification { get; set; }
    public string ImpactIfNotFilled { get; set; }

    // Budget
    public string BudgetCode { get; set; }
    public bool IsBudgeted { get; set; }
    public decimal? ProposedMinSalary { get; set; }
    public decimal? ProposedMaxSalary { get; set; }

    // Job Requirements
    public string ModifiedJobDescription { get; set; }
    public string EssentialQualifications { get; set; }
    public string PreferredQualifications { get; set; }
    public string KeyResponsibilities { get; set; }
    public string ProposedBenefits { get; set; }

    // Timing
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public DateTime? ProposedStartDate { get; set; }

    // Requesting Info
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; }

    // Approval Workflow
    public RequisitionStatus Status { get; set; }
    public string StatusName { get; set; }

    // Supervisor Approval
    public DateTime? SupervisorApprovalDate { get; set; }
    public Guid? SupervisorApprovedById { get; set; }
    public string SupervisorApprovedByName { get; set; }
    public string SupervisorComments { get; set; }

    // HOD Approval
    public DateTime? HodApprovalDate { get; set; }
    public Guid? HodApprovedById { get; set; }
    public string HodApprovedByName { get; set; }
    public string HodComments { get; set; }

    // HR Approval
    public DateTime? HrApprovalDate { get; set; }
    public Guid? HrApprovedById { get; set; }
    public string HrApprovedByName { get; set; }
    public string HrComments { get; set; }

    // Finance Approval
    public DateTime? FinanceApprovalDate { get; set; }
    public Guid? FinanceApprovedById { get; set; }
    public string FinanceApprovedByName { get; set; }
    public string FinanceComments { get; set; }

    // CEO Approval
    public DateTime? CeoApprovalDate { get; set; }
    public Guid? CeoApprovedById { get; set; }
    public string CeoApprovedByName { get; set; }
    public string CeoComments { get; set; }

    // Rejection
    public DateTime? RejectionDate { get; set; }
    public Guid? RejectedById { get; set; }
    public string RejectedByName { get; set; }
    public string RejectionReason { get; set; }

    // Cancellation
    public DateTime? CancellationDate { get; set; }
    public string CancellationReason { get; set; }

    // Fulfillment
    public DateTime? FulfilledDate { get; set; }
    public Guid? VacancyId { get; set; }
    public string VacancyNumber { get; set; }

    // Collections
    public List<RequisitionAttachmentDto> Attachments { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateStaffRequisitionDto
{
    public Guid PositionId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? StationId { get; set; }
    public RequisitionType Type { get; set; }
    public int NumberOfPositions { get; set; }
    public RequisitionPriority Priority { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public string ContractDuration { get; set; }
    public Guid? ReplacementForEmployeeId { get; set; }
    public ReplacementReason? ReplacementReason { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }
    public string BusinessJustification { get; set; }
    public string ImpactIfNotFilled { get; set; }
    public string BudgetCode { get; set; }
    public bool IsBudgeted { get; set; }
    public decimal? ProposedMinSalary { get; set; }
    public decimal? ProposedMaxSalary { get; set; }
    public string ModifiedJobDescription { get; set; }
    public string EssentialQualifications { get; set; }
    public string PreferredQualifications { get; set; }
    public string KeyResponsibilities { get; set; }
    public string ProposedBenefits { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public DateTime? ProposedStartDate { get; set; }
}

// Update DTO
public class UpdateStaffRequisitionDto
{
    public Guid Id { get; set; }
    public int NumberOfPositions { get; set; }
    public RequisitionPriority Priority { get; set; }
    public string BusinessJustification { get; set; }
    public string ImpactIfNotFilled { get; set; }
    public decimal? ProposedMinSalary { get; set; }
    public decimal? ProposedMaxSalary { get; set; }
    public string ModifiedJobDescription { get; set; }
    public string EssentialQualifications { get; set; }
    public string PreferredQualifications { get; set; }
    public string KeyResponsibilities { get; set; }
    public string ProposedBenefits { get; set; }
    public DateTime? RequiredByDate { get; set; }
    public DateTime? ProposedStartDate { get; set; }
}

// Approval DTOs
public class ApproveRequisitionDto
{
    public Guid Id { get; set; }
    public string Comments { get; set; }
}

public class RejectRequisitionDto
{
    public Guid Id { get; set; }
    public string RejectionReason { get; set; }
}

public class CancelRequisitionDto
{
    public Guid Id { get; set; }
    public string CancellationReason { get; set; }
}

// Attachment DTO
public class RequisitionAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Headcount Authorization DTO
public class HeadcountAuthorizationDto
{
    public Guid Id { get; set; }
    public string FiscalYear { get; set; }
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public int AuthorizedHeadcount { get; set; }
    public int CurrentHeadcount { get; set; }
    public int AvailableHeadcount { get; set; }
    public string AuthorizedByName { get; set; }
    public DateTime AuthorizationDate { get; set; }
    public string Notes { get; set; }
}

// Dashboard DTO
public class RequisitionDashboardDto
{
    public int TotalRequisitions { get; set; }
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int InRecruitment { get; set; }
    public int Fulfilled { get; set; }
    public int Rejected { get; set; }
    public Dictionary<RequisitionStatus, int> RequisitionsByStatus { get; set; }
    public Dictionary<RequisitionPriority, int> RequisitionsByPriority { get; set; }
    public List<StaffRequisitionListDto> UrgentRequisitions { get; set; }
    public List<StaffRequisitionListDto> PendingMyApproval { get; set; }
}