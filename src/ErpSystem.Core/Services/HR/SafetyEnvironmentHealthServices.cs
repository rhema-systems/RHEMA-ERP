using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheWasteManagementService> _logger;

    public SheWasteManagementService(
        ISheWasteTypeRepository wasteTypeRepository,
        ISheWasteDisposalRecordRepository disposalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheWasteManagementService> logger)
    {
        _wasteTypeRepository = wasteTypeRepository;
        _disposalRepository = disposalRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheWasteType> GetOwnedWasteTypeAsync(Guid id)
    {
        var entity = await _wasteTypeRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Waste type with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheWasteDisposalRecord> GetOwnedDisposalAsync(Guid id)
    {
        var entity = await _disposalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Waste disposal record with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateDisposalRecordNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"WD-{year}-";
        var last = await _disposalRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.RecordNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RecordNumber)
            .Select(r => r.RecordNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    // ── Waste types ──
    public async Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _wasteTypeRepository.GetActiveAsync() : await _wasteTypeRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheWasteTypeDto>> GetWasteTypesByClassificationAsync(SheWasteClassification classification, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _wasteTypeRepository.GetByClassificationAsync(classification))
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.ToDto());
    }

    public async Task<SheWasteTypeDto> CreateWasteTypeAsync(CreateSheWasteTypeDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var code = dto.Code.Trim();
        var exists = await _wasteTypeRepository.GetQueryable()
            .AnyAsync(t => t.TenantId == tenantId && t.Code == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A waste type with code '{code}' already exists for this tenant.");

        var entity = dto.ToEntity(tenantId, userId);
        await _wasteTypeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWasteTypeDto> UpdateWasteTypeAsync(UpdateSheWasteTypeDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWasteTypeAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _wasteTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWasteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWasteTypeAsync(id);
        await _wasteTypeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Disposal records ──
    public async Task<SheWasteDisposalRecordDto> GetDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _disposalRepository.GetByIdAsync(id, r => r.WasteType, r => r.Location, r => r.WasteContractor, r => r.RecordedBy);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Waste disposal record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByWasteTypeAsync(Guid wasteTypeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _disposalRepository.GetByWasteTypeAsync(wasteTypeId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _disposalRepository.GetByDateRangeAsync(fromDate, toDate))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheWasteDisposalRecordSummaryDto>> GetDisposalRecordsByContractorAsync(Guid contractorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _disposalRepository.GetByContractorAsync(contractorId))
            .Where(e => e.TenantId == tenantId)
            .ToSummaryDtoList();
    }

    public async Task<SheWasteDisposalRecordDto> CreateDisposalRecordAsync(CreateSheWasteDisposalRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await GetOwnedWasteTypeAsync(dto.WasteTypeId);
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.RecordNumber))
            entity.RecordNumber = await GenerateDisposalRecordNumberAsync(tenantId, cancellationToken);
        else
        {
            var number = entity.RecordNumber.Trim();
            var exists = await _disposalRepository.GetQueryable()
                .AnyAsync(r => r.TenantId == tenantId && r.RecordNumber == number, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"A waste disposal record with number '{number}' already exists for this tenant.");
        }

        await _disposalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWasteDisposalRecordDto> UpdateDisposalRecordAsync(UpdateSheWasteDisposalRecordDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDisposalAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _disposalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDisposalRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDisposalAsync(id);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheEnvironmentalService> _logger;

    public SheEnvironmentalService(
        ISheEnvironmentalIncidentRepository incidentRepository,
        ISheEnvironmentalMonitoringRecordRepository monitoringRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheEnvironmentalService> logger)
    {
        _incidentRepository = incidentRepository;
        _monitoringRepository = monitoringRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheEnvironmentalIncident> GetOwnedIncidentAsync(Guid id)
    {
        var entity = await _incidentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Environmental incident with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheEnvironmentalMonitoringRecord> GetOwnedMonitoringAsync(Guid id)
    {
        var entity = await _monitoringRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Environmental monitoring record with ID '{id}' not found.");
        return entity;
    }

    private async Task<string> GenerateIncidentNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"ENV-{year}-";
        var last = await _incidentRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.IncidentNumber.StartsWith(prefix))
            .OrderByDescending(e => e.IncidentNumber)
            .Select(e => e.IncidentNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    private async Task<string> GenerateMonitoringRecordNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"EM-{year}-";
        var last = await _monitoringRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.RecordNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RecordNumber)
            .Select(r => r.RecordNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var n))
            next = n + 1;
        return $"{prefix}{next:D4}";
    }

    // ── Incidents ──
    public async Task<SheEnvironmentalIncidentDto> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _incidentRepository.GetWithDetailsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Environmental incident with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalIncidentDto?> GetIncidentByNumberAsync(string incidentNumber, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _incidentRepository.GetByNumberAsync(incidentNumber);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByStatusAsync(SheEnvironmentalIncidentStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByTypeAsync(SheEnvironmentalIncidentType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetByDateRangeAsync(fromDate, toDate)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetOpenIncidentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetOpenAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheEnvironmentalIncidentSummaryDto>> GetIncidentsReportedToEpaAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _incidentRepository.GetReportedToEpaAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<SheEnvironmentalIncidentDto> CreateIncidentAsync(CreateSheEnvironmentalIncidentDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.IncidentNumber))
            entity.IncidentNumber = await GenerateIncidentNumberAsync(tenantId, cancellationToken);
        else
        {
            var number = entity.IncidentNumber.Trim();
            var exists = await _incidentRepository.GetQueryable()
                .AnyAsync(e => e.TenantId == tenantId && e.IncidentNumber == number, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"An environmental incident with number '{number}' already exists for this tenant.");
        }

        await _incidentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Environmental incident created: {IncidentNumber}", entity.IncidentNumber);
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalIncidentDto> UpdateIncidentAsync(UpdateSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _incidentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> CloseIncidentAsync(CloseSheEnvironmentalIncidentDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedIncidentAsync(dto.IncidentId);

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
        var entity = await GetOwnedIncidentAsync(id);
        await _incidentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Monitoring ──
    public async Task<SheEnvironmentalMonitoringRecordDto> GetMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _monitoringRepository.GetByIdAsync(id, r => r.Location, r => r.MeasuredBy);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Environmental monitoring record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByTypeAsync(SheEnvironmentalMonitoringType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _monitoringRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _monitoringRepository.GetByDateRangeAsync(fromDate, toDate)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetMonitoringByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _monitoringRepository.GetByLocationAsync(locationId)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheEnvironmentalMonitoringRecordDto>> GetExceedancesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _monitoringRepository.GetExceedancesAsync()).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheEnvironmentalMonitoringRecordDto> CreateMonitoringRecordAsync(CreateSheEnvironmentalMonitoringRecordDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = dto.ToEntity(tenantId, userId);
        if (string.IsNullOrWhiteSpace(entity.RecordNumber))
            entity.RecordNumber = await GenerateMonitoringRecordNumberAsync(tenantId, cancellationToken);
        else
        {
            var number = entity.RecordNumber.Trim();
            var exists = await _monitoringRepository.GetQueryable()
                .AnyAsync(r => r.TenantId == tenantId && r.RecordNumber == number, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"An environmental monitoring record with number '{number}' already exists for this tenant.");
        }

        await _monitoringRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheEnvironmentalMonitoringRecordDto> UpdateMonitoringRecordAsync(UpdateSheEnvironmentalMonitoringRecordDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMonitoringAsync(dto.Id);
        entity.UpdateEntity(dto, userId);
        await _monitoringRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMonitoringRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedMonitoringAsync(id);
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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SheOccupationalHealthService> _logger;

    public SheOccupationalHealthService(
        ISheOccupationalHealthSurveillanceRepository surveillanceRepository,
        ISheFirstAidStationRepository firstAidRepository,
        ISheWellnessProgramRepository wellnessRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<SheOccupationalHealthService> logger)
    {
        _surveillanceRepository = surveillanceRepository;
        _firstAidRepository = firstAidRepository;
        _wellnessRepository = wellnessRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<SheOccupationalHealthSurveillance> GetOwnedSurveillanceAsync(Guid id)
    {
        var entity = await _surveillanceRepository.GetByIdAsync(id, s => s.Employee, s => s.HealthcareFacility!, s => s.RecordedBy);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Health surveillance record with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheFirstAidStation> GetOwnedFirstAidStationAsync(Guid id)
    {
        var entity = await _firstAidRepository.GetByIdAsync(id, s => s.Location, s => s.ResponsibleAider!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"First aid station with ID '{id}' not found.");
        return entity;
    }

    private async Task<SheWellnessProgram> GetOwnedWellnessProgramAsync(Guid id)
    {
        var entity = await _wellnessRepository.GetByIdAsync(id, p => p.Coordinator!);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Wellness program with ID '{id}' not found.");
        return entity;
    }

    // Guards body-supplied FKs so a bad id surfaces as 404 instead of SQL 547/HTTP 500; fetching on
    // this context also lets change-tracker fixup resolve the navigation for the write response.
    private async Task<Employee> GetOwnedEmployeeAsync(Guid id)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(id);
        if (employee == null || employee.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{id}' not found.");
        return employee;
    }

    private async Task<Location> GetOwnedLocationAsync(Guid id)
    {
        var location = await _unitOfWork.Repository<Location>().GetByIdAsync(id);
        if (location == null || location.TenantId != GetTenantId())
            throw new ArgumentException($"Location with ID '{id}' not found.");
        return location;
    }

    // The facility register belongs to the Medical module; SHE surveillance references it by id
    // (the agreed bridge), so the FK is validated against the owned tenant like any other.
    private async Task<HealthcareFacility> GetOwnedFacilityAsync(Guid id)
    {
        var facility = await _unitOfWork.Repository<HealthcareFacility>().GetByIdAsync(id);
        if (facility == null || facility.TenantId != GetTenantId())
            throw new ArgumentException($"Healthcare facility with ID '{id}' not found.");
        return facility;
    }

    // ── Health surveillance ──
    public async Task<SheOccupationalHealthSurveillanceDto> GetSurveillanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _surveillanceRepository.GetByIdAsync(id, s => s.Employee, s => s.HealthcareFacility, s => s.RecordedBy);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Health surveillance record with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetAllSurveillanceAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetAllSummaryAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetByEmployeeAsync(employeeId)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByTypeAsync(SheHealthSurveillanceType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceByResultAsync(SheHealthSurveillanceResult result, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetByResultAsync(result)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceDueForExaminationAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetDueForExaminationAsync(daysAhead)).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<SheOccupationalHealthSurveillanceSummaryDto>> GetSurveillanceWithRestrictionsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _surveillanceRepository.GetWithRestrictionsAsync()).Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<SheOccupationalHealthSurveillanceDto> CreateSurveillanceAsync(CreateSheOccupationalHealthSurveillanceDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var number = dto.SurveillanceNumber.Trim();
        var exists = await _surveillanceRepository.GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.SurveillanceNumber == number, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A health surveillance record with number '{number}' already exists for this tenant.");

        await GetOwnedEmployeeAsync(dto.EmployeeId);
        await GetOwnedEmployeeAsync(dto.RecordedById);
        if (dto.HealthcareFacilityId.HasValue) await GetOwnedFacilityAsync(dto.HealthcareFacilityId.Value);

        dto.SurveillanceNumber = number;
        var entity = dto.ToEntity(tenantId, userId);
        await _surveillanceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheOccupationalHealthSurveillanceDto> UpdateSurveillanceAsync(UpdateSheOccupationalHealthSurveillanceDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSurveillanceAsync(dto.Id);
        if (dto.HealthcareFacilityId.HasValue && dto.HealthcareFacilityId != entity.HealthcareFacilityId)
            await GetOwnedFacilityAsync(dto.HealthcareFacilityId.Value);
        entity.UpdateEntity(dto, userId);
        await _surveillanceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteSurveillanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSurveillanceAsync(id);
        await _surveillanceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── First aid stations ──
    public async Task<SheFirstAidStationDto> GetFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _firstAidRepository.GetByIdAsync(id, s => s.Location, s => s.ResponsibleAider);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"First aid station with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _firstAidRepository.GetActiveAsync() : await _firstAidRepository.GetAllListAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _firstAidRepository.GetByLocationAsync(locationId)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetFirstAidStationsDueForInspectionAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _firstAidRepository.GetDueForInspectionAsync(daysAhead)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheFirstAidStationDto>> GetUnderStockedStationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _firstAidRepository.GetUnderStockedAsync()).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheFirstAidStationDto> CreateFirstAidStationAsync(CreateSheFirstAidStationDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var code = dto.StationCode.Trim();
        var exists = await _firstAidRepository.GetQueryable()
            .AnyAsync(s => s.TenantId == tenantId && s.StationCode == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A first aid station with code '{code}' already exists for this tenant.");

        await GetOwnedLocationAsync(dto.LocationId);
        if (dto.ResponsibleAiderId.HasValue) await GetOwnedEmployeeAsync(dto.ResponsibleAiderId.Value);

        dto.StationCode = code;
        var entity = dto.ToEntity(tenantId, userId);
        await _firstAidRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheFirstAidStationDto> UpdateFirstAidStationAsync(UpdateSheFirstAidStationDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFirstAidStationAsync(dto.Id);
        if (dto.LocationId != entity.LocationId)
            await GetOwnedLocationAsync(dto.LocationId);
        if (dto.ResponsibleAiderId.HasValue && dto.ResponsibleAiderId != entity.ResponsibleAiderId)
            await GetOwnedEmployeeAsync(dto.ResponsibleAiderId.Value);
        entity.UpdateEntity(dto, userId);
        await _firstAidRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteFirstAidStationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedFirstAidStationAsync(id);
        await _firstAidRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── Wellness programs ──
    public async Task<SheWellnessProgramDto> GetWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _wellnessRepository.GetByIdAsync(id, p => p.Coordinator);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Wellness program with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = activeOnly ? await _wellnessRepository.GetActiveAsync() : await _wellnessRepository.GetAllListAsync();
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByStatusAsync(SheWellnessProgramStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _wellnessRepository.GetByStatusAsync(status)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<IEnumerable<SheWellnessProgramDto>> GetWellnessProgramsByTypeAsync(SheWellnessProgramType type, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _wellnessRepository.GetByTypeAsync(type)).Where(e => e.TenantId == tenantId).Select(e => e.ToDto());
    }

    public async Task<SheWellnessProgramDto> CreateWellnessProgramAsync(CreateSheWellnessProgramDto dto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var code = dto.ProgramCode.Trim();
        var exists = await _wellnessRepository.GetQueryable()
            .AnyAsync(p => p.TenantId == tenantId && p.ProgramCode == code, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A wellness program with code '{code}' already exists for this tenant.");

        if (dto.CoordinatorId.HasValue) await GetOwnedEmployeeAsync(dto.CoordinatorId.Value);

        dto.ProgramCode = code;
        var entity = dto.ToEntity(tenantId, userId);
        await _wellnessRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<SheWellnessProgramDto> UpdateWellnessProgramAsync(UpdateSheWellnessProgramDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWellnessProgramAsync(dto.Id);
        if (dto.CoordinatorId.HasValue && dto.CoordinatorId != entity.CoordinatorId)
            await GetOwnedEmployeeAsync(dto.CoordinatorId.Value);
        entity.UpdateEntity(dto, userId);
        await _wellnessRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWellnessProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedWellnessProgramAsync(id);
        await _wellnessRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
