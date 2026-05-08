namespace ErpSystem.Core.DTOs.Sales;

// ==================== Sales Agreement DTOs ====================

#region Summary & Detail

public class SalesLinkedProjectUnitContextDto
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public Guid ProjectUnitId { get; set; }
    public string? ProjectUnitCode { get; set; }
    public string ProjectUnitName { get; set; } = string.Empty;
    public string ProjectUnitType { get; set; } = string.Empty;
    public string ProjectUnitStatus { get; set; } = string.Empty;
    public string ProjectUnitCommercialStatus { get; set; } = string.Empty;
    public string ProjectUnitHandoverStatus { get; set; } = string.Empty;
    public bool IsReleasedForMarket { get; set; }
    public DateTime? HandoverDate { get; set; }
}

public class SalesAgreementSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string AgreementTitle { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string AgreementType { get; set; } = string.Empty;
    public string AgreementStatus { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal AgreedValue { get; set; }
    public decimal UtilizedValue { get; set; }
    public string Currency { get; set; } = "GHS";
    public string? PropertyReference { get; set; }
    public string? SalesRepName { get; set; }
    public bool AutoRenew { get; set; }
    public int CompletedMilestones { get; set; }
    public int TotalMilestones { get; set; }
    public DateTime CreatedAt { get; set; }
    public SalesLinkedProjectUnitContextDto? ProjectUnitContext { get; set; }
}

public class SalesAgreementDetailDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string AgreementTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string AgreementType { get; set; } = string.Empty;
    public string AgreementStatus { get; set; } = string.Empty;

    // Validity
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ExpiryWarningDays { get; set; }
    public bool AutoRenew { get; set; }
    public int? RenewalPeriodMonths { get; set; }

    // Financial
    public decimal AgreedValue { get; set; }
    public decimal MinimumCommitment { get; set; }
    public decimal MaximumCommitment { get; set; }
    public decimal UtilizedValue { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal? DiscountPercentage { get; set; }
    public string? PricingTerms { get; set; }
    public string? PaymentSchedule { get; set; }

    // Property (TDC)
    public string? PropertyReference { get; set; }
    public string? PropertyType { get; set; }
    public string? PropertyDescription { get; set; }
    public string? PropertyLocation { get; set; }

    // People
    public Guid? SalesRepId { get; set; }
    public string? SalesRepName { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovalComments { get; set; }

    // Termination
    public DateTime? TerminatedDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? TerminatedByName { get; set; }

    // Notes
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public string? TermsAndConditions { get; set; }

    // Children
    public List<SalesAgreementLineDto> Lines { get; set; } = new();
    public List<SalesAgreementMilestoneDto> Milestones { get; set; } = new();
    public List<SalesAgreementRenewalDto> Renewals { get; set; } = new();
    public List<SalesAgreementDocumentDto> Documents { get; set; } = new();
    public SalesLinkedProjectUnitContextDto? ProjectUnitContext { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

#endregion

#region Line DTOs

public class SalesAgreementLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal AgreedPrice { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal MaximumQuantity { get; set; }
    public decimal UtilizedQuantity { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string? DiscountTiersJson { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Milestone DTOs

public class SalesAgreementMilestoneDto
{
    public Guid Id { get; set; }
    public int SequenceNumber { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PaymentPercentage { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Renewal DTOs

public class SalesAgreementRenewalDto
{
    public Guid Id { get; set; }
    public int RenewalNumber { get; set; }
    public DateTime PreviousStartDate { get; set; }
    public DateTime PreviousEndDate { get; set; }
    public DateTime NewStartDate { get; set; }
    public DateTime NewEndDate { get; set; }
    public decimal PreviousValue { get; set; }
    public decimal NewValue { get; set; }
    public decimal? PriceChangePercentage { get; set; }
    public string? RenewalTerms { get; set; }
    public string? Notes { get; set; }
    public string? RenewedByName { get; set; }
    public DateTime RenewedDate { get; set; }
}

#endregion

#region Document DTOs

public class SalesAgreementDocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public string DocumentType { get; set; } = "Contract";
    public string? Description { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion

#region Create/Update DTOs

public class CreateSalesAgreementDto
{
    public Guid BusinessPartnerId { get; set; }
    public string AgreementTitle { get; set; } = string.Empty;
    public string? AgreementType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ExpiryWarningDays { get; set; } = 30;
    public bool AutoRenew { get; set; }
    public int? RenewalPeriodMonths { get; set; }
    public decimal AgreedValue { get; set; }
    public decimal MinimumCommitment { get; set; }
    public decimal MaximumCommitment { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal? DiscountPercentage { get; set; }
    public string? PricingTerms { get; set; }
    public string? PaymentSchedule { get; set; }
    public string? PropertyReference { get; set; }
    public string? PropertyType { get; set; }
    public string? PropertyDescription { get; set; }
    public string? PropertyLocation { get; set; }
    public Guid? SalesRepId { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public string? TermsAndConditions { get; set; }
    public List<CreateSalesAgreementLineDto>? Lines { get; set; }
    public List<CreateSalesAgreementMilestoneDto>? Milestones { get; set; }
}

public class UpdateSalesAgreementDto
{
    public string? AgreementTitle { get; set; }
    public DateTime? EndDate { get; set; }
    public int? ExpiryWarningDays { get; set; }
    public bool? AutoRenew { get; set; }
    public int? RenewalPeriodMonths { get; set; }
    public decimal? AgreedValue { get; set; }
    public decimal? MinimumCommitment { get; set; }
    public decimal? MaximumCommitment { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string? PricingTerms { get; set; }
    public string? PaymentSchedule { get; set; }
    public string? PropertyReference { get; set; }
    public string? PropertyType { get; set; }
    public string? PropertyDescription { get; set; }
    public string? PropertyLocation { get; set; }
    public Guid? SalesRepId { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public string? TermsAndConditions { get; set; }
}

public class CreateSalesAgreementLineDto
{
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal AgreedPrice { get; set; }
    public decimal MinimumQuantity { get; set; }
    public decimal MaximumQuantity { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string? DiscountTiersJson { get; set; }
    public string? Notes { get; set; }
}

public class CreateSalesAgreementMilestoneDto
{
    public string MilestoneName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PaymentPercentage { get; set; }
    public decimal PaymentAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Notes { get; set; }
}

public class SalesAgreementApprovalDto
{
    public bool IsApproved { get; set; }
    public string? Comments { get; set; }
}

public class RenewAgreementDto
{
    public DateTime NewEndDate { get; set; }
    public decimal? NewValue { get; set; }
    public string? RenewalTerms { get; set; }
    public string? Notes { get; set; }
}

public class TerminateAgreementDto
{
    public string Reason { get; set; } = string.Empty;
}

public class UpdateMilestoneStatusDto
{
    public string Status { get; set; } = string.Empty; // Completed, Paid
    public DateTime? ActualDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
}

#endregion
