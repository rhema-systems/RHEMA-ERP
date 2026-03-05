using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class MedicalExpenseClaimListDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Department { get; set; }
    public DateTime ClaimDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName { get; set; }
    public string PatientName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountClaimed { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public ClaimStatus Status { get; set; }
    public string StatusName { get; set; }
}

// Detail DTO
public class MedicalExpenseClaimDetailDto
{
    public Guid Id { get; set; }
    public string ClaimNumber { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }

    // Dependent Info
    public Guid? DependentId { get; set; }
    public string DependentName { get; set; }
    public string PatientName { get; set; }
    public string RelationshipToEmployee { get; set; }

    // Medical Policy
    public Guid? MedicalPolicyId { get; set; }
    public string MedicalPolicyName { get; set; }
    public decimal? PolicyCoverageLimit { get; set; }
    public decimal? EmployeeUtilizedAmount { get; set; }
    public decimal? RemainingLimit { get; set; }

    // Claim Details
    public DateTime ClaimDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; }
    public string ExpenseTypeName { get; set; }
    public string Description { get; set; }

    // Treatment Details
    public DateTime TreatmentDate { get; set; }
    public string HealthcareFacility { get; set; }
    public string FacilityAddress { get; set; }
    public string DoctorName { get; set; }
    public string Diagnosis { get; set; }
    public string TreatmentReceived { get; set; }

    // Financial Details
    public decimal TotalAmount { get; set; }
    public decimal AmountClaimed { get; set; }
    public string Currency { get; set; }

    // Insurance
    public bool CoveredByInsurance { get; set; }
    public string InsuranceProvider { get; set; }
    public string InsurancePolicyNumber { get; set; }
    public string InsuranceClaimNumber { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }

    // Approval Workflow
    public ClaimStatus Status { get; set; }
    public string StatusName { get; set; }

    // Supervisor Approval
    public DateTime? SupervisorApprovalDate { get; set; }
    public Guid? SupervisorApprovedById { get; set; }
    public string SupervisorApprovedByName { get; set; }
    public string SupervisorComments { get; set; }

    // HR Approval
    public DateTime? HrApprovalDate { get; set; }
    public Guid? HrApprovedById { get; set; }
    public string HrApprovedByName { get; set; }
    public string HrComments { get; set; }

    // Finance Approval
    public DateTime? FinanceApprovalDate { get; set; }
    public Guid? FinanceApprovedById { get; set; }
    public string FinanceApprovedByName { get; set; }
    public string FinanceComments { get; set; }

    // Approved Amount
    public decimal? ApprovedAmount { get; set; }
    public string RejectionReason { get; set; }

    // Payment
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string PaymentReference { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string PaymentMethodName { get; set; }

    public string AdditionalNotes { get; set; }

    // Collections
    public List<MedicalExpenseItemDto> Items { get; set; }
    public List<MedicalExpenseDocumentDto> Documents { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateMedicalExpenseClaimDto
{
    public Guid? DependentId { get; set; }
    public string PatientName { get; set; }
    public string RelationshipToEmployee { get; set; }
    public DateTime ClaimDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; }
    public string Description { get; set; }
    public DateTime TreatmentDate { get; set; }
    public string HealthcareFacility { get; set; }
    public string FacilityAddress { get; set; }
    public string DoctorName { get; set; }
    public string Diagnosis { get; set; }
    public string TreatmentReceived { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountClaimed { get; set; }
    public string Currency { get; set; }
    public bool CoveredByInsurance { get; set; }
    public string InsuranceProvider { get; set; }
    public string InsurancePolicyNumber { get; set; }
    public string InsuranceClaimNumber { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }
    public List<CreateMedicalExpenseItemDto> Items { get; set; }
}

// Update DTO
public class UpdateMedicalExpenseClaimDto
{
    public Guid Id { get; set; }
    public string Description { get; set; }
    public string Diagnosis { get; set; }
    public string TreatmentReceived { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountClaimed { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }
    public string AdditionalNotes { get; set; }
}

// Approval DTOs
public class ApproveClaimDto
{
    public Guid Id { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string Comments { get; set; }
}

public class RejectClaimDto
{
    public Guid Id { get; set; }
    public string RejectionReason { get; set; }
}

// Supporting DTOs
public class MedicalExpenseItemDto
{
    public Guid Id { get; set; }
    public MedicalItemType ItemType { get; set; }
    public string ItemTypeName { get; set; }
    public string ItemDescription { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string Remarks { get; set; }
}

public class CreateMedicalExpenseItemDto
{
    public MedicalItemType ItemType { get; set; }
    public string ItemDescription { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Remarks { get; set; }
}

public class MedicalExpenseDocumentDto
{
    public Guid Id { get; set; }
    public MedicalDocumentType DocumentType { get; set; }
    public string DocumentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Medical Policy DTOs
public class MedicalPolicyListDto
{
    public Guid Id { get; set; }
    public string PolicyName { get; set; }
    public string PolicyNumber { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool IsActive { get; set; }
    public decimal? AnnualLimitPerEmployee { get; set; }
    public int EnrolledEmployees { get; set; }
}

public class MedicalPolicyDetailDto
{
    public Guid Id { get; set; }
    public string PolicyName { get; set; }
    public string PolicyNumber { get; set; }
    public string Description { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool IsActive { get; set; }

    // Coverage Limits
    public decimal? AnnualLimitPerEmployee { get; set; }
    public decimal? AnnualLimitPerDependent { get; set; }
    public decimal? LifetimeLimit { get; set; }
    public decimal? ConsultationLimit { get; set; }
    public decimal? MedicationLimit { get; set; }
    public decimal? HospitalizationLimit { get; set; }
    public decimal? OpticalLimit { get; set; }
    public decimal? DentalLimit { get; set; }

    // Policy Terms
    public string Exclusions { get; set; }
    public string PreExistingConditionPolicy { get; set; }
    public string ClaimProcess { get; set; }
    public string RequiredDocuments { get; set; }

    // Employee Contribution
    public decimal? EmployeeContributionPercentage { get; set; }
    public decimal? EmployeeContributionAmount { get; set; }

    // Collections
    public List<MedicalPolicyBenefitDto> Benefits { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MedicalPolicyBenefitDto
{
    public Guid Id { get; set; }
    public string BenefitName { get; set; }
    public string Description { get; set; }
    public decimal? AnnualLimit { get; set; }
    public decimal? PerClaimLimit { get; set; }
    public string Terms { get; set; }
}

// Employee Medical Summary DTO
public class EmployeeMedicalSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public int Year { get; set; }
    public Guid? PolicyId { get; set; }
    public string PolicyName { get; set; }
    public decimal? AnnualLimit { get; set; }
    public decimal TotalClaimedAmount { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingLimit { get; set; }
    public int TotalClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int PendingClaims { get; set; }
}

// Dashboard DTO
public class MedicalExpenseDashboardDto
{
    public int TotalClaimsThisYear { get; set; }
    public int PendingClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int RejectedClaims { get; set; }
    public decimal TotalAmountClaimed { get; set; }
    public decimal TotalAmountApproved { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public Dictionary<MedicalExpenseType, int> ClaimsByType { get; set; }
    public List<MedicalExpenseClaimListDto> RecentClaims { get; set; }
    public List<MedicalExpenseClaimListDto> PendingApprovals { get; set; }
}