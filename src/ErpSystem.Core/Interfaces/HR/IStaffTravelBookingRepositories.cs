using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS
// ============================================================================

#region Staff Travel Flight Booking

public interface IStaffTravelFlightBookingRepository : IGenericRepository<StaffTravelFlightBooking>
{
    /// <summary>Returns all flight bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns a flight booking with its ordered segments loaded.</summary>
    Task<StaffTravelFlightBooking?> GetWithSegmentsAsync(Guid id);

    /// <summary>Returns flight bookings filtered by status.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByStatusAsync(TravelBookingStatus status);

    /// <summary>Returns flight bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelFlightBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Flight Segment

public interface IStaffTravelFlightSegmentRepository : IGenericRepository<StaffTravelFlightSegment>
{
    /// <summary>Returns the segments of a flight booking ordered by sequence.</summary>
    Task<IEnumerable<StaffTravelFlightSegment>> GetByBookingIdAsync(Guid flightBookingId);
}

#endregion

#region Staff Travel Hotel Booking

public interface IStaffTravelHotelBookingRepository : IGenericRepository<StaffTravelHotelBooking>
{
    /// <summary>Returns all hotel bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns hotel bookings filtered by status.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByStatusAsync(TravelBookingStatus status);

    /// <summary>Returns hotel bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelHotelBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Ground Transport

public interface IStaffTravelGroundTransportRepository : IGenericRepository<StaffTravelGroundTransport>
{
    /// <summary>Returns all ground transport arrangements for a request.</summary>
    Task<IEnumerable<StaffTravelGroundTransport>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns ground transport arrangements placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelGroundTransport>> GetByVendorIdAsync(Guid vendorId);
}

#endregion

#region Staff Travel Car Rental Booking

public interface IStaffTravelCarRentalBookingRepository : IGenericRepository<StaffTravelCarRentalBooking>
{
    /// <summary>Returns all car rental bookings for a request, with vendor loaded.</summary>
    Task<IEnumerable<StaffTravelCarRentalBooking>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns car rental bookings placed with the given vendor.</summary>
    Task<IEnumerable<StaffTravelCarRentalBooking>> GetByVendorIdAsync(Guid vendorId);
}

#endregion
