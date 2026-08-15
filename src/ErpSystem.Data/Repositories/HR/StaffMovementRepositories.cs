using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF MOVEMENT REPOSITORY
// ============================================================================

#region Staff Movement Repository

public class StaffMovementRepository : GenericRepository<StaffMovement>, IStaffMovementRepository
{
    public StaffMovementRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// The navigations every summary row reads (see <c>ToSummaryDto</c>), plus the tenant predicate.
    ///
    /// Applied through one helper because the reads used to carry different subsets of it — by-employee
    /// had no Employee, by-type-and-status had no NewOrganizationUnit, none of them had Promotion or
    /// Transfer — so which columns rendered blank depended on which list you happened to open. The
    /// tenant predicate was missing entirely: every read fetched all tenants' rows and the service
    /// filtered them in memory afterwards.
    /// </summary>
    private IQueryable<StaffMovement> SummaryQuery(Guid tenantId)
        => _dbSet
            .Where(m => m.TenantId == tenantId && !m.IsDeleted)
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Include(m => m.Promotion)
            .Include(m => m.Transfer);

    public async Task<StaffMovement?> GetByMovementNumberAsync(Guid tenantId, string movementNumber)
    {
        return await SummaryQuery(tenantId)
            .FirstOrDefaultAsync(m => m.MovementNumber == movementNumber);
    }

    /// <remarks>
    /// AsSplitQuery is load-bearing, not a tuning choice. Thirty-odd includes across four collections
    /// join into one result row wide enough that SQL Server refuses the plan outright — "a worktable is
    /// required, and its minimum row size exceeds the maximum allowable of 8060 bytes". Because
    /// <c>CreateAsync</c> re-reads through this method to populate its response, every attempt to raise
    /// a staff movement failed with a 500 AFTER the row had been written: the movement existed and the
    /// caller was told the request had failed.
    /// </remarks>
    public async Task<StaffMovement?> GetWithFullDetailsAsync(Guid tenantId, Guid id)
    {
        return await _dbSet
            .AsSplitQuery()
            .Where(m => m.TenantId == tenantId && !m.IsDeleted)
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.CurrentOrganizationLevel)
            .Include(m => m.CurrentLocation)
            .Include(m => m.CurrentLocationLevel)
            .Include(m => m.CurrentSupervisor)
            .Include(m => m.CurrentSalaryGrade)
            .Include(m => m.CurrentSalaryLevel)
            .Include(m => m.CurrentSalaryNotch)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Include(m => m.NewOrganizationLevel)
            .Include(m => m.NewLocation)
            .Include(m => m.NewLocationLevel)
            .Include(m => m.NewSupervisor)
            .Include(m => m.NewSalaryGrade)
            .Include(m => m.NewSalaryLevel)
            .Include(m => m.NewSalaryNotch)
            .Include(m => m.SuccessionPlan)
            .Include(m => m.BasedOnAppraisal)
            .Include(m => m.RequestedBy)
            .Include(m => m.AuthorizedBy)
            .Include(m => m.RejectedBy)
            .Include(m => m.CancelledBy)
            .Include(m => m.ReturnMovement)
            .Include(m => m.ApprovalLevels)
                .ThenInclude(al => al.Approver)
            .Include(m => m.ApprovalLevels)
                .ThenInclude(al => al.DelegatedTo)
            .Include(m => m.StatusHistory)
                .ThenInclude(sh => sh.ChangedBy)
            .Include(m => m.Attachments)
                .ThenInclude(a => a.UploadedBy)
            .Include(m => m.ChecklistItems)
                .ThenInclude(ci => ci.ResponsiblePerson)
            .Include(m => m.Promotion)
            .Include(m => m.Transfer)
            .Include(m => m.Demotion)
            .Include(m => m.Secondment)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<StaffMovement>> GetSummariesByIdsAsync(Guid tenantId, IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await SummaryQuery(tenantId)
            .Where(m => idList.Contains(m.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.EmployeeId == employeeId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByStatusAsync(Guid tenantId, StaffMovementStatus status)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.Status == status)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByTypeAsync(
        Guid tenantId, StaffMovementType type, DateTime? from = null, DateTime? to = null)
    {
        var query = SummaryQuery(tenantId).Where(m => m.MovementType == type);

        if (from.HasValue) query = query.Where(m => m.EffectiveDate >= from.Value);
        if (to.HasValue)   query = query.Where(m => m.EffectiveDate <= to.Value);

        return await query.OrderByDescending(m => m.EffectiveDate).ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByTypeAndStatusAsync(
        Guid tenantId, StaffMovementType type, StaffMovementStatus status)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.MovementType == type && m.Status == status)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByCurrentOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.CurrentOrganizationUnitId == organizationUnitId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByNewOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.NewOrganizationUnitId == organizationUnitId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    /// <remarks>
    /// "Awaiting approval" is a STATUS, not the presence of a pending row in the retired bespoke
    /// chain. This query used to ask whether the movement had an unactioned approval level, which
    /// stopped being a question about approval the moment the workflow engine took the route over:
    /// no new movement has approval levels at all, so the queue would have gone quietly empty — the
    /// same way it was empty before, for the opposite reason.
    ///
    /// The legacy include stays so a chain recorded by an earlier build still renders on the rows it
    /// belongs to; for everything since, the live step and its approver come from the workflow
    /// instance, which is what the movement page shows.
    /// </remarks>
    public async Task<IEnumerable<StaffMovement>> GetPendingApprovalAsync(Guid tenantId)
    {
        var awaitingApproval = new[]
        {
            StaffMovementStatus.Submitted,
            StaffMovementStatus.CurrentSupervisorApproval,
            StaffMovementStatus.NewSupervisorApproval,
            StaffMovementStatus.CurrentHodApproval,
            StaffMovementStatus.NewHodApproval,
            StaffMovementStatus.HrReview,
            StaffMovementStatus.ManagementApproval,
        };

        return await SummaryQuery(tenantId)
            .Include(m => m.ApprovalLevels.Where(al => al.Status == ApprovalStatus.Pending))
                .ThenInclude(al => al.Approver)
            .Where(m => awaitingApproval.Contains(m.Status))
            .OrderBy(m => m.RequestSubmissionDate)
            .ThenBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetPendingEmployeeAcceptanceAsync(Guid tenantId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.RequiresEmployeeAcceptance && m.EmployeeAccepted == null)
            .OrderBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetPendingHandoverAsync(Guid tenantId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.RequiresHandover && m.HandoverCompletionDate == null)
            .OrderBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetActiveTemporaryAssignmentsAsync(Guid tenantId)
    {
        var today = DateTime.UtcNow;
        return await SummaryQuery(tenantId)
            .Where(m => m.IsTemporary
                     && !m.ReturnProcessed
                     && (m.TemporaryEndDate == null || m.TemporaryEndDate >= today))
            .OrderBy(m => m.TemporaryEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetExpiringTemporaryAssignmentsAsync(Guid tenantId, int daysAhead = 30)
    {
        var today  = DateTime.UtcNow;
        var cutoff = today.AddDays(daysAhead);
        return await SummaryQuery(tenantId)
            .Where(m => m.IsTemporary
                     && !m.ReturnProcessed
                     && m.TemporaryEndDate != null
                     && m.TemporaryEndDate >= today
                     && m.TemporaryEndDate <= cutoff)
            .OrderBy(m => m.TemporaryEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByEffectiveDateRangeAsync(Guid tenantId, DateTime from, DateTime to)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.EffectiveDate >= from && m.EffectiveDate <= to)
            .OrderBy(m => m.EffectiveDate)
            .ThenBy(m => m.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByRequestedByAsync(Guid tenantId, Guid requestedByEmployeeId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.RequestedById == requestedByEmployeeId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetLatestMovementsForEmployeeAsync(Guid tenantId, Guid employeeId)
    {
        // GroupBy + Include cannot be translated to SQL — materialise first, then group in memory.
        var movements = await SummaryQuery(tenantId)
            .Where(m => m.EmployeeId == employeeId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();

        return movements
            .GroupBy(m => m.MovementType)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<IEnumerable<StaffMovement>> GetBySuccessionPlanAsync(Guid tenantId, Guid successionPlanId)
    {
        return await SummaryQuery(tenantId)
            .Where(m => m.SuccessionPlanId == successionPlanId)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF MOVEMENT APPROVAL LEVEL REPOSITORY
// ============================================================================

#region Staff Movement Approval Level Repository

public class StaffMovementApprovalLevelRepository
    : GenericRepository<StaffMovementApprovalLevel>, IStaffMovementApprovalLevelRepository
{
    public StaffMovementApprovalLevelRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffMovementApprovalLevel>> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(al => al.Approver)
            .Include(al => al.DelegatedTo)
            .Where(al => al.MovementId == movementId && !al.IsDeleted)
            .OrderBy(al => al.Level)
            .ToListAsync();
    }

    public async Task<StaffMovementApprovalLevel?> GetCurrentPendingLevelAsync(Guid movementId)
    {
        return await _dbSet
            .Include(al => al.Approver)
            .Include(al => al.DelegatedTo)
            .Where(al => al.MovementId == movementId
                      && al.Status == ApprovalStatus.Pending
                      && !al.IsDeleted)
            .OrderBy(al => al.Level)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<StaffMovementApprovalLevel>> GetPendingByApproverAsync(Guid approverId)
    {
        return await _dbSet
            .Include(al => al.Movement)
                .ThenInclude(m => m.Employee)
            .Include(al => al.Approver)
            .Where(al => al.ApproverId == approverId
                      && al.Status == ApprovalStatus.Pending
                      && !al.IsDeleted)
            .OrderBy(al => al.Movement.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovementApprovalLevel>> GetPendingByDelegateAsync(Guid delegatedToId)
    {
        return await _dbSet
            .Include(al => al.Movement)
                .ThenInclude(m => m.Employee)
            .Include(al => al.Approver)
            .Include(al => al.DelegatedTo)
            .Where(al => al.DelegatedToId == delegatedToId
                      && al.Status == ApprovalStatus.Pending
                      && !al.IsDeleted)
            .OrderBy(al => al.Movement.EffectiveDate)
            .ToListAsync();
    }

    /// <remarks>
    /// A movement with no approval levels is NOT "all approved" — <c>AllAsync</c> is vacuously true on
    /// an empty set, which made the old authorisation guard's "all levels must be approved" test pass
    /// on any movement whose chain had never been built, letting it be authorised with nobody's
    /// approval recorded against it. That guard is gone with the bespoke chain, but the method is
    /// kept honest for the legacy rows that still read it.
    /// </remarks>
    public async Task<bool> AllLevelsApprovedAsync(Guid movementId)
    {
        var levels = _dbSet.Where(al => al.MovementId == movementId && !al.IsDeleted);

        return await levels.AnyAsync()
            && await levels.AllAsync(al => al.Status == ApprovalStatus.Approved);
    }
}

#endregion

// ============================================================================
// STAFF MOVEMENT STATUS HISTORY REPOSITORY
// ============================================================================

#region Staff Movement Status History Repository

public class StaffMovementStatusHistoryRepository
    : GenericRepository<StaffMovementStatusHistory>, IStaffMovementStatusHistoryRepository
{
    public StaffMovementStatusHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffMovementStatusHistory>> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(sh => sh.ChangedBy)
            .Where(sh => sh.MovementId == movementId && !sh.IsDeleted)
            .OrderByDescending(sh => sh.ChangedDate)
            .ToListAsync();
    }

    public async Task<StaffMovementStatusHistory?> GetLatestAsync(Guid movementId)
    {
        return await _dbSet
            .Include(sh => sh.ChangedBy)
            .Where(sh => sh.MovementId == movementId && !sh.IsDeleted)
            .OrderByDescending(sh => sh.ChangedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<StaffMovementStatusHistory>> GetByToStatusAsync(
        StaffMovementStatus status, DateTime? from = null, DateTime? to = null)
    {
        var query = _dbSet
            .Include(sh => sh.Movement).ThenInclude(m => m.Employee)
            .Include(sh => sh.ChangedBy)
            .Where(sh => sh.ToStatus == status && !sh.IsDeleted);

        if (from.HasValue) query = query.Where(sh => sh.ChangedDate >= from.Value);
        if (to.HasValue)   query = query.Where(sh => sh.ChangedDate <= to.Value);

        return await query.OrderByDescending(sh => sh.ChangedDate).ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF MOVEMENT ATTACHMENT REPOSITORY
// ============================================================================

#region Staff Movement Attachment Repository

public class StaffMovementAttachmentRepository
    : GenericRepository<StaffMovementAttachment>, IStaffMovementAttachmentRepository
{
    public StaffMovementAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffMovementAttachment>> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.MovementId == movementId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovementAttachment>> GetByTypeAsync(
        Guid movementId, StaffMovementAttachmentType type)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.MovementId == movementId
                     && a.Type == type
                     && !a.IsDeleted)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// STAFF MOVEMENT CHECKLIST ITEM REPOSITORY
// ============================================================================

#region Staff Movement Checklist Item Repository

public class StaffMovementChecklistItemRepository
    : GenericRepository<StaffMovementChecklistItem>, IStaffMovementChecklistItemRepository
{
    public StaffMovementChecklistItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StaffMovementChecklistItem>> GetByMovementIdAsync(Guid movementId)
    {
        return await _dbSet
            .Include(ci => ci.ResponsiblePerson)
            .Where(ci => ci.MovementId == movementId && !ci.IsDeleted)
            .OrderBy(ci => ci.Category)
            .ThenBy(ci => ci.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovementChecklistItem>> GetPendingItemsAsync(Guid movementId)
    {
        return await _dbSet
            .Include(ci => ci.ResponsiblePerson)
            .Where(ci => ci.MovementId == movementId
                      && !ci.IsCompleted
                      && !ci.IsDeleted)
            .OrderBy(ci => ci.DueDate)
            .ThenBy(ci => ci.DisplayOrder)
            .ToListAsync();
    }

    // These two are the only checklist reads not scoped by a movement the service has already
    // proved ownership of, so they take the tenant themselves rather than fetching every tenant's
    // rows for the service to discard.
    public async Task<IEnumerable<StaffMovementChecklistItem>> GetOverdueItemsAsync(Guid tenantId, Guid? movementId = null)
    {
        var today = DateTime.UtcNow;
        var query = _dbSet
            .Include(ci => ci.ResponsiblePerson)
            .Include(ci => ci.Movement).ThenInclude(m => m.Employee)
            .Where(ci => ci.TenantId == tenantId
                      && !ci.IsDeleted
                      && !ci.IsCompleted
                      && ci.DueDate != null
                      && ci.DueDate < today);

        if (movementId.HasValue)
            query = query.Where(ci => ci.MovementId == movementId.Value);

        return await query.OrderBy(ci => ci.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<StaffMovementChecklistItem>> GetByResponsiblePersonAsync(Guid tenantId, Guid employeeId)
    {
        return await _dbSet
            .Include(ci => ci.Movement).ThenInclude(m => m.Employee)
            .Include(ci => ci.ResponsiblePerson)
            .Where(ci => ci.TenantId == tenantId
                      && ci.ResponsiblePersonId == employeeId
                      && !ci.IsCompleted
                      && !ci.IsDeleted)
            .OrderBy(ci => ci.DueDate)
            .ToListAsync();
    }

    public async Task<bool> AllRequiredItemsCompletedAsync(Guid movementId)
    {
        return await _dbSet
            .Where(ci => ci.MovementId == movementId
                      && ci.IsRequired
                      && !ci.IsDeleted)
            .AllAsync(ci => ci.IsCompleted);
    }
}

#endregion
