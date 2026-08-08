using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF REQUISITION
// ============================================================================

#region Staff Requisition

public interface IStaffRequisitionRepository : IGenericRepository<StaffRequisition>
{
    /// <summary>Returns a fully-loaded requisition including all navigation properties and child collections.</summary>
    Task<StaffRequisition?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns a requisition with only its lightweight navigation properties loaded (no child collections).</summary>
    Task<StaffRequisition?> GetWithSummaryNavAsync(Guid id);

    /// <summary>Returns the requisition matching the unique requisition number.</summary>
    Task<StaffRequisition?> GetByRequisitionNumberAsync(string requisitionNumber);

    /// <summary>Returns all requisitions for a given organisation unit, ordered by request date descending.</summary>
    Task<IEnumerable<StaffRequisition>> GetByOrganizationUnitAsync(Guid organizationUnitId);

    /// <summary>Returns all requisitions for a given location, ordered by request date descending.</summary>
    Task<IEnumerable<StaffRequisition>> GetByLocationAsync(Guid locationId);

    /// <summary>Returns all requisitions for a given position, ordered by request date descending.</summary>
    Task<IEnumerable<StaffRequisition>> GetByPositionAsync(Guid positionId);

    /// <summary>Returns all requisitions filtered by status, ordered by request date descending.</summary>
    Task<IEnumerable<StaffRequisition>> GetByStatusAsync(StaffRequisitionStatus status);

    /// <summary>Returns all requisitions filtered by type, ordered by request date descending.</summary>
    Task<IEnumerable<StaffRequisition>> GetByTypeAsync(StaffRequisitionType type);

    /// <summary>Returns all requisitions raised by the specified employee.</summary>
    Task<IEnumerable<StaffRequisition>> GetByRequestedByAsync(Guid employeeId);

    /// <summary>Returns all requisitions in a submitted or under-review state awaiting action.</summary>
    Task<IEnumerable<StaffRequisition>> GetPendingReviewAsync();

    /// <summary>Returns all open (non-cancelled, non-fulfilled) requisitions.</summary>
    Task<IEnumerable<StaffRequisition>> GetOpenRequisitionsAsync();

    /// <summary>Returns all unfulfilled requisitions whose desired start date is within the given number of days.</summary>
    Task<IEnumerable<StaffRequisition>> GetUpcomingStartDateAsync(int daysAhead = 30);

    /// <summary>Returns all unfulfilled requisitions whose target fill date has passed.</summary>
    Task<IEnumerable<StaffRequisition>> GetOverdueAsync();

    /// <summary>Returns all requisitions linked to the given job vacancy.</summary>
    Task<IEnumerable<StaffRequisition>> GetByJobVacancyAsync(Guid jobVacancyId);

    /// <summary>Returns the next sequence number to use when generating a requisition number for a tenant.</summary>
    Task<int> GetNextSequenceNumberAsync(Guid tenantId);

    /// <summary>Returns a queryable with Position, OrganizationUnit, Location and RequestedBy already included — for list/paged queries that map to SummaryDto.</summary>
    IQueryable<StaffRequisition> GetSummaryQueryable();
}

#endregion

// ============================================================================
// STAFF REQUISITION COST
// ============================================================================

#region Staff Requisition Cost

public interface IStaffRequisitionCostRepository : IGenericRepository<StaffRequisitionCost>
{
    /// <summary>Returns all costs recorded against a requisition, ordered by recorded date.</summary>
    Task<IEnumerable<StaffRequisitionCost>> GetByRequisitionIdAsync(Guid requisitionId);

    /// <summary>Returns costs for a requisition filtered by category.</summary>
    Task<IEnumerable<StaffRequisitionCost>> GetByRequisitionAndCategoryAsync(Guid requisitionId, StaffRequisitionCostCategory category);

    /// <summary>Returns the total cost amount (in base currency) recorded against a requisition.</summary>
    Task<decimal> GetTotalCostAsync(Guid requisitionId);
}

#endregion

// ============================================================================
// STAFF REQUISITION ATTACHMENT
// ============================================================================

#region Staff Requisition Attachment

public interface IStaffRequisitionAttachmentRepository : IGenericRepository<StaffRequisitionAttachment>
{
    /// <summary>Returns all attachments for a requisition, ordered by upload date.</summary>
    Task<IEnumerable<StaffRequisitionAttachment>> GetByRequisitionIdAsync(Guid requisitionId);

    /// <summary>Returns all attachments uploaded by the specified employee.</summary>
    Task<IEnumerable<StaffRequisitionAttachment>> GetByUploadedByAsync(Guid employeeId);
}

#endregion

// ============================================================================
// STAFF REQUISITION COMMENT
// ============================================================================

#region Staff Requisition Comment

public interface IStaffRequisitionCommentRepository : IGenericRepository<StaffRequisitionComment>
{
    /// <summary>Returns all top-level comments (no parent) for a requisition, with their replies loaded, ordered by posted date.</summary>
    Task<IEnumerable<StaffRequisitionComment>> GetThreadedByRequisitionIdAsync(Guid requisitionId);

    /// <summary>Returns a flat list of all comments and replies for a requisition, ordered by posted date.</summary>
    Task<IEnumerable<StaffRequisitionComment>> GetAllByRequisitionIdAsync(Guid requisitionId);

    /// <summary>Returns all comments posted by the specified author across all requisitions.</summary>
    Task<IEnumerable<StaffRequisitionComment>> GetByAuthorAsync(Guid authorId);

    /// <summary>Returns all direct replies to the specified parent comment.</summary>
    Task<IEnumerable<StaffRequisitionComment>> GetRepliesAsync(Guid parentCommentId);
}

#endregion

// ============================================================================
// STAFF REQUISITION HISTORY
// ============================================================================

#region Staff Requisition History

public interface IStaffRequisitionHistoryRepository : IGenericRepository<StaffRequisitionHistory>
{
    /// <summary>Returns all history entries for a requisition, ordered from newest to oldest.</summary>
    Task<IEnumerable<StaffRequisitionHistory>> GetByRequisitionIdAsync(Guid requisitionId);

    /// <summary>Returns the most recent history entry for a requisition.</summary>
    Task<StaffRequisitionHistory?> GetLatestAsync(Guid requisitionId);
}

#endregion

