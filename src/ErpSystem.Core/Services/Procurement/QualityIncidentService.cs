using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class QualityIncidentService : IQualityIncidentService
{
    private readonly IQualityIncidentRepository _incidentRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<QualityIncidentService> _logger;

    public QualityIncidentService(
        IQualityIncidentRepository incidentRepository,
        IBusinessPartnerRepository partnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<QualityIncidentService> logger)
    {
        _incidentRepository = incidentRepository;
        _partnerRepository = partnerRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<QualityIncidentDto?> GetByIdAsync(Guid id)
    {
        var incident = await _incidentRepository.GetByIdAsync(id);
        return incident == null ? null : MapToDto(incident);
    }

    public async Task<QualityIncidentDto?> GetByIncidentNumberAsync(string incidentNumber)
    {
        var incident = await _incidentRepository.GetByIncidentNumberAsync(incidentNumber);
        return incident == null ? null : MapToDto(incident);
    }

    public async Task<IEnumerable<QualityIncidentDto>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        var incidents = await _incidentRepository.GetByBusinessPartnerAsync(businessPartnerId);
        return incidents.Select(MapToDto);
    }

    public async Task<IEnumerable<QualityIncidentDto>> GetByStatusAsync(string status)
    {
        var incidents = await _incidentRepository.GetByStatusAsync(status);
        return incidents.Select(MapToDto);
    }

    public async Task<IEnumerable<QualityIncidentDto>> GetBySeverityAsync(string severity)
    {
        var incidents = await _incidentRepository.GetBySeverityAsync(severity);
        return incidents.Select(MapToDto);
    }

    public async Task<IEnumerable<QualityIncidentDto>> GetOpenIncidentsAsync()
    {
        var incidents = await _incidentRepository.GetOpenIncidentsAsync();
        return incidents.Select(MapToDto);
    }

    public async Task<IEnumerable<QualityIncidentDto>> GetRecentIncidentsAsync(Guid businessPartnerId, int days = 90)
    {
        var incidents = await _incidentRepository.GetRecentIncidentsAsync(businessPartnerId, days);
        return incidents.Select(MapToDto);
    }

    public async Task<QualityIncidentDto> CreateAsync(CreateQualityIncidentDto createDto)
    {
        _logger.LogInformation("Creating quality incident for BusinessPartner {BusinessPartnerId}", createDto.BusinessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(createDto.BusinessPartnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {createDto.BusinessPartnerId} not found");
        }

        var incidentNumber = await _incidentRepository.GenerateIncidentNumberAsync();

        var incident = new QualityIncident
        {
            Id = Guid.NewGuid(),
            BusinessPartnerId = createDto.BusinessPartnerId,
            PurchaseOrderId = createDto.PurchaseOrderId,
            IncidentNumber = incidentNumber,
            IncidentDate = createDto.IncidentDate,
            IncidentType = createDto.IncidentType,
            Severity = createDto.Severity,
            Description = createDto.Description,
            QuantityAffected = createDto.QuantityAffected,
            FinancialImpact = createDto.FinancialImpact,
            Status = "Open",
            ReportedDate = DateTime.UtcNow,
            ReportedById = _currentUserProvider.UserId,
            RequiresSupplierResponse = createDto.RequiresSupplierResponse,
            Notes = createDto.Notes,
            TenantId = _currentUserProvider.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _incidentRepository.AddAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quality incident created with ID {IncidentId} and number {IncidentNumber}", incident.Id, incident.IncidentNumber);

        return MapToDto(incident);
    }

    public async Task<QualityIncidentDto> UpdateAsync(Guid id, UpdateQualityIncidentDto updateDto)
    {
        var incident = await _incidentRepository.GetByIdAsync(id);
        if (incident == null)
        {
            throw new ArgumentException($"Quality incident with ID {id} not found");
        }

        if (!string.IsNullOrEmpty(updateDto.Status))
            incident.Status = updateDto.Status;

        if (!string.IsNullOrEmpty(updateDto.Resolution))
            incident.Resolution = updateDto.Resolution;

        if (!string.IsNullOrEmpty(updateDto.RootCause))
            incident.RootCause = updateDto.RootCause;

        if (!string.IsNullOrEmpty(updateDto.CorrectiveAction))
            incident.CorrectiveAction = updateDto.CorrectiveAction;

        if (!string.IsNullOrEmpty(updateDto.PreventiveAction))
            incident.PreventiveAction = updateDto.PreventiveAction;

        incident.UpdatedAt = DateTime.UtcNow;
        incident.LastModifiedById = _currentUserProvider.UserId;

        await _incidentRepository.UpdateAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quality incident {IncidentId} updated", id);

        return MapToDto(incident);
    }

    public async Task<QualityIncidentDto> AcknowledgeAsync(Guid id)
    {
        var incident = await _incidentRepository.GetByIdAsync(id);
        if (incident == null)
        {
            throw new ArgumentException($"Quality incident with ID {id} not found");
        }

        incident.Status = "Acknowledged";
        incident.AcknowledgedDate = DateTime.UtcNow;
        incident.AcknowledgedById = _currentUserProvider.UserId;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.LastModifiedById = _currentUserProvider.UserId;

        await _incidentRepository.UpdateAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quality incident {IncidentId} acknowledged", id);

        return MapToDto(incident);
    }

    public async Task<QualityIncidentDto> ResolveAsync(Guid id, UpdateQualityIncidentDto updateDto)
    {
        var incident = await _incidentRepository.GetByIdAsync(id);
        if (incident == null)
        {
            throw new ArgumentException($"Quality incident with ID {id} not found");
        }

        incident.Status = "Resolved";
        incident.ResolvedDate = DateTime.UtcNow;
        incident.ResolvedById = _currentUserProvider.UserId;

        if (!string.IsNullOrEmpty(updateDto.Resolution))
            incident.Resolution = updateDto.Resolution;

        if (!string.IsNullOrEmpty(updateDto.RootCause))
            incident.RootCause = updateDto.RootCause;

        if (!string.IsNullOrEmpty(updateDto.CorrectiveAction))
            incident.CorrectiveAction = updateDto.CorrectiveAction;

        if (!string.IsNullOrEmpty(updateDto.PreventiveAction))
            incident.PreventiveAction = updateDto.PreventiveAction;

        incident.UpdatedAt = DateTime.UtcNow;
        incident.LastModifiedById = _currentUserProvider.UserId;

        await _incidentRepository.UpdateAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quality incident {IncidentId} resolved", id);

        return MapToDto(incident);
    }

    public async Task<QualityIncidentDto> SubmitSupplierResponseAsync(Guid id, SupplierResponseDto responseDto)
    {
        var incident = await _incidentRepository.GetByIdAsync(id);
        if (incident == null)
        {
            throw new ArgumentException($"Quality incident with ID {id} not found");
        }

        incident.SupplierResponse = responseDto.Response;
        incident.SupplierResponseDate = DateTime.UtcNow;
        incident.UpdatedAt = DateTime.UtcNow;
        incident.LastModifiedById = _currentUserProvider.UserId;

        await _incidentRepository.UpdateAsync(incident);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Supplier response submitted for quality incident {IncidentId}", id);

        return MapToDto(incident);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _incidentRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    private QualityIncidentDto MapToDto(QualityIncident incident)
    {
        return new QualityIncidentDto
        {
            Id = incident.Id,
            BusinessPartnerId = incident.BusinessPartnerId,
            PartnerName = incident.BusinessPartner?.PartnerName,
            PurchaseOrderId = incident.PurchaseOrderId,
            IncidentNumber = incident.IncidentNumber,
            IncidentDate = incident.IncidentDate,
            IncidentType = incident.IncidentType,
            Severity = incident.Severity,
            Description = incident.Description,
            QuantityAffected = incident.QuantityAffected,
            FinancialImpact = incident.FinancialImpact,
            Status = incident.Status,
            ReportedDate = incident.ReportedDate,
            ReportedByName = incident.ReportedBy?.FullName,
            ResolvedDate = incident.ResolvedDate,
            Resolution = incident.Resolution,
            RootCause = incident.RootCause,
            CorrectiveAction = incident.CorrectiveAction,
            RequiresSupplierResponse = incident.RequiresSupplierResponse,
            SupplierResponseDate = incident.SupplierResponseDate,
            SupplierResponse = incident.SupplierResponse,
            Notes = incident.Notes,
            CreatedAt = incident.CreatedAt
        };
    }
}

