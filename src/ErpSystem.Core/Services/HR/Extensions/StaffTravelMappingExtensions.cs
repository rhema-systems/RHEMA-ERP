using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class StaffTravelMappingExtensions
{
    // ========================================================================
    // GROUP 1 — CORE TRAVEL REQUEST
    // ========================================================================

    #region StaffTravelRequest

    public static StaffTravelRequestDto ToDto(this StaffTravelRequest entity)
    {
        return new StaffTravelRequestDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber,
            InitiatedById = entity.InitiatedById,
            InitiatedByName = entity.InitiatedBy?.FullName ?? string.Empty,
            InitiatedByRole = entity.InitiatedByRole,
            TravelType = entity.TravelType,
            TravelPurpose = entity.TravelPurpose,
            PurposeDescription = entity.PurposeDescription,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            Priority = entity.Priority,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            DestinationCity = entity.DestinationCity,
            OriginCountryId = entity.OriginCountryId,
            OriginCountryName = entity.OriginCountry?.Name,
            OriginCity = entity.OriginCity,
            TravelStartDate = entity.TravelStartDate,
            TravelEndDate = entity.TravelEndDate,
            EstimatedDurationDays = entity.EstimatedDurationDays,
            EstimatedTotalCost = entity.EstimatedTotalCost,
            ApprovedBudget = entity.ApprovedBudget,
            CurrencyCode = entity.CurrencyCode,
            PolicyId = entity.PolicyId,
            PolicyName = entity.Policy?.PolicyName,
            IsInternational = entity.IsInternational,
            RequiresVisa = entity.RequiresVisa,
            RequiresHealthClearance = entity.RequiresHealthClearance,
            RiskLevel = entity.RiskLevel,
            GroupTravelId = entity.GroupTravelId,
            GroupTravelName = entity.GroupTravel?.GroupName,
            ParentRequestId = entity.ParentRequestId,
            ParentRequestNumber = entity.ParentRequest?.RequestNumber,
            AmendmentReason = entity.AmendmentReason,
            CancellationReason = entity.CancellationReason,
            CancelledById = entity.CancelledById,
            CancelledByName = entity.CancelledBy?.FullName,
            CancelledAt = entity.CancelledAt,
            SubmittedAt = entity.SubmittedAt,
            ApprovedAt = entity.ApprovedAt,
            CompletedAt = entity.CompletedAt,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ReturnedAt = entity.ReturnedAt,
            ReturnedById = entity.ReturnedById,
            ReturnedByName = entity.ReturnedBy?.FullName,
            ReturnReason = entity.ReturnReason,
            ChangeRequestedAt = entity.ChangeRequestedAt,
            ChangeRequestedById = entity.ChangeRequestedById,
            ChangeRequestedByName = entity.ChangeRequestedBy?.FullName,
            ChangeReason = entity.ChangeReason,
            ClosedAt = entity.ClosedAt,
            ClosedById = entity.ClosedById,
            ClosedByName = entity.ClosedBy?.FullName,
            Budget = entity.Budget?.ToDto(),
            Comments = entity.Comments.Select(c => c.ToDto()).ToList(),
            Attachments = entity.Attachments.Select(a => a.ToDto()).ToList(),
            Itineraries = entity.Itineraries.Select(i => i.ToSummaryDto()).ToList(),
            FlightBookings = entity.FlightBookings.Select(f => f.ToSummaryDto()).ToList(),
            HotelBookings = entity.HotelBookings.Select(h => h.ToSummaryDto()).ToList(),
            GroundTransports = entity.GroundTransports.Select(g => g.ToDto()).ToList(),
            CarRentalBookings = entity.CarRentalBookings.Select(c => c.ToDto()).ToList(),
            ExpenseClaims = entity.ExpenseClaims.Select(c => c.ToSummaryDto()).ToList(),
            Advances = entity.Advances.Select(a => a.ToSummaryDto()).ToList(),
            PolicyExceptions = entity.PolicyExceptions.Select(e => e.ToDto()).ToList(),
            VisaApplications = entity.VisaApplications.Select(v => v.ToSummaryDto()).ToList(),
            RiskAssessments = entity.RiskAssessments.Select(r => r.ToDto()).ToList(),
            InsurancePolicies = entity.InsurancePolicies.Select(i => i.ToDto()).ToList(),
        };
    }

    public static StaffTravelRequestSummaryDto ToSummaryDto(this StaffTravelRequest entity)
    {
        return new StaffTravelRequestSummaryDto
        {
            Id = entity.Id,
            RequestNumber = entity.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            TravelType = entity.TravelType,
            TravelPurpose = entity.TravelPurpose,
            DestinationCity = entity.DestinationCity,
            DestinationCountryName = entity.DestinationCountry?.Name,
            TravelStartDate = entity.TravelStartDate,
            TravelEndDate = entity.TravelEndDate,
            Status = entity.Status,
            Priority = entity.Priority,
            RiskLevel = entity.RiskLevel,
            EstimatedTotalCost = entity.EstimatedTotalCost,
            ApprovedBudget = entity.ApprovedBudget,
            CurrencyCode = entity.CurrencyCode,
            IsInternational = entity.IsInternational,
            SubmittedAt = entity.SubmittedAt,
        };
    }

    /// <remarks>
    /// The organisation unit, <c>IsInternational</c> and the policy are not mapped: the service sets
    /// them from the traveller's record, the two countries and the policy guard (lane 1 — A5, O-5).
    /// </remarks>
    public static StaffTravelRequest ToEntity(this CreateStaffTravelRequestDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelRequest
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            InitiatedById = dto.InitiatedById,
            InitiatedByRole = dto.InitiatedByRole,
            TravelType = dto.TravelType,
            TravelPurpose = dto.TravelPurpose,
            PurposeDescription = dto.PurposeDescription,
            Status = StaffTravelRequestStatus.Draft,
            Priority = dto.Priority,
            DestinationCountryId = dto.DestinationCountryId,
            DestinationCity = dto.DestinationCity,
            OriginCountryId = dto.OriginCountryId,
            OriginCity = dto.OriginCity,
            TravelStartDate = dto.TravelStartDate,
            TravelEndDate = dto.TravelEndDate,
            EstimatedDurationDays = Math.Max(0, dto.TravelEndDate.DayNumber - dto.TravelStartDate.DayNumber + 1),
            EstimatedTotalCost = dto.EstimatedTotalCost,
            CurrencyCode = dto.CurrencyCode,
            RequiresVisa = dto.RequiresVisa,
            RequiresHealthClearance = dto.RequiresHealthClearance,
            RiskLevel = dto.RiskLevel,
            ParentRequestId = dto.ParentRequestId,
            AmendmentReason = dto.AmendmentReason,
            CreatedBy = userId.ToString(),
        };
    }

    /// <remarks>
    /// Writes only what the requester may change. The unit, <c>IsInternational</c> and the policy
    /// are the service's (see <see cref="ToEntity(CreateStaffTravelRequestDto, Guid, Guid)"/>),
    /// <c>ApprovedBudget</c> is the approver's, and the group link is the group's endpoints' (slice
    /// 1c) — a plain edit used to overwrite all five.
    /// </remarks>
    public static void UpdateEntity(this StaffTravelRequest entity, UpdateStaffTravelRequestDto dto, Guid userId)
    {
        entity.TravelType = dto.TravelType;
        entity.TravelPurpose = dto.TravelPurpose;
        entity.PurposeDescription = dto.PurposeDescription;
        entity.Priority = dto.Priority;
        entity.DestinationCountryId = dto.DestinationCountryId;
        entity.DestinationCity = dto.DestinationCity;
        entity.OriginCountryId = dto.OriginCountryId;
        entity.OriginCity = dto.OriginCity;
        entity.TravelStartDate = dto.TravelStartDate;
        entity.TravelEndDate = dto.TravelEndDate;
        entity.EstimatedDurationDays = Math.Max(0, dto.TravelEndDate.DayNumber - dto.TravelStartDate.DayNumber + 1);
        entity.EstimatedTotalCost = dto.EstimatedTotalCost;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.RequiresVisa = dto.RequiresVisa;
        entity.RequiresHealthClearance = dto.RequiresHealthClearance;
        entity.RiskLevel = dto.RiskLevel;
        entity.AmendmentReason = dto.AmendmentReason;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelRequestSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelRequest> entities)
        => entities.Select(e => e.ToSummaryDto());

    /// <summary>
    /// A request as its traveller may read it: comments the desk shared, and no policy exceptions.
    /// </summary>
    /// <remarks>
    /// Finding A6 (lane 1, slice 1c): the self-service read returned every comment — internal notes
    /// included, at any depth of replies — and the desk's policy-exception decisions, and only the
    /// portal page's own filter hid them. Anything sent to the traveller's browser is theirs to read,
    /// so the filter is here, on the server.
    /// </remarks>
    public static StaffTravelRequestDto ToTravellerView(this StaffTravelRequestDto dto)
    {
        dto.Comments = SharedWithTraveller(dto.Comments);
        dto.PolicyExceptions = new List<StaffTravelPolicyExceptionDto>();
        return dto;
    }

    private static List<StaffTravelRequestCommentDto> SharedWithTraveller(IEnumerable<StaffTravelRequestCommentDto> comments)
        => comments
            .Where(c => c.IsVisibleToTraveller)
            .Select(c => { c.Replies = SharedWithTraveller(c.Replies); return c; })
            .ToList();

    #endregion

    #region StaffGroupTravel

    public static StaffGroupTravelDto ToDto(this StaffGroupTravel entity)
    {
        return new StaffGroupTravelDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            GroupName = entity.GroupName,
            LeadEmployeeId = entity.LeadEmployeeId,
            LeadEmployeeName = entity.LeadEmployee?.FullName ?? string.Empty,
            EventName = entity.EventName,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            DestinationCity = entity.DestinationCity,
            TravelStartDate = entity.TravelStartDate,
            TravelEndDate = entity.TravelEndDate,
            Status = entity.Status,
            MaxParticipants = entity.MaxParticipants,
            CurrentParticipantCount = entity.SeatsTaken(),
            Requests = entity.Requests.Select(r => r.ToSummaryDto()).ToList(),
        };
    }

    /// <summary>
    /// The places a group's travellers hold: every linked trip still going ahead or able to — not a
    /// cancelled or rejected one, which used to count against <c>MaxParticipants</c> for ever.
    /// </summary>
    public static int SeatsTaken(this StaffGroupTravel entity)
        => entity.Requests.Count(r => r.Status is not (StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected));

    public static StaffGroupTravelSummaryDto ToSummaryDto(this StaffGroupTravel entity)
    {
        return new StaffGroupTravelSummaryDto
        {
            Id = entity.Id,
            GroupName = entity.GroupName,
            LeadEmployeeName = entity.LeadEmployee?.FullName ?? string.Empty,
            EventName = entity.EventName,
            DestinationCity = entity.DestinationCity,
            TravelStartDate = entity.TravelStartDate,
            TravelEndDate = entity.TravelEndDate,
            Status = entity.Status,
            MaxParticipants = entity.MaxParticipants,
            CurrentParticipantCount = entity.SeatsTaken(),
        };
    }

    public static StaffGroupTravel ToEntity(this CreateStaffGroupTravelDto dto, Guid tenantId, Guid userId)
    {
        return new StaffGroupTravel
        {
            TenantId = tenantId,
            GroupName = dto.GroupName,
            LeadEmployeeId = dto.LeadEmployeeId,
            EventName = dto.EventName,
            DestinationCountryId = dto.DestinationCountryId,
            DestinationCity = dto.DestinationCity,
            TravelStartDate = dto.TravelStartDate,
            TravelEndDate = dto.TravelEndDate,
            Status = GroupTravelStatus.Planning,
            MaxParticipants = dto.MaxParticipants,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffGroupTravel entity, UpdateStaffGroupTravelDto dto, Guid userId)
    {
        entity.GroupName = dto.GroupName;
        entity.LeadEmployeeId = dto.LeadEmployeeId;
        entity.EventName = dto.EventName;
        entity.DestinationCountryId = dto.DestinationCountryId;
        entity.DestinationCity = dto.DestinationCity;
        entity.TravelStartDate = dto.TravelStartDate;
        entity.TravelEndDate = dto.TravelEndDate;
        // No status: the group's verbs move it (slice 1c, finding A11).
        entity.MaxParticipants = dto.MaxParticipants;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffGroupTravelSummaryDto> ToSummaryDtoList(this IEnumerable<StaffGroupTravel> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelRequestComment

    public static StaffTravelRequestCommentDto ToDto(this StaffTravelRequestComment entity)
    {
        return new StaffTravelRequestCommentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            AuthorId = entity.AuthorId,
            AuthorName = entity.Author?.FullName ?? string.Empty,
            CommentType = entity.CommentType,
            Body = entity.Body,
            IsVisibleToTraveller = entity.IsVisibleToTraveller,
            ParentCommentId = entity.ParentCommentId,
            Replies = entity.Replies.Select(r => r.ToDto()).ToList(),
        };
    }

    public static StaffTravelRequestComment ToEntity(this CreateStaffTravelRequestCommentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelRequestComment
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            // AuthorId is assigned by the service from the caller's token.
            CommentType = dto.CommentType,
            Body = dto.Body,
            IsVisibleToTraveller = dto.IsVisibleToTraveller,
            ParentCommentId = dto.ParentCommentId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelRequestComment entity, UpdateStaffTravelRequestCommentDto dto, Guid userId)
    {
        entity.Body = dto.Body;
        entity.IsVisibleToTraveller = dto.IsVisibleToTraveller;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelRequestAttachment

    public static StaffTravelRequestAttachmentDto ToDto(this StaffTravelRequestAttachment entity)
    {
        return new StaffTravelRequestAttachmentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            FileName = entity.FileName,
            FileUrl = entity.FileUrl,
            FileSizeBytes = entity.FileSizeBytes,
            MimeType = entity.MimeType,
            AttachmentType = entity.AttachmentType,
            FileUploadRecordId = entity.FileUploadRecordId,
            DocumentRecordId = entity.DocumentRecordId,
            DocumentVersionId = entity.DocumentVersionId,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
            UploadedAt = entity.UploadedAt,
        };
    }

    public static StaffTravelRequestAttachment ToEntity(this CreateStaffTravelRequestAttachmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelRequestAttachment
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            FileName = dto.FileName,
            // FileUrl stays empty for gated uploads — the file is addressed by its DMS ids.
            FileUrl = string.Empty,
            FileSizeBytes = dto.FileSizeBytes,
            MimeType = dto.MimeType,
            FileUploadRecordId = dto.FileUploadRecordId,
            DocumentRecordId = dto.DocumentRecordId,
            DocumentVersionId = dto.DocumentVersionId,
            AttachmentType = dto.AttachmentType,
            // UploadedById is assigned by the service from the caller's token.
            UploadedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    // ========================================================================
    // GROUP 2 — ITINERARY & LEGS
    // ========================================================================

    #region StaffTravelItinerary

    public static StaffTravelItineraryDto ToDto(this StaffTravelItinerary entity)
    {
        return new StaffTravelItineraryDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            VersionNumber = entity.VersionNumber,
            IsCurrentVersion = entity.IsCurrentVersion,
            Status = entity.Status,
            Title = entity.Title,
            TotalTravelDays = entity.TotalTravelDays,
            TotalWorkingDays = entity.TotalWorkingDays,
            TotalWeekendDays = entity.TotalWeekendDays,
            SummaryNotes = entity.SummaryNotes,
            FinalizedAt = entity.FinalizedAt,
            Legs = entity.Legs.Select(l => l.ToDto()).ToList(),
        };
    }

    public static StaffTravelItinerarySummaryDto ToSummaryDto(this StaffTravelItinerary entity)
    {
        return new StaffTravelItinerarySummaryDto
        {
            Id = entity.Id,
            VersionNumber = entity.VersionNumber,
            IsCurrentVersion = entity.IsCurrentVersion,
            Status = entity.Status,
            Title = entity.Title,
            TotalTravelDays = entity.TotalTravelDays,
            LegCount = entity.Legs.Count,
            FinalizedAt = entity.FinalizedAt,
        };
    }

    public static StaffTravelItinerary ToEntity(this CreateStaffTravelItineraryDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelItinerary
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            VersionNumber = dto.VersionNumber,
            IsCurrentVersion = dto.IsCurrentVersion,
            Status = TravelItineraryStatus.Draft,
            Title = dto.Title,
            TotalTravelDays = dto.TotalTravelDays,
            TotalWorkingDays = dto.TotalWorkingDays,
            TotalWeekendDays = dto.TotalWeekendDays,
            SummaryNotes = dto.SummaryNotes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelItinerary entity, UpdateStaffTravelItineraryDto dto, Guid userId)
    {
        entity.Status = dto.Status;
        entity.Title = dto.Title;
        entity.IsCurrentVersion = dto.IsCurrentVersion;
        entity.TotalTravelDays = dto.TotalTravelDays;
        entity.TotalWorkingDays = dto.TotalWorkingDays;
        entity.TotalWeekendDays = dto.TotalWeekendDays;
        entity.SummaryNotes = dto.SummaryNotes;
        entity.FinalizedAt = dto.FinalizedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelItinerarySummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelItinerary> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelItineraryLeg

    public static StaffTravelItineraryLegDto ToDto(this StaffTravelItineraryLeg entity)
    {
        return new StaffTravelItineraryLegDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelItineraryId = entity.StaffTravelItineraryId,
            SequenceOrder = entity.SequenceOrder,
            LegType = entity.LegType,
            LegDate = entity.LegDate,
            OriginCity = entity.OriginCity,
            OriginCountryId = entity.OriginCountryId,
            OriginCountryName = entity.OriginCountry?.Name,
            DestinationCity = entity.DestinationCity,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            TransportMode = entity.TransportMode,
            DepartureDatetime = entity.DepartureDatetime,
            ArrivalDatetime = entity.ArrivalDatetime,
            FlightBookingId = entity.FlightBookingId,
            HotelBookingId = entity.HotelBookingId,
            GroundTransportId = entity.GroundTransportId,
            Notes = entity.Notes,
            Activities = entity.Activities.Select(a => a.ToDto()).ToList(),
        };
    }

    public static StaffTravelItineraryLeg ToEntity(this CreateStaffTravelItineraryLegDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelItineraryLeg
        {
            TenantId = tenantId,
            StaffTravelItineraryId = dto.StaffTravelItineraryId,
            SequenceOrder = dto.SequenceOrder,
            LegType = dto.LegType,
            LegDate = dto.LegDate,
            OriginCity = dto.OriginCity,
            OriginCountryId = dto.OriginCountryId,
            DestinationCity = dto.DestinationCity,
            DestinationCountryId = dto.DestinationCountryId,
            TransportMode = dto.TransportMode,
            DepartureDatetime = dto.DepartureDatetime,
            ArrivalDatetime = dto.ArrivalDatetime,
            FlightBookingId = dto.FlightBookingId,
            HotelBookingId = dto.HotelBookingId,
            GroundTransportId = dto.GroundTransportId,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelItineraryLeg entity, UpdateStaffTravelItineraryLegDto dto, Guid userId)
    {
        entity.SequenceOrder = dto.SequenceOrder;
        entity.LegType = dto.LegType;
        entity.LegDate = dto.LegDate;
        entity.OriginCity = dto.OriginCity;
        entity.OriginCountryId = dto.OriginCountryId;
        entity.DestinationCity = dto.DestinationCity;
        entity.DestinationCountryId = dto.DestinationCountryId;
        entity.TransportMode = dto.TransportMode;
        entity.DepartureDatetime = dto.DepartureDatetime;
        entity.ArrivalDatetime = dto.ArrivalDatetime;
        entity.FlightBookingId = dto.FlightBookingId;
        entity.HotelBookingId = dto.HotelBookingId;
        entity.GroundTransportId = dto.GroundTransportId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelItineraryActivity

    public static StaffTravelItineraryActivityDto ToDto(this StaffTravelItineraryActivity entity)
    {
        return new StaffTravelItineraryActivityDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelItineraryLegId = entity.StaffTravelItineraryLegId,
            ActivityType = entity.ActivityType,
            Title = entity.Title,
            Description = entity.Description,
            LocationName = entity.LocationName,
            LocationAddress = entity.LocationAddress,
            StartDatetime = entity.StartDatetime,
            EndDatetime = entity.EndDatetime,
            ContactName = entity.ContactName,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            IsMandatory = entity.IsMandatory,
        };
    }

    public static StaffTravelItineraryActivity ToEntity(this CreateStaffTravelItineraryActivityDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelItineraryActivity
        {
            TenantId = tenantId,
            StaffTravelItineraryLegId = dto.StaffTravelItineraryLegId,
            ActivityType = dto.ActivityType,
            Title = dto.Title,
            Description = dto.Description,
            LocationName = dto.LocationName,
            LocationAddress = dto.LocationAddress,
            StartDatetime = dto.StartDatetime,
            EndDatetime = dto.EndDatetime,
            ContactName = dto.ContactName,
            ContactEmail = dto.ContactEmail,
            ContactPhone = dto.ContactPhone,
            IsMandatory = dto.IsMandatory,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelItineraryActivity entity, UpdateStaffTravelItineraryActivityDto dto, Guid userId)
    {
        entity.ActivityType = dto.ActivityType;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.LocationName = dto.LocationName;
        entity.LocationAddress = dto.LocationAddress;
        entity.StartDatetime = dto.StartDatetime;
        entity.EndDatetime = dto.EndDatetime;
        entity.ContactName = dto.ContactName;
        entity.ContactEmail = dto.ContactEmail;
        entity.ContactPhone = dto.ContactPhone;
        entity.IsMandatory = dto.IsMandatory;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // GROUP 3 — APPROVAL WORKFLOW
    // ========================================================================


    // ========================================================================
    // GROUP 4 — BOOKINGS
    // ========================================================================

    #region StaffTravelFlightBooking

    public static StaffTravelFlightBookingDto ToDto(this StaffTravelFlightBooking entity)
    {
        return new StaffTravelFlightBookingDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            BookingReference = entity.BookingReference,
            AirlineCode = entity.AirlineCode,
            AirlineName = entity.AirlineName,
            BookingClass = entity.BookingClass,
            PolicyAllowedClass = entity.PolicyAllowedClass,
            ClassExceptionApproved = entity.ClassExceptionApproved,
            ClassExceptionReason = entity.ClassExceptionReason,
            BookedBy = entity.BookedBy,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            TotalFare = entity.TotalFare,
            TaxesAndFees = entity.TaxesAndFees,
            CurrencyCode = entity.CurrencyCode,
            TicketNumber = entity.TicketNumber,
            Status = entity.Status,
            BookedAt = entity.BookedAt,
            CancelledAt = entity.CancelledAt,
            CancellationFee = entity.CancellationFee,
            Segments = entity.Segments.OrderBy(s => s.SegmentOrder).Select(s => s.ToDto()).ToList(),
        };
    }

    public static StaffTravelFlightBookingSummaryDto ToSummaryDto(this StaffTravelFlightBooking entity)
    {
        return new StaffTravelFlightBookingSummaryDto
        {
            Id = entity.Id,
            BookingReference = entity.BookingReference,
            AirlineName = entity.AirlineName,
            BookingClass = entity.BookingClass,
            TotalFare = entity.TotalFare,
            CurrencyCode = entity.CurrencyCode,
            Status = entity.Status,
            TicketNumber = entity.TicketNumber,
            SegmentCount = entity.Segments.Count,
        };
    }

    public static StaffTravelFlightBooking ToEntity(this CreateStaffTravelFlightBookingDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelFlightBooking
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            BookingReference = dto.BookingReference,
            AirlineCode = dto.AirlineCode,
            AirlineName = dto.AirlineName,
            BookingClass = dto.BookingClass,
            PolicyAllowedClass = dto.PolicyAllowedClass,
            ClassExceptionApproved = dto.ClassExceptionApproved,
            ClassExceptionReason = dto.ClassExceptionReason,
            BookedBy = dto.BookedBy,
            VendorId = dto.VendorId,
            TotalFare = dto.TotalFare,
            TaxesAndFees = dto.TaxesAndFees,
            CurrencyCode = dto.CurrencyCode,
            TicketNumber = dto.TicketNumber,
            Status = dto.Status,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelFlightBooking entity, UpdateStaffTravelFlightBookingDto dto, Guid userId)
    {
        entity.BookingReference = dto.BookingReference;
        entity.AirlineCode = dto.AirlineCode;
        entity.AirlineName = dto.AirlineName;
        entity.BookingClass = dto.BookingClass;
        entity.PolicyAllowedClass = dto.PolicyAllowedClass;
        entity.ClassExceptionApproved = dto.ClassExceptionApproved;
        entity.ClassExceptionReason = dto.ClassExceptionReason;
        entity.BookedBy = dto.BookedBy;
        entity.VendorId = dto.VendorId;
        entity.TotalFare = dto.TotalFare;
        entity.TaxesAndFees = dto.TaxesAndFees;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.TicketNumber = dto.TicketNumber;
        entity.Status = dto.Status;
        entity.BookedAt = dto.BookedAt;
        entity.CancelledAt = dto.CancelledAt;
        entity.CancellationFee = dto.CancellationFee;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelFlightBookingSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelFlightBooking> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelFlightSegment

    public static StaffTravelFlightSegmentDto ToDto(this StaffTravelFlightSegment entity)
    {
        return new StaffTravelFlightSegmentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelFlightBookingId = entity.StaffTravelFlightBookingId,
            SegmentOrder = entity.SegmentOrder,
            FlightNumber = entity.FlightNumber,
            OperatingCarrier = entity.OperatingCarrier,
            OriginAirport = entity.OriginAirport,
            DestinationAirport = entity.DestinationAirport,
            DepartureDatetime = entity.DepartureDatetime,
            ArrivalDatetime = entity.ArrivalDatetime,
            DepartureTerminal = entity.DepartureTerminal,
            ArrivalTerminal = entity.ArrivalTerminal,
            DurationMinutes = entity.DurationMinutes,
            AircraftType = entity.AircraftType,
            SeatNumber = entity.SeatNumber,
            IsLayover = entity.IsLayover,
            LayoverDurationMinutes = entity.LayoverDurationMinutes,
            BaggageAllowanceKg = entity.BaggageAllowanceKg,
        };
    }

    public static StaffTravelFlightSegment ToEntity(this CreateStaffTravelFlightSegmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelFlightSegment
        {
            TenantId = tenantId,
            StaffTravelFlightBookingId = dto.StaffTravelFlightBookingId,
            SegmentOrder = dto.SegmentOrder,
            FlightNumber = dto.FlightNumber,
            OperatingCarrier = dto.OperatingCarrier,
            OriginAirport = dto.OriginAirport,
            DestinationAirport = dto.DestinationAirport,
            DepartureDatetime = dto.DepartureDatetime,
            ArrivalDatetime = dto.ArrivalDatetime,
            DepartureTerminal = dto.DepartureTerminal,
            ArrivalTerminal = dto.ArrivalTerminal,
            DurationMinutes = dto.DurationMinutes,
            AircraftType = dto.AircraftType,
            SeatNumber = dto.SeatNumber,
            IsLayover = dto.IsLayover,
            LayoverDurationMinutes = dto.LayoverDurationMinutes,
            BaggageAllowanceKg = dto.BaggageAllowanceKg,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelFlightSegment entity, UpdateStaffTravelFlightSegmentDto dto, Guid userId)
    {
        entity.SegmentOrder = dto.SegmentOrder;
        entity.FlightNumber = dto.FlightNumber;
        entity.OperatingCarrier = dto.OperatingCarrier;
        entity.OriginAirport = dto.OriginAirport;
        entity.DestinationAirport = dto.DestinationAirport;
        entity.DepartureDatetime = dto.DepartureDatetime;
        entity.ArrivalDatetime = dto.ArrivalDatetime;
        entity.DepartureTerminal = dto.DepartureTerminal;
        entity.ArrivalTerminal = dto.ArrivalTerminal;
        entity.DurationMinutes = dto.DurationMinutes;
        entity.AircraftType = dto.AircraftType;
        entity.SeatNumber = dto.SeatNumber;
        entity.IsLayover = dto.IsLayover;
        entity.LayoverDurationMinutes = dto.LayoverDurationMinutes;
        entity.BaggageAllowanceKg = dto.BaggageAllowanceKg;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelHotelBooking

    public static StaffTravelHotelBookingDto ToDto(this StaffTravelHotelBooking entity)
    {
        return new StaffTravelHotelBookingDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            BookingReference = entity.BookingReference,
            HotelName = entity.HotelName,
            HotelChain = entity.HotelChain,
            HotelAddress = entity.HotelAddress,
            City = entity.City,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            StarRating = entity.StarRating,
            CheckInDate = entity.CheckInDate,
            CheckOutDate = entity.CheckOutDate,
            NumberOfNights = entity.NumberOfNights,
            RoomType = entity.RoomType,
            RatePerNight = entity.RatePerNight,
            TotalCost = entity.TotalCost,
            CurrencyCode = entity.CurrencyCode,
            PolicyMaxRatePerNight = entity.PolicyMaxRatePerNight,
            RateExceptionApproved = entity.RateExceptionApproved,
            RateExceptionReason = entity.RateExceptionReason,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            BookedBy = entity.BookedBy,
            Status = entity.Status,
            CancellationPolicy = entity.CancellationPolicy,
            BookedAt = entity.BookedAt,
            CancelledAt = entity.CancelledAt,
            CancellationFee = entity.CancellationFee,
        };
    }

    public static StaffTravelHotelBookingSummaryDto ToSummaryDto(this StaffTravelHotelBooking entity)
    {
        return new StaffTravelHotelBookingSummaryDto
        {
            Id = entity.Id,
            HotelName = entity.HotelName,
            City = entity.City,
            CheckInDate = entity.CheckInDate,
            CheckOutDate = entity.CheckOutDate,
            NumberOfNights = entity.NumberOfNights,
            TotalCost = entity.TotalCost,
            CurrencyCode = entity.CurrencyCode,
            Status = entity.Status,
        };
    }

    public static StaffTravelHotelBooking ToEntity(this CreateStaffTravelHotelBookingDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelHotelBooking
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            BookingReference = dto.BookingReference,
            HotelName = dto.HotelName,
            HotelChain = dto.HotelChain,
            HotelAddress = dto.HotelAddress,
            City = dto.City,
            CountryId = dto.CountryId,
            StarRating = dto.StarRating,
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            NumberOfNights = dto.NumberOfNights,
            RoomType = dto.RoomType,
            RatePerNight = dto.RatePerNight,
            TotalCost = dto.TotalCost,
            CurrencyCode = dto.CurrencyCode,
            PolicyMaxRatePerNight = dto.PolicyMaxRatePerNight,
            RateExceptionApproved = dto.RateExceptionApproved,
            RateExceptionReason = dto.RateExceptionReason,
            VendorId = dto.VendorId,
            BookedBy = dto.BookedBy,
            Status = dto.Status,
            CancellationPolicy = dto.CancellationPolicy,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelHotelBooking entity, UpdateStaffTravelHotelBookingDto dto, Guid userId)
    {
        entity.BookingReference = dto.BookingReference;
        entity.HotelName = dto.HotelName;
        entity.HotelChain = dto.HotelChain;
        entity.HotelAddress = dto.HotelAddress;
        entity.City = dto.City;
        entity.CountryId = dto.CountryId;
        entity.StarRating = dto.StarRating;
        entity.CheckInDate = dto.CheckInDate;
        entity.CheckOutDate = dto.CheckOutDate;
        entity.NumberOfNights = dto.NumberOfNights;
        entity.RoomType = dto.RoomType;
        entity.RatePerNight = dto.RatePerNight;
        entity.TotalCost = dto.TotalCost;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.PolicyMaxRatePerNight = dto.PolicyMaxRatePerNight;
        entity.RateExceptionApproved = dto.RateExceptionApproved;
        entity.RateExceptionReason = dto.RateExceptionReason;
        entity.VendorId = dto.VendorId;
        entity.BookedBy = dto.BookedBy;
        entity.Status = dto.Status;
        entity.CancellationPolicy = dto.CancellationPolicy;
        entity.BookedAt = dto.BookedAt;
        entity.CancelledAt = dto.CancelledAt;
        entity.CancellationFee = dto.CancellationFee;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelHotelBookingSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelHotelBooking> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelGroundTransport

    public static StaffTravelGroundTransportDto ToDto(this StaffTravelGroundTransport entity)
    {
        return new StaffTravelGroundTransportDto
        {
            FleetTripId = entity.FleetTripId,
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            TransportType = entity.TransportType,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            BookingReference = entity.BookingReference,
            PickupLocation = entity.PickupLocation,
            DropoffLocation = entity.DropoffLocation,
            PickupDatetime = entity.PickupDatetime,
            DropoffDatetime = entity.DropoffDatetime,
            EstimatedCost = entity.EstimatedCost,
            ActualCost = entity.ActualCost,
            CurrencyCode = entity.CurrencyCode,
            Status = entity.Status,
            Notes = entity.Notes,
        };
    }

    public static StaffTravelGroundTransport ToEntity(this CreateStaffTravelGroundTransportDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelGroundTransport
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            TransportType = dto.TransportType,
            VendorId = dto.VendorId,
            BookingReference = dto.BookingReference,
            PickupLocation = dto.PickupLocation,
            DropoffLocation = dto.DropoffLocation,
            PickupDatetime = dto.PickupDatetime,
            DropoffDatetime = dto.DropoffDatetime,
            EstimatedCost = dto.EstimatedCost,
            ActualCost = dto.ActualCost,
            CurrencyCode = dto.CurrencyCode,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelGroundTransport entity, UpdateStaffTravelGroundTransportDto dto, Guid userId)
    {
        entity.TransportType = dto.TransportType;
        entity.VendorId = dto.VendorId;
        entity.BookingReference = dto.BookingReference;
        entity.PickupLocation = dto.PickupLocation;
        entity.DropoffLocation = dto.DropoffLocation;
        entity.PickupDatetime = dto.PickupDatetime;
        entity.DropoffDatetime = dto.DropoffDatetime;
        entity.EstimatedCost = dto.EstimatedCost;
        entity.ActualCost = dto.ActualCost;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelCarRentalBooking

    public static StaffTravelCarRentalBookingDto ToDto(this StaffTravelCarRentalBooking entity)
    {
        return new StaffTravelCarRentalBookingDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            BookingReference = entity.BookingReference,
            PickupLocation = entity.PickupLocation,
            DropoffLocation = entity.DropoffLocation,
            PickupDatetime = entity.PickupDatetime,
            DropoffDatetime = entity.DropoffDatetime,
            VehicleCategory = entity.VehicleCategory,
            VehicleModel = entity.VehicleModel,
            DailyRate = entity.DailyRate,
            TotalCost = entity.TotalCost,
            CurrencyCode = entity.CurrencyCode,
            InsuranceIncluded = entity.InsuranceIncluded,
            FuelPolicy = entity.FuelPolicy,
            DriverLicenseRequired = entity.DriverLicenseRequired,
            Status = entity.Status,
            BookedAt = entity.BookedAt,
        };
    }

    public static StaffTravelCarRentalBooking ToEntity(this CreateStaffTravelCarRentalBookingDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelCarRentalBooking
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            VendorId = dto.VendorId,
            BookingReference = dto.BookingReference,
            PickupLocation = dto.PickupLocation,
            DropoffLocation = dto.DropoffLocation,
            PickupDatetime = dto.PickupDatetime,
            DropoffDatetime = dto.DropoffDatetime,
            VehicleCategory = dto.VehicleCategory,
            VehicleModel = dto.VehicleModel,
            DailyRate = dto.DailyRate,
            TotalCost = dto.TotalCost,
            CurrencyCode = dto.CurrencyCode,
            InsuranceIncluded = dto.InsuranceIncluded,
            FuelPolicy = dto.FuelPolicy,
            DriverLicenseRequired = dto.DriverLicenseRequired,
            Status = dto.Status,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelCarRentalBooking entity, UpdateStaffTravelCarRentalBookingDto dto, Guid userId)
    {
        entity.VendorId = dto.VendorId;
        entity.BookingReference = dto.BookingReference;
        entity.PickupLocation = dto.PickupLocation;
        entity.DropoffLocation = dto.DropoffLocation;
        entity.PickupDatetime = dto.PickupDatetime;
        entity.DropoffDatetime = dto.DropoffDatetime;
        entity.VehicleCategory = dto.VehicleCategory;
        entity.VehicleModel = dto.VehicleModel;
        entity.DailyRate = dto.DailyRate;
        entity.TotalCost = dto.TotalCost;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.InsuranceIncluded = dto.InsuranceIncluded;
        entity.FuelPolicy = dto.FuelPolicy;
        entity.DriverLicenseRequired = dto.DriverLicenseRequired;
        entity.Status = dto.Status;
        entity.BookedAt = dto.BookedAt;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // GROUP 5 — FINANCE: BUDGET, EXPENSES & ADVANCES
    // ========================================================================

    #region StaffTravelBudget

    public static StaffTravelBudgetDto ToDto(this StaffTravelBudget entity)
    {
        return new StaffTravelBudgetDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            BudgetYear = entity.BudgetYear,
            ApprovedTotal = entity.ApprovedTotal,
            CurrencyCode = entity.CurrencyCode,
            FlightBudget = entity.FlightBudget,
            AccommodationBudget = entity.AccommodationBudget,
            PerDiemBudget = entity.PerDiemBudget,
            TransportBudget = entity.TransportBudget,
            MiscellaneousBudget = entity.MiscellaneousBudget,
            TotalCommitted = entity.TotalCommitted,
            TotalActual = entity.TotalActual,
            Variance = entity.Variance,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovedAt = entity.ApprovedAt,
        };
    }

    /// <summary>The currency and the total are the service's (lane 3): the trip's currency, and the total it settled.</summary>
    public static StaffTravelBudget ToEntity(
        this CreateStaffTravelBudgetDto dto, Guid tenantId, Guid userId, string currencyCode, decimal approvedTotal)
    {
        return new StaffTravelBudget
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            BudgetYear = dto.BudgetYear,
            ApprovedTotal = approvedTotal,
            CurrencyCode = currencyCode,
            FlightBudget = dto.FlightBudget,
            AccommodationBudget = dto.AccommodationBudget,
            PerDiemBudget = dto.PerDiemBudget,
            TransportBudget = dto.TransportBudget,
            MiscellaneousBudget = dto.MiscellaneousBudget,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>The total is the service's, as on create; the currency stays the trip's.</summary>
    public static void UpdateEntity(this StaffTravelBudget entity, UpdateStaffTravelBudgetDto dto, Guid userId, decimal approvedTotal)
    {
        entity.BudgetYear = dto.BudgetYear;
        entity.ApprovedTotal = approvedTotal;
        entity.FlightBudget = dto.FlightBudget;
        entity.AccommodationBudget = dto.AccommodationBudget;
        entity.PerDiemBudget = dto.PerDiemBudget;
        entity.TransportBudget = dto.TransportBudget;
        entity.MiscellaneousBudget = dto.MiscellaneousBudget;
        // TotalCommitted / TotalActual / Variance are NOT mapped from the DTO: the service derives
        // all three from the request's bookings and claims immediately after this runs
        // (StaffTravelFinanceService.ApplyRollupAsync). Assigning them here as well would be
        // harmless but misleading — a reader would think the client's figures survive.
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelExpenseClaim

    public static StaffTravelExpenseClaimDto ToDto(this StaffTravelExpenseClaim entity)
    {
        return new StaffTravelExpenseClaimDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ClaimNumber = entity.ClaimNumber,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ClaimType = entity.ClaimType,
            Status = entity.Status,
            TravelAdvanceId = entity.TravelAdvanceId,
            TravelAdvanceNumber = entity.TravelAdvance?.AdvanceNumber,
            TotalClaimed = entity.TotalClaimed,
            TotalApproved = entity.TotalApproved,
            TotalRejected = entity.TotalRejected,
            AdvanceDeducted = entity.AdvanceDeducted,
            NetPayable = entity.NetPayable,
            CurrencyCode = entity.CurrencyCode,
            PaymentMethod = entity.PaymentMethod,
            PaymentReference = entity.PaymentReference,
            PaidAt = entity.PaidAt,
            FinanceReviewedById = entity.FinanceReviewedById,
            FinanceReviewedByName = entity.FinanceReviewedBy?.FullName,
            FinanceReviewedAt = entity.FinanceReviewedAt,
            SubmittedAt = entity.SubmittedAt,
            ReviewNotes = entity.ReviewNotes,
            PaidById = entity.PaidById,
            PaidByName = entity.PaidBy?.FullName,
            AdvanceWaiverReason = entity.AdvanceWaiverReason,
            PaymentVoidedAt = entity.PaymentVoidedAt,
            PaymentVoidedByName = entity.PaymentVoidedBy?.FullName,
            PaymentVoidReason = entity.PaymentVoidReason,
            Lines = entity.Lines.Where(l => !l.IsDeleted).Select(l => l.ToDto()).ToList(),
        };
    }

    public static StaffTravelExpenseClaimSummaryDto ToSummaryDto(this StaffTravelExpenseClaim entity)
    {
        return new StaffTravelExpenseClaimSummaryDto
        {
            Id = entity.Id,
            ClaimNumber = entity.ClaimNumber,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ClaimType = entity.ClaimType,
            Status = entity.Status,
            TotalClaimed = entity.TotalClaimed,
            NetPayable = entity.NetPayable,
            CurrencyCode = entity.CurrencyCode,
            SubmittedAt = entity.SubmittedAt,
        };
    }

    /// <summary>The traveller and the currency are the server's (lane 3, B3 and B11): the trip's traveller, and the
    /// base currency every claim total is kept in.</summary>
    public static StaffTravelExpenseClaim ToEntity(
        this CreateStaffTravelExpenseClaimDto dto, Guid tenantId, Guid userId, Guid employeeId, string baseCurrencyCode)
    {
        return new StaffTravelExpenseClaim
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            EmployeeId = employeeId,
            ClaimType = dto.ClaimType,
            Status = TravelClaimStatus.Draft,
            TravelAdvanceId = dto.TravelAdvanceId,
            CurrencyCode = baseCurrencyCode,
            CreatedBy = userId.ToString(),
            Lines = dto.Lines.Select(l => l.ToEntity(tenantId, userId)).ToList(),
        };
    }

    public static void UpdateEntity(this StaffTravelExpenseClaim entity, UpdateStaffTravelExpenseClaimDto dto, Guid userId)
    {
        entity.ClaimType = dto.ClaimType;
        entity.TravelAdvanceId = dto.TravelAdvanceId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelExpenseClaimSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelExpenseClaim> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelExpenseClaimLine

    public static StaffTravelExpenseClaimLineDto ToDto(this StaffTravelExpenseClaimLine entity)
    {
        return new StaffTravelExpenseClaimLineDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelExpenseClaimId = entity.StaffTravelExpenseClaimId,
            ExpenseCategory = entity.ExpenseCategory,
            ExpenseDate = entity.ExpenseDate,
            Description = entity.Description,
            MerchantName = entity.MerchantName,
            AmountOriginal = entity.AmountOriginal,
            CurrencyOriginal = entity.CurrencyOriginal,
            ExchangeRate = entity.ExchangeRate,
            AmountBaseCurrency = entity.AmountBaseCurrency,
            PolicyLimit = entity.PolicyLimit,
            AmountApproved = entity.AmountApproved,
            AmountRejected = entity.AmountRejected,
            RejectionReason = entity.RejectionReason,
            ReceiptAttachmentId = entity.ReceiptAttachmentId,
            IsPerDiem = entity.IsPerDiem,
            PerDiemRateId = entity.PerDiemRateId,
            Status = entity.Status,
            ReviewedById = entity.ReviewedById,
            ReviewedByName = entity.ReviewedBy?.FullName,
            ReviewedAt = entity.ReviewedAt,
        };
    }

    public static StaffTravelExpenseClaimLine ToEntity(this CreateStaffTravelExpenseClaimLineDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelExpenseClaimLine
        {
            TenantId = tenantId,
            StaffTravelExpenseClaimId = dto.StaffTravelExpenseClaimId,
            ExpenseCategory = dto.ExpenseCategory,
            ExpenseDate = dto.ExpenseDate,
            Description = dto.Description,
            MerchantName = dto.MerchantName,
            AmountOriginal = dto.AmountOriginal,
            CurrencyOriginal = dto.CurrencyOriginal,
            // ⚠ ExchangeRate and AmountBaseCurrency are set by the service from Finance's published
            // rate immediately after this, so copying the payload's numbers here only made it look
            // as though the caller's figures counted.
            PolicyLimit = dto.PolicyLimit,
            ReceiptAttachmentId = dto.ReceiptAttachmentId,
            IsPerDiem = dto.IsPerDiem,
            PerDiemRateId = dto.PerDiemRateId,
            Status = TravelExpenseLineStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelExpenseClaimLine entity, UpdateStaffTravelExpenseClaimLineDto dto, Guid userId)
    {
        entity.ExpenseCategory = dto.ExpenseCategory;
        entity.ExpenseDate = dto.ExpenseDate;
        entity.Description = dto.Description;
        entity.MerchantName = dto.MerchantName;
        entity.AmountOriginal = dto.AmountOriginal;
        entity.CurrencyOriginal = dto.CurrencyOriginal;
        // ⚠ ExchangeRate and AmountBaseCurrency are DERIVED, not accepted. The service applies
        // Finance's published rate for the expense date and does the arithmetic itself; taking them
        // from the payload here is what let an edited line be valued at whatever the caller said.
        entity.PolicyLimit = dto.PolicyLimit;
        entity.ReceiptAttachmentId = dto.ReceiptAttachmentId;
        entity.IsPerDiem = dto.IsPerDiem;
        entity.PerDiemRateId = dto.PerDiemRateId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelAdvance

    public static StaffTravelAdvanceDto ToDto(this StaffTravelAdvance entity)
    {
        return new StaffTravelAdvanceDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AdvanceNumber = entity.AdvanceNumber,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            RequestedAmount = entity.RequestedAmount,
            ApprovedAmount = entity.ApprovedAmount,
            CurrencyCode = entity.CurrencyCode,
            AdvanceType = entity.AdvanceType,
            Status = entity.Status,
            DisbursedAt = entity.DisbursedAt,
            SettlementDeadline = entity.SettlementDeadline,
            SettledAmount = entity.SettledAmount,
            UnsettledAmount = entity.UnsettledAmount,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            DisbursedById = entity.DisbursedById,
            DisbursedByName = entity.DisbursedBy?.FullName,
            RejectedAt = entity.RejectedAt,
            RejectedByName = entity.RejectedBy?.FullName,
            RejectionReason = entity.RejectionReason,
            CancelledAt = entity.CancelledAt,
            CancelledByName = entity.CancelledBy?.FullName,
            CancellationReason = entity.CancellationReason,
            WrittenOffAt = entity.WrittenOffAt,
            WrittenOffByName = entity.WrittenOffBy?.FullName,
            WriteOffReason = entity.WriteOffReason,
            WrittenOffAmount = entity.Status == TravelAdvanceStatus.WrittenOff
                ? (entity.ApprovedAmount ?? 0m) - entity.SettledAmount
                : null,
            RefundedAmount = entity.RefundedAmount,
            RefundedAt = entity.RefundedAt,
            RefundedByName = entity.RefundedBy?.FullName,
            RefundReference = entity.RefundReference,
            IsOverdue = IsAdvanceOverdue(entity),
        };
    }

    /// <summary>Cash out past its deadline, read from the figures rather than the stored status, which the
    /// nightly sweep writes (<see cref="ErpSystem.Core.Services.HR.StaffTravelAdvanceRules"/>).</summary>
    private static bool IsAdvanceOverdue(StaffTravelAdvance entity)
        => ErpSystem.Core.Services.HR.StaffTravelAdvanceRules.IsCashOutStatus(entity.Status)
           && entity.UnsettledAmount > 0m
           && entity.SettlementDeadline is DateOnly deadline
           && deadline < DateOnly.FromDateTime(DateTime.UtcNow);

    public static StaffTravelAdvanceSummaryDto ToSummaryDto(this StaffTravelAdvance entity)
    {
        return new StaffTravelAdvanceSummaryDto
        {
            Id = entity.Id,
            AdvanceNumber = entity.AdvanceNumber,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            RequestedAmount = entity.RequestedAmount,
            ApprovedAmount = entity.ApprovedAmount,
            CurrencyCode = entity.CurrencyCode,
            AdvanceType = entity.AdvanceType,
            Status = entity.Status,
            SettledAmount = entity.SettledAmount,
            UnsettledAmount = entity.UnsettledAmount,
            RefundedAmount = entity.RefundedAmount,
            SettlementDeadline = entity.SettlementDeadline,
            DisbursedAt = entity.DisbursedAt,
            IsOverdue = IsAdvanceOverdue(entity),
            OutcomeReason = entity.Status switch
            {
                TravelAdvanceStatus.Rejected => entity.RejectionReason,
                TravelAdvanceStatus.Cancelled => entity.CancellationReason,
                TravelAdvanceStatus.WrittenOff => entity.WriteOffReason,
                _ => null,
            },
        };
    }

    /// <summary>The traveller (<paramref name="employeeId"/>) is the trip's, passed in by the service; nothing is
    /// owed until the advance is disbursed (lane 3, B3 and B8).</summary>
    public static StaffTravelAdvance ToEntity(this CreateStaffTravelAdvanceDto dto, Guid tenantId, Guid userId, Guid employeeId)
    {
        return new StaffTravelAdvance
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            EmployeeId = employeeId,
            RequestedAmount = dto.RequestedAmount,
            CurrencyCode = dto.CurrencyCode,
            AdvanceType = dto.AdvanceType,
            Status = TravelAdvanceStatus.Requested,
            SettlementDeadline = dto.SettlementDeadline,
            UnsettledAmount = 0m,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelAdvance entity, UpdateStaffTravelAdvanceDto dto, Guid userId)
    {
        entity.RequestedAmount = dto.RequestedAmount;
        // ⚠ No approved amount: approving an advance is `ApproveAdvanceAsync`, which stamps the approver
        // from the token and checks the status (the DTO lost the ignored field in lane 3). An edit is a
        // Requested advance's only, so nothing it changes has been approved, paid or settled.
        entity.CurrencyCode = dto.CurrencyCode;
        entity.AdvanceType = dto.AdvanceType;
        entity.SettlementDeadline = dto.SettlementDeadline;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelAdvanceSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelAdvance> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelPerDiemRate

    public static StaffTravelPerDiemRateDto ToDto(this StaffTravelPerDiemRate entity)
    {
        return new StaffTravelPerDiemRateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            City = entity.City,
            StaffLevelId = entity.StaffLevelId,
            StaffLevelName = entity.StaffLevel?.Name,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            DailyAllowance = entity.DailyAllowance,
            AccommodationLimit = entity.AccommodationLimit,
            MealAllowance = entity.MealAllowance,
            IncidentalAllowance = entity.IncidentalAllowance,
            CurrencyCode = entity.CurrencyCode,
            MealBreakdownBreakfast = entity.MealBreakdownBreakfast,
            MealBreakdownLunch = entity.MealBreakdownLunch,
            MealBreakdownDinner = entity.MealBreakdownDinner,
            IsActive = entity.IsActive,
        };
    }

    public static StaffTravelPerDiemRate ToEntity(this CreateStaffTravelPerDiemRateDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelPerDiemRate
        {
            TenantId = tenantId,
            CountryId = dto.CountryId,
            City = dto.City,
            StaffLevelId = dto.StaffLevelId,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            DailyAllowance = dto.DailyAllowance,
            AccommodationLimit = dto.AccommodationLimit,
            MealAllowance = dto.MealAllowance,
            IncidentalAllowance = dto.IncidentalAllowance,
            CurrencyCode = dto.CurrencyCode,
            MealBreakdownBreakfast = dto.MealBreakdownBreakfast,
            MealBreakdownLunch = dto.MealBreakdownLunch,
            MealBreakdownDinner = dto.MealBreakdownDinner,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelPerDiemRate entity, UpdateStaffTravelPerDiemRateDto dto, Guid userId)
    {
        entity.CountryId = dto.CountryId;
        entity.City = dto.City;
        entity.StaffLevelId = dto.StaffLevelId;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.DailyAllowance = dto.DailyAllowance;
        entity.AccommodationLimit = dto.AccommodationLimit;
        entity.MealAllowance = dto.MealAllowance;
        entity.IncidentalAllowance = dto.IncidentalAllowance;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.MealBreakdownBreakfast = dto.MealBreakdownBreakfast;
        entity.MealBreakdownLunch = dto.MealBreakdownLunch;
        entity.MealBreakdownDinner = dto.MealBreakdownDinner;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // GROUP 6 — POLICY & VENDOR
    // ========================================================================

    #region StaffTravelPolicy

    public static StaffTravelPolicyDto ToDto(this StaffTravelPolicy entity)
    {
        return new StaffTravelPolicyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PolicyName = entity.PolicyName,
            VersionNumber = entity.VersionNumber,
            IsCurrentVersion = entity.IsCurrentVersion,
            AppliesToLevelFromId = entity.AppliesToLevelFromId,
            AppliesToLevelFromName = entity.AppliesToLevelFrom?.Name,
            AppliesToLevelToId = entity.AppliesToLevelToId,
            AppliesToLevelToName = entity.AppliesToLevelTo?.Name,
            AppliesToOrganizationUnitId = entity.AppliesToOrganizationUnitId,
            AppliesToOrganizationUnitName = entity.AppliesToOrganizationUnit?.Name,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            MaxFlightClassDomestic = entity.MaxFlightClassDomestic,
            MaxFlightClassInternational = entity.MaxFlightClassInternational,
            MaxHotelRateDomestic = entity.MaxHotelRateDomestic,
            MaxHotelRateInternational = entity.MaxHotelRateInternational,
            CurrencyCode = entity.CurrencyCode,
            AdvanceBookingDaysFlight = entity.AdvanceBookingDaysFlight,
            AdvanceBookingDaysHotel = entity.AdvanceBookingDaysHotel,
            PreferredVendorMandatory = entity.PreferredVendorMandatory,
            MaxSingleTripBudget = entity.MaxSingleTripBudget,
            ReceiptRequiredAbove = entity.ReceiptRequiredAbove,
            ExpenseSubmissionDays = entity.ExpenseSubmissionDays,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovedAt = entity.ApprovedAt,
            Rules = entity.Rules.Select(r => r.ToDto()).ToList(),
        };
    }

    public static StaffTravelPolicySummaryDto ToSummaryDto(this StaffTravelPolicy entity)
    {
        return new StaffTravelPolicySummaryDto
        {
            Id = entity.Id,
            PolicyName = entity.PolicyName,
            VersionNumber = entity.VersionNumber,
            IsCurrentVersion = entity.IsCurrentVersion,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            MaxSingleTripBudget = entity.MaxSingleTripBudget,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy != null ? entity.ApprovedBy.FullName : null,
            ApprovedAt = entity.ApprovedAt,
            RuleCount = entity.Rules.Count,
        };
    }

    /// <summary>The version, the currency and whether it is in force are the service's (lane 4).</summary>
    public static StaffTravelPolicy ToEntity(this CreateStaffTravelPolicyDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelPolicy
        {
            TenantId = tenantId,
            PolicyName = dto.PolicyName.Trim(),
            AppliesToLevelFromId = dto.AppliesToLevelFromId,
            AppliesToLevelToId = dto.AppliesToLevelToId,
            AppliesToOrganizationUnitId = dto.AppliesToOrganizationUnitId,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            MaxFlightClassDomestic = dto.MaxFlightClassDomestic,
            MaxFlightClassInternational = dto.MaxFlightClassInternational,
            MaxHotelRateDomestic = dto.MaxHotelRateDomestic,
            MaxHotelRateInternational = dto.MaxHotelRateInternational,
            AdvanceBookingDaysFlight = dto.AdvanceBookingDaysFlight,
            AdvanceBookingDaysHotel = dto.AdvanceBookingDaysHotel,
            PreferredVendorMandatory = dto.PreferredVendorMandatory,
            MaxSingleTripBudget = dto.MaxSingleTripBudget,
            ReceiptRequiredAbove = dto.ReceiptRequiredAbove,
            ExpenseSubmissionDays = dto.ExpenseSubmissionDays,
            CreatedBy = userId.ToString(),
        };
    }

    /// <summary>As <see cref="ToEntity"/>: the version, the currency and whether it is in force are the service's.</summary>
    public static void UpdateEntity(this StaffTravelPolicy entity, UpdateStaffTravelPolicyDto dto, Guid userId)
    {
        entity.PolicyName = dto.PolicyName.Trim();
        entity.AppliesToLevelFromId = dto.AppliesToLevelFromId;
        entity.AppliesToLevelToId = dto.AppliesToLevelToId;
        entity.AppliesToOrganizationUnitId = dto.AppliesToOrganizationUnitId;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.MaxFlightClassDomestic = dto.MaxFlightClassDomestic;
        entity.MaxFlightClassInternational = dto.MaxFlightClassInternational;
        entity.MaxHotelRateDomestic = dto.MaxHotelRateDomestic;
        entity.MaxHotelRateInternational = dto.MaxHotelRateInternational;
        entity.AdvanceBookingDaysFlight = dto.AdvanceBookingDaysFlight;
        entity.AdvanceBookingDaysHotel = dto.AdvanceBookingDaysHotel;
        entity.PreferredVendorMandatory = dto.PreferredVendorMandatory;
        entity.MaxSingleTripBudget = dto.MaxSingleTripBudget;
        entity.ReceiptRequiredAbove = dto.ReceiptRequiredAbove;
        entity.ExpenseSubmissionDays = dto.ExpenseSubmissionDays;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelPolicySummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelPolicy> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelPolicyRule

    public static StaffTravelPolicyRuleDto ToDto(this StaffTravelPolicyRule entity)
    {
        return new StaffTravelPolicyRuleDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PolicyId = entity.PolicyId,
            RuleCode = entity.RuleCode,
            RuleName = entity.RuleName,
            RuleType = entity.RuleType,
            ExpenseCategory = entity.ExpenseCategory,
            TravelType = entity.TravelType,
            LimitValue = entity.LimitValue,
            LimitUnit = entity.LimitUnit,
            ExceptionAllowed = entity.ExceptionAllowed,
            ExceptionRequiresApproval = entity.ExceptionRequiresApproval,
            ViolationAction = entity.ViolationAction,
            IsActive = entity.IsActive,
        };
    }

    public static StaffTravelPolicyRule ToEntity(this CreateStaffTravelPolicyRuleDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelPolicyRule
        {
            TenantId = tenantId,
            PolicyId = dto.PolicyId,
            RuleCode = dto.RuleCode,
            RuleName = dto.RuleName,
            RuleType = dto.RuleType,
            ExpenseCategory = dto.ExpenseCategory,
            TravelType = dto.TravelType,
            LimitValue = dto.LimitValue,
            LimitUnit = dto.LimitUnit,
            ExceptionAllowed = dto.ExceptionAllowed,
            ExceptionRequiresApproval = dto.ExceptionRequiresApproval,
            ViolationAction = dto.ViolationAction,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelPolicyRule entity, UpdateStaffTravelPolicyRuleDto dto, Guid userId)
    {
        entity.RuleCode = dto.RuleCode;
        entity.RuleName = dto.RuleName;
        entity.RuleType = dto.RuleType;
        entity.ExpenseCategory = dto.ExpenseCategory;
        entity.TravelType = dto.TravelType;
        entity.LimitValue = dto.LimitValue;
        entity.LimitUnit = dto.LimitUnit;
        entity.ExceptionAllowed = dto.ExceptionAllowed;
        entity.ExceptionRequiresApproval = dto.ExceptionRequiresApproval;
        entity.ViolationAction = dto.ViolationAction;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelPolicyException

    public static StaffTravelPolicyExceptionDto ToDto(this StaffTravelPolicyException entity)
    {
        return new StaffTravelPolicyExceptionDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            PolicyRuleId = entity.PolicyRuleId,
            PolicyRuleName = entity.PolicyRule?.RuleName,
            ExceptionReason = entity.ExceptionReason,
            RequestedValue = entity.RequestedValue,
            PolicyLimit = entity.PolicyLimit,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            DecidedAt = entity.DecidedAt,
            DecisionNotes = entity.DecisionNotes,
        };
    }

    public static StaffTravelPolicyException ToEntity(this CreateStaffTravelPolicyExceptionDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelPolicyException
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            PolicyRuleId = dto.PolicyRuleId,
            ExceptionReason = dto.ExceptionReason,
            RequestedValue = dto.RequestedValue,
            PolicyLimit = dto.PolicyLimit,
            Status = TravelPolicyExceptionStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion


    // ========================================================================
    // GROUP 7 — COMPLIANCE & SAFETY
    // ========================================================================

    #region StaffTravelDocument

    public static StaffTravelDocumentDto ToDto(this StaffTravelDocument entity)
    {
        return new StaffTravelDocumentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            DocumentType = entity.DocumentType,
            DocumentNumber = entity.DocumentNumber,
            IssuingCountryId = entity.IssuingCountryId,
            IssuingCountryName = entity.IssuingCountry?.Name,
            IssueDate = entity.IssueDate,
            ExpiryDate = entity.ExpiryDate,
            IsPrimary = entity.IsPrimary,
            IsVerified = entity.IsVerified,
            VerifiedById = entity.VerifiedById,
            VerifiedByName = entity.VerifiedBy?.FullName,
            VerifiedAt = entity.VerifiedAt,
        };
    }

    public static StaffTravelDocument ToEntity(this CreateStaffTravelDocumentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelDocument
        {
            TenantId = tenantId,
            EmployeeId = dto.EmployeeId,
            DocumentType = dto.DocumentType,
            DocumentNumber = dto.DocumentNumber,
            IssuingCountryId = dto.IssuingCountryId,
            IssueDate = dto.IssueDate,
            ExpiryDate = dto.ExpiryDate,
            IsPrimary = dto.IsPrimary,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelDocument entity, UpdateStaffTravelDocumentDto dto, Guid userId)
    {
        entity.DocumentType = dto.DocumentType;
        entity.DocumentNumber = dto.DocumentNumber;
        entity.IssuingCountryId = dto.IssuingCountryId;
        entity.IssueDate = dto.IssueDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.IsPrimary = dto.IsPrimary;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelVisaRequirement

    public static StaffTravelVisaRequirementDto ToDto(this StaffTravelVisaRequirement entity)
    {
        return new StaffTravelVisaRequirementDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            PassportCountryId = entity.PassportCountryId,
            PassportCountryName = entity.PassportCountry?.Name,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            VisaRequirementType = entity.VisaRequirementType,
            VisaCategory = entity.VisaCategory,
            MaxStayDays = entity.MaxStayDays,
            ProcessingDays = entity.ProcessingDays,
            OfficialSourceUrl = entity.OfficialSourceUrl,
            LastVerifiedAt = entity.LastVerifiedAt,
            Notes = entity.Notes,
        };
    }

    public static StaffTravelVisaRequirement ToEntity(this CreateStaffTravelVisaRequirementDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelVisaRequirement
        {
            TenantId = tenantId,
            PassportCountryId = dto.PassportCountryId,
            DestinationCountryId = dto.DestinationCountryId,
            VisaRequirementType = dto.VisaRequirementType,
            VisaCategory = dto.VisaCategory,
            MaxStayDays = dto.MaxStayDays,
            ProcessingDays = dto.ProcessingDays,
            OfficialSourceUrl = dto.OfficialSourceUrl,
            LastVerifiedAt = dto.LastVerifiedAt,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelVisaRequirement entity, UpdateStaffTravelVisaRequirementDto dto, Guid userId)
    {
        entity.VisaRequirementType = dto.VisaRequirementType;
        entity.VisaCategory = dto.VisaCategory;
        entity.MaxStayDays = dto.MaxStayDays;
        entity.ProcessingDays = dto.ProcessingDays;
        entity.OfficialSourceUrl = dto.OfficialSourceUrl;
        entity.LastVerifiedAt = dto.LastVerifiedAt;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelVisaApplication

    public static StaffTravelVisaApplicationDto ToDto(this StaffTravelVisaApplication entity)
    {
        return new StaffTravelVisaApplicationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            VisaType = entity.VisaType,
            Status = entity.Status,
            SubmittedDate = entity.SubmittedDate,
            ApprovedDate = entity.ApprovedDate,
            ExpiryDate = entity.ExpiryDate,
            VisaNumber = entity.VisaNumber,
            ProcessingFee = entity.ProcessingFee,
            CurrencyCode = entity.CurrencyCode,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            Notes = entity.Notes,
        };
    }

    public static StaffTravelVisaApplicationSummaryDto ToSummaryDto(this StaffTravelVisaApplication entity)
    {
        return new StaffTravelVisaApplicationSummaryDto
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            DestinationCountryName = entity.DestinationCountry?.Name,
            VisaType = entity.VisaType,
            Status = entity.Status,
            SubmittedDate = entity.SubmittedDate,
            ApprovedDate = entity.ApprovedDate,
            ExpiryDate = entity.ExpiryDate,
            VisaNumberMasked = MaskAllButLastFour(entity.VisaNumber),
            ProcessingFee = entity.ProcessingFee,
            CurrencyCode = entity.CurrencyCode,
        };
    }

    /// <summary>
    /// "••••••1234": every character but the last four replaced; a value of four or fewer is masked
    /// whole. Null or blank stays null. Same shape as the bank-account mask on profile changes.
    /// </summary>
    private static string? MaskAllButLastFour(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= 4
                ? new string('•', value.Length)
                : $"{new string('•', value.Length - 4)}{value[^4..]}";

    public static StaffTravelVisaApplication ToEntity(this CreateStaffTravelVisaApplicationDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelVisaApplication
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            EmployeeId = dto.EmployeeId,
            DestinationCountryId = dto.DestinationCountryId,
            VisaType = dto.VisaType,
            Status = dto.Status,
            SubmittedDate = dto.SubmittedDate,
            ApprovedDate = dto.ApprovedDate,
            ExpiryDate = dto.ExpiryDate,
            VisaNumber = dto.VisaNumber,
            VendorId = dto.VendorId,
            ProcessingFee = dto.ProcessingFee,
            CurrencyCode = dto.CurrencyCode,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelVisaApplication entity, UpdateStaffTravelVisaApplicationDto dto, Guid userId)
    {
        entity.DestinationCountryId = dto.DestinationCountryId;
        entity.VisaType = dto.VisaType;
        entity.Status = dto.Status;
        entity.SubmittedDate = dto.SubmittedDate;
        entity.ApprovedDate = dto.ApprovedDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.VisaNumber = dto.VisaNumber;
        entity.ProcessingFee = dto.ProcessingFee;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.VendorId = dto.VendorId;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelVisaApplicationSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelVisaApplication> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelRiskAssessment

    public static StaffTravelRiskAssessmentDto ToDto(this StaffTravelRiskAssessment entity)
    {
        return new StaffTravelRiskAssessmentDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            DestinationCountryId = entity.DestinationCountryId,
            DestinationCountryName = entity.DestinationCountry?.Name,
            DestinationCity = entity.DestinationCity,
            RiskLevel = entity.RiskLevel,
            RiskCategory = entity.RiskCategory,
            AssessmentSource = entity.AssessmentSource,
            AssessmentSummary = entity.AssessmentSummary,
            MitigationRequired = entity.MitigationRequired,
            MitigationNotes = entity.MitigationNotes,
            DutyOfCareBriefingSent = entity.DutyOfCareBriefingSent,
            EmployeeAcknowledged = entity.EmployeeAcknowledged,
            AcknowledgedAt = entity.AcknowledgedAt,
            AssessedById = entity.AssessedById,
            AssessedByName = entity.AssessedBy?.FullName,
            AssessedAt = entity.AssessedAt,
            ValidUntil = entity.ValidUntil,
        };
    }

    public static StaffTravelRiskAssessment ToEntity(this CreateStaffTravelRiskAssessmentDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelRiskAssessment
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            DestinationCountryId = dto.DestinationCountryId,
            DestinationCity = dto.DestinationCity,
            RiskLevel = dto.RiskLevel,
            RiskCategory = dto.RiskCategory,
            AssessmentSource = dto.AssessmentSource,
            AssessmentSummary = dto.AssessmentSummary,
            MitigationRequired = dto.MitigationRequired,
            MitigationNotes = dto.MitigationNotes,
            DutyOfCareBriefingSent = dto.DutyOfCareBriefingSent,
            AssessedById = dto.AssessedById,
            AssessedAt = dto.AssessedById.HasValue ? DateTime.UtcNow : null,
            ValidUntil = dto.ValidUntil,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelRiskAssessment entity, UpdateStaffTravelRiskAssessmentDto dto, Guid userId)
    {
        entity.DestinationCountryId = dto.DestinationCountryId;
        entity.DestinationCity = dto.DestinationCity;
        entity.RiskLevel = dto.RiskLevel;
        entity.RiskCategory = dto.RiskCategory;
        entity.AssessmentSource = dto.AssessmentSource;
        entity.AssessmentSummary = dto.AssessmentSummary;
        entity.MitigationRequired = dto.MitigationRequired;
        entity.MitigationNotes = dto.MitigationNotes;
        entity.DutyOfCareBriefingSent = dto.DutyOfCareBriefingSent;
        entity.AssessedById = dto.AssessedById;
        entity.ValidUntil = dto.ValidUntil;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelAlert

    public static StaffTravelAlertDto ToDto(this StaffTravelAlert entity)
    {
        return new StaffTravelAlertDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            AlertType = entity.AlertType,
            Severity = entity.Severity,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            City = entity.City,
            Title = entity.Title,
            Body = entity.Body,
            Source = entity.Source,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive,
        };
    }

    public static StaffTravelAlertSummaryDto ToSummaryDto(this StaffTravelAlert entity)
    {
        return new StaffTravelAlertSummaryDto
        {
            Id = entity.Id,
            AlertType = entity.AlertType,
            Severity = entity.Severity,
            CountryName = entity.Country?.Name,
            City = entity.City,
            Title = entity.Title,
            EffectiveFrom = entity.EffectiveFrom,
            IsActive = entity.IsActive,
        };
    }

    public static StaffTravelAlert ToEntity(this CreateStaffTravelAlertDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelAlert
        {
            TenantId = tenantId,
            AlertType = dto.AlertType,
            Severity = dto.Severity,
            CountryId = dto.CountryId,
            City = dto.City,
            Title = dto.Title,
            Body = dto.Body,
            Source = dto.Source,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelAlert entity, UpdateStaffTravelAlertDto dto, Guid userId)
    {
        entity.AlertType = dto.AlertType;
        entity.Severity = dto.Severity;
        entity.CountryId = dto.CountryId;
        entity.City = dto.City;
        entity.Title = dto.Title;
        entity.Body = dto.Body;
        entity.Source = dto.Source;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<StaffTravelAlertSummaryDto> ToSummaryDtoList(this IEnumerable<StaffTravelAlert> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region StaffTravelAlertNotification

    public static StaffTravelAlertNotificationDto ToDto(this StaffTravelAlertNotification entity)
    {
        return new StaffTravelAlertNotificationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            TravelAlertId = entity.TravelAlertId,
            AlertTitle = entity.TravelAlert?.Title,
            AlertBody = entity.TravelAlert?.Body,
            Severity = entity.TravelAlert?.Severity,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            RequestNumber = entity.StaffTravelRequest?.RequestNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            NotificationSentAt = entity.NotificationSentAt,
            IsAcknowledged = entity.IsAcknowledged,
            AcknowledgedAt = entity.AcknowledgedAt,
        };
    }

    /// <param name="travellerEmployeeId">
    /// The traveller, taken from the travel request. ⚠ A PARAMETER rather than a DTO field, so a
    /// caller cannot assert who the notification is addressed to — the same treatment D-15 gave the
    /// succession document uploader. See the remarks on CreateStaffTravelAlertNotificationDto.
    /// </param>
    public static StaffTravelAlertNotification ToEntity(this CreateStaffTravelAlertNotificationDto dto, Guid tenantId, Guid userId, Guid travellerEmployeeId)
    {
        return new StaffTravelAlertNotification
        {
            TenantId = tenantId,
            TravelAlertId = dto.TravelAlertId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            EmployeeId = travellerEmployeeId,
            NotificationSentAt = dto.NotificationSentAt ?? DateTime.UtcNow,
            IsAcknowledged = false,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    #region StaffTravelInsurancePolicy

    public static StaffTravelInsurancePolicyDto ToDto(this StaffTravelInsurancePolicy entity)
    {
        return new StaffTravelInsurancePolicyDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            StaffTravelRequestId = entity.StaffTravelRequestId,
            VendorId = entity.VendorId,
            VendorName = entity.Vendor?.Name,   // Supplier.Name — Procurement owns the vendor master
            PolicyNumber = entity.PolicyNumber,
            InsuranceType = entity.InsuranceType,
            CoverageType = entity.CoverageType,
            CoverageStart = entity.CoverageStart,
            CoverageEnd = entity.CoverageEnd,
            SumInsured = entity.SumInsured,
            CurrencyCode = entity.CurrencyCode,
            Premium = entity.Premium,
            EmergencyContact = entity.EmergencyContact,
        };
    }

    public static StaffTravelInsurancePolicy ToEntity(this CreateStaffTravelInsurancePolicyDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelInsurancePolicy
        {
            TenantId = tenantId,
            StaffTravelRequestId = dto.StaffTravelRequestId,
            VendorId = dto.VendorId,
            PolicyNumber = dto.PolicyNumber,
            InsuranceType = dto.InsuranceType,
            CoverageType = dto.CoverageType,
            CoverageStart = dto.CoverageStart,
            CoverageEnd = dto.CoverageEnd,
            SumInsured = dto.SumInsured,
            CurrencyCode = dto.CurrencyCode,
            Premium = dto.Premium,
            EmergencyContact = dto.EmergencyContact,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelInsurancePolicy entity, UpdateStaffTravelInsurancePolicyDto dto, Guid userId)
    {
        entity.VendorId = dto.VendorId;
        entity.PolicyNumber = dto.PolicyNumber;
        entity.InsuranceType = dto.InsuranceType;
        entity.CoverageType = dto.CoverageType;
        entity.CoverageStart = dto.CoverageStart;
        entity.CoverageEnd = dto.CoverageEnd;
        entity.SumInsured = dto.SumInsured;
        entity.CurrencyCode = dto.CurrencyCode;
        entity.Premium = dto.Premium;
        entity.EmergencyContact = dto.EmergencyContact;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region StaffTravelHealthRequirement

    public static StaffTravelHealthRequirementDto ToDto(this StaffTravelHealthRequirement entity)
    {
        return new StaffTravelHealthRequirementDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CountryId = entity.CountryId,
            CountryName = entity.Country?.Name,
            RequirementType = entity.RequirementType,
            RequirementName = entity.RequirementName,
            IsMandatory = entity.IsMandatory,
            ValidityDays = entity.ValidityDays,
            Notes = entity.Notes,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive,
        };
    }

    public static StaffTravelHealthRequirement ToEntity(this CreateStaffTravelHealthRequirementDto dto, Guid tenantId, Guid userId)
    {
        return new StaffTravelHealthRequirement
        {
            TenantId = tenantId,
            CountryId = dto.CountryId,
            RequirementType = dto.RequirementType,
            RequirementName = dto.RequirementName,
            IsMandatory = dto.IsMandatory,
            ValidityDays = dto.ValidityDays,
            Notes = dto.Notes,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this StaffTravelHealthRequirement entity, UpdateStaffTravelHealthRequirementDto dto, Guid userId)
    {
        entity.CountryId = dto.CountryId;
        entity.RequirementType = dto.RequirementType;
        entity.RequirementName = dto.RequirementName;
        entity.IsMandatory = dto.IsMandatory;
        entity.ValidityDays = dto.ValidityDays;
        entity.Notes = dto.Notes;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // GROUP 8 — CONFIGURATION
    // ========================================================================

}
