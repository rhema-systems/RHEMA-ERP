using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 1: CORE TRAVEL REQUEST
// ============================================================================

#region Staff Travel Request Repository

public class StaffTravelRequestRepository : GenericRepository<StaffTravelRequest>, IStaffTravelRequestRepository
{
    public StaffTravelRequestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffTravelRequest?> GetByRequestNumberAsync(string requestNumber)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Include(r => r.OriginCountry)
            .FirstOrDefaultAsync(r => r.RequestNumber == requestNumber && !r.IsDeleted);
    }

    public async Task<StaffTravelRequest?> GetWithFullDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .AsSplitQuery()
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Employee)
            .Include(r => r.InitiatedBy)
            .Include(r => r.CancelledBy)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.DestinationCountry)
            .Include(r => r.OriginCountry)
            .Include(r => r.Policy)
            .Include(r => r.GroupTravel)
            .Include(r => r.ParentRequest)
            .Include(r => r.Budget).ThenInclude(b => b!.ApprovedBy)
            .Include(r => r.Comments).ThenInclude(c => c.Author)
            .Include(r => r.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(r => r.Itineraries).ThenInclude(i => i.Legs).ThenInclude(l => l.Activities)
            .Include(r => r.ApprovalInstances).ThenInclude(a => a.Decisions).ThenInclude(d => d.Approver)
            .Include(r => r.FlightBookings).ThenInclude(f => f.Segments)
            .Include(r => r.FlightBookings).ThenInclude(f => f.Vendor)
            .Include(r => r.HotelBookings).ThenInclude(h => h.Vendor)
            .Include(r => r.GroundTransports).ThenInclude(g => g.Vendor)
            .Include(r => r.CarRentalBookings).ThenInclude(c => c.Vendor)
            .Include(r => r.ExpenseClaims).ThenInclude(c => c.Lines)
            .Include(r => r.Advances)
            .Include(r => r.PolicyExceptions).ThenInclude(e => e.PolicyRule)
            .Include(r => r.VisaApplications).ThenInclude(v => v.DestinationCountry)
            .Include(r => r.RiskAssessments).ThenInclude(a => a.DestinationCountry)
            .Include(r => r.InsurancePolicies).ThenInclude(i => i.Vendor)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.EmployeeId == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByEmployeeAndStatusAsync(Guid employeeId, StaffTravelRequestStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.EmployeeId == employeeId && r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByStatusAsync(StaffTravelRequestStatus status)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByDateRangeAsync(DateOnly start, DateOnly end)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => !r.IsDeleted && r.TravelStartDate <= end && r.TravelEndDate >= start)
            .OrderBy(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByGroupTravelIdAsync(Guid groupTravelId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.GroupTravelId == groupTravelId && !r.IsDeleted)
            .OrderBy(r => r.Employee.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetChildRequestsAsync(Guid parentRequestId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.ParentRequestId == parentRequestId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetByOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.OrganizationUnitId == organizationUnitId && !r.IsDeleted)
            .OrderByDescending(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => r.Status == StaffTravelRequestStatus.Submitted && !r.IsDeleted)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequest>> GetUpcomingTripsAsync(int daysAhead = 30)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .Where(r => !r.IsDeleted
                     && (r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress)
                     && r.TravelStartDate >= today && r.TravelStartDate <= cutoff)
            .OrderBy(r => r.TravelStartDate)
            .ToListAsync();
    }

    public async Task<int> CountByYearAsync(int year)
    {
        return await _dbSet.CountAsync(r => r.CreatedAt.Year == year);
    }
}

#endregion

#region Staff Group Travel Repository

public class StaffGroupTravelRepository : GenericRepository<StaffGroupTravel>, IStaffGroupTravelRepository
{
    public StaffGroupTravelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffGroupTravel?> GetWithRequestsAsync(Guid id)
    {
        return await _dbSet
            .Include(g => g.LeadEmployee)
            .Include(g => g.DestinationCountry)
            .Include(g => g.Requests).ThenInclude(r => r.Employee)
            .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);
    }

    public async Task<IEnumerable<StaffGroupTravel>> GetByStatusAsync(GroupTravelStatus status)
    {
        return await _dbSet
            .Include(g => g.LeadEmployee)
            .Include(g => g.DestinationCountry)
            .Where(g => g.Status == status && !g.IsDeleted)
            .OrderByDescending(g => g.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffGroupTravel>> GetByLeadEmployeeAsync(Guid leadEmployeeId)
    {
        return await _dbSet
            .Include(g => g.DestinationCountry)
            .Where(g => g.LeadEmployeeId == leadEmployeeId && !g.IsDeleted)
            .OrderByDescending(g => g.TravelStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffGroupTravel>> GetUpcomingAsync(int daysAhead = 60)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(g => g.LeadEmployee)
            .Include(g => g.DestinationCountry)
            .Where(g => !g.IsDeleted && g.TravelStartDate >= today && g.TravelStartDate <= cutoff)
            .OrderBy(g => g.TravelStartDate)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Request Comment Repository

public class StaffTravelRequestCommentRepository : GenericRepository<StaffTravelRequestComment>, IStaffTravelRequestCommentRepository
{
    public StaffTravelRequestCommentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelRequestComment>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Include(c => c.Replies).ThenInclude(r => r.Author)
            .Where(c => c.StaffTravelRequestId == requestId && c.ParentCommentId == null && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelRequestComment?> GetWithAuthorAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Include(c => c.Replies).ThenInclude(r => r.Author)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelRequestComment>> GetThreadAsync(Guid parentCommentId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.ParentCommentId == parentCommentId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffTravelRequestComment>> GetVisibleToTravellerAsync(Guid requestId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.StaffTravelRequestId == requestId && c.IsVisibleToTraveller && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }
}

#endregion

#region Staff Travel Request Attachment Repository

public class StaffTravelRequestAttachmentRepository : GenericRepository<StaffTravelRequestAttachment>, IStaffTravelRequestAttachmentRepository
{
    public StaffTravelRequestAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffTravelRequestAttachment>> GetByRequestIdAsync(Guid requestId)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.StaffTravelRequestId == requestId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }

    public async Task<StaffTravelRequestAttachment?> GetWithUploaderAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && !a.IsDeleted);
    }

    public async Task<IEnumerable<StaffTravelRequestAttachment>> GetByTypeAsync(Guid requestId, TravelAttachmentType attachmentType)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.StaffTravelRequestId == requestId && a.AttachmentType == attachmentType && !a.IsDeleted)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }
}

#endregion
