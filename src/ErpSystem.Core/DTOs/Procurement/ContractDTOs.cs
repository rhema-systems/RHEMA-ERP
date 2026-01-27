using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Contract DTO for display
/// </summary>
public class ContractDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string ContractType { get; set; } = "Service";
    public string Status { get; set; } = "Draft";

    // Linked Records
    public Guid TenderAwardId { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public Guid? TenderBidId { get; set; }

    // Financial Details
    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = "USD";
    public string? PaymentTerms { get; set; }
    public decimal RetentionPercentage { get; set; }

    // Timeline
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DurationDays { get; set; }
    public int? WarrantyPeriodDays { get; set; }

    // Scope & Terms
    public string? ScopeOfWork { get; set; }
    public string? Deliverables { get; set; }
    public string? SpecialConditions { get; set; }
    public string? PenaltyClause { get; set; }

    // Signing Information
    public DateTime? SignedDate { get; set; }
    public string? SignedByName { get; set; }
    public string? ContractorSignatoryName { get; set; }
    public DateTime? ContractorSignedDate { get; set; }
    public string? ContractDocumentPath { get; set; }

    public string? Notes { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TerminatedAt { get; set; }
    public string? TerminationReason { get; set; }

    // Related data
    public List<ContractMilestoneDto> Milestones { get; set; } = new();
    public List<ContractAmendmentDto> Amendments { get; set; } = new();
    public List<ContractDocumentDto> Documents { get; set; } = new();

    // Computed
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int CompletedMilestones { get; set; }
    public int TotalMilestones { get; set; }
}

/// <summary>
/// Contract list item DTO (for grid/table display)
/// </summary>
public class ContractListDto
{
    public Guid Id { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TenderNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create contract DTO
/// </summary>
public class CreateContractDto
{
    [Required]
    public Guid TenderAwardId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ContractTitle { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ContractType { get; set; } = "Service";

    // Financial
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal ContractValue { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [MaxLength(200)]
    public string? PaymentTerms { get; set; }

    [Range(0, 100)]
    public decimal RetentionPercentage { get; set; } = 0;

    // Timeline
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DurationDays { get; set; }
    public int? WarrantyPeriodDays { get; set; }

    // Scope & Terms
    public string? ScopeOfWork { get; set; }
    public string? Deliverables { get; set; }
    public string? SpecialConditions { get; set; }
    public string? PenaltyClause { get; set; }

    public string? Notes { get; set; }

    // Optional milestones to create with contract
    public List<CreateContractMilestoneDto>? Milestones { get; set; }
}

/// <summary>
/// Update contract DTO
/// </summary>
public class UpdateContractDto
{
    [MaxLength(200)]
    public string? ContractTitle { get; set; }

    [MaxLength(50)]
    public string? ContractType { get; set; }

    // Financial
    [Range(0.01, double.MaxValue)]
    public decimal? ContractValue { get; set; }

    [MaxLength(200)]
    public string? PaymentTerms { get; set; }

    [Range(0, 100)]
    public decimal? RetentionPercentage { get; set; }

    // Timeline
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DurationDays { get; set; }
    public int? WarrantyPeriodDays { get; set; }

    // Scope & Terms
    public string? ScopeOfWork { get; set; }
    public string? Deliverables { get; set; }
    public string? SpecialConditions { get; set; }
    public string? PenaltyClause { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Contract milestone DTO
/// </summary>
public class ContractMilestoneDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SequenceNumber { get; set; }
    public decimal PaymentPercentage { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? CompletedAt { get; set; }
    public DateTime? InvoicedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create contract milestone DTO
/// </summary>
public class CreateContractMilestoneDto
{
    [Required]
    [MaxLength(100)]
    public string MilestoneName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SequenceNumber { get; set; } = 1;

    [Required]
    [Range(0, 100)]
    public decimal PaymentPercentage { get; set; }

    public DateTime? PlannedDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Update contract milestone DTO
/// </summary>
public class UpdateContractMilestoneDto
{
    [MaxLength(100)]
    public string? MilestoneName { get; set; }

    public string? Description { get; set; }

    public int? SequenceNumber { get; set; }

    [Range(0, 100)]
    public decimal? PaymentPercentage { get; set; }

    public DateTime? PlannedDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Update milestone status DTO
/// </summary>
public class UpdateMilestoneStatusDto
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public string? InvoiceNumber { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Contract amendment DTO
/// </summary>
public class ContractAmendmentDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string AmendmentNumber { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string AmendmentType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Description { get; set; }
    public decimal? PreviousValue { get; set; }
    public decimal? NewValue { get; set; }
    public decimal? ValueChange { get; set; }
    public DateTime? PreviousEndDate { get; set; }
    public DateTime? NewEndDate { get; set; }
    public int? DaysExtended { get; set; }
    public string? ScopeChanges { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? RequestedDate { get; set; }
    public string? RequestedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovalNotes { get; set; }
    public string? DocumentPath { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create contract amendment DTO
/// </summary>
public class CreateContractAmendmentDto
{
    [Required]
    [MaxLength(100)]
    public string AmendmentType { get; set; } = "ValueChange";

    [MaxLength(500)]
    public string? Reason { get; set; }

    public string? Description { get; set; }

    // Value Change
    public decimal? NewValue { get; set; }

    // Timeline Change
    public DateTime? NewEndDate { get; set; }

    // Scope Change
    public string? ScopeChanges { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Approve/Reject amendment DTO
/// </summary>
public class ProcessAmendmentDto
{
    [Required]
    public bool Approved { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Contract document DTO
/// </summary>
public class ContractDocumentDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string? Description { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Contract status update DTO
/// </summary>
public class UpdateContractStatusDto
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public DateTime? SignedDate { get; set; }

    [MaxLength(200)]
    public string? SignedByName { get; set; }

    [MaxLength(200)]
    public string? ContractorSignatoryName { get; set; }

    public DateTime? ContractorSignedDate { get; set; }

    [MaxLength(500)]
    public string? TerminationReason { get; set; }

    public string? Notes { get; set; }
}

