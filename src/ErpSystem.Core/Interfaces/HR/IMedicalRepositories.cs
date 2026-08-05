using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// HEALTHCARE FACILITY
// ============================================================================

#region Healthcare Facility

public interface IHealthcareFacilityRepository : IGenericRepository<HealthcareFacility>
{
    /// <summary>Returns the facility matching the unique facility code.</summary>
    Task<HealthcareFacility?> GetByFacilityCodeAsync(string facilityCode);

    /// <summary>Returns facilities filtered by type.</summary>
    Task<IEnumerable<HealthcareFacility>> GetByFacilityTypeAsync(HealthFacilityType facilityType);

    /// <summary>Returns all active facilities.</summary>
    Task<IEnumerable<HealthcareFacility>> GetActiveFacilitiesAsync();

    /// <summary>Returns facilities that offer emergency services.</summary>
    Task<IEnumerable<HealthcareFacility>> GetFacilitiesWithEmergencyServicesAsync();

    /// <summary>Returns facilities accredited to accept NHIS.</summary>
    Task<IEnumerable<HealthcareFacility>> GetFacilitiesAcceptingNHISAsync();

    /// <summary>Returns facilities in the specified country.</summary>
    Task<IEnumerable<HealthcareFacility>> GetFacilitiesByCountryAsync(Guid countryId);

    /// <summary>Searches facilities by name, code, or city.</summary>
    Task<IEnumerable<HealthcareFacility>> SearchFacilitiesAsync(string searchTerm);

    /// <summary>Returns a fully-loaded facility including physicians, services, and provider networks.</summary>
    Task<HealthcareFacility?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// PHYSICIAN
// ============================================================================

#region Physician

public interface IPhysicianRepository : IGenericRepository<Physician>
{
    /// <summary>Returns physicians assigned to a facility.</summary>
    Task<IEnumerable<Physician>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns all active physicians.</summary>
    Task<IEnumerable<Physician>> GetActivePhysiciansAsync();

    /// <summary>Returns physicians that have been verified.</summary>
    Task<IEnumerable<Physician>> GetVerifiedPhysiciansAsync();

    /// <summary>Returns physicians filtered by specialization.</summary>
    Task<IEnumerable<Physician>> GetBySpecializationAsync(string specialization);

    /// <summary>Searches physicians by name, email, or license number.</summary>
    Task<IEnumerable<Physician>> SearchPhysiciansAsync(string searchTerm);

    /// <summary>Returns a physician with facility details loaded.</summary>
    Task<Physician?> GetWithDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// FACILITY SERVICE
// ============================================================================

#region Facility Service

public interface IFacilityServiceRepository : IGenericRepository<FacilityService>
{
    /// <summary>Returns all services offered by a facility.</summary>
    Task<IEnumerable<FacilityService>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns services filtered by medical service type.</summary>
    Task<IEnumerable<FacilityService>> GetByServiceTypeAsync(MedicalServiceType serviceType);

    /// <summary>Returns active services for a facility.</summary>
    Task<IEnumerable<FacilityService>> GetActiveServicesAsync(Guid facilityId);

    /// <summary>Returns emergency services for a facility.</summary>
    Task<IEnumerable<FacilityService>> GetEmergencyServicesAsync(Guid facilityId);

    /// <summary>Returns services that require pre-authorization before use.</summary>
    Task<IEnumerable<FacilityService>> GetRequiringPreAuthorizationAsync(Guid facilityId);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER
// ============================================================================

#region Medical Insurance Provider

public interface IMedicalInsuranceProviderRepository : IGenericRepository<MedicalInsuranceProvider>
{
    /// <summary>Returns the provider matching the unique code.</summary>
    Task<MedicalInsuranceProvider?> GetByCodeAsync(string code);

    /// <summary>Returns providers filtered by type.</summary>
    Task<IEnumerable<MedicalInsuranceProvider>> GetByProviderTypeAsync(MedicalInsuranceProviderType providerType);

    /// <summary>Returns all active providers.</summary>
    Task<IEnumerable<MedicalInsuranceProvider>> GetActiveProvidersAsync();

    /// <summary>Searches providers by name or code.</summary>
    Task<IEnumerable<MedicalInsuranceProvider>> SearchProvidersAsync(string searchTerm);

    /// <summary>Returns a fully-loaded provider including plans, network facilities, and documents.</summary>
    Task<MedicalInsuranceProvider?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PLAN
// ============================================================================

#region Medical Insurance Plan

public interface IMedicalInsurancePlanRepository : IGenericRepository<MedicalInsurancePlan>
{
    /// <summary>Returns the plan matching the code within a provider.</summary>
    Task<MedicalInsurancePlan?> GetByCodeAsync(Guid providerId, string code);

    /// <summary>Returns all plans for a provider.</summary>
    Task<IEnumerable<MedicalInsurancePlan>> GetByProviderIdAsync(Guid providerId);

    /// <summary>Returns plans filtered by plan type.</summary>
    Task<IEnumerable<MedicalInsurancePlan>> GetByPlanTypeAsync(MedicalInsurancePlanType planType);

    /// <summary>Returns active plans for a provider.</summary>
    Task<IEnumerable<MedicalInsurancePlan>> GetActivePlansAsync(Guid providerId);

    /// <summary>Returns plans that cover dependents for a provider.</summary>
    Task<IEnumerable<MedicalInsurancePlan>> GetPlansCoveringDependentsAsync(Guid providerId);
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL INSURANCE POLICY
// ============================================================================

#region Employee Medical Insurance Policy

public interface IEmployeeMedicalInsurancePolicyRepository : IGenericRepository<EmployeeMedicalInsurancePolicy>
{
    /// <summary>Returns the policy matching the policy number.</summary>
    Task<EmployeeMedicalInsurancePolicy?> GetByPolicyNumberAsync(string policyNumber);

    /// <summary>Returns all policies for an employee.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns all policies under a provider.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByProviderIdAsync(Guid providerId);

    /// <summary>Returns all policies under a plan.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns policies linked to a benefit tier.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByBenefitTierIdAsync(Guid benefitTierId);

    /// <summary>Returns policies filtered by status.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetByStatusAsync(MedicalInsurancePolicyStatus status);

    /// <summary>Returns the employee's current active policy, or null if none.</summary>
    Task<EmployeeMedicalInsurancePolicy?> GetActivePolicyAsync(Guid employeeId);

    /// <summary>Returns active policies whose end date falls within the specified number of days.</summary>
    Task<IEnumerable<EmployeeMedicalInsurancePolicy>> GetExpiringPoliciesAsync(int daysAhead = 30);

    /// <summary>Returns a fully-loaded policy including dependents, claims, and premium records.</summary>
    Task<EmployeeMedicalInsurancePolicy?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE POLICY DEPENDENT
// ============================================================================

#region Medical Insurance Policy Dependent

public interface IMedicalInsurancePolicyDependentRepository : IGenericRepository<MedicalInsurancePolicyDependent>
{
    /// <summary>Returns all dependents covered under a policy.</summary>
    Task<IEnumerable<MedicalInsurancePolicyDependent>> GetByPolicyIdAsync(Guid policyId);

    /// <summary>Returns all policy enrollments for a dependent.</summary>
    Task<IEnumerable<MedicalInsurancePolicyDependent>> GetByDependentIdAsync(Guid dependentId);

    /// <summary>Returns active dependents under a policy.</summary>
    Task<IEnumerable<MedicalInsurancePolicyDependent>> GetActiveDependentsAsync(Guid policyId);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE CLAIM
// ============================================================================

#region Medical Insurance Claim

public interface IMedicalInsuranceClaimRepository : IGenericRepository<MedicalInsuranceClaim>
{
    /// <summary>Returns the insurance claim matching the insurer claim number.</summary>
    Task<MedicalInsuranceClaim?> GetByInsuranceClaimNumberAsync(string insuranceClaimNumber);

    /// <summary>Returns all insurance claims for a policy.</summary>
    Task<IEnumerable<MedicalInsuranceClaim>> GetByPolicyIdAsync(Guid policyId);

    /// <summary>Returns insurance claims linked to a medical expense claim.</summary>
    Task<IEnumerable<MedicalInsuranceClaim>> GetByMedicalExpenseClaimIdAsync(Guid medicalExpenseClaimId);

    /// <summary>Returns insurance claims filtered by status.</summary>
    Task<IEnumerable<MedicalInsuranceClaim>> GetByStatusAsync(MedicalInsuranceClaimStatus status);

    /// <summary>Returns claims awaiting insurer action (submitted or under review).</summary>
    Task<IEnumerable<MedicalInsuranceClaim>> GetPendingClaimsAsync();
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER FACILITY
// ============================================================================

#region Medical Insurance Provider Facility

public interface IMedicalInsuranceProviderFacilityRepository : IGenericRepository<MedicalInsuranceProviderFacility>
{
    /// <summary>Returns network facilities for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderFacility>> GetByProviderIdAsync(Guid providerId);

    /// <summary>Returns providers that include a facility in their network.</summary>
    Task<IEnumerable<MedicalInsuranceProviderFacility>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns active network facilities for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderFacility>> GetActiveNetworkFacilitiesAsync(Guid providerId);

    /// <summary>Returns preferred network facilities for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderFacility>> GetPreferredProvidersAsync(Guid providerId);

    /// <summary>Returns whether a facility is currently in a provider's active network.</summary>
    Task<bool> IsFacilityInNetworkAsync(Guid providerId, Guid facilityId);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PROVIDER DOCUMENT
// ============================================================================

#region Medical Insurance Provider Document

public interface IMedicalInsuranceProviderDocumentRepository : IGenericRepository<MedicalInsuranceProviderDocument>
{
    /// <summary>Returns all documents for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderDocument>> GetByProviderIdAsync(Guid providerId);

    /// <summary>Returns documents of a specific type for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderDocument>> GetByDocumentTypeAsync(Guid providerId, MedicalInsuranceProviderDocumentType documentType);

    /// <summary>Returns active documents for a provider.</summary>
    Task<IEnumerable<MedicalInsuranceProviderDocument>> GetActiveDocumentsAsync(Guid providerId);

    /// <summary>Returns active documents expiring within the specified number of days.</summary>
    Task<IEnumerable<MedicalInsuranceProviderDocument>> GetExpiringDocumentsAsync(Guid providerId, int daysAhead = 30);
}

#endregion

// ============================================================================
// MEDICAL INSURANCE PREMIUM RECORD
// ============================================================================

#region Medical Insurance Premium Record

public interface IMedicalInsurancePremiumRecordRepository : IGenericRepository<MedicalInsurancePremiumRecord>
{
    /// <summary>Returns premium records for a provider.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByProviderIdAsync(Guid providerId);

    /// <summary>Returns premium records for a plan.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns premium records for a specific employee policy.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByPolicyIdAsync(Guid policyId);

    /// <summary>Returns premium records filtered by payment status.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByStatusAsync(MedicalInsurancePremiumPaymentStatus status);

    /// <summary>Returns premium records whose billing period overlaps the given date range.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetByBillingPeriodAsync(DateOnly periodStart, DateOnly periodEnd);

    /// <summary>Returns unpaid premium records whose due date has passed.</summary>
    Task<IEnumerable<MedicalInsurancePremiumRecord>> GetOverduePremiumsAsync();
}

#endregion

// ============================================================================
// MEDICAL BENEFIT SCHEME
// ============================================================================

#region Medical Benefit Scheme

public interface IMedicalBenefitSchemeRepository : IGenericRepository<MedicalBenefitScheme>
{
    /// <summary>Returns the scheme matching the unique code.</summary>
    Task<MedicalBenefitScheme?> GetByCodeAsync(string code);

    /// <summary>Returns all active benefit schemes.</summary>
    Task<IEnumerable<MedicalBenefitScheme>> GetActiveSchemesAsync();

    /// <summary>Returns a scheme with all tiers loaded.</summary>
    Task<MedicalBenefitScheme?> GetWithTiersAsync(Guid id);
}

#endregion

// ============================================================================
// MEDICAL BENEFIT TIER
// ============================================================================

#region Medical Benefit Tier

public interface IMedicalBenefitTierRepository : IGenericRepository<MedicalBenefitTier>
{
    /// <summary>Returns all tiers for a benefit scheme.</summary>
    Task<IEnumerable<MedicalBenefitTier>> GetBySchemeIdAsync(Guid schemeId);

    /// <summary>Returns tiers mapped to a staff level.</summary>
    Task<IEnumerable<MedicalBenefitTier>> GetByStaffLevelIdAsync(Guid staffLevelId);

    /// <summary>Returns active tiers for a scheme.</summary>
    Task<IEnumerable<MedicalBenefitTier>> GetActiveTiersAsync(Guid schemeId);
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH PROFILE
// ============================================================================

#region Employee Health Profile

public interface IEmployeeHealthProfileRepository : IGenericRepository<EmployeeHealthProfile>
{
    /// <summary>Returns the health profile for an employee (one per employee per tenant).</summary>
    Task<EmployeeHealthProfile?> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns a fully-loaded profile including conditions, allergies, and exams.</summary>
    Task<EmployeeHealthProfile?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// EMPLOYEE HEALTH CONDITION
// ============================================================================

#region Employee Health Condition

public interface IEmployeeHealthConditionRepository : IGenericRepository<EmployeeHealthCondition>
{
    /// <summary>Returns all conditions recorded on a health profile.</summary>
    Task<IEnumerable<EmployeeHealthCondition>> GetByHealthProfileIdAsync(Guid healthProfileId);

    /// <summary>Returns conditions that are not resolved.</summary>
    Task<IEnumerable<EmployeeHealthCondition>> GetActiveConditionsAsync(Guid healthProfileId);

    /// <summary>Returns conditions matching an ICD code.</summary>
    Task<IEnumerable<EmployeeHealthCondition>> GetByICDCodeAsync(string icdCode);
}

#endregion

// ============================================================================
// EMPLOYEE ALLERGY
// ============================================================================

#region Employee Allergy

public interface IEmployeeAllergyRepository : IGenericRepository<EmployeeAllergy>
{
    /// <summary>Returns all allergies recorded on a health profile.</summary>
    Task<IEnumerable<EmployeeAllergy>> GetByHealthProfileIdAsync(Guid healthProfileId);

    /// <summary>Returns active allergies for a health profile.</summary>
    Task<IEnumerable<EmployeeAllergy>> GetActiveAllergiesAsync(Guid healthProfileId);
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL EXAM
// ============================================================================

#region Employee Medical Exam

public interface IEmployeeMedicalExamRepository : IGenericRepository<EmployeeMedicalExam>
{
    /// <summary>Returns all exams for a health profile, ordered newest-first.</summary>
    Task<IEnumerable<EmployeeMedicalExam>> GetByHealthProfileIdAsync(Guid healthProfileId);

    /// <summary>Returns exams conducted at a facility.</summary>
    Task<IEnumerable<EmployeeMedicalExam>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns exams whose exam date falls within the specified range.</summary>
    Task<IEnumerable<EmployeeMedicalExam>> GetByDateRangeAsync(DateOnly startDate, DateOnly endDate);

    /// <summary>Returns exams whose next due date falls within the specified number of days.</summary>
    Task<IEnumerable<EmployeeMedicalExam>> GetDueForExamAsync(int daysAhead = 30);

    /// <summary>Returns an exam with documents loaded.</summary>
    Task<EmployeeMedicalExam?> GetWithDocumentsAsync(Guid id);
}

#endregion

// ============================================================================
// EMPLOYEE MEDICAL EXAM DOCUMENT
// ============================================================================

#region Employee Medical Exam Document

public interface IEmployeeMedicalExamDocumentRepository : IGenericRepository<EmployeeMedicalExamDocument>
{
    /// <summary>Returns all documents attached to an exam.</summary>
    Task<IEnumerable<EmployeeMedicalExamDocument>> GetByExamIdAsync(Guid examId);
}

#endregion

// ============================================================================
// MEDICAL CLAIM PRE-AUTHORIZATION
// ============================================================================

#region Medical Claim Pre-Authorization

public interface IMedicalClaimPreAuthorizationRepository : IGenericRepository<MedicalClaimPreAuthorization>
{
    /// <summary>Returns the pre-authorization matching the authorization number.</summary>
    Task<MedicalClaimPreAuthorization?> GetByAuthorizationNumberAsync(string authorizationNumber);

    /// <summary>Returns pre-authorizations for an employee.</summary>
    Task<IEnumerable<MedicalClaimPreAuthorization>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns pre-authorizations under a policy.</summary>
    Task<IEnumerable<MedicalClaimPreAuthorization>> GetByPolicyIdAsync(Guid policyId);

    /// <summary>Returns pre-authorizations filtered by status.</summary>
    Task<IEnumerable<MedicalClaimPreAuthorization>> GetByStatusAsync(ClaimPreAuthorizationStatus status);

    /// <summary>Returns pre-authorizations awaiting internal approval.</summary>
    Task<IEnumerable<MedicalClaimPreAuthorization>> GetPendingApprovalsAsync();

    /// <summary>Returns approved pre-authorizations expiring within the specified number of days.</summary>
    Task<IEnumerable<MedicalClaimPreAuthorization>> GetExpiringAuthorizationsAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// MEDICAL REFERRAL
// ============================================================================

#region Medical Referral

public interface IMedicalReferralRepository : IGenericRepository<MedicalReferral>
{
    /// <summary>Returns the referral matching the referral number.</summary>
    Task<MedicalReferral?> GetByReferralNumberAsync(string referralNumber);

    /// <summary>Returns referrals for an employee.</summary>
    Task<IEnumerable<MedicalReferral>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns referrals filtered by status.</summary>
    Task<IEnumerable<MedicalReferral>> GetByStatusAsync(MedicalReferralStatus status);

    /// <summary>Returns referrals that are pending or issued but not yet completed.</summary>
    Task<IEnumerable<MedicalReferral>> GetPendingReferralsAsync();

    /// <summary>Returns referrals expiring within the specified number of days.</summary>
    Task<IEnumerable<MedicalReferral>> GetExpiringReferralsAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// MEDICAL APPOINTMENT
// ============================================================================

#region Medical Appointment

public interface IMedicalAppointmentRepository : IGenericRepository<MedicalAppointment>
{
    /// <summary>Returns the appointment matching the appointment number.</summary>
    Task<MedicalAppointment?> GetByAppointmentNumberAsync(string appointmentNumber);

    /// <summary>Returns appointments for an employee.</summary>
    Task<IEnumerable<MedicalAppointment>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns appointments at a facility.</summary>
    Task<IEnumerable<MedicalAppointment>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns appointments with a physician.</summary>
    Task<IEnumerable<MedicalAppointment>> GetByPhysicianIdAsync(Guid physicianId);

    /// <summary>Returns appointments filtered by status.</summary>
    Task<IEnumerable<MedicalAppointment>> GetByStatusAsync(MedicalAppointmentStatus status);

    /// <summary>Returns appointments scheduled within the specified date range.</summary>
    Task<IEnumerable<MedicalAppointment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>Returns upcoming appointments within the specified number of days.</summary>
    Task<IEnumerable<MedicalAppointment>> GetUpcomingAppointmentsAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// NHIS CLAIM
// ============================================================================

#region NHIS Claim

public interface INHISClaimRepository : IGenericRepository<NHISClaim>
{
    /// <summary>Returns the NHIS claim matching the claim number.</summary>
    Task<NHISClaim?> GetByClaimNumberAsync(string claimNumber);

    /// <summary>Returns NHIS claims for an employee.</summary>
    Task<IEnumerable<NHISClaim>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns NHIS claims at a facility.</summary>
    Task<IEnumerable<NHISClaim>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns NHIS claims filtered by status.</summary>
    Task<IEnumerable<NHISClaim>> GetByStatusAsync(NHISClaimStatus status);

    /// <summary>Returns NHIS claims in a submission batch.</summary>
    Task<IEnumerable<NHISClaim>> GetByBatchNumberAsync(string batchNumber);

    /// <summary>Returns NHIS claims linked to an employer expense claim.</summary>
    Task<IEnumerable<NHISClaim>> GetByLinkedMedicalClaimIdAsync(Guid medicalExpenseClaimId);

    /// <summary>Returns NHIS claims awaiting submission or insurer action.</summary>
    Task<IEnumerable<NHISClaim>> GetPendingClaimsAsync();

    /// <summary>Returns an NHIS claim with documents loaded.</summary>
    Task<NHISClaim?> GetWithDocumentsAsync(Guid id);
}

#endregion

// ============================================================================
// NHIS CLAIM DOCUMENT
// ============================================================================

#region NHIS Claim Document

public interface INHISClaimDocumentRepository : IGenericRepository<NHISClaimDocument>
{
    /// <summary>Returns all documents attached to an NHIS claim.</summary>
    Task<IEnumerable<NHISClaimDocument>> GetByNHISClaimIdAsync(Guid nhisClaimId);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM
// ============================================================================

#region Medical Expense Claim

public interface IMedicalExpenseClaimRepository : IGenericRepository<MedicalExpenseClaim>
{
    /// <summary>Returns the expense claim matching the claim number.</summary>
    Task<MedicalExpenseClaim?> GetByClaimNumberAsync(string claimNumber);

    /// <summary>Returns expense claims for an employee.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns expense claims for a dependent.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByDependentIdAsync(Guid dependentId);

    /// <summary>Returns expense claims at a facility.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByFacilityIdAsync(Guid facilityId);

    /// <summary>Returns expense claims filtered by status.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByStatusAsync(ClaimStatus status);

    /// <summary>Returns expense claims filtered by expense type.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByExpenseTypeAsync(MedicalExpenseType expenseType);

    /// <summary>Returns expense claims whose claim date falls within the specified range.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>Returns expense claims awaiting HR/Finance action.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetPendingClaimsAsync();

    /// <summary>Returns expense claims flagged for review.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetFlaggedForReviewAsync();

    /// <summary>Returns expense claims with pending approvals assigned to the specified approver.</summary>
    Task<IEnumerable<MedicalExpenseClaim>> GetPendingApprovalsByApproverAsync(Guid approverId);

    /// <summary>Returns a fully-loaded expense claim including items, approvals, documents, and notes.</summary>
    Task<MedicalExpenseClaim?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE APPROVAL
// ============================================================================

#region Medical Expense Approval

public interface IMedicalExpenseApprovalRepository : IGenericRepository<MedicalExpenseApproval>
{
    /// <summary>Returns all approval records for an expense claim.</summary>
    Task<IEnumerable<MedicalExpenseApproval>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Returns approval records for an approver.</summary>
    Task<IEnumerable<MedicalExpenseApproval>> GetByApproverIdAsync(Guid approverId);

    /// <summary>Returns pending approvals assigned to an approver.</summary>
    Task<IEnumerable<MedicalExpenseApproval>> GetPendingApprovalsByApproverAsync(Guid approverId);

    /// <summary>Returns the most recent approval record for a claim.</summary>
    Task<MedicalExpenseApproval?> GetLatestApprovalAsync(Guid claimId);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE ITEM
// ============================================================================

#region Medical Expense Item

public interface IMedicalExpenseItemRepository : IGenericRepository<MedicalExpenseItem>
{
    /// <summary>Returns all line items for an expense claim.</summary>
    Task<IEnumerable<MedicalExpenseItem>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Returns line items of a specific type for a claim.</summary>
    Task<IEnumerable<MedicalExpenseItem>> GetByItemTypeAsync(Guid claimId, MedicalItemType itemType);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE DOCUMENT
// ============================================================================

#region Medical Expense Document

public interface IMedicalExpenseDocumentRepository : IGenericRepository<MedicalExpenseDocument>
{
    /// <summary>Returns all documents attached to an expense claim.</summary>
    Task<IEnumerable<MedicalExpenseDocument>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Returns documents of a specific type for a claim.</summary>
    Task<IEnumerable<MedicalExpenseDocument>> GetByDocumentTypeAsync(Guid claimId, MedicalDocumentType documentType);
}

#endregion

// ============================================================================
// MEDICAL EXPENSE CLAIM NOTE
// ============================================================================

#region Medical Expense Claim Note

public interface IMedicalExpenseClaimNoteRepository : IGenericRepository<MedicalExpenseClaimNote>
{
    /// <summary>Returns all notes on an expense claim, ordered newest-first.</summary>
    Task<IEnumerable<MedicalExpenseClaimNote>> GetByClaimIdAsync(Guid claimId);

    /// <summary>Returns internal-only notes on a claim (HR/Finance visibility).</summary>
    Task<IEnumerable<MedicalExpenseClaimNote>> GetInternalNotesAsync(Guid claimId);

    /// <summary>Returns notes authored by a specific employee.</summary>
    Task<IEnumerable<MedicalExpenseClaimNote>> GetByAuthorIdAsync(Guid authorId);
}

#endregion
