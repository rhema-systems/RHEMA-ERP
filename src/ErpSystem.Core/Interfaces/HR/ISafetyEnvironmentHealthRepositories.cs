using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE repositories — Waste (J), Environmental (K) and Occupational Health (L).
// ============================================================================

// ============================================================================
// J. WASTE MANAGEMENT
// ============================================================================

public interface ISheWasteTypeRepository : IGenericRepository<SheWasteType>
{
    Task<SheWasteType?> GetByCodeAsync(string code);
    Task<IEnumerable<SheWasteType>> GetActiveAsync();
    Task<IEnumerable<SheWasteType>> GetByClassificationAsync(SheWasteClassification classification);
}

public interface ISheWasteDisposalRecordRepository : IGenericRepository<SheWasteDisposalRecord>
{
    Task<SheWasteDisposalRecord?> GetByNumberAsync(string recordNumber);
    Task<IEnumerable<SheWasteDisposalRecord>> GetByWasteTypeAsync(Guid wasteTypeId);
    Task<IEnumerable<SheWasteDisposalRecord>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<SheWasteDisposalRecord>> GetByContractorAsync(Guid contractorId);
    Task<string> GetNextRecordNumberAsync();
}

// ============================================================================
// K. ENVIRONMENTAL MANAGEMENT
// ============================================================================

public interface ISheEnvironmentalIncidentRepository : IGenericRepository<SheEnvironmentalIncident>
{
    Task<SheEnvironmentalIncident?> GetByNumberAsync(string incidentNumber);
    Task<SheEnvironmentalIncident?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<SheEnvironmentalIncident>> GetByStatusAsync(SheEnvironmentalIncidentStatus status);
    Task<IEnumerable<SheEnvironmentalIncident>> GetByTypeAsync(SheEnvironmentalIncidentType type);
    Task<IEnumerable<SheEnvironmentalIncident>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>Returns incidents that have been reported to the EPA.</summary>
    Task<IEnumerable<SheEnvironmentalIncident>> GetReportedToEpaAsync();

    /// <summary>Returns incidents that are not yet closed.</summary>
    Task<IEnumerable<SheEnvironmentalIncident>> GetOpenAsync();

    Task<string> GetNextIncidentNumberAsync();
}

public interface ISheEnvironmentalMonitoringRecordRepository : IGenericRepository<SheEnvironmentalMonitoringRecord>
{
    Task<SheEnvironmentalMonitoringRecord?> GetByNumberAsync(string recordNumber);
    Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByTypeAsync(SheEnvironmentalMonitoringType type);
    Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);

    /// <summary>Returns readings that exceeded the regulatory limit or action level.</summary>
    Task<IEnumerable<SheEnvironmentalMonitoringRecord>> GetExceedancesAsync();

    Task<string> GetNextRecordNumberAsync();
}

// ============================================================================
// L. OCCUPATIONAL HEALTH MANAGEMENT
// ============================================================================

public interface ISheOccupationalHealthSurveillanceRepository : IGenericRepository<SheOccupationalHealthSurveillance>
{
    Task<SheOccupationalHealthSurveillance?> GetByNumberAsync(string surveillanceNumber);
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetAllSummaryAsync();
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByTypeAsync(SheHealthSurveillanceType type);
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetByResultAsync(SheHealthSurveillanceResult result);

    /// <summary>Returns surveillance records whose next examination falls within the specified number of days.</summary>
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetDueForExaminationAsync(int daysAhead = 30);

    /// <summary>Returns records where a work restriction was issued.</summary>
    Task<IEnumerable<SheOccupationalHealthSurveillance>> GetWithRestrictionsAsync();
}

public interface ISheFirstAidStationRepository : IGenericRepository<SheFirstAidStation>
{
    Task<SheFirstAidStation?> GetByCodeAsync(string stationCode);

    /// <summary>All stations with Location and ResponsibleAider resolved — the default register read.</summary>
    Task<IEnumerable<SheFirstAidStation>> GetAllListAsync();

    Task<IEnumerable<SheFirstAidStation>> GetByLocationAsync(Guid locationId);
    Task<IEnumerable<SheFirstAidStation>> GetActiveAsync();

    /// <summary>Returns active stations whose next inspection falls within the specified number of days.</summary>
    Task<IEnumerable<SheFirstAidStation>> GetDueForInspectionAsync(int daysAhead = 30);

    /// <summary>Returns active stations that are not fully stocked.</summary>
    Task<IEnumerable<SheFirstAidStation>> GetUnderStockedAsync();
}

public interface ISheWellnessProgramRepository : IGenericRepository<SheWellnessProgram>
{
    Task<SheWellnessProgram?> GetByCodeAsync(string programCode);

    /// <summary>All programs with the Coordinator resolved — the default register read.</summary>
    Task<IEnumerable<SheWellnessProgram>> GetAllListAsync();
    Task<IEnumerable<SheWellnessProgram>> GetByStatusAsync(SheWellnessProgramStatus status);
    Task<IEnumerable<SheWellnessProgram>> GetByTypeAsync(SheWellnessProgramType type);
    Task<IEnumerable<SheWellnessProgram>> GetActiveAsync();
}
