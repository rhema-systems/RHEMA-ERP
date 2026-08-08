using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 4: BOOKINGS
// ============================================================================

#region Staff Travel Flight Booking Repository

public class StaffTravelFlightBookingRepository : GenericRepository<StaffTravelFlightBooking>, IStaffTravelFlightBookingRepository
{
    public StaffTravelFlightBookingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelFlightBooking>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(f => f.Vendor)
            .Include(f => f.Segments)
            .Where(f => f.StaffTravelRequestId == requestId && !f.IsDeleted)
            .OrderBy(f => f.BookedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelFlightBooking?> GetWithSegmentsAsync(Guid id)
    {
        return await _dbSet
            .Include(f => f.Vendor)
            .Include(f => f.Segments.OrderBy(s => s.SegmentOrder))
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelFlightBooking>> GetByStatusAsync(TravelBookingStatus status)
    {
        return await _dbSet
            .Include(f => f.Vendor)
            .Where(f => f.Status == status && !f.IsDeleted)
            .OrderByDescending(f => f.BookedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelFlightBooking>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Where(f => f.VendorId == vendorId && !f.IsDeleted)
            .OrderByDescending(f => f.BookedAt)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Flight Segment Repository

public class StaffTravelFlightSegmentRepository : GenericRepository<StaffTravelFlightSegment>, IStaffTravelFlightSegmentRepository
{
    public StaffTravelFlightSegmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelFlightSegment>> GetByBookingIdAsync(Guid flightBookingId)
    {
        return await _dbSet
            .Where(s => s.StaffTravelFlightBookingId == flightBookingId && !s.IsDeleted)
            .OrderBy(s => s.SegmentOrder)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Hotel Booking Repository

public class StaffTravelHotelBookingRepository : GenericRepository<StaffTravelHotelBooking>, IStaffTravelHotelBookingRepository
{
    public StaffTravelHotelBookingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelHotelBooking>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(h => h.Vendor)
            .Include(h => h.Country)
            .Where(h => h.StaffTravelRequestId == requestId && !h.IsDeleted)
            .OrderBy(h => h.CheckInDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelHotelBooking>> GetByStatusAsync(TravelBookingStatus status)
    {
        return await _dbSet
            .Include(h => h.Vendor)
            .Where(h => h.Status == status && !h.IsDeleted)
            .OrderByDescending(h => h.CheckInDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelHotelBooking>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Where(h => h.VendorId == vendorId && !h.IsDeleted)
            .OrderByDescending(h => h.CheckInDate)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Ground Transport Repository

public class StaffTravelGroundTransportRepository : GenericRepository<StaffTravelGroundTransport>, IStaffTravelGroundTransportRepository
{
    public StaffTravelGroundTransportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelGroundTransport>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(g => g.Vendor)
            .Where(g => g.StaffTravelRequestId == requestId && !g.IsDeleted)
            .OrderBy(g => g.PickupDatetime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelGroundTransport>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Where(g => g.VendorId == vendorId && !g.IsDeleted)
            .OrderByDescending(g => g.PickupDatetime)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Car Rental Booking Repository

public class StaffTravelCarRentalBookingRepository : GenericRepository<StaffTravelCarRentalBooking>, IStaffTravelCarRentalBookingRepository
{
    public StaffTravelCarRentalBookingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelCarRentalBooking>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(c => c.Vendor)
            .Where(c => c.StaffTravelRequestId == requestId && !c.IsDeleted)
            .OrderBy(c => c.PickupDatetime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelCarRentalBooking>> GetByVendorIdAsync(Guid vendorId)
    {
        return await _dbSet
            .Where(c => c.VendorId == vendorId && !c.IsDeleted)
            .OrderByDescending(c => c.PickupDatetime)
            .ToListAsync();
    }
}

#endregion
