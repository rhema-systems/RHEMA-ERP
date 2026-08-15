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

    public async Task<StaffMovement?> GetByMovementNumberAsync(string movementNumber)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .FirstOrDefaultAsync(m => m.MovementNumber == movementNumber && !m.IsDeleted);
    }

    /// <remarks>
    /// AsSplitQuery is load-bearing, not a tuning choice. Thirty-odd includes across four collections
    /// join into one result row wide enough that SQL Server refuses the plan outright — "a worktable is
    /// required, and its minimum row size exceeds the maximum allowable of 8060 bytes". Because
    /// <c>CreateAsync</c> re-reads through this method to populate its response, every attempt to raise
    /// a staff movement failed with a 500 AFTER the row had been written: the movement existed and the
    /// caller was told the request had failed.
    /// </remarks>
    public async Task<StaffMovement?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .AsSplitQuery()
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
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
    }

    public async Task<IEnumerable<StaffMovement>> GetSummariesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
            return [];

        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Include(m => m.Promotion)
            .Include(m => m.Transfer)
            .Where(m => idList.Contains(m.Id) && !m.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByEmployeeAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByStatusAsync(StaffMovementStatus status)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => m.Status == status && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByTypeAsync(
        StaffMovementType type, DateTime? from = null, DateTime? to = null)
    {
        var query = _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => m.MovementType == type && !m.IsDeleted);

        if (from.HasValue) query = query.Where(m => m.EffectiveDate >= from.Value);
        if (to.HasValue)   query = query.Where(m => m.EffectiveDate <= to.Value);

        return await query.OrderByDescending(m => m.EffectiveDate).ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByTypeAndStatusAsync(
        StaffMovementType type, StaffMovementStatus status)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Where(m => m.MovementType == type && m.Status == status && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByCurrentOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Where(m => m.CurrentOrganizationUnitId == organizationUnitId && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByNewOrganizationUnitAsync(Guid organizationUnitId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => m.NewOrganizationUnitId == organizationUnitId && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.ApprovalLevels.Where(al => al.Status == ApprovalStatus.Pending))
                .ThenInclude(al => al.Approver)
            .Where(m => !m.IsDeleted
                     && m.ApprovalLevels.Any(al => al.Status == ApprovalStatus.Pending))
            .OrderBy(m => m.RequestSubmissionDate)
            .ThenBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetPendingEmployeeAcceptanceAsync()
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => !m.IsDeleted
                     && m.RequiresEmployeeAcceptance
                     && m.EmployeeAccepted == null)
            .OrderBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetPendingHandoverAsync()
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Where(m => !m.IsDeleted
                     && m.RequiresHandover
                     && m.HandoverCompletionDate == null)
            .OrderBy(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetActiveTemporaryAssignmentsAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => !m.IsDeleted
                     && m.IsTemporary
                     && !m.ReturnProcessed
                     && (m.TemporaryEndDate == null || m.TemporaryEndDate >= today))
            .OrderBy(m => m.TemporaryEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetExpiringTemporaryAssignmentsAsync(int daysAhead = 30)
    {
        var today  = DateTime.UtcNow;
        var cutoff = today.AddDays(daysAhead);
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => !m.IsDeleted
                     && m.IsTemporary
                     && !m.ReturnProcessed
                     && m.TemporaryEndDate != null
                     && m.TemporaryEndDate >= today
                     && m.TemporaryEndDate <= cutoff)
            .OrderBy(m => m.TemporaryEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByEffectiveDateRangeAsync(DateTime from, DateTime to)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => !m.IsDeleted
                     && m.EffectiveDate >= from
                     && m.EffectiveDate <= to)
            .OrderBy(m => m.EffectiveDate)
            .ThenBy(m => m.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetByRequestedByAsync(Guid requestedByEmployeeId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.NewPosition)
            .Where(m => m.RequestedById == requestedByEmployeeId && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StaffMovement>> GetLatestMovementsForEmployeeAsync(Guid employeeId)
    {
        // GroupBy + Include cannot be translated to SQL — materialise first, then group in memory.
        var movements = await _dbSet
            .Include(m => m.CurrentPosition)
            .Include(m => m.CurrentOrganizationUnit)
            .Include(m => m.NewPosition)
            .Include(m => m.NewOrganizationUnit)
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted)
            .OrderByDescending(m => m.EffectiveDate)
            .ToListAsync();

        return movements
            .GroupBy(m => m.MovementType)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<IEnumerable<StaffMovement>> GetBySuccessionPlanAsync(Guid successionPlanId)
    {
        return await _dbSet
            .Include(m => m.Employee)
            .Include(m => m.CurrentPosition)
            .Include(m => m.NewPosition)
            .Where(m => m.SuccessionPlanId == successionPlanId && !m.IsDeleted)
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
    /// an empty set, which made <c>AuthorizeAsync</c>'s "all levels must be approved" guard pass on any
    /// movement whose chain had never been built. That let a submitted movement be authorised with
    /// nobody's approval recorded against it.
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

    public async Task<IEnumerable<StaffMovementChecklistItem>> GetOverdueItemsAsync(Guid? movementId = null)
    {
        var today = DateTime.UtcNow;
        var query = _dbSet
            .Include(ci => ci.ResponsiblePerson)
            .Include(ci => ci.Movement).ThenInclude(m => m.Employee)
            .Where(ci => !ci.IsDeleted
                      && !ci.IsCompleted
                      && ci.DueDate != null
                      && ci.DueDate < today);

        if (movementId.HasValue)
            query = query.Where(ci => ci.MovementId == movementId.Value);

        return await query.OrderBy(ci => ci.DueDate).ToListAsync();
    }

    public async Task<IEnumerable<StaffMovementChecklistItem>> GetByResponsiblePersonAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(ci => ci.Movement).ThenInclude(m => m.Employee)
            .Include(ci => ci.ResponsiblePerson)
            .Where(ci => ci.ResponsiblePersonId == employeeId
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
