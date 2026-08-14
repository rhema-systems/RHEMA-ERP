using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// SHE repositories — Waste (J), Environmental (K) and Occupational Health (L).
// ============================================================================

#region Waste

public class SheWasteTypeRepository : GenericRepository<SheWasteType>, ISheWasteTypeRepository
{
    public SheWasteTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheWasteType?> GetByCodeAsync(string code) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);

    public async Task<IEnumerable<SheWasteType>> GetActiveAsync() =>
        await _dbSet.Where(t => t.IsActive && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();

    public async Task<IEnumerable<SheWasteType>> GetByClassificationAsync(SheWasteClassification classification) =>
        await _dbSet.Where(t => t.Classification == classification && !t.IsDeleted).OrderBy(t => t.Name).ToListAsync();
}

public class SheWasteDisposalRecordRepository : GenericRepository<SheWasteDisposalRecord>, ISheWasteDisposalRecordRepository
{
    public SheWasteDisposalRecordRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheWasteDisposalRecord> WithNavigations() =>
        _dbSet.Include(r => r.WasteType).Include(r => r.Location).Include(r => r.WasteContractor).Include(r => r.RecordedBy);

    public async Task<SheWasteDisposalRecord?> GetByNumberAsync(string recordNumber) =>
        await WithNavigations().FirstOrDefaultAsync(r => r.RecordNumber == recordNumber && !r.IsDeleted);

    public async Task<IEnumerable<SheWasteDisposalRecord>> GetByWasteTypeAsync(Guid wasteTypeId) =>
        await WithNavigations().Where(r => r.WasteTypeId == wasteTypeId && !r.IsDeleted)
            .OrderByDescending(r => r.DisposalDate).ToListAsync();

    public async Task<IEnumerable<SheWasteDisposalRecord>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithNavigations().Where(r => r.DisposalDate >= fromDate && r.DisposalDate <= toDate && !r.IsDeleted)
            .OrderByDescending(r => r.DisposalDate).ToListAsync();

    public async Task<IEnumerable<SheWasteDisposalRecord>> GetByContractorAsync(Guid contractorId) =>
        await WithNavigations().Where(r => r.WasteContractorId == contractorId && !r.IsDeleted)
            .OrderByDescending(r => r.DisposalDate).ToListAsync();

}

#endregion

#region Environmental

public class SheEnvironmentalIncidentRepository : GenericRepository<SheEnvironmentalIncident>, ISheEnvironmentalIncidentRepository
{
    public SheEnvironmentalIncidentRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheEnvironmentalIncident> WithListNavigations() =>
        _dbSet.Include(e => e.Location).Include(e => e.ReportedBy);

    public async Task<SheEnvironmentalIncident?> GetByNumberAsync(string incidentNumber) =>
        await WithListNavigations().FirstOrDefaultAsync(e => e.IncidentNumber == incidentNumber && !e.IsDeleted);

    public async Task<SheEnvironmentalIncident?> GetWithDetailsAsync(Guid id) =>
        await _dbSet
            .Include(e => e.SafetyIncident)
            .Include(e => e.Location)
            .Include(e => e.ReportedBy)
            .Include(e => e.ClosedBy)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

    public async Task<IEnumerable<SheEnvironmentalIncident>> GetByStatusAsync(SheEnvironmentalIncidentStatus status) =>
        await WithListNavigations().Where(e => e.Status == status && !e.IsDeleted)
            .OrderByDescending(e => e.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalIncident>> GetByTypeAsync(SheEnvironmentalIncidentType type) =>
        await WithListNavigations().Where(e => e.Type == type && !e.IsDeleted)
            .OrderByDescending(e => e.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalIncident>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithListNavigations().Where(e => e.IncidentDate >= fromDate && e.IncidentDate <= toDate && !e.IsDeleted)
            .OrderByDescending(e => e.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalIncident>> GetReportedToEpaAsync() =>
        await WithListNavigations().Where(e => e.ReportedToEpa && !e.IsDeleted)
            .OrderByDescending(e => e.IncidentDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalIncident>> GetOpenAsync() =>
        await WithListNavigations().Where(e => e.Status != SheEnvironmentalIncidentStatus.Closed && !e.IsDeleted)
            .OrderByDescending(e => e.IncidentDate).ToListAsync();

}

public class SheEnvironmentalMonitoringRecordRepository : GenericRepository<SheEnvironmentalMonitoringRecord>, ISheEnvironmentalMonitoringRecordRepository
{
    public SheEnvironmentalMonitoringRecordRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheEnvironmentalMonitoringRecord> WithNavigations() =>
        _dbSet.Include(r => r.Location).Include(r => r.MeasuredBy);

    public async Task<SheEnvironmentalMonitoringRecord?> GetByNumberAsync(string recordNumber) =>
        await WithNavigations().FirstOrDefaultAsync(r => r.RecordNumber == recordNumber && !r.IsDeleted);

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByTypeAsync(SheEnvironmentalMonitoringType type) =>
        await WithNavigations().Where(r => r.MonitoringType == type && !r.IsDeleted)
            .OrderByDescending(r => r.MeasurementDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByLocationAsync(Guid locationId) =>
        await WithNavigations().Where(r => r.LocationId == locationId && !r.IsDeleted)
            .OrderByDescending(r => r.MeasurementDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate) =>
        await WithNavigations().Where(r => r.MeasurementDate >= fromDate && r.MeasurementDate <= toDate && !r.IsDeleted)
            .OrderByDescending(r => r.MeasurementDate).ToListAsync();

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetExceedancesAsync() =>
        await WithNavigations().Where(r => (r.ExceedsLimit || r.ExceedsActionLevel) && !r.IsDeleted)
            .OrderByDescending(r => r.MeasurementDate).ToListAsync();

}

#endregion

#region Occupational Health

public class SheOccupationalHealthSurveillanceRepository : GenericRepository<SheOccupationalHealthSurveillance>, ISheOccupationalHealthSurveillanceRepository
{
    public SheOccupationalHealthSurveillanceRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheOccupationalHealthSurveillance> WithNavigations() =>
        _dbSet.Include(s => s.Employee).Include(s => s.HealthcareFacility).Include(s => s.RecordedBy);

    public async Task<SheOccupationalHealthSurveillance?> GetByNumberAsync(string surveillanceNumber) =>
        await WithNavigations().FirstOrDefaultAsync(s => s.SurveillanceNumber == surveillanceNumber && !s.IsDeleted);

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByEmployeeAsync(Guid employeeId) =>
        await WithNavigations().Where(s => s.EmployeeId == employeeId && !s.IsDeleted)
            .OrderByDescending(s => s.ExaminationDate).ToListAsync();

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetAllSummaryAsync() =>
        await WithNavigations().Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.ExaminationDate).ToListAsync();

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByTypeAsync(SheHealthSurveillanceType type) =>
        await WithNavigations().Where(s => s.Type == type && !s.IsDeleted)
            .OrderByDescending(s => s.ExaminationDate).ToListAsync();

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByResultAsync(SheHealthSurveillanceResult result) =>
        await WithNavigations().Where(s => s.Result == result && !s.IsDeleted)
            .OrderByDescending(s => s.ExaminationDate).ToListAsync();

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetDueForExaminationAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithNavigations()
            .Where(s => !s.IsDeleted && s.NextExaminationDate != null && s.NextExaminationDate <= cutoff)
            .OrderBy(s => s.NextExaminationDate).ToListAsync();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillance>> GetWithRestrictionsAsync() =>
        await WithNavigations().Where(s => s.WorkRestrictionIssued && !s.IsDeleted)
            .OrderByDescending(s => s.ExaminationDate).ToListAsync();
}

public class SheFirstAidStationRepository : GenericRepository<SheFirstAidStation>, ISheFirstAidStationRepository
{
    public SheFirstAidStationRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<SheFirstAidStation> WithNavigations() =>
        _dbSet.Include(s => s.Location).Include(s => s.ResponsibleAider);

    public async Task<SheFirstAidStation?> GetByCodeAsync(string stationCode) =>
        await WithNavigations()
            .FirstOrDefaultAsync(s => s.StationCode == stationCode && !s.IsDeleted);

    public async Task<IEnumerable<SheFirstAidStation>> GetAllListAsync() =>
        await WithNavigations()
            .Where(s => !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();

    public async Task<IEnumerable<SheFirstAidStation>> GetByLocationAsync(Guid locationId) =>
        await WithNavigations()
            .Where(s => s.LocationId == locationId && !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();

    public async Task<IEnumerable<SheFirstAidStation>> GetActiveAsync() =>
        await WithNavigations()
            .Where(s => s.IsActive && !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();

    public async Task<IEnumerable<SheFirstAidStation>> GetDueForInspectionAsync(int daysAhead = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await WithNavigations()
            .Where(s => s.IsActive && !s.IsDeleted && s.NextInspectionDate != null && s.NextInspectionDate <= cutoff)
            .OrderBy(s => s.NextInspectionDate).ToListAsync();
    }

    public async Task<IEnumerable<SheFirstAidStation>> GetUnderStockedAsync() =>
        await WithNavigations()
            .Where(s => s.IsActive && !s.IsFullyStocked && !s.IsDeleted).OrderBy(s => s.Name).ToListAsync();
}

public class SheWellnessProgramRepository : GenericRepository<SheWellnessProgram>, ISheWellnessProgramRepository
{
    public SheWellnessProgramRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SheWellnessProgram?> GetByCodeAsync(string programCode) =>
        await _dbSet.Include(p => p.Coordinator)
            .FirstOrDefaultAsync(p => p.ProgramCode == programCode && !p.IsDeleted);

    public async Task<IEnumerable<SheWellnessProgram>> GetAllListAsync() =>
        await _dbSet.Include(p => p.Coordinator)
            .Where(p => !p.IsDeleted).OrderByDescending(p => p.StartDate).ToListAsync();

    public async Task<IEnumerable<SheWellnessProgram>> GetByStatusAsync(SheWellnessProgramStatus status) =>
        await _dbSet.Include(p => p.Coordinator)
            .Where(p => p.Status == status && !p.IsDeleted).OrderByDescending(p => p.StartDate).ToListAsync();

    public async Task<IEnumerable<SheWellnessProgram>> GetByTypeAsync(SheWellnessProgramType type) =>
        await _dbSet.Include(p => p.Coordinator)
            .Where(p => p.Type == type && !p.IsDeleted).OrderByDescending(p => p.StartDate).ToListAsync();

    public async Task<IEnumerable<SheWellnessProgram>> GetActiveAsync() =>
        await _dbSet.Include(p => p.Coordinator)
            .Where(p => p.IsActive && !p.IsDeleted).OrderByDescending(p => p.StartDate).ToListAsync();
}

#endregion
