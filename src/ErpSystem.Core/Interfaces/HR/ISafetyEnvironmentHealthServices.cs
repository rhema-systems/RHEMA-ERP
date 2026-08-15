using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE services — Waste (J), Environmental (K) and Occupational Health (L).
// ============================================================================

// ============================================================================
// J. WASTE MANAGEMENT SERVICE
// ============================================================================

public interface ISheWasteManagementService
{
    // Waste types
    Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesByClassificationAsync(SheWasteClassification classification, CancellationToken cancellationToken = default);
    Task<SheWasteTypeDto> CreateWasteTypeAsync(CreateSheWasteTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheWasteTypeDto> UpdateWasteTypeAsync(UpdateSheWasteTypeDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteWasteTypeAsync(Guid id, CancellationToken cancellationToken = default);

    // Disposal records
    Task<SheWasteDisposalRecordDto> GetDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByWasteTypeAsync(Guid wasteTypeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default);
    Task<SheWasteDisposalRecordDto> CreateDisposalRecordAsync(CreateSheWasteDisposalRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheWasteDisposalRecordDto> UpdateDisposalRecordAsync(UpdateSheWasteDisposalRecordDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default);
}

// ============================================================================
// K. ENVIRONMENTAL SERVICE
// ============================================================================

public interface ISheEnvironmentalService
{
    // Incidents
    Task<SheEnvironmentalIncidentDto> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalIncidentDto?> GetIncidentByNumberAsync(string incidentNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByStatusAsync(SheEnvironmentalIncidentStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByTypeAsync(SheEnvironmentalIncidentType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetOpenIncidentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsReportedToEpaAsync(CancellationToken cancellationToken = default);

    /// <summary>The caller's own reported incidents — the slice-17 open self-service read (FR-ENV-025).</summary>
    Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetMyIncidentsAsync(Guid employeeId, CancellationToken cancellationToken = default);

    Task<SheEnvironmentalIncidentDto> CreateIncidentAsync(CreateSheEnvironmentalIncidentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalIncidentDto> UpdateIncidentAsync(UpdateSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CloseIncidentAsync(CloseSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteIncidentAsync(Guid id, CancellationToken cancellationToken = default);

    // Monitoring
    Task<SheEnvironmentalMonitoringRecordDto> GetMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByTypeAsync(SheEnvironmentalMonitoringType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetExceedancesAsync(CancellationToken cancellationToken = default);
    Task<SheEnvironmentalMonitoringRecordDto> CreateMonitoringRecordAsync(CreateSheEnvironmentalMonitoringRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheEnvironmentalMonitoringRecordDto> UpdateMonitoringRecordAsync(UpdateSheEnvironmentalMonitoringRecordDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default);
}

// ============================================================================
// L. OCCUPATIONAL HEALTH SERVICE  (surveillance, first aid, wellness)
// ============================================================================

public interface ISheOccupationalHealthService
{
    // Health surveillance
    Task<SheOccupationalHealthSurveillanceDto> GetSurveillanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetAllSurveillanceAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByTypeAsync(SheHealthSurveillanceType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByResultAsync(SheHealthSurveillanceResult result, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceDueForExaminationAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceWithRestrictionsAsync(CancellationToken cancellationToken = default);
    Task<SheOccupationalHealthSurveillanceDto> CreateSurveillanceAsync(CreateSheOccupationalHealthSurveillanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheOccupationalHealthSurveillanceDto> UpdateSurveillanceAsync(UpdateSheOccupationalHealthSurveillanceDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSurveillanceAsync(Guid id, CancellationToken cancellationToken = default);

    // First aid stations
    Task<SheFirstAidStationDto> GetFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheFirstAidStationDto>> GetUnderStockedStationsAsync(CancellationToken cancellationToken = default);
    Task<SheFirstAidStationDto> CreateFirstAidStationAsync(CreateSheFirstAidStationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheFirstAidStationDto> UpdateFirstAidStationAsync(UpdateSheFirstAidStationDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default);

    // Wellness programs
    Task<SheWellnessProgramDto> GetWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByStatusAsync(SheWellnessProgramStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByTypeAsync(SheWellnessProgramType type, CancellationToken cancellationToken = default);
    Task<SheWellnessProgramDto> CreateWellnessProgramAsync(CreateSheWellnessProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<SheWellnessProgramDto> UpdateWellnessProgramAsync(UpdateSheWellnessProgramDto dto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default);
}
