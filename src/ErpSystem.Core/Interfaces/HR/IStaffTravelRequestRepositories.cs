using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 1: CORE TRAVEL REQUEST
// ============================================================================

#region Staff Travel Request

public interface IStaffTravelRequestRepository : IGenericRepository<StaffTravelRequest>
{
    /// <summary>Returns the request matching the unique request number, with traveller and route loaded.</summary>
    Task<StaffTravelRequest?> GetByRequestNumberAsync(string requestNumber);

    /// <summary>
    /// Returns a fully-loaded request including itineraries, bookings, approvals, expenses,
    /// advances, budget, visas, risk assessments and insurance.
    /// </summary>
    Task<StaffTravelRequest?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns all requests raised for (or on behalf of) the given traveller, newest-first.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns requests for a traveller filtered by status.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByEmployeeAndStatusAsync(Guid employeeId, StaffTravelRequestStatus status);

    /// <summary>Returns requests filtered by status, with lightweight navigation for list views.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByStatusAsync(StaffTravelRequestStatus status);

    /// <summary>Returns requests whose travel window overlaps the given date range.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByDateRangeAsync(DateOnly start, DateOnly end);

    /// <summary>Returns all requests belonging to a group travel record.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByGroupTravelIdAsync(Guid groupTravelId);

    /// <summary>Returns child (amendment/extension) requests of a parent request.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetChildRequestsAsync(Guid parentRequestId);

    /// <summary>Returns requests for an organization unit.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetByOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns requests that have been submitted and are awaiting approval.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetPendingApprovalAsync();

    /// <summary>Returns approved/in-progress requests whose travel start date falls within the given window.</summary>
    Task<IEnumerable<StaffTravelRequest>> GetUpcomingTripsAsync(int daysAhead = 30);

    /// <summary>Returns the count of requests created in a given calendar year (supports request-number generation).</summary>
    Task<int> CountByYearAsync(int year);
}

#endregion

#region Staff Group Travel

public interface IStaffGroupTravelRepository : IGenericRepository<StaffGroupTravel>
{
    /// <summary>Returns a group travel record with all member requests loaded.</summary>
    Task<StaffGroupTravel?> GetWithRequestsAsync(Guid id);

    /// <summary>Returns group travel records filtered by status.</summary>
    Task<IEnumerable<StaffGroupTravel>> GetByStatusAsync(GroupTravelStatus status);

    /// <summary>Returns group travel records led by the given employee.</summary>
    Task<IEnumerable<StaffGroupTravel>> GetByLeadEmployeeAsync(Guid leadEmployeeId);

    /// <summary>Returns group trips departing within the given window.</summary>
    Task<IEnumerable<StaffGroupTravel>> GetUpcomingAsync(int daysAhead = 60);
}

#endregion

#region Staff Travel Request Comment

public interface IStaffTravelRequestCommentRepository : IGenericRepository<StaffTravelRequestComment>
{
    /// <summary>Returns top-level comments for a request (oldest-first), with authors and replies loaded.</summary>
    Task<IEnumerable<StaffTravelRequestComment>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns the reply thread for a parent comment.</summary>
    Task<IEnumerable<StaffTravelRequestComment>> GetThreadAsync(Guid parentCommentId);

    /// <summary>Returns only comments marked visible to the traveller for a request.</summary>
    Task<IEnumerable<StaffTravelRequestComment>> GetVisibleToTravellerAsync(Guid requestId);
}

#endregion

#region Staff Travel Request Attachment

public interface IStaffTravelRequestAttachmentRepository : IGenericRepository<StaffTravelRequestAttachment>
{
    /// <summary>Returns all attachments for a request, newest-first.</summary>
    Task<IEnumerable<StaffTravelRequestAttachment>> GetByRequestIdAsync(Guid requestId);

    /// <summary>Returns attachments for a request filtered by attachment type.</summary>
    Task<IEnumerable<StaffTravelRequestAttachment>> GetByTypeAsync(Guid requestId, TravelAttachmentType attachmentType);
}

#endregion
