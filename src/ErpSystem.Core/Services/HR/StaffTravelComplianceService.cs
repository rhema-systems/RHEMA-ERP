using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 7: COMPLIANCE & SAFETY SERVICE
// ============================================================================

#region Staff Travel Compliance Service

public class StaffTravelComplianceService : IStaffTravelComplianceService
{
    private readonly IStaffTravelDocumentRepository _documentRepository;
    private readonly IStaffTravelVisaRequirementRepository _visaRequirementRepository;
    private readonly IStaffTravelVisaApplicationRepository _visaApplicationRepository;
    private readonly IStaffTravelRiskAssessmentRepository _riskAssessmentRepository;
    private readonly IStaffTravelAlertRepository _alertRepository;
    private readonly IStaffTravelAlertNotificationRepository _notificationRepository;
    private readonly IStaffTravelInsurancePolicyRepository _insuranceRepository;
    private readonly IStaffTravelHealthRequirementRepository _healthRequirementRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelComplianceService> _logger;

    public StaffTravelComplianceService(
        IStaffTravelDocumentRepository documentRepository,
        IStaffTravelVisaRequirementRepository visaRequirementRepository,
        IStaffTravelVisaApplicationRepository visaApplicationRepository,
        IStaffTravelRiskAssessmentRepository riskAssessmentRepository,
        IStaffTravelAlertRepository alertRepository,
        IStaffTravelAlertNotificationRepository notificationRepository,
        IStaffTravelInsurancePolicyRepository insuranceRepository,
        IStaffTravelHealthRequirementRepository healthRequirementRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelComplianceService> logger)
    {
        _documentRepository = documentRepository;
        _visaRequirementRepository = visaRequirementRepository;
        _visaApplicationRepository = visaApplicationRepository;
        _riskAssessmentRepository = riskAssessmentRepository;
        _alertRepository = alertRepository;
        _notificationRepository = notificationRepository;
        _insuranceRepository = insuranceRepository;
        _healthRequirementRepository = healthRequirementRepository;
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

    private async Task<StaffTravelDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Travel document with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelVisaRequirement> GetOwnedVisaRequirementAsync(Guid id)
    {
        var entity = await _visaRequirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Visa requirement with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelVisaApplication> GetOwnedVisaApplicationAsync(Guid id)
    {
        var entity = await _visaApplicationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Visa application with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelRiskAssessment> GetOwnedRiskAssessmentAsync(Guid id)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelAlert> GetOwnedAlertAsync(Guid id)
    {
        var entity = await _alertRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelAlertNotification> GetOwnedNotificationAsync(Guid id)
    {
        var entity = await _notificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Alert notification with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelInsurancePolicy> GetOwnedInsuranceAsync(Guid id)
    {
        var entity = await _insuranceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Insurance policy with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelHealthRequirement> GetOwnedHealthRequirementAsync(Guid id)
    {
        var entity = await _healthRequirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Health requirement with ID '{id}' not found.");
        return entity;
    }

    // ---- Travel documents --------------------------------------------------

    public async Task<StaffTravelDocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetAllWithDetailsAsync())
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetDocumentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetByEmployeeIdAsync(employeeId))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetExpiringDocumentsAsync(daysAhead))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<StaffTravelDocumentDto> CreateDocumentAsync(CreateStaffTravelDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelDocumentDto> UpdateDocumentAsync(UpdateStaffTravelDocumentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> VerifyDocumentAsync(VerifyStaffTravelDocumentDto verifyDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(verifyDto.DocumentId);

        entity.IsVerified = true;
        entity.VerifiedById = verifyDto.VerifiedById;
        entity.VerifiedAt = verifyDto.VerifiedAt;
        entity.UpdatedBy = verifyDto.VerifiedById.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);
        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa requirements -------------------------------------------------

    public async Task<StaffTravelVisaRequirementDto?> GetVisaRequirementAsync(Guid passportCountryId, Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _visaRequirementRepository.GetRequirementAsync(passportCountryId, destinationCountryId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaRequirementDto>> GetVisaRequirementsByDestinationAsync(Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaRequirementRepository.GetByDestinationAsync(destinationCountryId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    public async Task<StaffTravelVisaRequirementDto> CreateVisaRequirementAsync(CreateStaffTravelVisaRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelVisaRequirementDto> UpdateVisaRequirementAsync(UpdateStaffTravelVisaRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaRequirementAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVisaRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaRequirementAsync(id);
        await _visaRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa applications --------------------------------------------------

    public async Task<StaffTravelVisaApplicationDto> GetVisaApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetAllVisaApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetAllWithDetailsAsync())
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByStatusAsync(VisaApplicationStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByStatusAsync(status))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByRequestIdAsync(requestId))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByEmployeeIdAsync(employeeId))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetExpiringVisasAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetExpiringVisasAsync(daysAhead))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelVisaApplicationDto> CreateVisaApplicationAsync(CreateStaffTravelVisaApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaApplicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelVisaApplicationDto> UpdateVisaApplicationAsync(UpdateStaffTravelVisaApplicationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaApplicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVisaApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(id);
        await _visaApplicationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Risk assessments --------------------------------------------------

    public async Task<StaffTravelRiskAssessmentDto> GetRiskAssessmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetRiskAssessmentsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _riskAssessmentRepository.GetByRequestIdAsync(requestId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelRiskAssessmentDto?> GetCurrentRiskAssessmentAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _riskAssessmentRepository.GetCurrentForRequestAsync(requestId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetAssessmentsRequiringAcknowledgementAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _riskAssessmentRepository.GetRequiringAcknowledgementAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelRiskAssessmentDto> CreateRiskAssessmentAsync(CreateStaffTravelRiskAssessmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _riskAssessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelRiskAssessmentDto> UpdateRiskAssessmentAsync(UpdateStaffTravelRiskAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> AcknowledgeRiskAssessmentAsync(AcknowledgeStaffTravelRiskAssessmentDto acknowledgeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(acknowledgeDto.RiskAssessmentId);

        entity.EmployeeAcknowledged = true;
        entity.AcknowledgedAt = acknowledgeDto.AcknowledgedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteRiskAssessmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(id);
        await _riskAssessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alerts ------------------------------------------------------------

    public async Task<StaffTravelAlertDto> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _alertRepository.GetWithNotificationsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetActiveAlertsAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetAlertsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetByCountryAsync(countryId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetCurrentAlertsForCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetCurrentAlertsForCountryAsync(countryId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelAlertDto> CreateAlertAsync(CreateStaffTravelAlertDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _alertRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelAlertDto> UpdateAlertAsync(UpdateStaffTravelAlertDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAlertAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _alertRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAlertAsync(id);
        await _alertRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alert notifications -----------------------------------------------

    public async Task<StaffTravelAlertNotificationDto> CreateAlertNotificationAsync(CreateStaffTravelAlertNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetNotificationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _notificationRepository.GetByEmployeeIdAsync(employeeId))
            .Where(n => n.TenantId == tenantId)
            .Select(n => n.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetUnacknowledgedNotificationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _notificationRepository.GetUnacknowledgedAsync(employeeId))
            .Where(n => n.TenantId == tenantId)
            .Select(n => n.ToDto())
            .ToList();
    }

    public async Task<bool> AcknowledgeNotificationAsync(AcknowledgeStaffTravelAlertNotificationDto acknowledgeDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNotificationAsync(acknowledgeDto.NotificationId);

        entity.IsAcknowledged = true;
        entity.AcknowledgedAt = acknowledgeDto.AcknowledgedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await _notificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Insurance policies ------------------------------------------------

    public async Task<StaffTravelInsurancePolicyDto> GetInsuranceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInsuranceAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicyDto>> GetInsuranceByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _insuranceRepository.GetByRequestIdAsync(requestId))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToDto())
            .ToList();
    }

    public async Task<StaffTravelInsurancePolicyDto> CreateInsuranceAsync(CreateStaffTravelInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _insuranceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelInsurancePolicyDto> UpdateInsuranceAsync(UpdateStaffTravelInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInsuranceAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _insuranceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInsuranceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInsuranceAsync(id);
        await _insuranceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Health requirements -----------------------------------------------

    public async Task<StaffTravelHealthRequirementDto> GetHealthRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHealthRequirementAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetHealthRequirementsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetByCountryAsync(countryId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetMandatoryHealthRequirementsAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetMandatoryByCountryAsync(countryId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetActiveHealthRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetActiveAsync())
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<StaffTravelHealthRequirementDto> CreateHealthRequirementAsync(CreateStaffTravelHealthRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _healthRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelHealthRequirementDto> UpdateHealthRequirementAsync(UpdateStaffTravelHealthRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHealthRequirementAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _healthRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHealthRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHealthRequirementAsync(id);
        await _healthRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
