using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 7: COMPLIANCE & SAFETY SERVICE
// ============================================================================

#region Staff Travel Compliance Service

public interface IStaffTravelComplianceService
{
    // Travel documents
    Task<StaffTravelDocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelDocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelDocumentDto>> GetDocumentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 90, CancellationToken cancellationToken = default);
    Task<StaffTravelDocumentDto> CreateDocumentAsync(CreateStaffTravelDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelDocumentDto> UpdateDocumentAsync(UpdateStaffTravelDocumentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> VerifyDocumentAsync(VerifyStaffTravelDocumentDto verifyDto, Guid verifierEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default);

    // Visa requirements (reference data)
    Task<StaffTravelVisaRequirementDto?> GetVisaRequirementAsync(Guid passportCountryId, Guid destinationCountryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaRequirementDto>> GetVisaRequirementsByDestinationAsync(Guid destinationCountryId, CancellationToken cancellationToken = default);
    Task<StaffTravelVisaRequirementDto> CreateVisaRequirementAsync(CreateStaffTravelVisaRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelVisaRequirementDto> UpdateVisaRequirementAsync(UpdateStaffTravelVisaRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteVisaRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    // Visa applications
    Task<StaffTravelVisaApplicationDto> GetVisaApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetAllVisaApplicationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByStatusAsync(VisaApplicationStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetExpiringVisasAsync(int daysAhead = 90, CancellationToken cancellationToken = default);
    Task<StaffTravelVisaApplicationDto> CreateVisaApplicationAsync(CreateStaffTravelVisaApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelVisaApplicationDto> UpdateVisaApplicationAsync(UpdateStaffTravelVisaApplicationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteVisaApplicationAsync(Guid id, CancellationToken cancellationToken = default);

    // Risk assessments
    Task<StaffTravelRiskAssessmentDto> GetRiskAssessmentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetRiskAssessmentsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelRiskAssessmentDto?> GetCurrentRiskAssessmentAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetAssessmentsRequiringAcknowledgementAsync(CancellationToken cancellationToken = default);
    Task<StaffTravelRiskAssessmentDto> CreateRiskAssessmentAsync(CreateStaffTravelRiskAssessmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelRiskAssessmentDto> UpdateRiskAssessmentAsync(UpdateStaffTravelRiskAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeRiskAssessmentAsync(AcknowledgeStaffTravelRiskAssessmentDto acknowledgeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteRiskAssessmentAsync(Guid id, CancellationToken cancellationToken = default);

    // Alerts
    Task<StaffTravelAlertDto> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAlertSummaryDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAlertSummaryDto>> GetAlertsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAlertSummaryDto>> GetCurrentAlertsForCountryAsync(Guid countryId, CancellationToken cancellationToken = default);
    Task<StaffTravelAlertDto> CreateAlertAsync(CreateStaffTravelAlertDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelAlertDto> UpdateAlertAsync(UpdateStaffTravelAlertDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default);

    // Alert notifications
    Task<StaffTravelAlertNotificationDto> CreateAlertNotificationAsync(CreateStaffTravelAlertNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAlertNotificationDto>> GetNotificationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelAlertNotificationDto>> GetUnacknowledgedNotificationsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeNotificationAsync(AcknowledgeStaffTravelAlertNotificationDto acknowledgeDto, CancellationToken cancellationToken = default);

    // Insurance policies
    Task<StaffTravelInsurancePolicyDto> GetInsuranceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelInsurancePolicyDto>> GetInsuranceByRequestAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<StaffTravelInsurancePolicyDto> CreateInsuranceAsync(CreateStaffTravelInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelInsurancePolicyDto> UpdateInsuranceAsync(UpdateStaffTravelInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteInsuranceAsync(Guid id, CancellationToken cancellationToken = default);

    // Health requirements (reference data)
    Task<StaffTravelHealthRequirementDto> GetHealthRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelHealthRequirementDto>> GetHealthRequirementsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelHealthRequirementDto>> GetMandatoryHealthRequirementsAsync(Guid countryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffTravelHealthRequirementDto>> GetActiveHealthRequirementsAsync(CancellationToken cancellationToken = default);
    Task<StaffTravelHealthRequirementDto> CreateHealthRequirementAsync(CreateStaffTravelHealthRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<StaffTravelHealthRequirementDto> UpdateHealthRequirementAsync(UpdateStaffTravelHealthRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteHealthRequirementAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
