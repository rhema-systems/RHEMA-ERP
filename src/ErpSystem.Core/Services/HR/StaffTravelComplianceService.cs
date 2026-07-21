using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
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
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---- Travel documents --------------------------------------------------

    public async Task<StaffTravelDocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel document with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
        => (await _documentRepository.GetAllWithDetailsAsync()).Select(d => d.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetDocumentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _documentRepository.GetByEmployeeIdAsync(employeeId)).Select(d => d.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
        => (await _documentRepository.GetExpiringDocumentsAsync(daysAhead)).Select(d => d.ToDto()).ToList();

    public async Task<StaffTravelDocumentDto> CreateDocumentAsync(CreateStaffTravelDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelDocumentDto> UpdateDocumentAsync(UpdateStaffTravelDocumentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Travel document with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> VerifyDocumentAsync(VerifyStaffTravelDocumentDto verifyDto, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetByIdAsync(verifyDto.DocumentId);
        if (entity == null)
            throw new ArgumentException($"Travel document with ID '{verifyDto.DocumentId}' not found.");

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
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel document with ID '{id}' not found.");

        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa requirements -------------------------------------------------

    public async Task<StaffTravelVisaRequirementDto?> GetVisaRequirementAsync(Guid passportCountryId, Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var entity = await _visaRequirementRepository.GetRequirementAsync(passportCountryId, destinationCountryId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaRequirementDto>> GetVisaRequirementsByDestinationAsync(Guid destinationCountryId, CancellationToken cancellationToken = default)
        => (await _visaRequirementRepository.GetByDestinationAsync(destinationCountryId)).Select(r => r.ToDto()).ToList();

    public async Task<StaffTravelVisaRequirementDto> CreateVisaRequirementAsync(CreateStaffTravelVisaRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelVisaRequirementDto> UpdateVisaRequirementAsync(UpdateStaffTravelVisaRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _visaRequirementRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Visa requirement with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVisaRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _visaRequirementRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Visa requirement with ID '{id}' not found.");

        await _visaRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa applications --------------------------------------------------

    public async Task<StaffTravelVisaApplicationDto> GetVisaApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _visaApplicationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Visa application with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetAllVisaApplicationsAsync(CancellationToken cancellationToken = default)
        => (await _visaApplicationRepository.GetAllWithDetailsAsync()).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByStatusAsync(VisaApplicationStatus status, CancellationToken cancellationToken = default)
        => (await _visaApplicationRepository.GetByStatusAsync(status)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _visaApplicationRepository.GetByRequestIdAsync(requestId)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _visaApplicationRepository.GetByEmployeeIdAsync(employeeId)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetExpiringVisasAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
        => (await _visaApplicationRepository.GetExpiringVisasAsync(daysAhead)).Select(v => v.ToSummaryDto()).ToList();

    public async Task<StaffTravelVisaApplicationDto> CreateVisaApplicationAsync(CreateStaffTravelVisaApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaApplicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelVisaApplicationDto> UpdateVisaApplicationAsync(UpdateStaffTravelVisaApplicationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _visaApplicationRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Visa application with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaApplicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteVisaApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _visaApplicationRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Visa application with ID '{id}' not found.");

        await _visaApplicationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Risk assessments --------------------------------------------------

    public async Task<StaffTravelRiskAssessmentDto> GetRiskAssessmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetRiskAssessmentsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _riskAssessmentRepository.GetByRequestIdAsync(requestId)).Select(a => a.ToDto()).ToList();

    public async Task<StaffTravelRiskAssessmentDto?> GetCurrentRiskAssessmentAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetCurrentForRequestAsync(requestId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetAssessmentsRequiringAcknowledgementAsync(CancellationToken cancellationToken = default)
        => (await _riskAssessmentRepository.GetRequiringAcknowledgementAsync()).Select(a => a.ToDto()).ToList();

    public async Task<StaffTravelRiskAssessmentDto> CreateRiskAssessmentAsync(CreateStaffTravelRiskAssessmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _riskAssessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelRiskAssessmentDto> UpdateRiskAssessmentAsync(UpdateStaffTravelRiskAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Risk assessment with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> AcknowledgeRiskAssessmentAsync(AcknowledgeStaffTravelRiskAssessmentDto acknowledgeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(acknowledgeDto.RiskAssessmentId);
        if (entity == null)
            throw new ArgumentException($"Risk assessment with ID '{acknowledgeDto.RiskAssessmentId}' not found.");

        entity.EmployeeAcknowledged = true;
        entity.AcknowledgedAt = acknowledgeDto.AcknowledgedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteRiskAssessmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");

        await _riskAssessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alerts ------------------------------------------------------------

    public async Task<StaffTravelAlertDto> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _alertRepository.GetWithNotificationsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        => (await _alertRepository.GetActiveAlertsAsync()).Select(a => a.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetAlertsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
        => (await _alertRepository.GetByCountryAsync(countryId)).Select(a => a.ToSummaryDto()).ToList();

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetCurrentAlertsForCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
        => (await _alertRepository.GetCurrentAlertsForCountryAsync(countryId)).Select(a => a.ToSummaryDto()).ToList();

    public async Task<StaffTravelAlertDto> CreateAlertAsync(CreateStaffTravelAlertDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _alertRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelAlertDto> UpdateAlertAsync(UpdateStaffTravelAlertDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _alertRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Travel alert with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _alertRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _alertRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");

        await _alertRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alert notifications -----------------------------------------------

    public async Task<StaffTravelAlertNotificationDto> CreateAlertNotificationAsync(CreateStaffTravelAlertNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetNotificationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _notificationRepository.GetByEmployeeIdAsync(employeeId)).Select(n => n.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetUnacknowledgedNotificationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _notificationRepository.GetUnacknowledgedAsync(employeeId)).Select(n => n.ToDto()).ToList();

    public async Task<bool> AcknowledgeNotificationAsync(AcknowledgeStaffTravelAlertNotificationDto acknowledgeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _notificationRepository.GetByIdAsync(acknowledgeDto.NotificationId);
        if (entity == null)
            throw new ArgumentException($"Alert notification with ID '{acknowledgeDto.NotificationId}' not found.");

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
        var entity = await _insuranceRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Insurance policy with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicyDto>> GetInsuranceByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _insuranceRepository.GetByRequestIdAsync(requestId)).Select(i => i.ToDto()).ToList();

    public async Task<StaffTravelInsurancePolicyDto> CreateInsuranceAsync(CreateStaffTravelInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _insuranceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelInsurancePolicyDto> UpdateInsuranceAsync(UpdateStaffTravelInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Insurance policy with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _insuranceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteInsuranceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Insurance policy with ID '{id}' not found.");

        await _insuranceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Health requirements -----------------------------------------------

    public async Task<StaffTravelHealthRequirementDto> GetHealthRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _healthRequirementRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Health requirement with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetHealthRequirementsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
        => (await _healthRequirementRepository.GetByCountryAsync(countryId)).Select(h => h.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetMandatoryHealthRequirementsAsync(Guid countryId, CancellationToken cancellationToken = default)
        => (await _healthRequirementRepository.GetMandatoryByCountryAsync(countryId)).Select(h => h.ToDto()).ToList();

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetActiveHealthRequirementsAsync(CancellationToken cancellationToken = default)
        => (await _healthRequirementRepository.GetActiveAsync()).Select(h => h.ToDto()).ToList();

    public async Task<StaffTravelHealthRequirementDto> CreateHealthRequirementAsync(CreateStaffTravelHealthRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _healthRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<StaffTravelHealthRequirementDto> UpdateHealthRequirementAsync(UpdateStaffTravelHealthRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _healthRequirementRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Health requirement with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _healthRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteHealthRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _healthRequirementRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Health requirement with ID '{id}' not found.");

        await _healthRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
