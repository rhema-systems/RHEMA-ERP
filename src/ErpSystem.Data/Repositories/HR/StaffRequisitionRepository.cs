using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF REQUISITION REPOSITORY
// ============================================================================

#region Staff Requisition Repository

public class StaffRequisitionRepository : GenericRepository<StaffRequisition>, IStaffRequisitionRepository
{
    private readonly INumberSequenceService _sequences;

    public StaffRequisitionRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    public async Task<StaffRequisition?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.JobDescription)
            .Include(r => r.LocationLevel)
            .Include(r => r.Location)
            .Include(r => r.OrganizationLevel)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.ReplacementForEmployee)
            .Include(r => r.RequestedBy)
            .Include(r => r.CancelledBy)
            .Include(r => r.JobVacancy)
            .Include(r => r.Costs).ThenInclude(c => c.RecordedBy)
            .Include(r => r.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(r => r.Comments).ThenInclude(c => c.Author)
            .Include(r => r.Comments).ThenInclude(c => c.Replies).ThenInclude(r => r.Author)
            .Include(r => r.History).ThenInclude(h => h.ChangedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    /// <summary>
    /// Backs <c>GET /{id}</c> and every write response, so it must cover everything
    /// <c>StaffRequisitionDto</c> reads through a navigation — not just the columns the list shows.
    ///
    /// <para><c>JobDescription</c> and <c>JobVacancy</c> were missing, so <c>jobDescriptionTitle</c>
    /// and <c>jobVacancyNumber</c> came back null on every single read even when both were set. The
    /// second one matters most: <c>link-vacancy</c> exists to tie a requisition to its vacancy, and
    /// the detail screen could never show that it had worked.</para>
    ///
    /// <para>Round 3 (demo feedback, lane Q): the same shape a second time. <c>LocationLevel</c>,
    /// <c>OrganizationLevel</c>, <c>ReplacementForEmployee</c> and <c>CancelledBy</c> were still
    /// absent, so the replacement's name and the whole cancellation block on the detail screen had
    /// never once rendered. The list below is now the DTO's navigation list, in the DTO's order —
    /// when a navigation is added to <c>StaffRequisitionDto</c>, add it here in the same commit.</para>
    /// </summary>
    private IQueryable<StaffRequisition> WithSummaryNavigations() =>
        _dbSet
            .Include(r => r.LocationLevel)
            .Include(r => r.Location)
            .Include(r => r.OrganizationLevel)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Position)
            .Include(r => r.JobDescription)
            .Include(r => r.ReplacementForEmployee)
            .Include(r => r.RequestedBy)
            .Include(r => r.CancelledBy)
            .Include(r => r.JobVacancy);

    public async Task<StaffRequisition?> GetWithSummaryNavAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<StaffRequisition?> GetByRequisitionNumberAsync(string requisitionNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(r => r.RequisitionNumber == requisitionNumber && !r.IsDeleted);
    }

    public async Task<IEnumerable<StaffRequisition>> GetByOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Where(r => r.OrganizationUnitId == organizationUnitId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByLocationAsync(Guid locationId)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Where(r => r.LocationId == locationId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => r.PositionId == positionId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByStatusAsync(StaffRequisitionStatus status)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByTypeAsync(StaffRequisitionType type)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => r.Type == type && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByRequestedByAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => r.RequestedById == employeeId && !r.IsDeleted)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetPendingReviewAsync()
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Where(r => !r.IsDeleted &&
                        (r.Status == StaffRequisitionStatus.Submitted ||
                         r.Status == StaffRequisitionStatus.UnderReview))
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetOpenRequisitionsAsync()
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Where(r => !r.IsDeleted &&
                        r.Status != StaffRequisitionStatus.Cancelled &&
                        r.Status != StaffRequisitionStatus.Fulfilled)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.DesiredStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetUpcomingStartDateAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => !r.IsDeleted &&
                        !r.IsFulfilled &&
                        r.Status != StaffRequisitionStatus.Cancelled &&
                        r.DesiredStartDate <= cutoff)
            .OrderBy(r => r.DesiredStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetOverdueAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => !r.IsDeleted &&
                        !r.IsFulfilled &&
                        r.Status != StaffRequisitionStatus.Cancelled &&
                        r.TargetFillDate != null &&
                        r.TargetFillDate < now)
            .OrderBy(r => r.TargetFillDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisition>> GetByJobVacancyAsync(Guid jobVacancyId)
    {
        return await _dbSet
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.RequestedBy)
            .Where(r => r.JobVacancyId == jobVacancyId && !r.IsDeleted)
            .ToListAsync();
    }

    /// <summary>
    /// Next per-year sequence value for the tenant's requisition numbers (printed as
    /// <c>REQ-{year}-{seq:D5}</c>).
    ///
    /// <para>Previously this was <c>CountAsync(...) + 1</c>, which had three failure modes: two
    /// concurrent creates produced the same number; the count spanned all years so the sequence never
    /// reset in January; and deleting any requisition made the next create <i>reuse</i> an existing
    /// number. It is now served by the atomic per-tenant/per-year sequence table.</para>
    /// </summary>
    public async Task<int> GetNextSequenceNumberAsync(Guid tenantId)
    {
        // The caller prints REQ-{year}-{seq:D5}; the probe has to assemble the same string to ask
        // whether the register already holds it. See NumberSequenceExtensions for why it asks at all.
        var year = DateTime.UtcNow.Year;
        var prefix = $"REQ-{year}-";

        // The counter resets each January, so the high-water mark is this year's numbers only —
        // last year's REQ-2025-00312 must not push 2026 up to 312.
        var number = await _sequences.NextUnusedAsync(
            "REQ",
            tenantId,
            year,
            format: value => $"{prefix}{value:D5}",
            isTaken: candidate => _dbSet.IgnoreQueryFilters()
                .AnyAsync(r => r.TenantId == tenantId && r.RequisitionNumber == candidate),
            highestIssued: async () => NumberSequenceExtensions.HighestIssued(
                await _dbSet.IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId && r.RequisitionNumber.StartsWith(prefix))
                    .Select(r => r.RequisitionNumber)
                    .ToListAsync()));

        return int.Parse(number[prefix.Length..]);
    }

    public IQueryable<StaffRequisition> GetSummaryQueryable()
    {
        return _dbSet
            .Where(r => !r.IsDeleted)
            .Include(r => r.Position)
            .Include(r => r.OrganizationUnit)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy);
    }
}

#endregion

// ============================================================================
// STAFF REQUISITION COST REPOSITORY
// ============================================================================

#region Staff Requisition Cost Repository

public class StaffRequisitionCostRepository : GenericRepository<StaffRequisitionCost>, IStaffRequisitionCostRepository
{
    public StaffRequisitionCostRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffRequisitionCost>> GetByRequisitionIdAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(c => c.RecordedBy).Include(c => c.Supplier).Include(c => c.ApprovedBy)
            .Where(c => c.RequisitionId == requisitionId && !c.IsDeleted)
            .OrderBy(c => c.RecordedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisitionCost>> GetByRequisitionAndCategoryAsync(
        Guid requisitionId, StaffRequisitionCostCategory category)
    {
        return await _dbSet
            .Include(c => c.RecordedBy)
            .Where(c => c.RequisitionId == requisitionId &&
                        c.Category == category &&
                        !c.IsDeleted)
            .OrderBy(c => c.RecordedDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalCostAsync(Guid requisitionId)
    {
        return await _dbSet
            .Where(c => c.RequisitionId == requisitionId && !c.IsDeleted)
            .SumAsync(c => c.Amount * c.ExchangeRate);
    }
}

#endregion

// ============================================================================
// STAFF REQUISITION ATTACHMENT REPOSITORY
// ============================================================================

#region Staff Requisition Attachment Repository

public class StaffRequisitionAttachmentRepository : GenericRepository<StaffRequisitionAttachment>, IStaffRequisitionAttachmentRepository
{
    public StaffRequisitionAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffRequisitionAttachment>> GetByRequisitionIdAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.RequisitionId == requisitionId && !a.IsDeleted)
            .OrderBy(a => a.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisitionAttachment>> GetByUploadedByAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(a => a.Requisition)
            .Include(a => a.UploadedBy)
            .Where(a => a.UploadedById == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF REQUISITION COMMENT REPOSITORY
// ============================================================================

#region Staff Requisition Comment Repository

public class StaffRequisitionCommentRepository : GenericRepository<StaffRequisitionComment>, IStaffRequisitionCommentRepository
{
    public StaffRequisitionCommentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffRequisitionComment>> GetThreadedByRequisitionIdAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Include(c => c.Replies).ThenInclude(r => r.Author)
            .Where(c => c.RequisitionId == requisitionId &&
                        c.ParentCommentId == null &&
                        !c.IsDeleted)
            .OrderBy(c => c.PostedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisitionComment>> GetAllByRequisitionIdAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.RequisitionId == requisitionId && !c.IsDeleted)
            .OrderBy(c => c.PostedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisitionComment>> GetByAuthorAsync(Guid authorId)
    {
        return await _dbSet
            .Include(c => c.Requisition)
            .Include(c => c.Author)
            .Where(c => c.AuthorId == authorId && !c.IsDeleted)
            .OrderByDescending(c => c.PostedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffRequisitionComment>> GetRepliesAsync(Guid parentCommentId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.ParentCommentId == parentCommentId && !c.IsDeleted)
            .OrderBy(c => c.PostedDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF REQUISITION HISTORY REPOSITORY
// ============================================================================

#region Staff Requisition History Repository

public class StaffRequisitionHistoryRepository : GenericRepository<StaffRequisitionHistory>, IStaffRequisitionHistoryRepository
{
    public StaffRequisitionHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffRequisitionHistory>> GetByRequisitionIdAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(h => h.ChangedBy)
            .Where(h => h.RequisitionId == requisitionId && !h.IsDeleted)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();
    }

    public async Task<StaffRequisitionHistory?> GetLatestAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(h => h.ChangedBy)
            .Where(h => h.RequisitionId == requisitionId && !h.IsDeleted)
            .OrderByDescending(h => h.ActionDate)
            .FirstOrDefaultAsync();
    }
}

#endregion


