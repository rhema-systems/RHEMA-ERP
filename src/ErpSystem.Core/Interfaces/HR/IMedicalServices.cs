using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// HEALTHCARE FACILITY SERVICE
// ============================================================================

#region Healthcare Facility Service

public interface IHealthcareFacilityService
{
    // Healthcare facility
    Task<HealthcareFacilityDto> GetFacilityByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HealthcareFacilityDetailDto> GetFacilityWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<HealthcareFacilityDto?> GetFacilityByCodeAsync(string facilityCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<HealthcareFacilitySummaryDto>> GetAllFacilitiesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<HealthcareFacilitySummaryDto>> GetActiveFacilitiesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<HealthcareFacilitySummaryDto>> GetFacilitiesByTypeAsync(HealthFacilityType facilityType, CancellationToken cancellationToken = default);
    Task<IEnumerable<HealthcareFacilitySummaryDto>> GetFacilitiesAcceptingNHISAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<HealthcareFacilitySummaryDto>> SearchFacilitiesAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<PagedResult<HealthcareFacilitySummaryDto>> GetFacilitiesPagedAsync(int pageNumber, int pageSize, string? search = null, CancellationToken cancellationToken = default);
    Task<HealthcareFacilityDto> CreateFacilityAsync(CreateHealthcareFacilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<HealthcareFacilityDto> UpdateFacilityAsync(UpdateHealthcareFacilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFacilityAsync(Guid id, CancellationToken cancellationToken = default);

    // Physician
    Task<PhysicianDto> GetPhysicianByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PhysicianSummaryDto>> GetAllPhysiciansAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PhysicianSummaryDto>> GetPhysiciansByFacilityAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PhysicianSummaryDto>> SearchPhysiciansAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<PhysicianDto> CreatePhysicianAsync(CreatePhysicianDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<PhysicianDto> UpdatePhysicianAsync(UpdatePhysicianDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> VerifyPhysicianAsync(VerifyPhysicianDto verifyDto, CancellationToken cancellationToken = default);
    Task<bool> DeletePhysicianAsync(Guid id, CancellationToken cancellationToken = default);

    // Facility service
    Task<FacilityServiceDto> GetFacilityServiceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<FacilityServiceDto>> GetFacilityServicesAsync(Guid facilityId, CancellationToken cancellationToken = default);
    Task<FacilityServiceDto> CreateFacilityServiceAsync(CreateFacilityServiceDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<FacilityServiceDto> UpdateFacilityServiceAsync(UpdateFacilityServiceDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteFacilityServiceAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE SERVICE
// ============================================================================

#region Medical Insurance Service

public interface IMedicalInsuranceService
{
    // Provider
    Task<MedicalInsuranceProviderDto> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceProviderDetailDto> GetProviderWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceProviderDto?> GetProviderByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetAllProvidersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetProvidersByTypeAsync(MedicalInsuranceProviderType providerType, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> GetActiveProvidersAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderSummaryDto>> SearchProvidersAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceProviderDto> CreateProviderAsync(CreateMedicalInsuranceProviderDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceProviderDto> UpdateProviderAsync(UpdateMedicalInsuranceProviderDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default);

    // Plan
    Task<MedicalInsurancePlanDto> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsurancePlanDto>> GetPlansByProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<MedicalInsurancePlanDto> CreatePlanAsync(CreateMedicalInsurancePlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalInsurancePlanDto> UpdatePlanAsync(UpdateMedicalInsurancePlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePlanAsync(Guid id, CancellationToken cancellationToken = default);

    // Employee policy
    Task<EmployeeMedicalInsurancePolicyDto> GetPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalInsurancePolicyDetailDto> GetPolicyWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetAllPoliciesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetPoliciesByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalInsurancePolicyDto?> GetActivePolicyForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalInsurancePolicySummaryDto>> GetExpiringPoliciesAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalInsurancePolicyDto> CreatePolicyAsync(CreateEmployeeMedicalInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalInsurancePolicyDto> UpdatePolicyAsync(UpdateEmployeeMedicalInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CancelPolicyAsync(CancelEmployeeMedicalInsurancePolicyDto cancelDto, CancellationToken cancellationToken = default);
    Task<bool> DeletePolicyAsync(Guid id, CancellationToken cancellationToken = default);

    // Policy dependent
    Task<MedicalInsurancePolicyDependentDto> AddPolicyDependentAsync(AddMedicalInsurancePolicyDependentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsurancePolicyDependentDto>> GetPolicyDependentsAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<MedicalInsurancePolicyDependentDto> UpdatePolicyDependentAsync(UpdateMedicalInsurancePolicyDependentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePolicyDependentAsync(Guid id, CancellationToken cancellationToken = default);

    // Insurance claim (submitted to provider)
    Task<MedicalInsuranceClaimDto> GetInsuranceClaimByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceClaimSummaryDto>> GetInsuranceClaimsByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceClaimSummaryDto>> GetInsuranceClaimsByExpenseClaimAsync(Guid medicalExpenseClaimId, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceClaimDto> CreateInsuranceClaimAsync(CreateMedicalInsuranceClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceClaimDto> UpdateInsuranceClaimStatusAsync(UpdateMedicalInsuranceClaimStatusDto statusDto, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceClaimDto> RecordInsuranceClaimPaymentAsync(RecordMedicalInsuranceClaimPaymentDto paymentDto, CancellationToken cancellationToken = default);

    // Provider network facility
    Task<MedicalInsuranceProviderFacilityDto> AddNetworkFacilityAsync(AddMedicalInsuranceProviderFacilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderFacilityDto>> GetNetworkFacilitiesByProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<MedicalInsuranceProviderFacilityDto> UpdateNetworkFacilityAsync(UpdateMedicalInsuranceProviderFacilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveNetworkFacilityAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsFacilityInNetworkAsync(Guid providerId, Guid facilityId, CancellationToken cancellationToken = default);

    // Provider document
    Task<MedicalInsuranceProviderDocumentDto> AddProviderDocumentAsync(CreateMedicalInsuranceProviderDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsuranceProviderDocumentDto>> GetProviderDocumentsAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProviderDocumentAsync(Guid id, CancellationToken cancellationToken = default);

    // Premium record
    Task<MedicalInsurancePremiumRecordDto> GetPremiumRecordByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsurancePremiumRecordDto>> GetPremiumRecordsByProviderAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<MedicalInsurancePremiumRecordDto> CreatePremiumRecordAsync(CreateMedicalInsurancePremiumRecordDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalInsurancePremiumRecordDto> RecordPremiumPaymentAsync(RecordMedicalInsurancePremiumPaymentDto paymentDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalInsurancePremiumRecordSummaryDto>> GetOverduePremiumsAsync(CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MEDICAL BENEFIT SCHEME SERVICE
// ============================================================================

#region Medical Benefit Scheme Service

public interface IMedicalBenefitSchemeService
{
    Task<MedicalBenefitSchemeDto> GetSchemeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalBenefitSchemeDetailDto> GetSchemeWithTiersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalBenefitSchemeDto?> GetSchemeByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalBenefitSchemeSummaryDto>> GetAllSchemesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalBenefitSchemeSummaryDto>> GetActiveSchemesAsync(CancellationToken cancellationToken = default);
    Task<MedicalBenefitSchemeDto> CreateSchemeAsync(CreateMedicalBenefitSchemeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalBenefitSchemeDto> UpdateSchemeAsync(UpdateMedicalBenefitSchemeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSchemeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MedicalBenefitTierDto> GetTierByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalBenefitTierDto>> GetTiersBySchemeAsync(Guid schemeId, CancellationToken cancellationToken = default);
    Task<MedicalBenefitTierDto> CreateTierAsync(CreateMedicalBenefitTierDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalBenefitTierDto> UpdateTierAsync(UpdateMedicalBenefitTierDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTierAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH SERVICE
// ============================================================================

#region Employee Health Service

public interface IEmployeeHealthService
{
    // Health profile
    Task<IEnumerable<EmployeeHealthProfileDto>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
    Task<EmployeeHealthProfileDto> GetProfileByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeHealthProfileDetailDto> GetProfileWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeHealthProfileDto?> GetProfileByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeHealthProfileDto> CreateProfileAsync(CreateEmployeeHealthProfileDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<EmployeeHealthProfileDto> UpdateProfileAsync(UpdateEmployeeHealthProfileDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default);

    // Health condition
    Task<EmployeeHealthConditionDto> AddConditionAsync(CreateEmployeeHealthConditionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeHealthConditionDto>> GetConditionsAsync(Guid healthProfileId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeHealthConditionDto>> GetActiveConditionsAsync(Guid healthProfileId, CancellationToken cancellationToken = default);
    Task<EmployeeHealthConditionDto> UpdateConditionAsync(UpdateEmployeeHealthConditionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteConditionAsync(Guid id, CancellationToken cancellationToken = default);

    // Allergy
    Task<EmployeeAllergyDto> AddAllergyAsync(CreateEmployeeAllergyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeAllergyDto>> GetAllergiesAsync(Guid healthProfileId, CancellationToken cancellationToken = default);
    Task<EmployeeAllergyDto> UpdateAllergyAsync(UpdateEmployeeAllergyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAllergyAsync(Guid id, CancellationToken cancellationToken = default);

    // Medical exam
    Task<EmployeeMedicalExamDto> GetExamByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalExamDetailDto> GetExamWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalExamSummaryDto>> GetExamsByProfileAsync(Guid healthProfileId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalExamSummaryDto>> GetExamsDueAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalExamDto> CreateExamAsync(CreateEmployeeMedicalExamDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<EmployeeMedicalExamDto> UpdateExamAsync(UpdateEmployeeMedicalExamDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteExamAsync(Guid id, CancellationToken cancellationToken = default);

    // Exam document
    Task<EmployeeMedicalExamDocumentDto> AddExamDocumentAsync(CreateEmployeeMedicalExamDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeMedicalExamDocumentDto>> GetExamDocumentsAsync(Guid examId, CancellationToken cancellationToken = default);
    Task<bool> DeleteExamDocumentAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MEDICAL CLINICAL SERVICE
// ============================================================================

#region Medical Clinical Service

public interface IMedicalClinicalService
{
    // Pre-authorization
    Task<MedicalClaimPreAuthorizationDto> GetPreAuthorizationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalClaimPreAuthorizationDto?> GetPreAuthorizationByNumberAsync(string authorizationNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetAllPreAuthorizationsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetPreAuthorizationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalClaimPreAuthorizationSummaryDto>> GetPendingPreAuthorizationsAsync(CancellationToken cancellationToken = default);
    Task<MedicalClaimPreAuthorizationDto> CreatePreAuthorizationAsync(CreateMedicalClaimPreAuthorizationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalClaimPreAuthorizationDto> UpdatePreAuthorizationAsync(UpdateMedicalClaimPreAuthorizationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ApprovePreAuthorizationAsync(ApproveMedicalClaimPreAuthorizationDto approveDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RejectPreAuthorizationAsync(RejectMedicalClaimPreAuthorizationDto rejectDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeletePreAuthorizationAsync(Guid id, CancellationToken cancellationToken = default);

    // Referral
    Task<MedicalReferralDto> GetReferralByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalReferralDto?> GetReferralByNumberAsync(string referralNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalReferralSummaryDto>> GetAllReferralsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalReferralSummaryDto>> GetReferralsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalReferralSummaryDto>> GetPendingReferralsAsync(CancellationToken cancellationToken = default);
    Task<MedicalReferralDto> CreateReferralAsync(CreateMedicalReferralDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalReferralDto> UpdateReferralAsync(UpdateMedicalReferralDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> UpdateReferralStatusAsync(UpdateMedicalReferralStatusDto statusDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteReferralAsync(CompleteMedicalReferralDto completeDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteReferralAsync(Guid id, CancellationToken cancellationToken = default);

    // Appointment
    Task<MedicalAppointmentDto> GetAppointmentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalAppointmentDto?> GetAppointmentByNumberAsync(string appointmentNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalAppointmentSummaryDto>> GetAllAppointmentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalAppointmentSummaryDto>> GetAppointmentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalAppointmentSummaryDto>> GetUpcomingAppointmentsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<MedicalAppointmentDto> CreateAppointmentAsync(CreateMedicalAppointmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalAppointmentDto> UpdateAppointmentAsync(UpdateMedicalAppointmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAppointmentStatusAsync(UpdateMedicalAppointmentStatusDto statusDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CancelAppointmentAsync(CancelMedicalAppointmentDto cancelDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CheckInAppointmentAsync(CheckInMedicalAppointmentDto checkInDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CheckOutAppointmentAsync(CheckOutMedicalAppointmentDto checkOutDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAppointmentAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// NHIS SERVICE
// ============================================================================

#region NHIS Service

public interface INHISService
{
    Task<NHISClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NHISClaimDetailDto> GetClaimWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NHISClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<NHISClaimSummaryDto>> GetAllClaimsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<NHISClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<NHISClaimSummaryDto>> GetPendingClaimsAsync(CancellationToken cancellationToken = default);
    Task<NHISClaimDto> CreateClaimAsync(CreateNHISClaimDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<NHISClaimDto> UpdateClaimAsync(UpdateNHISClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> UpdateClaimStatusAsync(UpdateNHISClaimStatusDto statusDto, CancellationToken cancellationToken = default);
    Task<bool> SubmitClaimAsync(SubmitNHISClaimDto submitDto, CancellationToken cancellationToken = default);
    Task<bool> RecordClaimPaymentAsync(RecordNHISClaimPaymentDto paymentDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default);

    Task<NHISClaimDocumentDto> AddClaimDocumentAsync(CreateNHISClaimDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<NHISClaimDocumentDto>> GetClaimDocumentsAsync(Guid nhisClaimId, CancellationToken cancellationToken = default);
    Task<bool> DeleteClaimDocumentAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM SERVICE
// ============================================================================

#region Medical Expense Claim Service

public interface IMedicalExpenseClaimService
{
    Task<MedicalExpenseClaimDto> GetClaimByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalExpenseClaimDetailDto> GetClaimWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MedicalExpenseClaimDto?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetClaimsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<PagedResult<MedicalExpenseClaimSummaryDto>> GetClaimsPagedAsync(int pageNumber, int pageSize, ClaimStatus? status = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetPendingClaimsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseClaimSummaryDto>> GetFlaggedClaimsAsync(CancellationToken cancellationToken = default);
    Task<MedicalExpenseClaimDto> CreateClaimAsync(CreateMedicalExpenseClaimDto createDto, Guid employeeId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<MedicalExpenseClaimDto> UpdateClaimAsync(UpdateMedicalExpenseClaimDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ProcessApprovalAsync(ProcessMedicalExpenseClaimDto processDto, Guid tenantId, Guid approverEmployeeId, Guid processedByUserId, CancellationToken cancellationToken = default);
    Task<bool> ProcessPaymentAsync(ProcessMedicalExpensePaymentDto paymentDto, CancellationToken cancellationToken = default);
    Task<bool> FlagClaimAsync(FlagMedicalExpenseClaimDto flagDto, CancellationToken cancellationToken = default);
    Task<bool> UnflagClaimAsync(UnflagMedicalExpenseClaimDto unflagDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteClaimAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MedicalExpenseItemDto> AddItemAsync(CreateMedicalExpenseItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseItemDto>> GetItemsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<MedicalExpenseItemDto> UpdateItemAsync(UpdateMedicalExpenseItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MedicalExpenseDocumentDto> AddDocumentAsync(CreateMedicalExpenseDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseDocumentDto>> GetDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MedicalExpenseClaimNoteDto> AddNoteAsync(AddMedicalExpenseClaimNoteDto createDto, Guid tenantId, Guid authorEmployeeId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseClaimNoteDto>> GetNotesAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalExpenseClaimNoteDto>> GetInternalNotesAsync(Guid claimId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MEDICAL DASHBOARD SERVICE
// ============================================================================

#region Medical Dashboard Service

public interface IMedicalDashboardService
{
    /// <summary>Returns aggregated, tenant-scoped medical KPIs for the dashboard.</summary>
    Task<MedicalDashboardDto> GetDashboardAsync(int upcomingDays = 30, CancellationToken cancellationToken = default);
}

#endregion
