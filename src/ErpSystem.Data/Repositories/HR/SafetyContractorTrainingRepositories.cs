using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Contractor SHE Management (H) and Training (I).
// ============================================================================

#region Contractor

public class SheContractorRepository : GenericRepository<SheContractor>, ISheContractorRepository
{
    public SheContractorRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheContractor?> GetByCodeAsync(string contractorCode) =>
        await _dbSet.FirstOrDefaultAsync(c => c.ContractorCode == contractorCode && !c.IsDeleted);

    public async Task<SheContractor?> GetWithFullDetailsAsync(Guid id) =>
        await _dbSet
            .Include(c => c.PreQualifiedBy)
            .Include(c => c.Inductions).ThenInclude(i => i.ConductedBy)
            .Include(c => c.SheInspections).ThenInclude(i => i.Inspector)
            .Include(c => c.SheInspections).ThenInclude(i => i.Location)
            .Include(c => c.NonCompliances).ThenInclude(n => n.IssuedBy)
            .Include(c => c.NonCompliances).ThenInclude(n => n.ClosedBy)
            .Include(c => c.Documents).ThenInclude(d => d.UploadedBy)
            .Include(c => c.Documents).ThenInclude(d => d.VerifiedBy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    public async Task<IEnumerable<SheContractor>> GetAllSummaryAsync() =>
        await _dbSet.Include(c => c.NonCompliances).Where(c => !c.IsDeleted).OrderBy(c => c.CompanyName).ToListAsync();

    public async Task<IEnumerable<SheContractor>> GetByStatusAsync(SheContractorStatus status) =>
        await _dbSet.Include(c => c.NonCompliances)
            .Where(c => c.SheStatus == status && !c.IsDeleted).OrderBy(c => c.CompanyName).ToListAsync();

    public async Task<IEnumerable<SheContractor>> GetActiveAsync() =>
        await _dbSet.Include(c => c.NonCompliances)
            .Where(c => c.IsActive && !c.IsDeleted).OrderBy(c => c.CompanyName).ToListAsync();

    public async Task<IEnumerable<SheContractor>> GetExpiringPreQualificationAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(c => c.NonCompliances)
            .Where(c => !c.IsDeleted && c.SheStatus == SheContractorStatus.Approved
                     && c.PreQualificationExpiryDate != null && c.PreQualificationExpiryDate <= cutoff)
            .OrderBy(c => c.PreQualificationExpiryDate).ToListAsync();
    }

    public async Task<IEnumerable<SheContractor>> GetWithOpenNonCompliancesAsync() =>
        await _dbSet.Include(c => c.NonCompliances)
            .Where(c => !c.IsDeleted && c.NonCompliances.Any(n => n.Status != SheNonComplianceStatus.Closed))
            .OrderBy(c => c.CompanyName).ToListAsync();
}

public class SheContractorInspectionRepository : GenericRepository<SheContractorInspection>, ISheContractorInspectionRepository
{
    public SheContractorInspectionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheContractorInspection?> GetByNumberAsync(string inspectionNumber) =>
        await _dbSet.Include(i => i.Contractor).Include(i => i.Inspector)
            .FirstOrDefaultAsync(i => i.InspectionNumber == inspectionNumber && !i.IsDeleted);

    public async Task<IEnumerable<SheContractorInspection>> GetByContractorIdAsync(Guid contractorId) =>
        await _dbSet.Include(i => i.Inspector).Include(i => i.Location)
            .Where(i => i.ContractorId == contractorId && !i.IsDeleted)
            .OrderByDescending(i => i.InspectionDate).ToListAsync();
}

public class SheContractorNonComplianceRepository : GenericRepository<SheContractorNonCompliance>, ISheContractorNonComplianceRepository
{
    public SheContractorNonComplianceRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheContractorNonCompliance> WithNavigations() =>
        _dbSet.Include(n => n.Contractor).Include(n => n.IssuedBy).Include(n => n.ClosedBy);

    public async Task<SheContractorNonCompliance?> GetByNumberAsync(string noticeNumber) =>
        await WithNavigations().FirstOrDefaultAsync(n => n.NoticeNumber == noticeNumber && !n.IsDeleted);

    public async Task<IEnumerable<SheContractorNonCompliance>> GetByContractorIdAsync(Guid contractorId) =>
        await WithNavigations().Where(n => n.ContractorId == contractorId && !n.IsDeleted)
            .OrderByDescending(n => n.IssuedDate).ToListAsync();

    public async Task<IEnumerable<SheContractorNonCompliance>> GetByStatusAsync(SheNonComplianceStatus status) =>
        await WithNavigations().Where(n => n.Status == status && !n.IsDeleted)
            .OrderByDescending(n => n.IssuedDate).ToListAsync();

    public async Task<IEnumerable<SheContractorNonCompliance>> GetOpenAsync() =>
        await WithNavigations().Where(n => n.Status != SheNonComplianceStatus.Closed && !n.IsDeleted)
            .OrderBy(n => n.RectificationDeadline).ToListAsync();

    public async Task<IEnumerable<SheContractorNonCompliance>> GetOverdueAsync()
    {
        var today = DateTime.UtcNow;
        return await WithNavigations()
            .Where(n => !n.IsDeleted && n.Status != SheNonComplianceStatus.Closed && n.RectificationDeadline < today)
            .OrderBy(n => n.RectificationDeadline).ToListAsync();
    }

    public async Task<IEnumerable<SheContractorNonCompliance>> GetRepeatViolationsAsync() =>
        await WithNavigations().Where(n => n.IsRepeatViolation && !n.IsDeleted)
            .OrderByDescending(n => n.RepeatCount).ToListAsync();
}

public class SheContractorDocumentRepository : GenericRepository<SheContractorDocument>, ISheContractorDocumentRepository
{
    public SheContractorDocumentRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheContractorDocument> WithNavigations() =>
        _dbSet.Include(d => d.Contractor).Include(d => d.UploadedBy).Include(d => d.VerifiedBy);

    public async Task<IEnumerable<SheContractorDocument>> GetByContractorIdAsync(Guid contractorId) =>
        await WithNavigations().Where(d => d.ContractorId == contractorId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedDate).ToListAsync();

    public async Task<IEnumerable<SheContractorDocument>> GetExpiringAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithNavigations()
            .Where(d => !d.IsDeleted && d.ExpiryDate != null && d.ExpiryDate <= cutoff)
            .OrderBy(d => d.ExpiryDate).ToListAsync();
    }

    public async Task<IEnumerable<SheContractorDocument>> GetUnverifiedAsync() =>
        await WithNavigations()
            .Where(d => !d.IsVerified && !d.IsDeleted)
            .OrderBy(d => d.UploadedDate).ToListAsync();
}

#endregion

#region Training

public class SheTrainingPlanRepository : GenericRepository<SheTrainingPlan>, ISheTrainingPlanRepository
{
    public SheTrainingPlanRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheTrainingPlan?> GetByNumberAsync(string planNumber) =>
        await _dbSet.Include(p => p.PreparedBy)
            .FirstOrDefaultAsync(p => p.PlanNumber == planNumber && !p.IsDeleted);

    public async Task<SheTrainingPlan?> GetWithProgramsAsync(Guid id) =>
        await _dbSet
            .Include(p => p.OrganizationUnit)
            .Include(p => p.PreparedBy)
            .Include(p => p.ApprovedBy)
            .Include(p => p.Programs)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<SheTrainingPlan>> GetByYearAsync(int year) =>
        await _dbSet.Include(p => p.PreparedBy)
            .Where(p => p.Year == year && !p.IsDeleted)
            .OrderByDescending(p => p.Year).ThenBy(p => p.Quarter).ToListAsync();

    public async Task<IEnumerable<SheTrainingPlan>> GetByStatusAsync(SheTrainingPlanStatus status) =>
        await _dbSet.Include(p => p.PreparedBy)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.Year).ToListAsync();
}

public class SheTrainingProgramRepository : GenericRepository<SheTrainingProgram>, ISheTrainingProgramRepository
{
    public SheTrainingProgramRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheTrainingProgram> WithListNavigations() =>
        _dbSet.Include(p => p.Location).Include(p => p.Trainer).Include(p => p.Plan);

    public async Task<SheTrainingProgram?> GetByCodeAsync(string programCode) =>
        await WithListNavigations().FirstOrDefaultAsync(p => p.ProgramCode == programCode && !p.IsDeleted);

    public async Task<SheTrainingProgram?> GetWithAttendancesAsync(Guid id) =>
        await _dbSet
            .Include(p => p.Location)
            .Include(p => p.Trainer)
            .Include(p => p.Plan)
            .Include(p => p.EvaluatedBy)
            .Include(p => p.Attendances).ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<SheTrainingProgram>> GetByPlanIdAsync(Guid planId) =>
        await WithListNavigations().Where(p => p.PlanId == planId && !p.IsDeleted)
            .OrderBy(p => p.ScheduledDate).ToListAsync();

    public async Task<IEnumerable<SheTrainingProgram>> GetByStatusAsync(SheTrainingStatus status) =>
        await WithListNavigations().Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.ScheduledDate).ToListAsync();

    public async Task<IEnumerable<SheTrainingProgram>> GetByCategoryAsync(SheTrainingCategory category) =>
        await WithListNavigations().Where(p => p.Category == category && !p.IsDeleted)
            .OrderByDescending(p => p.ScheduledDate).ToListAsync();

    public async Task<IEnumerable<SheTrainingProgram>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithListNavigations()
            .Where(p => !p.IsDeleted && p.ScheduledDate != null && p.ScheduledDate >= fromDate && p.ScheduledDate <= toDate)
            .OrderBy(p => p.ScheduledDate).ToListAsync();

    public async Task<IEnumerable<SheTrainingProgram>> GetUpcomingAsync(int daysAhead = 30)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(daysAhead);
        return await WithListNavigations()
            .Where(p => !p.IsDeleted
                     && (p.Status == SheTrainingStatus.Planned || p.Status == SheTrainingStatus.Scheduled)
                     && p.ScheduledDate != null && p.ScheduledDate >= now && p.ScheduledDate <= cutoff)
            .OrderBy(p => p.ScheduledDate).ToListAsync();
    }
}

public class SheTrainingAttendanceRepository : GenericRepository<SheTrainingAttendance>, ISheTrainingAttendanceRepository
{
    public SheTrainingAttendanceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<SheTrainingAttendance>> GetByProgramIdAsync(Guid programId) =>
        await _dbSet.Include(a => a.Employee)
            .Where(a => a.ProgramId == programId && !a.IsDeleted)
            .OrderBy(a => a.AttendanceName).ToListAsync();

    public async Task<IEnumerable<SheTrainingAttendance>> GetByEmployeeAsync(Guid employeeId) =>
        await _dbSet.Include(a => a.Program)
            .Where(a => a.EmployeeId == employeeId && !a.IsDeleted)
            .OrderByDescending(a => a.SignedDate).ToListAsync();

    public async Task<IEnumerable<SheTrainingAttendance>> GetExpiringCertificatesAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet.Include(a => a.Employee).Include(a => a.Program)
            .Where(a => !a.IsDeleted && a.CertificateExpiryDate != null && a.CertificateExpiryDate <= cutoff)
            .OrderBy(a => a.CertificateExpiryDate).ToListAsync();
    }
}

#endregion
