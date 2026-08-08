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
    Task<StaffTravelFlightBookingDto> CreateFlightAsync(CreateStaffTravelFlightBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelFlightBookingDto> UpdateFlightAsync(UpdateStaffTravelFlightBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFlightAsync(Guid id, CancellationToken cancellationToken = default);

    // Flight segments
    Task<StaffTravelFlightSegmentDto> AddSegmentAsync(CreateStaffTravelFlightSegmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelFlightSegmentDto>> GetSegmentsAsync(Guid flightBookingId, CancellationToken cancellationToken = default);
    Task<StaffTravelFlightSegmentDto> UpdateSegmentAsync(UpdateStaffTravelFlightSegmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSegmentAsync(Guid segmentId, CancellationToken cancellationToken = default);

    // Hotel bookings
    Task<StaffTravelHotelBookingDto> GetHotelByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelHotelBookingSummaryDto>> GetHotelsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelHotelBookingDto> CreateHotelAsync(CreateStaffTravelHotelBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelHotelBookingDto> UpdateHotelAsync(UpdateStaffTravelHotelBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHotelAsync(Guid id, CancellationToken cancellationToken = default);

    // Ground transport
    Task<StaffTravelGroundTransportDto> GetGroundTransportByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelGroundTransportDto>> GetGroundTransportsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelGroundTransportDto> CreateGroundTransportAsync(CreateStaffTravelGroundTransportDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelGroundTransportDto> UpdateGroundTransportAsync(UpdateStaffTravelGroundTransportDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteGroundTransportAsync(Guid id, CancellationToken cancellationToken = default);

    // Car rental bookings
    Task<StaffTravelCarRentalBookingDto> GetCarRentalByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelCarRentalBookingDto>> GetCarRentalsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelCarRentalBookingDto> CreateCarRentalAsync(CreateStaffTravelCarRentalBookingDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelCarRentalBookingDto> UpdateCarRentalAsync(UpdateStaffTravelCarRentalBookingDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteCarRentalAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
