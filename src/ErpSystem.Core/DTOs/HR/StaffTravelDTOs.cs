using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF TRAVEL DTOs
// ----------------------------------------------------------------------------
// Read DTOs extend BaseDto and surface FK display names + enum name helpers.
// Summary DTOs are lightweight projections for lists/child collections.
// Create/Update DTOs carry validation; action DTOs drive workflow transitions.
// ============================================================================

// ============================================================================
// GROUP 1 — CORE TRAVEL REQUEST
// ============================================================================

#region Staff Travel Request

public class StaffTravelRequestDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    // Traveller & initiator
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public Guid InitiatedById { get; set; }
    public string InitiatedByName { get; set; } = string.Empty;
    public TravelInitiatorRole InitiatedByRole { get; set; }
    public string InitiatedByRoleName => InitiatedByRole.ToString();

    // Classification
    public StaffTravelType TravelType { get; set; }
    public string TravelTypeName => TravelType.ToString();
    public StaffTravelPurpose TravelPurpose { get; set; }
    public string TravelPurposeName => TravelPurpose.ToString();
    public string? PurposeDescription { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    public StaffTravelRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public StaffTravelPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();

    // Route
    public Guid DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public string DestinationCity { get; set; } = string.Empty;
    public Guid OriginCountryId { get; set; }
    public string? OriginCountryName { get; set; }
    public string OriginCity { get; set; } = string.Empty;

    // Dates & duration
    public DateOnly TravelStartDate { get; set; }
    public DateOnly TravelEndDate { get; set; }
    public int EstimatedDurationDays { get; set; }

    // Cost
    public decimal EstimatedTotalCost { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    // Policy & flags
    public Guid? PolicyId { get; set; }
    public string? PolicyName { get; set; }
    public bool IsInternational { get; set; }
    public bool RequiresVisa { get; set; }
    public bool RequiresHealthClearance { get; set; }
    public TravelRiskLevel RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();

    // Grouping & amendment chain
    public Guid? GroupTravelId { get; set; }
    public string? GroupTravelName { get; set; }
    public Guid? ParentRequestId { get; set; }
    public string? ParentRequestNumber { get; set; }
    public string? AmendmentReason { get; set; }

    // Cancellation
    public string? CancellationReason { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Lifecycle timestamps
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Child collections
    public StaffTravelBudgetDto? Budget { get; set; }
    public List<StaffTravelRequestCommentDto> Comments { get; set; } = new();
    public List<StaffTravelRequestAttachmentDto> Attachments { get; set; } = new();
    public List<StaffTravelItinerarySummaryDto> Itineraries { get; set; } = new();
    public List<StaffTravelFlightBookingSummaryDto> FlightBookings { get; set; } = new();
    public List<StaffTravelHotelBookingSummaryDto> HotelBookings { get; set; } = new();
    public List<StaffTravelGroundTransportDto> GroundTransports { get; set; } = new();
    public List<StaffTravelCarRentalBookingDto> CarRentalBookings { get; set; } = new();
    public List<StaffTravelExpenseClaimSummaryDto> ExpenseClaims { get; set; } = new();
    public List<StaffTravelAdvanceSummaryDto> Advances { get; set; } = new();
    public List<StaffTravelPolicyExceptionDto> PolicyExceptions { get; set; } = new();
    public List<StaffTravelVisaApplicationSummaryDto> VisaApplications { get; set; } = new();
    public List<StaffTravelRiskAssessmentDto> RiskAssessments { get; set; } = new();
    public List<StaffTravelInsurancePolicyDto> InsurancePolicies { get; set; } = new();
}

public class StaffTravelRequestSummaryDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public StaffTravelType TravelType { get; set; }
    public string TravelTypeName => TravelType.ToString();
    public StaffTravelPurpose TravelPurpose { get; set; }
    public string TravelPurposeName => TravelPurpose.ToString();
    public string DestinationCity { get; set; } = string.Empty;
    public string? DestinationCountryName { get; set; }
    public DateOnly TravelStartDate { get; set; }
    public DateOnly TravelEndDate { get; set; }
    public StaffTravelRequestStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public StaffTravelPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public TravelRiskLevel RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public decimal EstimatedTotalCost { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsInternational { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class CreateStaffTravelRequestDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Who raised the request. <b>Server-assigned — anything sent here is overwritten</b> with the
    /// caller's employee id (falling back to <see cref="EmployeeId"/> for an unlinked account).
    /// Kept on the DTO because the entity mapper reads it; it is not a client input.
    /// </summary>
    public Guid InitiatedById { get; set; }

    [Required]
    public TravelInitiatorRole InitiatedByRole { get; set; }

    [Required]
    public StaffTravelType TravelType { get; set; }

    [Required]
    public StaffTravelPurpose TravelPurpose { get; set; }

    [MaxLength(1000)]
    public string? PurposeDescription { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public StaffTravelPriority Priority { get; set; } = StaffTravelPriority.Routine;

    [Required]
    public Guid DestinationCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = string.Empty;

    [Required]
    public Guid OriginCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string OriginCity { get; set; } = string.Empty;

    [Required]
    public DateOnly TravelStartDate { get; set; }

    [Required]
    public DateOnly TravelEndDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedTotalCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public Guid? PolicyId { get; set; }
    public bool IsInternational { get; set; }
    public bool RequiresVisa { get; set; }
    public bool RequiresHealthClearance { get; set; }

    public TravelRiskLevel RiskLevel { get; set; } = TravelRiskLevel.Low;

    public Guid? GroupTravelId { get; set; }
    public Guid? ParentRequestId { get; set; }

    [MaxLength(1000)]
    public string? AmendmentReason { get; set; }
}

public class UpdateStaffTravelRequestDto : UpdateDtoBase
{
    [Required]
    public StaffTravelType TravelType { get; set; }

    [Required]
    public StaffTravelPurpose TravelPurpose { get; set; }

    [MaxLength(1000)]
    public string? PurposeDescription { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [Required]
    public StaffTravelPriority Priority { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = string.Empty;

    [Required]
    public Guid OriginCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string OriginCity { get; set; } = string.Empty;

    [Required]
    public DateOnly TravelStartDate { get; set; }

    [Required]
    public DateOnly TravelEndDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal EstimatedTotalCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ApprovedBudget { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public Guid? PolicyId { get; set; }
    public bool IsInternational { get; set; }
    public bool RequiresVisa { get; set; }
    public bool RequiresHealthClearance { get; set; }

    [Required]
    public TravelRiskLevel RiskLevel { get; set; }

    public Guid? GroupTravelId { get; set; }

    [MaxLength(1000)]
    public string? AmendmentReason { get; set; }
}

public class SubmitStaffTravelRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid SubmittedById { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class ApproveStaffTravelRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid ApprovedById { get; set; }

    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;

    [Range(0, double.MaxValue)]
    public decimal? ApprovedBudget { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CancelStaffTravelRequestDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid CancelledById { get; set; }

    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

/// <summary>
/// Withdrawing your own travel request, from the self-service surface.
/// </summary>
/// <remarks>
/// Carries the reason and nothing else. <c>RequestId</c> comes from the route and
/// <c>CancelledById</c> from the token, so neither appears here — accepting either from the body
/// would let a caller withdraw someone else's travel, or claim someone else did.
/// </remarks>
public class CancelMyStaffTravelRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

#endregion

#region Staff Group Travel

public class StaffGroupTravelDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid LeadEmployeeId { get; set; }
    public string LeadEmployeeName { get; set; } = string.Empty;
    public string? EventName { get; set; }
    public Guid DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public string DestinationCity { get; set; } = string.Empty;
    public DateOnly TravelStartDate { get; set; }
    public DateOnly TravelEndDate { get; set; }
    public GroupTravelStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? MaxParticipants { get; set; }
    public int CurrentParticipantCount { get; set; }
    public List<StaffTravelRequestSummaryDto> Requests { get; set; } = new();
}

public class StaffGroupTravelSummaryDto
{
    public Guid Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string LeadEmployeeName { get; set; } = string.Empty;
    public string? EventName { get; set; }
    public string DestinationCity { get; set; } = string.Empty;
    public DateOnly TravelStartDate { get; set; }
    public DateOnly TravelEndDate { get; set; }
    public GroupTravelStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int? MaxParticipants { get; set; }
    public int CurrentParticipantCount { get; set; }
}

public class CreateStaffGroupTravelDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string GroupName { get; set; } = string.Empty;

    [Required]
    public Guid LeadEmployeeId { get; set; }

    [MaxLength(200)]
    public string? EventName { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = string.Empty;

    [Required]
    public DateOnly TravelStartDate { get; set; }

    [Required]
    public DateOnly TravelEndDate { get; set; }

    [Range(1, 10000)]
    public int? MaxParticipants { get; set; }
}

public class UpdateStaffGroupTravelDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string GroupName { get; set; } = string.Empty;

    [Required]
    public Guid LeadEmployeeId { get; set; }

    [MaxLength(200)]
    public string? EventName { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = string.Empty;

    [Required]
    public DateOnly TravelStartDate { get; set; }

    [Required]
    public DateOnly TravelEndDate { get; set; }

    [Required]
    public GroupTravelStatus Status { get; set; }

    [Range(1, 10000)]
    public int? MaxParticipants { get; set; }
}

#endregion

#region Staff Travel Request Comment

public class StaffTravelRequestCommentDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public TravelRequestCommentType CommentType { get; set; }
    public string CommentTypeName => CommentType.ToString();
    public string Body { get; set; } = string.Empty;
    public bool IsVisibleToTraveller { get; set; }
    public Guid? ParentCommentId { get; set; }
    public List<StaffTravelRequestCommentDto> Replies { get; set; } = new();
}

public class CreateStaffTravelRequestCommentDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    // AuthorId removed deliberately: it is stamped from the caller's token. Accepting it let
    // any caller post a comment under a colleague's name.

    [Required]
    public TravelRequestCommentType CommentType { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public bool IsVisibleToTraveller { get; set; } = true;

    public Guid? ParentCommentId { get; set; }
}

public class UpdateStaffTravelRequestCommentDto : UpdateDtoBase
{
    [Required]
    [MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    public bool IsVisibleToTraveller { get; set; }
}

#endregion

#region Staff Travel Request Attachment

/// <remarks>
/// <c>FileUrl</c> is empty for anything uploaded through the controlled gate; use
/// <c>documentRecordId</c> to tell a stored document from a legacy row, and download through
/// <c>GET .../attachments/{id}/download</c> rather than dereferencing a path.
/// </remarks>
public class StaffTravelRequestAttachmentDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public TravelAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName => AttachmentType.ToString();

    /// <summary>Scanned controlled upload backing this attachment; null on legacy rows.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered; null on legacy rows.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered; null on legacy rows.</summary>
    public Guid? DocumentVersionId { get; set; }

    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class CreateStaffTravelRequestAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    // FileName, FileUrl, FileSizeBytes and MimeType are no longer caller-supplied: they are read
    // off the stored document the upload gate returns. A caller-supplied FileUrl was the
    // path-injection sink the medical exam and claim documents were both fixed for.
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    public TravelAttachmentType AttachmentType { get; set; }

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    // UploadedById removed deliberately: stamped from the caller's token, same as AuthorId on a
    // comment. It was [Required], so a client had to state who uploaded — and could state anyone.
}

#endregion

// ============================================================================
// GROUP 2 — ITINERARY & LEGS
// ============================================================================

#region Staff Travel Itinerary

public class StaffTravelItineraryDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public int VersionNumber { get; set; }
    public bool IsCurrentVersion { get; set; }
    public TravelItineraryStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string Title { get; set; } = string.Empty;
    public int TotalTravelDays { get; set; }
    public int TotalWorkingDays { get; set; }
    public int TotalWeekendDays { get; set; }
    public string? SummaryNotes { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public List<StaffTravelItineraryLegDto> Legs { get; set; } = new();
}

public class StaffTravelItinerarySummaryDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public bool IsCurrentVersion { get; set; }
    public TravelItineraryStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string Title { get; set; } = string.Empty;
    public int TotalTravelDays { get; set; }
    public int LegCount { get; set; }
    public DateTime? FinalizedAt { get; set; }
}

public class CreateStaffTravelItineraryDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Range(1, int.MaxValue)]
    public int VersionNumber { get; set; } = 1;

    public bool IsCurrentVersion { get; set; } = true;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Range(0, 365)]
    public int TotalTravelDays { get; set; }

    [Range(0, 365)]
    public int TotalWorkingDays { get; set; }

    [Range(0, 365)]
    public int TotalWeekendDays { get; set; }

    [MaxLength(2000)]
    public string? SummaryNotes { get; set; }
}

public class UpdateStaffTravelItineraryDto : UpdateDtoBase
{
    [Required]
    public TravelItineraryStatus Status { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public bool IsCurrentVersion { get; set; }

    [Range(0, 365)]
    public int TotalTravelDays { get; set; }

    [Range(0, 365)]
    public int TotalWorkingDays { get; set; }

    [Range(0, 365)]
    public int TotalWeekendDays { get; set; }

    [MaxLength(2000)]
    public string? SummaryNotes { get; set; }

    public DateTime? FinalizedAt { get; set; }
}

#endregion

#region Staff Travel Itinerary Leg

public class StaffTravelItineraryLegDto : BaseDto
{
    public Guid StaffTravelItineraryId { get; set; }
    public int SequenceOrder { get; set; }
    public TravelItineraryLegType LegType { get; set; }
    public string LegTypeName => LegType.ToString();
    public DateOnly LegDate { get; set; }
    public string? OriginCity { get; set; }
    public Guid? OriginCountryId { get; set; }
    public string? OriginCountryName { get; set; }
    public string? DestinationCity { get; set; }
    public Guid? DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public StaffTravelTransportMode? TransportMode { get; set; }
    public string? TransportModeName => TransportMode?.ToString();
    public DateTime? DepartureDatetime { get; set; }
    public DateTime? ArrivalDatetime { get; set; }
    public Guid? FlightBookingId { get; set; }
    public Guid? HotelBookingId { get; set; }
    public Guid? GroundTransportId { get; set; }
    public string? Notes { get; set; }
    public List<StaffTravelItineraryActivityDto> Activities { get; set; } = new();
}

public class CreateStaffTravelItineraryLegDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelItineraryId { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceOrder { get; set; }

    [Required]
    public TravelItineraryLegType LegType { get; set; }

    [Required]
    public DateOnly LegDate { get; set; }

    [MaxLength(100)]
    public string? OriginCity { get; set; }

    public Guid? OriginCountryId { get; set; }

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    public Guid? DestinationCountryId { get; set; }

    public StaffTravelTransportMode? TransportMode { get; set; }
    public DateTime? DepartureDatetime { get; set; }
    public DateTime? ArrivalDatetime { get; set; }

    public Guid? FlightBookingId { get; set; }
    public Guid? HotelBookingId { get; set; }
    public Guid? GroundTransportId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateStaffTravelItineraryLegDto : UpdateDtoBase
{
    [Range(1, int.MaxValue)]
    public int SequenceOrder { get; set; }

    [Required]
    public TravelItineraryLegType LegType { get; set; }

    [Required]
    public DateOnly LegDate { get; set; }

    [MaxLength(100)]
    public string? OriginCity { get; set; }

    public Guid? OriginCountryId { get; set; }

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    public Guid? DestinationCountryId { get; set; }

    public StaffTravelTransportMode? TransportMode { get; set; }
    public DateTime? DepartureDatetime { get; set; }
    public DateTime? ArrivalDatetime { get; set; }

    public Guid? FlightBookingId { get; set; }
    public Guid? HotelBookingId { get; set; }
    public Guid? GroundTransportId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Staff Travel Itinerary Activity

public class StaffTravelItineraryActivityDto : BaseDto
{
    public Guid StaffTravelItineraryLegId { get; set; }
    public StaffTravelActivityType ActivityType { get; set; }
    public string ActivityTypeName => ActivityType.ToString();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsMandatory { get; set; }
}

public class CreateStaffTravelItineraryActivityDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelItineraryLegId { get; set; }

    [Required]
    public StaffTravelActivityType ActivityType { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public string? LocationName { get; set; }

    [MaxLength(500)]
    public string? LocationAddress { get; set; }

    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? ContactPhone { get; set; }

    public bool IsMandatory { get; set; }
}

public class UpdateStaffTravelItineraryActivityDto : UpdateDtoBase
{
    [Required]
    public StaffTravelActivityType ActivityType { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public string? LocationName { get; set; }

    [MaxLength(500)]
    public string? LocationAddress { get; set; }

    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? ContactPhone { get; set; }

    public bool IsMandatory { get; set; }
}

#endregion

// ============================================================================
// GROUP 4 — BOOKINGS
// ============================================================================

#region Staff Travel Flight Booking

public class StaffTravelFlightBookingDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public string? BookingReference { get; set; }
    public string? AirlineCode { get; set; }
    public string? AirlineName { get; set; }
    public FlightCabinClass BookingClass { get; set; }
    public string BookingClassName => BookingClass.ToString();
    public FlightCabinClass PolicyAllowedClass { get; set; }
    public string PolicyAllowedClassName => PolicyAllowedClass.ToString();
    public bool ClassExceptionApproved { get; set; }
    public string? ClassExceptionReason { get; set; }
    public TravelBookingChannel BookedBy { get; set; }
    public string BookedByName => BookedBy.ToString();
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public decimal TotalFare { get; set; }
    public decimal TaxesAndFees { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? TicketNumber { get; set; }
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal? CancellationFee { get; set; }
    public List<StaffTravelFlightSegmentDto> Segments { get; set; } = new();
}

public class StaffTravelFlightBookingSummaryDto
{
    public Guid Id { get; set; }
    public string? BookingReference { get; set; }
    public string? AirlineName { get; set; }
    public FlightCabinClass BookingClass { get; set; }
    public string BookingClassName => BookingClass.ToString();
    public decimal TotalFare { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? TicketNumber { get; set; }
    public int SegmentCount { get; set; }
}

public class CreateStaffTravelFlightBookingDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [MaxLength(50)]
    public string? BookingReference { get; set; }

    [MaxLength(2)]
    public string? AirlineCode { get; set; }

    [MaxLength(200)]
    public string? AirlineName { get; set; }

    [Required]
    public FlightCabinClass BookingClass { get; set; }

    /// <summary>
    /// <b>Server-assigned.</b> The cap comes from the travel policy in force for this traveller and
    /// trip; anything sent here is overwritten. It was a client input, which meant the caller
    /// declared what the policy permitted them to book.
    /// </summary>
    public FlightCabinClass PolicyAllowedClass { get; set; }

    /// <summary>
    /// Requests authorisation to book above the policy cap. <b>Honoured only for a caller holding
    /// <c>HR.Travel.Admin</c></b> — a Write-only travel clerk asking for it gets 403, and a booking
    /// over the cap without it gets 422. Stored as granted or not; never as claimed.
    /// </summary>
    public bool ClassExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? ClassExceptionReason { get; set; }

    [Required]
    public TravelBookingChannel BookedBy { get; set; }

    public Guid? VendorId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TotalFare { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxesAndFees { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TicketNumber { get; set; }

    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Pending;
}

public class UpdateStaffTravelFlightBookingDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string? BookingReference { get; set; }

    [MaxLength(2)]
    public string? AirlineCode { get; set; }

    [MaxLength(200)]
    public string? AirlineName { get; set; }

    [Required]
    public FlightCabinClass BookingClass { get; set; }

    /// <summary>
    /// <b>Server-assigned.</b> The cap comes from the travel policy in force for this traveller and
    /// trip; anything sent here is overwritten. It was a client input, which meant the caller
    /// declared what the policy permitted them to book.
    /// </summary>
    public FlightCabinClass PolicyAllowedClass { get; set; }

    /// <summary>
    /// Requests authorisation to book above the policy cap. <b>Honoured only for a caller holding
    /// <c>HR.Travel.Admin</c></b> — a Write-only travel clerk asking for it gets 403, and a booking
    /// over the cap without it gets 422. Stored as granted or not; never as claimed.
    /// </summary>
    public bool ClassExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? ClassExceptionReason { get; set; }

    [Required]
    public TravelBookingChannel BookedBy { get; set; }

    public Guid? VendorId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TotalFare { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TaxesAndFees { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TicketNumber { get; set; }

    [Required]
    public TravelBookingStatus Status { get; set; }

    /// <summary>
    /// <b>Server-stamped from <see cref="Status"/>.</b> Both were client inputs, so a caller
    /// asserted that a booking had been made or cancelled and nothing checked — the same fiction
    /// shape as F-09's <c>NotificationSentAt</c>. Kept on the DTO because the mapper reads them.
    /// </summary>
    public DateTime? BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CancellationFee { get; set; }
}

#endregion

#region Staff Travel Flight Segment

public class StaffTravelFlightSegmentDto : BaseDto
{
    public Guid StaffTravelFlightBookingId { get; set; }
    public int SegmentOrder { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public string OperatingCarrier { get; set; } = string.Empty;
    public string OriginAirport { get; set; } = string.Empty;
    public string DestinationAirport { get; set; } = string.Empty;
    public DateTime DepartureDatetime { get; set; }
    public DateTime ArrivalDatetime { get; set; }
    public string? DepartureTerminal { get; set; }
    public string? ArrivalTerminal { get; set; }
    public int DurationMinutes { get; set; }
    public string? AircraftType { get; set; }
    public string? SeatNumber { get; set; }
    public bool IsLayover { get; set; }
    public int? LayoverDurationMinutes { get; set; }
    public decimal? BaggageAllowanceKg { get; set; }
}

public class CreateStaffTravelFlightSegmentDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelFlightBookingId { get; set; }

    [Range(1, int.MaxValue)]
    public int SegmentOrder { get; set; }

    [Required]
    [MaxLength(10)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    public string OperatingCarrier { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string OriginAirport { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string DestinationAirport { get; set; } = string.Empty;

    [Required]
    public DateTime DepartureDatetime { get; set; }

    [Required]
    public DateTime ArrivalDatetime { get; set; }

    [MaxLength(10)]
    public string? DepartureTerminal { get; set; }

    [MaxLength(10)]
    public string? ArrivalTerminal { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the two datetimes above; anything sent here is overwritten. It
    /// was a client input sitting beside the values that define it, so a segment could claim any
    /// length at all — and the itinerary reads it.
    /// </summary>
    public int DurationMinutes { get; set; }

    [MaxLength(50)]
    public string? AircraftType { get; set; }

    [MaxLength(10)]
    public string? SeatNumber { get; set; }

    public bool IsLayover { get; set; }

    [Range(0, int.MaxValue)]
    public int? LayoverDurationMinutes { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? BaggageAllowanceKg { get; set; }
}

public class UpdateStaffTravelFlightSegmentDto : UpdateDtoBase
{
    [Range(1, int.MaxValue)]
    public int SegmentOrder { get; set; }

    [Required]
    [MaxLength(10)]
    public string FlightNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(2)]
    public string OperatingCarrier { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string OriginAirport { get; set; } = string.Empty;

    [Required]
    [MaxLength(3)]
    public string DestinationAirport { get; set; } = string.Empty;

    [Required]
    public DateTime DepartureDatetime { get; set; }

    [Required]
    public DateTime ArrivalDatetime { get; set; }

    [MaxLength(10)]
    public string? DepartureTerminal { get; set; }

    [MaxLength(10)]
    public string? ArrivalTerminal { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the two datetimes above; anything sent here is overwritten. It
    /// was a client input sitting beside the values that define it, so a segment could claim any
    /// length at all — and the itinerary reads it.
    /// </summary>
    public int DurationMinutes { get; set; }

    [MaxLength(50)]
    public string? AircraftType { get; set; }

    [MaxLength(10)]
    public string? SeatNumber { get; set; }

    public bool IsLayover { get; set; }

    [Range(0, int.MaxValue)]
    public int? LayoverDurationMinutes { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? BaggageAllowanceKg { get; set; }
}

#endregion

#region Staff Travel Hotel Booking

public class StaffTravelHotelBookingDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public string? BookingReference { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? HotelChain { get; set; }
    public string? HotelAddress { get; set; }
    public string City { get; set; } = string.Empty;
    public Guid CountryId { get; set; }
    public string? CountryName { get; set; }
    public short? StarRating { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public string? RoomType { get; set; }
    public decimal RatePerNight { get; set; }
    public decimal TotalCost { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal? PolicyMaxRatePerNight { get; set; }
    public bool RateExceptionApproved { get; set; }
    public string? RateExceptionReason { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public TravelBookingChannel BookedBy { get; set; }
    public string BookedByName => BookedBy.ToString();
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? CancellationPolicy { get; set; }
    public DateTime? BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal? CancellationFee { get; set; }
}

public class StaffTravelHotelBookingSummaryDto
{
    public Guid Id { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public decimal TotalCost { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

public class CreateStaffTravelHotelBookingDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [Required]
    [MaxLength(300)]
    public string HotelName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? HotelChain { get; set; }

    [MaxLength(500)]
    public string? HotelAddress { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    public Guid CountryId { get; set; }

    [Range(1, 7)]
    public short? StarRating { get; set; }

    [Required]
    public DateOnly CheckInDate { get; set; }

    [Required]
    public DateOnly CheckOutDate { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the two dates above; anything sent here is overwritten. It was a
    /// client input beside the dates that determine it, so a three-night stay could be recorded as
    /// one and every report downstream would believe it.
    /// </summary>
    public int NumberOfNights { get; set; }

    [MaxLength(100)]
    public string? RoomType { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RatePerNight { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the rate and the period; anything sent here is overwritten.
    /// </summary>
    public decimal TotalCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// <b>Server-assigned</b> from the travel policy in force for this traveller and trip.
    /// </summary>
    public decimal? PolicyMaxRatePerNight { get; set; }

    /// <summary>
    /// Requests authorisation to book above the policy rate cap. <b>Honoured only for a caller
    /// holding <c>HR.Travel.Admin</c></b>; over the cap without it is 422, asking for it without
    /// the right is 403.
    /// </summary>
    public bool RateExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? RateExceptionReason { get; set; }

    public Guid? VendorId { get; set; }

    [Required]
    public TravelBookingChannel BookedBy { get; set; }

    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Pending;

    [MaxLength(1000)]
    public string? CancellationPolicy { get; set; }
}

public class UpdateStaffTravelHotelBookingDto : UpdateDtoBase
{
    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [Required]
    [MaxLength(300)]
    public string HotelName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? HotelChain { get; set; }

    [MaxLength(500)]
    public string? HotelAddress { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    public Guid CountryId { get; set; }

    [Range(1, 7)]
    public short? StarRating { get; set; }

    [Required]
    public DateOnly CheckInDate { get; set; }

    [Required]
    public DateOnly CheckOutDate { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the two dates above; anything sent here is overwritten. It was a
    /// client input beside the dates that determine it, so a three-night stay could be recorded as
    /// one and every report downstream would believe it.
    /// </summary>
    public int NumberOfNights { get; set; }

    [MaxLength(100)]
    public string? RoomType { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RatePerNight { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the rate and the period; anything sent here is overwritten.
    /// </summary>
    public decimal TotalCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// <b>Server-assigned</b> from the travel policy in force for this traveller and trip.
    /// </summary>
    public decimal? PolicyMaxRatePerNight { get; set; }

    /// <summary>
    /// Requests authorisation to book above the policy rate cap. <b>Honoured only for a caller
    /// holding <c>HR.Travel.Admin</c></b>; over the cap without it is 422, asking for it without
    /// the right is 403.
    /// </summary>
    public bool RateExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? RateExceptionReason { get; set; }

    public Guid? VendorId { get; set; }

    [Required]
    public TravelBookingChannel BookedBy { get; set; }

    [Required]
    public TravelBookingStatus Status { get; set; }

    [MaxLength(1000)]
    public string? CancellationPolicy { get; set; }

    /// <summary><b>Server-stamped from <see cref="Status"/></b> — see the flight update DTO.</summary>
    public DateTime? BookedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? CancellationFee { get; set; }
}

#endregion

#region Staff Travel Ground Transport

public class StaffTravelGroundTransportDto : BaseDto
{
    /// <summary>The fleet trip reserving a company vehicle; null for external transport.</summary>
    public Guid? FleetTripId { get; set; }

    public Guid StaffTravelRequestId { get; set; }
    public GroundTransportType TransportType { get; set; }
    public string TransportTypeName => TransportType.ToString();
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? BookingReference { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime? PickupDatetime { get; set; }
    public DateTime? DropoffDatetime { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
}

public class CreateStaffTravelGroundTransportDto : CreateDtoBase
{
    /// <summary>
    /// Company vehicle to reserve. Required when <c>TransportType</c> is <c>CompanyVehicle</c> —
    /// that mode books a real vehicle through Fleet rather than recording a note.
    /// </summary>
    public Guid? VehicleAssetId { get; set; }

    /// <summary>Optional driver for the reserved vehicle.</summary>
    public Guid? DriverEmployeeId { get; set; }

    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public GroundTransportType TransportType { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    public DateTime? PickupDatetime { get; set; }
    public DateTime? DropoffDatetime { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Pending;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateStaffTravelGroundTransportDto : UpdateDtoBase
{
    [Required]
    public GroundTransportType TransportType { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    public DateTime? PickupDatetime { get; set; }
    public DateTime? DropoffDatetime { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required]
    public TravelBookingStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Staff Travel Car Rental Booking

public class StaffTravelCarRentalBookingDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? BookingReference { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public DateTime PickupDatetime { get; set; }
    public DateTime DropoffDatetime { get; set; }
    public VehicleCategory VehicleCategory { get; set; }
    public string VehicleCategoryName => VehicleCategory.ToString();
    public string? VehicleModel { get; set; }
    public decimal DailyRate { get; set; }
    public decimal TotalCost { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool InsuranceIncluded { get; set; }
    public string? FuelPolicy { get; set; }
    public bool DriverLicenseRequired { get; set; }
    public TravelBookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? BookedAt { get; set; }
}

public class CreateStaffTravelCarRentalBookingDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    [Required]
    public DateTime PickupDatetime { get; set; }

    [Required]
    public DateTime DropoffDatetime { get; set; }

    [Required]
    public VehicleCategory VehicleCategory { get; set; }

    [MaxLength(100)]
    public string? VehicleModel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DailyRate { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the rate and the period; anything sent here is overwritten.
    /// </summary>
    public decimal TotalCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public bool InsuranceIncluded { get; set; }

    [MaxLength(100)]
    public string? FuelPolicy { get; set; }

    public bool DriverLicenseRequired { get; set; }

    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Pending;
}

public class UpdateStaffTravelCarRentalBookingDto : UpdateDtoBase
{
    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    [Required]
    public DateTime PickupDatetime { get; set; }

    [Required]
    public DateTime DropoffDatetime { get; set; }

    [Required]
    public VehicleCategory VehicleCategory { get; set; }

    [MaxLength(100)]
    public string? VehicleModel { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DailyRate { get; set; }

    /// <summary>
    /// <b>Server-derived</b> from the rate and the period; anything sent here is overwritten.
    /// </summary>
    public decimal TotalCost { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public bool InsuranceIncluded { get; set; }

    [MaxLength(100)]
    public string? FuelPolicy { get; set; }

    public bool DriverLicenseRequired { get; set; }

    [Required]
    public TravelBookingStatus Status { get; set; }

    /// <summary><b>Server-stamped from <see cref="Status"/></b> — see the flight update DTO.</summary>
    public DateTime? BookedAt { get; set; }
}

#endregion

// ============================================================================
// GROUP 5 — FINANCE: BUDGET, EXPENSES & ADVANCES
// ============================================================================

#region Staff Travel Budget

public class StaffTravelBudgetDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public short BudgetYear { get; set; }
    public decimal ApprovedTotal { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal FlightBudget { get; set; }
    public decimal AccommodationBudget { get; set; }
    public decimal PerDiemBudget { get; set; }
    public decimal TransportBudget { get; set; }
    public decimal MiscellaneousBudget { get; set; }
    public decimal TotalCommitted { get; set; }
    public decimal TotalActual { get; set; }
    public decimal Variance { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class CreateStaffTravelBudgetDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Range(2000, 2100)]
    public short BudgetYear { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ApprovedTotal { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal FlightBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AccommodationBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PerDiemBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TransportBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MiscellaneousBudget { get; set; }
}

public class UpdateStaffTravelBudgetDto : UpdateDtoBase
{
    [Range(2000, 2100)]
    public short BudgetYear { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ApprovedTotal { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal FlightBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AccommodationBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PerDiemBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal TransportBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MiscellaneousBudget { get; set; }

    /// <summary>
    /// <b>Server-derived; anything sent here is overwritten.</b> Committed is the value of
    /// non-cancelled bookings on the request, actual is the value of paid expense claims, and
    /// <c>Variance</c> is <c>ApprovedTotal - TotalActual</c>. All three were caller-declared, so a
    /// budget-versus-actual screen showed whatever was last typed while the records that constitute
    /// the spend sat unread on the same request. See <c>StaffTravelBudgetRollup</c>.
    /// </summary>
    public decimal TotalCommitted { get; set; }

    /// <summary><b>Server-derived</b> — see <see cref="TotalCommitted"/>.</summary>
    public decimal TotalActual { get; set; }
}

#endregion

#region Staff Travel Expense Claim

public class StaffTravelExpenseClaimDto : BaseDto
{
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid StaffTravelRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public TravelClaimType ClaimType { get; set; }
    public string ClaimTypeName => ClaimType.ToString();
    public TravelClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? TravelAdvanceId { get; set; }
    public string? TravelAdvanceNumber { get; set; }
    public decimal TotalClaimed { get; set; }
    public decimal TotalApproved { get; set; }
    public decimal TotalRejected { get; set; }
    public decimal AdvanceDeducted { get; set; }
    public decimal NetPayable { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelPaymentMethod? PaymentMethod { get; set; }
    public string? PaymentMethodName => PaymentMethod?.ToString();
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? FinanceReviewedById { get; set; }
    public string? FinanceReviewedByName { get; set; }
    public DateTime? FinanceReviewedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public List<StaffTravelExpenseClaimLineDto> Lines { get; set; } = new();
}

public class StaffTravelExpenseClaimSummaryDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public TravelClaimType ClaimType { get; set; }
    public string ClaimTypeName => ClaimType.ToString();
    public TravelClaimStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalClaimed { get; set; }
    public decimal NetPayable { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
}

public class CreateStaffTravelExpenseClaimDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public TravelClaimType ClaimType { get; set; }

    public Guid? TravelAdvanceId { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public List<CreateStaffTravelExpenseClaimLineDto> Lines { get; set; } = new();
}

public class UpdateStaffTravelExpenseClaimDto : UpdateDtoBase
{
    [Required]
    public TravelClaimType ClaimType { get; set; }

    public Guid? TravelAdvanceId { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
}

public class ReviewStaffTravelExpenseClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }
    // FinanceReviewedById removed: stamped from the caller's token, never accepted from the body.

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public TravelClaimStatus NewStatus { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class PayStaffTravelExpenseClaimDto
{
    [Required]
    public Guid ClaimId { get; set; }

    [Required]
    public TravelPaymentMethod PaymentMethod { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Staff Travel Expense Claim Line

public class StaffTravelExpenseClaimLineDto : BaseDto
{
    public Guid StaffTravelExpenseClaimId { get; set; }
    public TravelExpenseCategory ExpenseCategory { get; set; }
    public string ExpenseCategoryName => ExpenseCategory.ToString();
    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? MerchantName { get; set; }
    public decimal AmountOriginal { get; set; }
    public string CurrencyOriginal { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal AmountBaseCurrency { get; set; }
    public decimal? PolicyLimit { get; set; }
    public decimal? AmountApproved { get; set; }
    public decimal? AmountRejected { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReceiptAttachmentId { get; set; }
    public bool IsPerDiem { get; set; }
    public Guid? PerDiemRateId { get; set; }
    public TravelExpenseLineStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class CreateStaffTravelExpenseClaimLineDto : CreateDtoBase
{
    public Guid StaffTravelExpenseClaimId { get; set; }

    [Required]
    public TravelExpenseCategory ExpenseCategory { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? MerchantName { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AmountOriginal { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyOriginal { get; set; } = string.Empty;

    // ⚠ No ExchangeRate or AmountBaseCurrency. Both are DERIVED: the service reads Finance's
    // published rate for the expense date and does the arithmetic, so a claim is valued at the
    // organisation's own rate and cannot disagree with what Finance reports the trip cost.


    [Range(0, double.MaxValue)]
    public decimal? PolicyLimit { get; set; }

    public Guid? ReceiptAttachmentId { get; set; }
    public bool IsPerDiem { get; set; }
    public Guid? PerDiemRateId { get; set; }
}

public class UpdateStaffTravelExpenseClaimLineDto : UpdateDtoBase
{
    [Required]
    public TravelExpenseCategory ExpenseCategory { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? MerchantName { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AmountOriginal { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyOriginal { get; set; } = string.Empty;

    // ⚠ No ExchangeRate or AmountBaseCurrency. Both are DERIVED: the service reads Finance's
    // published rate for the expense date and does the arithmetic, so a claim is valued at the
    // organisation's own rate and cannot disagree with what Finance reports the trip cost.


    [Range(0, double.MaxValue)]
    public decimal? PolicyLimit { get; set; }

    public Guid? ReceiptAttachmentId { get; set; }
    public bool IsPerDiem { get; set; }
    public Guid? PerDiemRateId { get; set; }
}

public class ReviewStaffTravelExpenseClaimLineDto
{
    [Required]
    public Guid LineId { get; set; }
    // ReviewedById removed: stamped from the caller's token, never accepted from the body.

    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public TravelExpenseLineStatus Status { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? AmountApproved { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? AmountRejected { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

#endregion

#region Staff Travel Advance

public class StaffTravelAdvanceDto : BaseDto
{
    public string AdvanceNumber { get; set; } = string.Empty;
    public Guid StaffTravelRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelAdvanceType AdvanceType { get; set; }
    public string AdvanceTypeName => AdvanceType.ToString();
    public TravelAdvanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? DisbursedAt { get; set; }
    public DateOnly? SettlementDeadline { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal UnsettledAmount { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public Guid? DisbursedById { get; set; }
    public string? DisbursedByName { get; set; }
}

public class StaffTravelAdvanceSummaryDto
{
    public Guid Id { get; set; }
    public string AdvanceNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public TravelAdvanceType AdvanceType { get; set; }
    public string AdvanceTypeName => AdvanceType.ToString();
    public TravelAdvanceStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal UnsettledAmount { get; set; }
    public DateOnly? SettlementDeadline { get; set; }
}

public class CreateStaffTravelAdvanceDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RequestedAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required]
    public TravelAdvanceType AdvanceType { get; set; }

    public DateOnly? SettlementDeadline { get; set; }
}

public class UpdateStaffTravelAdvanceDto : UpdateDtoBase
{
    [Range(0, double.MaxValue)]
    public decimal RequestedAmount { get; set; }

    /// <summary>
    /// <b>Ignored.</b> Approving an advance is <c>POST advances/{id}/approve</c>, which stamps the
    /// approver from the token and checks the status. Accepting it here left an advance with money
    /// approved and nobody on record as having approved it.
    /// </summary>
    public decimal? ApprovedAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Required]
    public TravelAdvanceType AdvanceType { get; set; }

    public DateOnly? SettlementDeadline { get; set; }
}

public class ApproveStaffTravelAdvanceDto
{
    [Required]
    public Guid AdvanceId { get; set; }
    // ApprovedById removed: stamped from the caller's token, never accepted from the body.

    [Range(0, double.MaxValue)]
    public decimal ApprovedAmount { get; set; }
}

public class DisburseStaffTravelAdvanceDto
{
    [Required]
    public Guid AdvanceId { get; set; }
    // DisbursedById removed: stamped from the caller's token, never accepted from the body.

    /// <summary>
    /// <b>Ignored — stamped from the clock.</b> It let a caller state when the money went out, which
    /// matters because the settlement deadline and the overdue-settlement sweep both run off dates.
    /// Kept on the DTO so existing callers do not break; the value is not read.
    /// </summary>
    public DateTime DisbursedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Staff Travel Per Diem Rate

public class StaffTravelPerDiemRateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public Guid? StaffLevelId { get; set; }
    public string? StaffLevelName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal DailyAllowance { get; set; }
    public decimal AccommodationLimit { get; set; }
    public decimal MealAllowance { get; set; }
    public decimal IncidentalAllowance { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal MealBreakdownBreakfast { get; set; }
    public decimal MealBreakdownLunch { get; set; }
    public decimal MealBreakdownDinner { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStaffTravelPerDiemRateDto : CreateDtoBase
{
    [Required]
    public Guid CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? StaffLevelId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DailyAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AccommodationLimit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal IncidentalAllowance { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownBreakfast { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownLunch { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownDinner { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateStaffTravelPerDiemRateDto : UpdateDtoBase
{
    [Required]
    public Guid CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public Guid? StaffLevelId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DailyAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AccommodationLimit { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealAllowance { get; set; }

    [Range(0, double.MaxValue)]
    public decimal IncidentalAllowance { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownBreakfast { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownLunch { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MealBreakdownDinner { get; set; }

    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// GROUP 6 — POLICY & VENDOR
// ============================================================================

#region Staff Travel Policy

public class StaffTravelPolicyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public bool IsCurrentVersion { get; set; }
    public Guid? AppliesToLevelFromId { get; set; }
    public string? AppliesToLevelFromName { get; set; }
    public Guid? AppliesToLevelToId { get; set; }
    public string? AppliesToLevelToName { get; set; }
    public Guid? AppliesToOrganizationUnitId { get; set; }
    public string? AppliesToOrganizationUnitName { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public FlightCabinClass MaxFlightClassDomestic { get; set; }
    public string MaxFlightClassDomesticName => MaxFlightClassDomestic.ToString();
    public FlightCabinClass MaxFlightClassInternational { get; set; }
    public string MaxFlightClassInternationalName => MaxFlightClassInternational.ToString();
    public decimal MaxHotelRateDomestic { get; set; }
    public decimal MaxHotelRateInternational { get; set; }
    public int AdvanceBookingDaysFlight { get; set; }
    public int AdvanceBookingDaysHotel { get; set; }
    public bool RequiresCheapestFare { get; set; }
    public bool PreferredVendorMandatory { get; set; }
    public decimal MaxSingleTripBudget { get; set; }
    public decimal MaxAnnualTravelBudget { get; set; }
    public decimal ReceiptRequiredAbove { get; set; }
    public int ExpenseSubmissionDays { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public List<StaffTravelPolicyRuleDto> Rules { get; set; } = new();
}

public class StaffTravelPolicySummaryDto
{
    public Guid Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public bool IsCurrentVersion { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public decimal MaxSingleTripBudget { get; set; }
    public int RuleCount { get; set; }

    /// <summary>
    /// Who approved the policy, and when. <b>Null means it is a draft that enforces nothing</b> —
    /// which a list of policies must be able to show: an unapproved policy caps nothing and is
    /// otherwise indistinguishable from one in force.
    /// </summary>
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class CreateStaffTravelPolicyDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string PolicyName { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int VersionNumber { get; set; } = 1;

    public bool IsCurrentVersion { get; set; } = true;

    public Guid? AppliesToLevelFromId { get; set; }
    public Guid? AppliesToLevelToId { get; set; }
    public Guid? AppliesToOrganizationUnitId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Required]
    public FlightCabinClass MaxFlightClassDomestic { get; set; }

    [Required]
    public FlightCabinClass MaxFlightClassInternational { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxHotelRateDomestic { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxHotelRateInternational { get; set; }

    [Range(0, 365)]
    public int AdvanceBookingDaysFlight { get; set; }

    [Range(0, 365)]
    public int AdvanceBookingDaysHotel { get; set; }

    public bool RequiresCheapestFare { get; set; }
    public bool PreferredVendorMandatory { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxSingleTripBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxAnnualTravelBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReceiptRequiredAbove { get; set; }

    [Range(0, 365)]
    public int ExpenseSubmissionDays { get; set; }
}

public class UpdateStaffTravelPolicyDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string PolicyName { get; set; } = string.Empty;

    public bool IsCurrentVersion { get; set; }

    public Guid? AppliesToLevelFromId { get; set; }
    public Guid? AppliesToLevelToId { get; set; }
    public Guid? AppliesToOrganizationUnitId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Required]
    public FlightCabinClass MaxFlightClassDomestic { get; set; }

    [Required]
    public FlightCabinClass MaxFlightClassInternational { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxHotelRateDomestic { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxHotelRateInternational { get; set; }

    [Range(0, 365)]
    public int AdvanceBookingDaysFlight { get; set; }

    [Range(0, 365)]
    public int AdvanceBookingDaysHotel { get; set; }

    public bool RequiresCheapestFare { get; set; }
    public bool PreferredVendorMandatory { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxSingleTripBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal MaxAnnualTravelBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ReceiptRequiredAbove { get; set; }

    [Range(0, 365)]
    public int ExpenseSubmissionDays { get; set; }
}

#endregion

#region Staff Travel Policy Rule

public class StaffTravelPolicyRuleDto : BaseDto
{
    public Guid PolicyId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public TravelPolicyRuleType RuleType { get; set; }
    public string RuleTypeName => RuleType.ToString();
    public TravelExpenseCategory? ExpenseCategory { get; set; }
    public string? ExpenseCategoryName => ExpenseCategory?.ToString();
    public StaffTravelType? TravelType { get; set; }
    public string? TravelTypeName => TravelType?.ToString();
    public decimal? LimitValue { get; set; }
    public string? LimitUnit { get; set; }
    public bool ExceptionAllowed { get; set; }
    public bool ExceptionRequiresApproval { get; set; }
    public TravelPolicyViolationAction ViolationAction { get; set; }
    public string ViolationActionName => ViolationAction.ToString();
    public bool IsActive { get; set; }
}

public class CreateStaffTravelPolicyRuleDto : CreateDtoBase
{
    [Required]
    public Guid PolicyId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RuleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required]
    public TravelPolicyRuleType RuleType { get; set; }

    public TravelExpenseCategory? ExpenseCategory { get; set; }
    public StaffTravelType? TravelType { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? LimitValue { get; set; }

    [MaxLength(20)]
    public string? LimitUnit { get; set; }

    public bool ExceptionAllowed { get; set; }
    public bool ExceptionRequiresApproval { get; set; }

    [Required]
    public TravelPolicyViolationAction ViolationAction { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateStaffTravelPolicyRuleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string RuleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required]
    public TravelPolicyRuleType RuleType { get; set; }

    public TravelExpenseCategory? ExpenseCategory { get; set; }
    public StaffTravelType? TravelType { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? LimitValue { get; set; }

    [MaxLength(20)]
    public string? LimitUnit { get; set; }

    public bool ExceptionAllowed { get; set; }
    public bool ExceptionRequiresApproval { get; set; }

    [Required]
    public TravelPolicyViolationAction ViolationAction { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region Staff Travel Policy Exception

public class StaffTravelPolicyExceptionDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public Guid PolicyRuleId { get; set; }
    public string? PolicyRuleName { get; set; }
    public string? ExceptionReason { get; set; }
    public decimal? RequestedValue { get; set; }
    public decimal? PolicyLimit { get; set; }
    public TravelPolicyExceptionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNotes { get; set; }
}

public class CreateStaffTravelPolicyExceptionDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public Guid PolicyRuleId { get; set; }

    [MaxLength(1000)]
    public string? ExceptionReason { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? RequestedValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? PolicyLimit { get; set; }
}

public class DecideStaffTravelPolicyExceptionDto
{
    [Required]
    public Guid ExceptionId { get; set; }
    // ApprovedById removed: stamped from the caller's token.

    [Required]
    public TravelPolicyExceptionStatus Status { get; set; }

    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Why the exception was granted or refused.
    /// </summary>
    /// <remarks>
    /// The client had been sending this as <c>notes</c> since the service layer was written and
    /// the DTO had no such property, so every decision's reasoning was silently discarded by the
    /// model binder — the shape a matched route cannot reveal, because the path resolves and the
    /// body does not.
    /// </remarks>
    [MaxLength(2000)]
    public string? DecisionNotes { get; set; }
}

#endregion

// ============================================================================
// GROUP 7 — COMPLIANCE & SAFETY
// ============================================================================

#region Staff Travel Document

public class StaffTravelDocumentDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public TravelDocumentType DocumentType { get; set; }
    public string DocumentTypeName => DocumentType.ToString();
    public string DocumentNumber { get; set; } = string.Empty;
    public Guid IssuingCountryId { get; set; }
    public string? IssuingCountryName { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    public Guid? VerifiedById { get; set; }
    public string? VerifiedByName { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public class CreateStaffTravelDocumentDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public TravelDocumentType DocumentType { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required]
    public Guid IssuingCountryId { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsPrimary { get; set; }
}

public class UpdateStaffTravelDocumentDto : UpdateDtoBase
{
    [Required]
    public TravelDocumentType DocumentType { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = string.Empty;

    [Required]
    public Guid IssuingCountryId { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public bool IsPrimary { get; set; }
}

public class VerifyStaffTravelDocumentDto
{
    [Required]
    public Guid DocumentId { get; set; }
    // VerifiedById removed: stamped from the caller's token.

    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Staff Travel Visa Requirement

public class StaffTravelVisaRequirementDto : BaseDto
{
    public Guid PassportCountryId { get; set; }
    public string? PassportCountryName { get; set; }
    public Guid DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public VisaRequirementType VisaRequirementType { get; set; }
    public string VisaRequirementTypeName => VisaRequirementType.ToString();
    public string? VisaCategory { get; set; }
    public int? MaxStayDays { get; set; }
    public int? ProcessingDays { get; set; }
    public string? OfficialSourceUrl { get; set; }
    public DateOnly? LastVerifiedAt { get; set; }
    public string? Notes { get; set; }
}

public class CreateStaffTravelVisaRequirementDto : CreateDtoBase
{
    [Required]
    public Guid PassportCountryId { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [Required]
    public VisaRequirementType VisaRequirementType { get; set; }

    [MaxLength(100)]
    public string? VisaCategory { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxStayDays { get; set; }

    [Range(0, int.MaxValue)]
    public int? ProcessingDays { get; set; }

    [MaxLength(2000)]
    public string? OfficialSourceUrl { get; set; }

    public DateOnly? LastVerifiedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateStaffTravelVisaRequirementDto : UpdateDtoBase
{
    [Required]
    public VisaRequirementType VisaRequirementType { get; set; }

    [MaxLength(100)]
    public string? VisaCategory { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaxStayDays { get; set; }

    [Range(0, int.MaxValue)]
    public int? ProcessingDays { get; set; }

    [MaxLength(2000)]
    public string? OfficialSourceUrl { get; set; }

    public DateOnly? LastVerifiedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Staff Travel Visa Application

public class StaffTravelVisaApplicationDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public string? VisaType { get; set; }
    public VisaApplicationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? SubmittedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? VisaNumber { get; set; }
    public decimal? ProcessingFee { get; set; }
    public string? CurrencyCode { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? Notes { get; set; }
}

public class StaffTravelVisaApplicationSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? DestinationCountryName { get; set; }
    public string? VisaType { get; set; }
    public VisaApplicationStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateOnly? SubmittedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class CreateStaffTravelVisaApplicationDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [MaxLength(100)]
    public string? VisaType { get; set; }

    public Guid? VendorId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ProcessingFee { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateStaffTravelVisaApplicationDto : UpdateDtoBase
{
    [Required]
    public Guid DestinationCountryId { get; set; }

    [MaxLength(100)]
    public string? VisaType { get; set; }

    [Required]
    public VisaApplicationStatus Status { get; set; }

    public DateOnly? SubmittedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(100)]
    public string? VisaNumber { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ProcessingFee { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Staff Travel Risk Assessment

public class StaffTravelRiskAssessmentDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public Guid DestinationCountryId { get; set; }
    public string? DestinationCountryName { get; set; }
    public string? DestinationCity { get; set; }
    public TravelRiskLevel RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public TravelRiskCategory RiskCategory { get; set; }
    public string RiskCategoryName => RiskCategory.ToString();
    public string? AssessmentSource { get; set; }
    public string? AssessmentSummary { get; set; }
    public bool MitigationRequired { get; set; }
    public string? MitigationNotes { get; set; }
    public bool DutyOfCareBriefingSent { get; set; }
    public bool EmployeeAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AssessedById { get; set; }
    public string? AssessedByName { get; set; }
    public DateTime? AssessedAt { get; set; }
    public DateOnly? ValidUntil { get; set; }
}

public class CreateStaffTravelRiskAssessmentDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    public Guid DestinationCountryId { get; set; }

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    [Required]
    public TravelRiskLevel RiskLevel { get; set; }

    [Required]
    public TravelRiskCategory RiskCategory { get; set; }

    [MaxLength(200)]
    public string? AssessmentSource { get; set; }

    [MaxLength(2000)]
    public string? AssessmentSummary { get; set; }

    public bool MitigationRequired { get; set; }

    [MaxLength(2000)]
    public string? MitigationNotes { get; set; }

    public bool DutyOfCareBriefingSent { get; set; }
    public Guid? AssessedById { get; set; }
    public DateOnly? ValidUntil { get; set; }
}

public class UpdateStaffTravelRiskAssessmentDto : UpdateDtoBase
{
    [Required]
    public Guid DestinationCountryId { get; set; }

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    [Required]
    public TravelRiskLevel RiskLevel { get; set; }

    [Required]
    public TravelRiskCategory RiskCategory { get; set; }

    [MaxLength(200)]
    public string? AssessmentSource { get; set; }

    [MaxLength(2000)]
    public string? AssessmentSummary { get; set; }

    public bool MitigationRequired { get; set; }

    [MaxLength(2000)]
    public string? MitigationNotes { get; set; }

    public bool DutyOfCareBriefingSent { get; set; }
    public Guid? AssessedById { get; set; }
    public DateOnly? ValidUntil { get; set; }
}

public class AcknowledgeStaffTravelRiskAssessmentDto
{
    [Required]
    public Guid RiskAssessmentId { get; set; }

    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Staff Travel Alert

public class StaffTravelAlertDto : BaseDto
{
    public Guid TenantId { get; set; }
    public TravelAlertType AlertType { get; set; }
    public string AlertTypeName => AlertType.ToString();
    public TravelAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public Guid CountryId { get; set; }
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? Source { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class StaffTravelAlertSummaryDto
{
    public Guid Id { get; set; }
    public TravelAlertType AlertType { get; set; }
    public string AlertTypeName => AlertType.ToString();
    public TravelAlertSeverity Severity { get; set; }
    public string SeverityName => Severity.ToString();
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStaffTravelAlertDto : CreateDtoBase
{
    [Required]
    public TravelAlertType AlertType { get; set; }

    [Required]
    public TravelAlertSeverity Severity { get; set; }

    [Required]
    public Guid CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Body { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateStaffTravelAlertDto : UpdateDtoBase
{
    [Required]
    public TravelAlertType AlertType { get; set; }

    [Required]
    public TravelAlertSeverity Severity { get; set; }

    [Required]
    public Guid CountryId { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Body { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Staff Travel Alert Notification

public class StaffTravelAlertNotificationDto : BaseDto
{
    public Guid TravelAlertId { get; set; }
    public string? AlertTitle { get; set; }

    /// <summary>The alert's own text, and its severity.</summary>
    /// <remarks>
    /// ⚠ Both were absent, and this row is what a traveller is shown. That is D-31 exactly — the
    /// destination-alert read returned a summary with no Body, so since the day the feature shipped
    /// every traveller saw "Civil unrest · High" and never a word about what was happening, where,
    /// or what to do. A severity with no text is not a security briefing, and a notification that
    /// forces the screen to fetch the alert separately invites exactly the same omission again.
    /// </remarks>
    public string? AlertBody { get; set; }

    /// <inheritdoc cref="AlertBody"/>
    public TravelAlertSeverity? Severity { get; set; }

    public Guid StaffTravelRequestId { get; set; }
    public string? RequestNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime? NotificationSentAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public class CreateStaffTravelAlertNotificationDto : CreateDtoBase
{
    [Required]
    public Guid TravelAlertId { get; set; }

    [Required]
    public Guid StaffTravelRequestId { get; set; }

    /// <summary>
    /// ⚠ <b>EmployeeId is deliberately absent.</b> The traveller is taken from the travel request,
    /// which already names them.
    /// </summary>
    /// <remarks>
    /// It used to be a required field on this DTO and it was the one parent
    /// <c>CreateAlertNotificationAsync</c> never validated — the alert and the request were both
    /// checked, so an unknown employee fell through to the foreign key and surfaced as the generic
    /// 500 naming nothing. Worse than the missing 404: nothing checked the employee was <i>this
    /// request's</i> traveller, so the desk could tell one person they were travelling on someone
    /// else's trip. Deriving it removes both problems and there is nothing left to forge — the same
    /// treatment D-05, D-15 and D-20 gave client-supplied actor ids. Nothing had ever sent the
    /// field, because the endpoint had no caller at all.
    /// </remarks>
    public DateTime? NotificationSentAt { get; set; }
}

public class AcknowledgeStaffTravelAlertNotificationDto
{
    [Required]
    public Guid NotificationId { get; set; }

    public DateTime AcknowledgedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Staff Travel Insurance Policy

public class StaffTravelInsurancePolicyDto : BaseDto
{
    public Guid StaffTravelRequestId { get; set; }
    public Guid? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? PolicyNumber { get; set; }
    public TravelInsuranceType InsuranceType { get; set; }
    public string InsuranceTypeName => InsuranceType.ToString();
    public TravelInsuranceCoverageType CoverageType { get; set; }
    public string CoverageTypeName => CoverageType.ToString();
    public DateOnly CoverageStart { get; set; }
    public DateOnly CoverageEnd { get; set; }
    public decimal SumInsured { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Premium { get; set; }
    public string? EmergencyContact { get; set; }
}

public class CreateStaffTravelInsurancePolicyDto : CreateDtoBase
{
    [Required]
    public Guid StaffTravelRequestId { get; set; }

    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    [Required]
    public TravelInsuranceType InsuranceType { get; set; }

    [Required]
    public TravelInsuranceCoverageType CoverageType { get; set; }

    [Required]
    public DateOnly CoverageStart { get; set; }

    [Required]
    public DateOnly CoverageEnd { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SumInsured { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Premium { get; set; }

    [MaxLength(200)]
    public string? EmergencyContact { get; set; }
}

public class UpdateStaffTravelInsurancePolicyDto : UpdateDtoBase
{
    public Guid? VendorId { get; set; }

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    [Required]
    public TravelInsuranceType InsuranceType { get; set; }

    [Required]
    public TravelInsuranceCoverageType CoverageType { get; set; }

    [Required]
    public DateOnly CoverageStart { get; set; }

    [Required]
    public DateOnly CoverageEnd { get; set; }

    [Range(0, double.MaxValue)]
    public decimal SumInsured { get; set; }

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Premium { get; set; }

    [MaxLength(200)]
    public string? EmergencyContact { get; set; }
}

#endregion

#region Staff Travel Health Requirement

public class StaffTravelHealthRequirementDto : BaseDto
{
    public Guid CountryId { get; set; }
    public string? CountryName { get; set; }
    public TravelHealthRequirementType RequirementType { get; set; }
    public string RequirementTypeName => RequirementType.ToString();
    public string RequirementName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public int? ValidityDays { get; set; }
    public string? Notes { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStaffTravelHealthRequirementDto : CreateDtoBase
{
    [Required]
    public Guid CountryId { get; set; }

    [Required]
    public TravelHealthRequirementType RequirementType { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }

    [Range(0, int.MaxValue)]
    public int? ValidityDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateStaffTravelHealthRequirementDto : UpdateDtoBase
{
    [Required]
    public Guid CountryId { get; set; }

    [Required]
    public TravelHealthRequirementType RequirementType { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = string.Empty;

    public bool IsMandatory { get; set; }

    [Range(0, int.MaxValue)]
    public int? ValidityDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

#endregion


#region Staff travel reminder engine (slice 5a)

public class StaffTravelReminderRunResultDto
{
    public Guid RunId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public int RemindersQueued { get; set; }

    /// <summary>How many candidates were found but already claimed by an earlier sweep.</summary>
    public int AlreadySent { get; set; }
}

/// <summary>
/// One thing a sweep would fire. Carries a reference and a date and nothing sensitive — see the
/// remarks on <c>StaffTravelReminderDispatchLog</c>.
/// </summary>
public class StaffTravelReminderPreviewItemDto
{
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>True when a previous sweep already claimed this key, so a real run would skip it.</summary>
    public bool AlreadySent { get; set; }
}

public class StaffTravelReminderRunDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public Guid? TriggeredByUserId { get; set; }
    public int RemindersQueued { get; set; }
}

public class StaffTravelReminderLogEntryDto
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int DaysRemaining { get; set; }
    public int EscalationTier { get; set; }
    public DateTime CreatedAt { get; set; }
}

#endregion
