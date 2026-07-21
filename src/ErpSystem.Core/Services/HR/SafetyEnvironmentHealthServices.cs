using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// SHE services — Waste (J), Environmental (K) and Occupational Health (L).
// ============================================================================

#region Waste Management Service

public class SheWasteManagementService : ISheWasteManagementService
{
    private readonly ISheWasteTypeRepository _wasteTypeRepository;
    private readonly ISheWasteDisposalRecordRepository _disposalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheWasteManagementService> _logger;

    public SheWasteManagementService(
        ISheWasteTypeRepository wasteTypeRepository,
        ISheWasteDisposalRecordRepository disposalRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheWasteManagementService> logger)
    {
        _wasteTypeRepository = wasteTypeRepository;
        _disposalRepository = disposalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Waste types ──
    public async Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _wasteTypeRepository.GetActiveAsync() : await _wasteTypeRepository.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesByClassificationAsync(SheWasteClassification classification, CancellationToken cancellationToken = default)
        => (await _wasteTypeRepository.GetByClassificationAsync(classification)).Select(e => e.ToDto());

    public async Task<SheWasteTypeDto> CreateWasteTypeAsync(CreateSheWasteTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _wasteTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWasteTypeDto> UpdateWasteTypeAsync(UpdateSheWasteTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _wasteTypeRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Waste type with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _wasteTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWasteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _wasteTypeRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Waste type with ID '{id}' not found.");
        await _wasteTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Disposal records ──
    public async Task<SheWasteDisposalRecordDto> GetDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _disposalRepository.GetByIdAsync(id, r => r.WasteType, r => r.Location, r => r.WasteContractor, r => r.RecordedBy)
            ?? throw new ArgumentException($"Waste disposal record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByWasteTypeAsync(Guid wasteTypeId, CancellationToken cancellationToken = default)
        => (await _disposalRepository.GetByWasteTypeAsync(wasteTypeId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => (await _disposalRepository.GetByDateRangeAsync(fromDate, toDate)).ToSummaryDtoList();

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default)
        => (await _disposalRepository.GetByContractorAsync(contractorId)).ToSummaryDtoList();

    public async Task<SheWasteDisposalRecordDto> CreateDisposalRecordAsync(CreateSheWasteDisposalRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.RecordNumber))
            entity.RecordNumber = await _disposalRepository.GetNextRecordNumberAsync();
        await _disposalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWasteDisposalRecordDto> UpdateDisposalRecordAsync(UpdateSheWasteDisposalRecordDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _disposalRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Waste disposal record with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _disposalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _disposalRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Waste disposal record with ID '{id}' not found.");
        await _disposalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Environmental Service

public class SheEnvironmentalService : ISheEnvironmentalService
{
    private readonly ISheEnvironmentalIncidentRepository _incidentRepository;
    private readonly ISheEnvironmentalMonitoringRecordRepository _monitoringRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheEnvironmentalService> _logger;

    public SheEnvironmentalService(
        ISheEnvironmentalIncidentRepository incidentRepository,
        ISheEnvironmentalMonitoringRecordRepository monitoringRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheEnvironmentalService> logger)
    {
        _incidentRepository = incidentRepository;
        _monitoringRepository = monitoringRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Incidents ──
    public async Task<SheEnvironmentalIncidentDto> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _incidentRepository.GetWithDetailsAsync(id)
            ?? throw new ArgumentException($"Environmental incident with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalIncidentDto?> GetIncidentByNumberAsync(string incidentNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _incidentRepository.GetByNumberAsync(incidentNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByStatusAsync(SheEnvironmentalIncidentStatus status, CancellationToken cancellationToken = default)
        => (await _incidentRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByTypeAsync(SheEnvironmentalIncidentType type, CancellationToken cancellationToken = default)
        => (await _incidentRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => (await _incidentRepository.GetByDateRangeAsync(fromDate, toDate)).ToSummaryDtoList();

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetOpenIncidentsAsync(CancellationToken cancellationToken = default)
        => (await _incidentRepository.GetOpenAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsReportedToEpaAsync(CancellationToken cancellationToken = default)
        => (await _incidentRepository.GetReportedToEpaAsync()).ToSummaryDtoList();

    public async Task<SheEnvironmentalIncidentDto> CreateIncidentAsync(CreateSheEnvironmentalIncidentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.IncidentNumber))
            entity.IncidentNumber = await _incidentRepository.GetNextIncidentNumberAsync();
        await _incidentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental incident created: {IncidentNumber}", entity.IncidentNumber);
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalIncidentDto> UpdateIncidentAsync(UpdateSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _incidentRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Environmental incident with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CloseIncidentAsync(CloseSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _incidentRepository.GetByIdAsync(dto.IncidentId)
            ?? throw new ArgumentException($"Environmental incident with ID '{dto.IncidentId}' not found.");

        entity.Status = SheEnvironmentalIncidentStatus.Closed;
        entity.ClosedById = dto.ClosedById;
        entity.ClosedDate = dto.ClosedDate;
        if (!string.IsNullOrWhiteSpace(dto.CorrectiveActions)) entity.CorrectiveActions = dto.CorrectiveActions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();

        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteIncidentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _incidentRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Environmental incident with ID '{id}' not found.");
        await _incidentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Monitoring ──
    public async Task<SheEnvironmentalMonitoringRecordDto> GetMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _monitoringRepository.GetByIdAsync(id, r => r.Location, r => r.MeasuredBy)
            ?? throw new ArgumentException($"Environmental monitoring record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByTypeAsync(SheEnvironmentalMonitoringType type, CancellationToken cancellationToken = default)
        => (await _monitoringRepository.GetByTypeAsync(type)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => (await _monitoringRepository.GetByDateRangeAsync(fromDate, toDate)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _monitoringRepository.GetByLocationAsync(locationId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetExceedancesAsync(CancellationToken cancellationToken = default)
        => (await _monitoringRepository.GetExceedancesAsync()).Select(e => e.ToDto());

    public async Task<SheEnvironmentalMonitoringRecordDto> CreateMonitoringRecordAsync(CreateSheEnvironmentalMonitoringRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.RecordNumber))
            entity.RecordNumber = await _monitoringRepository.GetNextRecordNumberAsync();
        await _monitoringRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalMonitoringRecordDto> UpdateMonitoringRecordAsync(UpdateSheEnvironmentalMonitoringRecordDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _monitoringRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Environmental monitoring record with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _monitoringRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _monitoringRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Environmental monitoring record with ID '{id}' not found.");
        await _monitoringRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion

#region Occupational Health Service

public class SheOccupationalHealthService : ISheOccupationalHealthService
{
    private readonly ISheOccupationalHealthSurveillanceRepository _surveillanceRepository;
    private readonly ISheFirstAidStationRepository _firstAidRepository;
    private readonly ISheWellnessProgramRepository _wellnessRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheOccupationalHealthService> _logger;

    public SheOccupationalHealthService(
        ISheOccupationalHealthSurveillanceRepository surveillanceRepository,
        ISheFirstAidStationRepository firstAidRepository,
        ISheWellnessProgramRepository wellnessRepository,
        IUnitOfWork unitOfWork,
        ILogger<SheOccupationalHealthService> logger)
    {
        _surveillanceRepository = surveillanceRepository;
        _firstAidRepository = firstAidRepository;
        _wellnessRepository = wellnessRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Health surveillance ──
    public async Task<SheOccupationalHealthSurveillanceDto> GetSurveillanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _surveillanceRepository.GetByIdAsync(id, s => s.Employee, s => s.HealthcareFacility, s => s.RecordedBy)
            ?? throw new ArgumentException($"Health surveillance record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetAllSurveillanceAsync(CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetAllSummaryAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetByEmployeeAsync(employeeId)).ToSummaryDtoList();

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByTypeAsync(SheHealthSurveillanceType type, CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetByTypeAsync(type)).ToSummaryDtoList();

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByResultAsync(SheHealthSurveillanceResult result, CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetByResultAsync(result)).ToSummaryDtoList();

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceDueForExaminationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetDueForExaminationAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceWithRestrictionsAsync(CancellationToken cancellationToken = default)
        => (await _surveillanceRepository.GetWithRestrictionsAsync()).ToSummaryDtoList();

    public async Task<SheOccupationalHealthSurveillanceDto> CreateSurveillanceAsync(CreateSheOccupationalHealthSurveillanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _surveillanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheOccupationalHealthSurveillanceDto> UpdateSurveillanceAsync(UpdateSheOccupationalHealthSurveillanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _surveillanceRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Health surveillance record with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _surveillanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSurveillanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _surveillanceRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Health surveillance record with ID '{id}' not found.");
        await _surveillanceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── First aid stations ──
    public async Task<SheFirstAidStationDto> GetFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _firstAidRepository.GetByIdAsync(id, s => s.Location, s => s.ResponsibleAider)
            ?? throw new ArgumentException($"First aid station with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _firstAidRepository.GetActiveAsync() : await _firstAidRepository.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _firstAidRepository.GetByLocationAsync(locationId)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _firstAidRepository.GetDueForInspectionAsync(daysAhead)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheFirstAidStationDto>> GetUnderStockedStationsAsync(CancellationToken cancellationToken = default)
        => (await _firstAidRepository.GetUnderStockedAsync()).Select(e => e.ToDto());

    public async Task<SheFirstAidStationDto> CreateFirstAidStationAsync(CreateSheFirstAidStationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _firstAidRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheFirstAidStationDto> UpdateFirstAidStationAsync(UpdateSheFirstAidStationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _firstAidRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"First aid station with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _firstAidRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _firstAidRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"First aid station with ID '{id}' not found.");
        await _firstAidRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Wellness programs ──
    public async Task<SheWellnessProgramDto> GetWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _wellnessRepository.GetByIdAsync(id, p => p.Coordinator)
            ?? throw new ArgumentException($"Wellness program with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var entities = activeOnly ? await _wellnessRepository.GetActiveAsync() : await _wellnessRepository.GetAllAsync();
        return entities.Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByStatusAsync(SheWellnessProgramStatus status, CancellationToken cancellationToken = default)
        => (await _wellnessRepository.GetByStatusAsync(status)).Select(e => e.ToDto());

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByTypeAsync(SheWellnessProgramType type, CancellationToken cancellationToken = default)
        => (await _wellnessRepository.GetByTypeAsync(type)).Select(e => e.ToDto());

    public async Task<SheWellnessProgramDto> CreateWellnessProgramAsync(CreateSheWellnessProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, userId);
        await _wellnessRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWellnessProgramDto> UpdateWellnessProgramAsync(UpdateSheWellnessProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _wellnessRepository.GetByIdAsync(dto.Id)
            ?? throw new ArgumentException($"Wellness program with ID '{dto.Id}' not found.");
        entity.UpdateEntity(dto, userId);
        await _wellnessRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _wellnessRepository.GetByIdAsync(id)
            ?? throw new ArgumentException($"Wellness program with ID '{id}' not found.");
        await _wellnessRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
