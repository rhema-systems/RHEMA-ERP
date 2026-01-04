using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Performance bond request DTO
/// </summary>
public class PerformanceBondRequestDto
{
    public Guid Id { get; set; }
    public Guid TenderAwardId { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string BidNumber { get; set; } = string.Empty;
    
    public string Status { get; set; } = "Pending";
    
    // Template info
    public string? TemplateFileName { get; set; }
    public string? TemplateFileType { get; set; }
    public long? TemplateFileSize { get; set; }
    public bool HasTemplate => !string.IsNullOrEmpty(TemplateFileName);
    
    public DateTime RequestedDate { get; set; }
    public string? RequestedByName { get; set; }
    
    // Submitted document info
    public string? SubmittedFileName { get; set; }
    public string? SubmittedFileType { get; set; }
    public long? SubmittedFileSize { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public string? SubmittedByName { get; set; }
    public bool HasSubmission => !string.IsNullOrEmpty(SubmittedFileName);
    
    // Review info
    public DateTime? ReviewedDate { get; set; }
    public string? ReviewedByName { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create performance bond request DTO (internal user sends to business partner)
/// </summary>
public class CreatePerformanceBondRequestDto
{
    [Required]
    public Guid TenderAwardId { get; set; }
    
    [Required]
    public Guid TenderBidId { get; set; }
    
    [Required]
    public Guid BusinessPartnerId { get; set; }
    
    public string? Notes { get; set; }
}

/// <summary>
/// Submit performance bond response DTO (business partner submits completed bond)
/// </summary>
public class SubmitPerformanceBondDto
{
    public string? Notes { get; set; }
}

/// <summary>
/// Review performance bond DTO (internal user approves/rejects)
/// </summary>
public class ReviewPerformanceBondDto
{
    [Required]
    public bool IsApproved { get; set; }
    
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
    
    public string? Notes { get; set; }
}

