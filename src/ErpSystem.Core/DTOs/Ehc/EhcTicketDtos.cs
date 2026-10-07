using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class CreateEhcTicketRequestDto
{
    public EhcTicketType TicketType { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }

    // Internal-only (ignored for ExternalOnly endpoints)
    public Guid? AssignedDepartmentId { get; set; }

    public EhcTicketPriority Priority { get; set; } = EhcTicketPriority.Medium;
    public EhcTicketSource Source { get; set; } = EhcTicketSource.Web;
    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityReference { get; set; }

    // Abuse protection (optional; enforced when tenant security settings enable CAPTCHA)
    public string? CaptchaToken { get; set; }
}

public sealed class EhcTicketCategoryTreeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EhcTicketType? AppliesToType { get; set; }
    public List<EhcTicketCategoryTreeDto> Subcategories { get; set; } = new();
}

public sealed class EhcTicketListItemDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public EhcTicketType TicketType { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public EhcTicketSource Source { get; set; }
    public EhcTicketStatus Status { get; set; }
    public string? Subject { get; set; }
    public string? CategoryName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // SLA timestamps (for grids/flags)
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Feedback (CSAT)
    public int? FeedbackRating { get; set; }
    public DateTime? FeedbackSubmittedAt { get; set; }

    // Internal-only (not populated for external users)
    public string? AssignedOrganizationUnitName { get; set; }
    public string? AssignedDepartmentName { get; set; }
    public string? AssignedToName { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterAuthenticationProvider { get; set; }
    public bool IsPublicSiteSubmission { get; set; }
}

public sealed class EhcTicketDetailDto
{
    public EhcPropertyListingContextDto? PropertyListing { get; set; }
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public EhcTicketType TicketType { get; set; }
    public EhcTicketPriority Priority { get; set; }
    public EhcTicketSource Source { get; set; }
    public EhcTicketStatus Status { get; set; }
    public string? Subject { get; set; }
    public string Description { get; set; } = string.Empty;

    public string? CategoryName { get; set; }
    public string? SubcategoryName { get; set; }

    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityReference { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? FirstResponseDueAt { get; set; }
    public DateTime? ResolutionDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    // Internal-only (not populated for external users)
    public Guid? AssignedOrganizationUnitId { get; set; }
    public string? AssignedOrganizationUnitName { get; set; }
    public Guid? AssignedDepartmentId { get; set; }
    public string? AssignedDepartmentName { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
    public string? RequesterAuthenticationProvider { get; set; }

    // Complaint/RCA (internal-only)
    public Guid? RootCauseId { get; set; }
    public string? RootCauseCode { get; set; }
    public string? RootCauseName { get; set; }
    public string? RootCauseDetails { get; set; }
    public string? ResolutionSummary { get; set; }

    // Feedback (CSAT)
    public int? FeedbackRating { get; set; }
    public string? FeedbackComment { get; set; }
    public DateTime? FeedbackSubmittedAt { get; set; }

    public List<EhcTicketMessageDto> Messages { get; set; } = new();
    public List<EhcTicketAttachmentDto> Attachments { get; set; } = new();
    public List<EhcTicketStatusHistoryDto> StatusHistory { get; set; } = new();
    public List<EhcTicketAuditEventDto> AuditTrail { get; set; } = new();
}

public sealed class SubmitEhcTicketFeedbackRequestDto
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public sealed class UpdateEhcTicketRcaRequestDto
{
    public Guid? RootCauseId { get; set; }
    public string? RootCauseDetails { get; set; }
    public string? ResolutionSummary { get; set; }
}

public sealed class EhcTicketMessageDto
{
    public Guid Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<EhcTicketAttachmentDto> Attachments { get; set; } = new();
}

public sealed class AddEhcTicketMessageRequestDto
{
    public string Body { get; set; } = string.Empty;
}

public sealed class EhcTicketAttachmentDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class AddEhcTicketAttachmentRequestDto
{
    public Guid? MessageId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }

    // Internal-only attachment (only respected by internal APIs)
    public bool IsInternal { get; set; } = false;
}

public sealed class EhcTicketStatusHistoryDto
{
    public Guid Id { get; set; }
    public EhcTicketStatus? FromStatus { get; set; }
    public EhcTicketStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class EhcTicketAuditEventDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsInternal { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public DateTime CreatedAt { get; set; }
}
