using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS SERVICE
// ============================================================================

#region Staff Travel Booking Service

public interface IStaffTravelBookingService
{
    // Flight bookings
    Task<StaffTravelFlightBookingDto> GetFlightByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelFlightBookingSummaryDto>> GetFlightsByStatusAsync(TravelBookingStatus status, CancellationToken cancellationToken = default);
    /// <param name="actorEmployeeId">
    /// The caller's employee record, when the login has one — recorded as who asked for a policy exception. Lane 4,
    /// D-8: a breach is no longer authorised in the booking request (it was, for a caller holding
    /// <c>HR.Travel.Admin</c>); a different administrator decides it with <see cref="DecideFlightExceptionAsync"/>.
    /// </param>
    Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Authorise (or refuse, with the reason) a flight booking's policy exception — lane 4, D-8.</summary>
    Task<StaffTravelFlightBookingDto> DecideFlightExceptionAsync(Guid id, bool authorise, string? reason, Guid deciderEmployeeId, CancellationToken cancellationToken = default);

    // Flight segments
    Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelFlightSegmentDto>> GetSegmentsAsync(Guid flightBookingId, CancellationToken cancellationToken = default);
    Task<StaffTravelFlightSegmentDto> UpdateSegmentAsync(UpdateStaffTravelFlightSegmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default);

    // Hotel bookings
    Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelHotelBookingSummaryDto>> GetHotelsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    /// <param name="actorEmployeeId">See <c>CreateFlightAsync</c> — same contract.</param>
    Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>As <see cref="DecideFlightExceptionAsync"/>, for a hotel booking.</summary>
    Task<StaffTravelHotelBookingDto> DecideHotelExceptionAsync(Guid id, bool authorise, string? reason, Guid deciderEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>The policy-breach register: flight and hotel bookings with an exception, pending first (lane 4, D-8).</summary>
    Task<IEnumerable<StaffTravelBookingExceptionDto>> GetBookingExceptionsAsync(TravelBookingExceptionState? state = null, CancellationToken cancellationToken = default);

    // Ground transport
    Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteGroundTransportAsync(Guid id, Guid? actorEmployeeId = null, CancellationToken cancellationToken = default);
    /// <summary>Lane 6 (D-33, D-34): raises the driver's own travel request for a company-vehicle leg — a Draft the leg keeps.</summary>
    Task<StaffTravelGroundTransportDto> RaiseDriverRequestAsync(Guid legId, Guid tenantId, Guid createdByUserId, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    // Car rental bookings
    Task<StaffTravelCarRentalBookingDto> GetCarRentalByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelCarRentalBookingDto>> GetCarRentalsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelCarRentalBookingDto> CreateCarRentalAsync(CreateStaffTravelCarRentalBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelCarRentalBookingDto> UpdateCarRentalAsync(UpdateStaffTravelCarRentalBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A trip's bookings as its traveller reads them (lane 7, 7c1) — the caller has established the trip is theirs.</summary>
    Task<StaffTravelTravellerBookingsDto> GetTravellerBookingsAsync(Guid requestId, CancellationToken cancellationToken = default);

    // Status verbs (lane 5, D1) — the only way a booking's status moves. A create is Pending and an edit leaves the
    // status alone. `actorEmployeeId` authors a cancellation's internal note; `cancel` carries its reason and fee.
    Task<StaffTravelFlightBookingDto> MoveFlightAsync(Guid id, TravelBookingVerb verb, string? ticketNumber, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<StaffTravelHotelBookingDto> MoveHotelAsync(Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<StaffTravelGroundTransportDto> MoveGroundTransportAsync(Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<StaffTravelCarRentalBookingDto> MoveCarRentalAsync(Guid id, TravelBookingVerb verb, CancelStaffTravelBookingDto? cancel, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
}

#endregion
