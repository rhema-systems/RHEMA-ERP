using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 2: ITINERARY & LEGS
// ============================================================================

#region Staff Travel Itinerary Repository

public class StaffTravelItineraryRepository : GenericRepository<StaffTravelItinerary>, IStaffTravelItineraryRepository
{
    public StaffTravelItineraryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelItinerary>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Where(i => i.StaffTravelRequestId == requestId && !i.IsDeleted)
            .OrderByDescending(i => i.VersionNumber)
            .ToListAsync();
    }

    public async Task<StaffTravelItinerary?> GetCurrentVersionAsync(Guid requestId)
    {
        return await _dbSet
            .Include(i => i.Legs.OrderBy(l => l.SequenceOrder)).ThenInclude(l => l.Activities)
            .FirstOrDefaultAsync(i => i.StaffTravelRequestId == requestId && i.IsCurrentVersion && !i.IsDeleted);
    }

    public async Task<StaffTravelItinerary?> GetWithLegsAsync(Guid id)
    {
        return await _dbSet
            .Include(i => i.Legs.OrderBy(l => l.SequenceOrder)).ThenInclude(l => l.Activities)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    public async Task<int> GetNextVersionNumberAsync(Guid requestId)
    {
        var maxVersion = await _dbSet
            .Where(i => i.StaffTravelRequestId == requestId && !i.IsDeleted)
            .MaxAsync(i => (int?)i.VersionNumber) ?? 0;

        return maxVersion + 1;
    }
}

#endregion

#region Staff Travel Itinerary Leg Repository

public class StaffTravelItineraryLegRepository : GenericRepository<StaffTravelItineraryLeg>, IStaffTravelItineraryLegRepository
{
    public StaffTravelItineraryLegRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelItineraryLeg>> GetByItineraryIdAsync(Guid itineraryId)
    {
        return await _dbSet
            .Include(l => l.OriginCountry)
            .Include(l => l.DestinationCountry)
            .Include(l => l.Activities)
            .Where(l => l.StaffTravelItineraryId == itineraryId && !l.IsDeleted)
            .OrderBy(l => l.SequenceOrder)
            .ToListAsync();
    }

    public async Task<StaffTravelItineraryLeg?> GetWithActivitiesAsync(Guid id)
    {
        return await _dbSet
            .Include(l => l.OriginCountry)
            .Include(l => l.DestinationCountry)
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
    }
}

#endregion

#region Staff Travel Itinerary Activity Repository

public class StaffTravelItineraryActivityRepository : GenericRepository<StaffTravelItineraryActivity>, IStaffTravelItineraryActivityRepository
{
    public StaffTravelItineraryActivityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelItineraryActivity>> GetByLegIdAsync(Guid legId)
    {
        return await _dbSet
            .Where(a => a.StaffTravelItineraryLegId == legId && !a.IsDeleted)
            .OrderBy(a => a.StartDatetime)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelItineraryActivity>> GetMandatoryActivitiesAsync(Guid legId)
    {
        return await _dbSet
            .Where(a => a.StaffTravelItineraryLegId == legId && a.IsMandatory && !a.IsDeleted)
            .OrderBy(a => a.StartDatetime)
            .ToListAsync();
    }
}

#endregion
