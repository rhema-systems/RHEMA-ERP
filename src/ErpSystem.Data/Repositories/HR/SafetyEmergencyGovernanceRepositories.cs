using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Emergency (M), Regulatory (N), Signage (O), KPI (P),
// Committee & Meetings (Q) and Return-to-Work (R).
// ============================================================================

#region Emergency

public class EmergencyPlanRepository : GenericRepository<EmergencyPlan>, IEmergencyPlanRepository
{
    public EmergencyPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EmergencyPlan?> GetByNumberAsync(string planNumber) =>
        await _dbSet.Include(p => p.Location).Include(p => p.PlanOwner)
            .FirstOrDefaultAsync(p => p.PlanNumber == planNumber && !p.IsDeleted);

    // AsSplitQuery: four collection includes is the 8060-byte single-query shape that 500'd
    // incident detail reads once children existed. Drill Location/Department are included
    // because the drill mapper reads them — blank names otherwise.
    public async Task<EmergencyPlan?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(p => p.Location)
            .Include(p => p.PlanOwner)
            .Include(p => p.AssemblyPoints).ThenInclude(a => a.Location)
            .Include(p => p.EmergencyContacts)
            .Include(p => p.Drills).ThenInclude(d => d.Coordinator)
            .Include(p => p.Drills).ThenInclude(d => d.Location)
            .Include(p => p.Drills).ThenInclude(d => d.Department)
            .Include(p => p.TeamMembers).ThenInclude(t => t.Employee)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<EmergencyPlan>> GetAllSummaryAsync() =>
        await _dbSet.Include(p => p.Location).Include(p => p.PlanOwner)
            .Where(p => !p.IsDeleted).OrderBy(p => p.PlanName).ToListAsync();

    public async Task<IEnumerable<EmergencyPlan>> GetByTypeAsync(SheEmergencyType type) =>
        await _dbSet.Include(p => p.Location).Include(p => p.PlanOwner)
            .Where(p => p.Type == type && !p.IsDeleted).OrderBy(p => p.PlanName).ToListAsync();

    public async Task<IEnumerable<EmergencyPlan>> GetActiveAsync() =>
        await _dbSet.Include(p => p.Location).Include(p => p.PlanOwner)
            .Where(p => p.IsActive && !p.IsDeleted).OrderBy(p => p.PlanName).ToListAsync();

    public async Task<IEnumerable<EmergencyPlan>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(p => p.Location).Include(p => p.PlanOwner)
            .Where(p => p.IsActive && !p.IsDeleted && p.NextReviewDate <= cutoff)
            .OrderBy(p => p.NextReviewDate).ToListAsync();
    }
}

public class EmergencyDrillRepository : GenericRepository<EmergencyDrill>, IEmergencyDrillRepository
{
    public EmergencyDrillRepository(ApplicationDbContext context) : base(context) { }

    // Location and Department are included because the drill mapper reads them — without
    // them every list read mapped blank location/department names.
    private IQueryable<EmergencyDrill> WithNavigations() =>
        _dbSet.Include(d => d.Coordinator).Include(d => d.Location).Include(d => d.Department);

    public async Task<EmergencyDrill?> GetByNumberAsync(string drillNumber) =>
        await WithNavigations()
            .FirstOrDefaultAsync(d => d.DrillNumber == drillNumber && !d.IsDeleted);

    public async Task<IEnumerable<EmergencyDrill>> GetByPlanIdAsync(Guid planId) =>
        await WithNavigations()
            .Where(d => d.EmergencyPlanId == planId && !d.IsDeleted)
            .OrderByDescending(d => d.DrillDate).ToListAsync();

    public async Task<IEnumerable<EmergencyDrill>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithNavigations()
            .Where(d => d.DrillDate >= fromDate && d.DrillDate <= toDate && !d.IsDeleted)
            .OrderByDescending(d => d.DrillDate).ToListAsync();

    public async Task<IEnumerable<EmergencyDrill>> GetUpcomingAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithNavigations()
            .Where(d => !d.IsDeleted && d.NextDrillScheduledDate != null
                     && d.NextDrillScheduledDate >= now && d.NextDrillScheduledDate <= cutoff)
            .OrderBy(d => d.NextDrillScheduledDate).ToListAsync();
    }
}

public class EmergencyResponseTeamRepository : GenericRepository<EmergencyResponseTeam>, IEmergencyResponseTeamRepository
{
    public EmergencyResponseTeamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EmergencyResponseTeam>> GetByPlanIdAsync(Guid planId) =>
        await _dbSet.Include(t => t.Employee)
            .Where(t => t.EmergencyPlanId == planId && !t.IsDeleted).ToListAsync();

    public async Task<IEnumerable<EmergencyResponseTeam>> GetByEmployeeAsync(Guid employeeId) =>
        await _dbSet.Include(t => t.EmergencyPlan)
            .Where(t => t.EmployeeId == employeeId && !t.IsDeleted).ToListAsync();

    public async Task<IEnumerable<EmergencyResponseTeam>> GetExpiringCertificatesAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(t => t.Employee).Include(t => t.EmergencyPlan)
            .Where(t => t.IsActive && !t.IsDeleted && t.CertificateExpiryDate != null && t.CertificateExpiryDate <= cutoff)
            .OrderBy(t => t.CertificateExpiryDate).ToListAsync();
    }
}

#endregion

#region Regulatory

public class SheRegulatoryObligationRepository : GenericRepository<SheRegulatoryObligation>, ISheRegulatoryObligationRepository
{
    public SheRegulatoryObligationRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheRegulatoryObligation> WithListNavigations() =>
        _dbSet.Include(o => o.RegulatoryBody).Include(o => o.ObligationOwner);

    public async Task<SheRegulatoryObligation?> GetByCodeAsync(string obligationCode) =>
        await WithListNavigations().FirstOrDefaultAsync(o => o.ObligationCode == obligationCode && !o.IsDeleted);

    public async Task<SheRegulatoryObligation?> GetWithEvidenceAsync(Guid id) =>
        await _dbSet
            .Include(o => o.RegulatoryBody)
            .Include(o => o.ObligationOwner)
            .Include(o => o.EvidenceRecords).ThenInclude(e => e.RecordedBy)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

    public async Task<IEnumerable<SheRegulatoryObligation>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(o => !o.IsDeleted)
            .OrderBy(o => o.Title).ToListAsync();

    public async Task<IEnumerable<SheRegulatoryObligation>> GetByDomainAsync(SheRegulatoryDomain domain) =>
        await WithListNavigations().Where(o => o.Domain == domain && !o.IsDeleted)
            .OrderBy(o => o.Title).ToListAsync();

    public async Task<IEnumerable<SheRegulatoryObligation>> GetByComplianceStatusAsync(SheComplianceStatus status) =>
        await WithListNavigations().Where(o => o.ComplianceStatus == status && !o.IsDeleted)
            .OrderBy(o => o.Title).ToListAsync();

    public async Task<IEnumerable<SheRegulatoryObligation>> GetByOwnerAsync(Guid ownerId) =>
        await WithListNavigations().Where(o => o.ObligationOwnerId == ownerId && !o.IsDeleted)
            .OrderBy(o => o.Title).ToListAsync();

    public async Task<IEnumerable<SheRegulatoryObligation>> GetDueForReviewAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(o => o.IsActive && !o.IsDeleted && o.NextReviewDate != null && o.NextReviewDate <= cutoff)
            .OrderBy(o => o.NextReviewDate).ToListAsync();
    }

    public async Task<IEnumerable<SheRegulatoryObligation>> GetNonCompliantAsync() =>
        await WithListNavigations()
            .Where(o => o.IsActive && !o.IsDeleted
                     && (o.ComplianceStatus == SheComplianceStatus.NonCompliant
                      || o.ComplianceStatus == SheComplianceStatus.PartiallyCompliant))
            .OrderBy(o => o.Title).ToListAsync();
}

#endregion

#region Signage

public class SafetySignRepository : GenericRepository<SafetySign>, ISafetySignRepository
{
    public SafetySignRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SafetySign?> GetByCodeAsync(string signCode) =>
        await _dbSet.Include(s => s.Location).FirstOrDefaultAsync(s => s.SignCode == signCode && !s.IsDeleted);

    public async Task<IEnumerable<SafetySign>> GetByLocationAsync(Guid locationId) =>
        await _dbSet.Include(s => s.Location)
            .Where(s => s.LocationId == locationId && !s.IsDeleted).OrderBy(s => s.SignCode).ToListAsync();

    public async Task<IEnumerable<SafetySign>> GetByTypeAsync(SheSafetySignType type) =>
        await _dbSet.Include(s => s.Location).Where(s => s.SignType == type && !s.IsDeleted)
            .OrderBy(s => s.SignCode).ToListAsync();

    public async Task<IEnumerable<SafetySign>> GetAllSummaryAsync() =>
        await _dbSet.Include(s => s.Location).Where(s => !s.IsDeleted)
            .OrderBy(s => s.SignCode).ToListAsync();

    public async Task<IEnumerable<SafetySign>> GetByStatusAsync(SheSafetySignStatus status) =>
        await _dbSet.Include(s => s.Location).Where(s => s.Status == status && !s.IsDeleted)
            .OrderBy(s => s.SignCode).ToListAsync();

    public async Task<IEnumerable<SafetySign>> GetActiveAsync() =>
        await _dbSet.Include(s => s.Location).Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.SignCode).ToListAsync();

    public async Task<IEnumerable<SafetySign>> GetDueForInspectionAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(s => s.Location)
            .Where(s => s.IsActive && !s.IsDeleted && s.NextInspectionDate != null && s.NextInspectionDate <= cutoff)
            .OrderBy(s => s.NextInspectionDate).ToListAsync();
    }
}

#endregion

#region Performance Snapshot

public class ShePerformanceSnapshotRepository : GenericRepository<ShePerformanceSnapshot>, IShePerformanceSnapshotRepository
{
    public ShePerformanceSnapshotRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ShePerformanceSnapshot?> GetByNumberAsync(string snapshotNumber) =>
        await _dbSet.Include(s => s.Location).Include(s => s.PreparedBy)
            .FirstOrDefaultAsync(s => s.SnapshotNumber == snapshotNumber && !s.IsDeleted);

    public async Task<ShePerformanceSnapshot?> GetByPeriodAsync(SheSnapshotPeriodType periodType, int year, int? periodNumber, Guid? locationId = null) =>
        await _dbSet.Include(s => s.Location).Include(s => s.PreparedBy)
            .FirstOrDefaultAsync(s => s.PeriodType == periodType && s.Year == year
                                   && s.PeriodNumber == periodNumber && s.LocationId == locationId && !s.IsDeleted);

    public async Task<IEnumerable<ShePerformanceSnapshot>> GetByYearAsync(int year) =>
        await _dbSet.Include(s => s.Location).Include(s => s.PreparedBy)
            .Where(s => s.Year == year && !s.IsDeleted)
            .OrderBy(s => s.PeriodType).ThenBy(s => s.PeriodNumber).ToListAsync();

    public async Task<IEnumerable<ShePerformanceSnapshot>> GetByLocationAsync(Guid locationId) =>
        await _dbSet.Include(s => s.PreparedBy)
            .Where(s => s.LocationId == locationId && !s.IsDeleted)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.PeriodNumber).ToListAsync();

    public async Task<ShePerformanceSnapshot?> GetLatestAsync(Guid tenantId) =>
        await _dbSet.Include(s => s.Location).Include(s => s.PreparedBy)
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderByDescending(s => s.PreparedDate).FirstOrDefaultAsync();
}

#endregion

#region Committee & Meetings

public class SafetyCommitteeRepository : GenericRepository<SafetyCommittee>, ISafetyCommitteeRepository
{
    public SafetyCommitteeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SafetyCommittee?> GetWithMembersAsync(Guid id) =>
        await _dbSet
            .Include(c => c.ChairPerson)
            .Include(c => c.Members).ThenInclude(m => m.Employee)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    public async Task<IEnumerable<SafetyCommittee>> GetActiveAsync() =>
        await _dbSet.Include(c => c.ChairPerson)
            .Where(c => c.IsActive && !c.IsDeleted).OrderBy(c => c.CommitteeName).ToListAsync();

    public async Task<IEnumerable<SafetyCommittee>> GetByChairPersonAsync(Guid chairPersonId) =>
        await _dbSet.Where(c => c.ChairPersonId == chairPersonId && !c.IsDeleted)
            .OrderBy(c => c.CommitteeName).ToListAsync();
}

public class SafetyCommitteeMemberRepository : GenericRepository<SafetyCommitteeMember>, ISafetyCommitteeMemberRepository
{
    public SafetyCommitteeMemberRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SafetyCommitteeMember>> GetByCommitteeIdAsync(Guid committeeId) =>
        await _dbSet.Include(m => m.Employee)
            .Where(m => m.CommitteeId == committeeId && !m.IsDeleted).ToListAsync();

    public async Task<IEnumerable<SafetyCommitteeMember>> GetByEmployeeAsync(Guid employeeId) =>
        await _dbSet.Include(m => m.Committee)
            .Where(m => m.EmployeeId == employeeId && !m.IsDeleted).ToListAsync();

    public async Task<IEnumerable<SafetyCommitteeMember>> GetActiveMembersAsync(Guid committeeId) =>
        await _dbSet.Include(m => m.Employee)
            .Where(m => m.CommitteeId == committeeId && m.IsActive && !m.IsDeleted).ToListAsync();
}

public class SafetyMeetingRepository : GenericRepository<SafetyMeeting>, ISafetyMeetingRepository
{
    public SafetyMeetingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SafetyMeeting?> GetByNumberAsync(string meetingNumber) =>
        await _dbSet.Include(m => m.Committee)
            .FirstOrDefaultAsync(m => m.MeetingNumber == meetingNumber && !m.IsDeleted);

    // Split query: three collection includes in one query multiply rows (and the joined width is
    // the 8060-byte worktable shape that 500'd the incident detail read).
    public async Task<SafetyMeeting?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(m => m.Committee)
            .Include(m => m.Facilitator)
            .Include(m => m.Attendees).ThenInclude(a => a.Employee)
            .Include(m => m.ActionItems).ThenInclude(a => a.AssignedTo)
            .Include(m => m.Documents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

    // List reads carry Committee (the mapper's CommitteeName) and ActionItems (the summary's
    // OpenActionItemCount — without the include it maps 0 on every row).
    public async Task<IEnumerable<SafetyMeeting>> GetByCommitteeIdAsync(Guid committeeId) =>
        await _dbSet.Include(m => m.Committee).Include(m => m.ActionItems)
            .Where(m => m.CommitteeId == committeeId && !m.IsDeleted)
            .OrderByDescending(m => m.MeetingDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeeting>> GetByTypeAsync(SheSafetyMeetingType type) =>
        await _dbSet.Include(m => m.Committee).Include(m => m.ActionItems)
            .Where(m => m.Type == type && !m.IsDeleted)
            .OrderByDescending(m => m.MeetingDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeeting>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await _dbSet.Include(m => m.Committee).Include(m => m.ActionItems)
            .Where(m => m.MeetingDate >= fromDate && m.MeetingDate <= toDate && !m.IsDeleted)
            .OrderByDescending(m => m.MeetingDate).ToListAsync();
}

public class SafetyMeetingActionItemRepository : GenericRepository<SafetyMeetingActionItem>, ISafetyMeetingActionItemRepository
{
    public SafetyMeetingActionItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SafetyMeetingActionItem>> GetByMeetingIdAsync(Guid meetingId) =>
        await _dbSet.Include(a => a.AssignedTo)
            .Where(a => a.MeetingId == meetingId && !a.IsDeleted)
            .OrderBy(a => a.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeetingActionItem>> GetByAssigneeAsync(Guid employeeId) =>
        await _dbSet.Include(a => a.Meeting)
            .Where(a => a.AssignedToId == employeeId && !a.IsDeleted)
            .OrderBy(a => a.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeetingActionItem>> GetByStatusAsync(SheActionItemStatus status) =>
        await _dbSet.Include(a => a.AssignedTo)
            .Where(a => a.Status == status && !a.IsDeleted)
            .OrderBy(a => a.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeetingActionItem>> GetOpenAsync() =>
        await _dbSet.Include(a => a.AssignedTo).Include(a => a.Meeting)
            .Where(a => a.Status != SheActionItemStatus.Completed && a.Status != SheActionItemStatus.Cancelled && !a.IsDeleted)
            .OrderBy(a => a.DueDate).ToListAsync();

    public async Task<IEnumerable<SafetyMeetingActionItem>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow;
        return await _dbSet.Include(a => a.AssignedTo).Include(a => a.Meeting)
            .Where(a => !a.IsDeleted
                     && a.Status != SheActionItemStatus.Completed
                     && a.Status != SheActionItemStatus.Cancelled
                     && a.DueDate != null && a.DueDate < today)
            .OrderBy(a => a.DueDate).ToListAsync();
    }
}

#endregion

#region Return-to-Work

public class SheReturnToWorkPlanRepository : GenericRepository<SheReturnToWorkPlan>, ISheReturnToWorkPlanRepository
{
    public SheReturnToWorkPlanRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheReturnToWorkPlan> WithListNavigations() =>
        _dbSet.Include(p => p.Employee).Include(p => p.SafetyIncident);

    public async Task<SheReturnToWorkPlan?> GetByNumberAsync(string planNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(p => p.PlanNumber == planNumber && !p.IsDeleted);

    public async Task<SheReturnToWorkPlan?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.SafetyIncident)
            .Include(p => p.Coordinator)
            .Include(p => p.Supervisor)
            .Include(p => p.Phases).ThenInclude(ph => ph.AssessedBy)
            .Include(p => p.Reviews).ThenInclude(r => r.ReviewedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<SheReturnToWorkPlan>> GetAllSummaryAsync() =>
        await WithListNavigations().Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.PlanDate).ToListAsync();

    public async Task<IEnumerable<SheReturnToWorkPlan>> GetByEmployeeAsync(Guid employeeId) =>
        await WithListNavigations().Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.PlanDate).ToListAsync();

    public async Task<IEnumerable<SheReturnToWorkPlan>> GetByStatusAsync(SheReturnToWorkStatus status) =>
        await WithListNavigations().Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.PlanDate).ToListAsync();

    public async Task<IEnumerable<SheReturnToWorkPlan>> GetByIncidentAsync(Guid safetyIncidentId) =>
        await WithListNavigations().Where(p => p.SafetyIncidentId == safetyIncidentId && !p.IsDeleted)
            .OrderByDescending(p => p.PlanDate).ToListAsync();

    public async Task<IEnumerable<SheReturnToWorkPlan>> GetActiveAsync() =>
        await WithListNavigations()
            .Where(p => !p.IsDeleted && p.Status != SheReturnToWorkStatus.Completed && p.Status != SheReturnToWorkStatus.Discontinued)
            .OrderByDescending(p => p.PlanDate).ToListAsync();
}

#endregion
