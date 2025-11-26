using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Requisition;

/// <summary>
/// Request to fill a position/create vacancy
/// </summary>
public class StaffRequisition : TenantEntity
{
    public string RequisitionNumber { get; set; } = string.Empty;

    // Position Details
    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }

    // Requisition Type
    public RequisitionType Type { get; set; } // New Position, Replacement, Temporary
    public RequisitionPriority Priority { get; set; }
    public RequisitionStatus Status { get; set; }

    // If Replacement
    public Guid? ReplacementForEmployeeId { get; set; }
    public Employee? ReplacementForEmployee { get; set; }
    public ReplacementReason? ReplacementReason { get; set; }
    public DateTime? EmployeeDepartureDate { get; set; }

    // Number of Positions
    public int NumberOfPositions { get; set; }

    // Employment Terms
    // public EmploymentType EmploymentType { get; set; }
    public DateTime? TemporaryStartDate { get; set; }
    public DateTime? TemporaryEndDate { get; set; }
    public string? ContractDuration { get; set; }

    // Justification
    public string BusinessJustification { get; set; } = string.Empty;
    public string? ImpactIfNotFilled { get; set; }
    public bool IsBudgeted { get; set; }
    public string? BudgetCode { get; set; }

    // Job Requirements
    public string? ModifiedJobDescription { get; set; }
    public string? EssentialQualifications { get; set; }
    public string? PreferredQualifications { get; set; }
    public string? KeyResponsibilities { get; set; }

    // Compensation
    public decimal? ProposedMinSalary { get; set; }
    public decimal? ProposedMaxSalary { get; set; }
    public string? ProposedBenefits { get; set; }

    // Timeline
    public DateTime RequestDate { get; set; }
    public DateTime DesiredStartDate { get; set; }
    public DateTime? LatestStartDate { get; set; }

    // Requestor
    public Guid RequestedById { get; set; }
    public Employee RequestedBy { get; set; } = null!;

    // Approval Workflow
    public Guid? SupervisorApprovedById { get; set; }
    public Employee? SupervisorApprovedBy { get; set; }
    public DateTime? SupervisorApprovalDate { get; set; }
    public string? SupervisorComments { get; set; }

    public Guid? HodApprovedById { get; set; }
    public Employee? HodApprovedBy { get; set; }
    public DateTime? HodApprovalDate { get; set; }
    public string? HodComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    public Employee? HrApprovedBy { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? HrComments { get; set; }

    public Guid? FinanceApprovedById { get; set; }
    public Employee? FinanceApprovedBy { get; set; }
    public DateTime? FinanceApprovalDate { get; set; }
    public string? FinanceComments { get; set; }

    public Guid? CeoApprovedById { get; set; }
    public Employee? CeoApprovedBy { get; set; }
    public DateTime? CeoApprovalDate { get; set; }
    public string? CeoComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public Guid? RejectedById { get; set; }
    public Employee? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    // Cancellation
    public DateTime? CancelledDate { get; set; }
    public string? CancellationReason { get; set; }

    // Fulfillment
    public bool IsFulfilled { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public int PositionsFilled { get; set; }

    // Linked Vacancy
    public Guid? VacancyId { get; set; }
    public Recruitment.Vacancy? Vacancy { get; set; }

    public ICollection<RequisitionAttachment> Attachments { get; set; } = new List<RequisitionAttachment>();
}

public class RequisitionAttachment : TenantEntity
{
    public Guid RequisitionId { get; set; }
    public StaffRequisition Requisition { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Headcount tracking and authorization
/// </summary>
public class HeadcountAuthorization : TenantEntity
{
    public int FiscalYear { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public int AuthorizedHeadcount { get; set; }
    public int CurrentHeadcount { get; set; }
    public int VacantPositions { get; set; }
    public int PendingRequisitions { get; set; }

    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public Guid AuthorizedById { get; set; }
    public Employee AuthorizedBy { get; set; } = null!;

    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}