using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Medical;

/// <summary>
/// Employee medical expense claim
/// </summary>
public class MedicalExpenseClaim : TenantEntity
{
    public string ClaimNumber { get; set; } = string.Empty;

    // Employee & Dependent
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public bool IsForDependent { get; set; }
    public Guid? DependentId { get; set; }
    public EmployeeDependent? Dependent { get; set; }
    public string? PatientName { get; set; }
    public string? RelationshipToEmployee { get; set; }

    // Claim Details
    public DateTime ClaimDate { get; set; }
    public DateTime ServiceDate { get; set; }
    public MedicalExpenseType ExpenseType { get; set; } // Consultation, Medication, Surgery, etc.
    public string Description { get; set; } = string.Empty;

    // Healthcare Provider
    public string HealthcareFacility { get; set; } = string.Empty;
    public string? FacilityAddress { get; set; }
    public string? DoctorName { get; set; }

    // Diagnosis & Treatment
    public string? Diagnosis { get; set; }
    public string? TreatmentReceived { get; set; }
    public bool IsEmergency { get; set; }
    public bool RequiredHospitalization { get; set; }
    public int? DaysHospitalized { get; set; }

    // Cost Details
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public decimal AmountClaimed { get; set; }

    // Insurance
    public bool HasInsuranceCoverage { get; set; }
    public string? InsuranceProvider { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }
    public string? InsuranceClaimNumber { get; set; }

    // Company Policy
    public Guid? MedicalPolicyId { get; set; }
    public MedicalPolicy? MedicalPolicy { get; set; }
    public decimal? PolicyCoverageLimit { get; set; }
    public decimal? EmployeeUtilizedAmount { get; set; }
    public decimal? RemainingLimit { get; set; }

    // Approval Workflow
    public ClaimStatus Status { get; set; }

    public Guid? SupervisorApprovedById { get; set; }
    public Employee? SupervisorApprovedBy { get; set; }
    public DateTime? SupervisorApprovalDate { get; set; }
    public string? SupervisorComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    public Employee? HrApprovedBy { get; set; }
    public DateTime? HrApprovalDate { get; set; }
    public string? HrComments { get; set; }

    public Guid? FinanceApprovedById { get; set; }
    public Employee? FinanceApprovedBy { get; set; }
    public DateTime? FinanceApprovalDate { get; set; }
    public string? FinanceComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }

    // Payment
    public decimal? ApprovedAmount { get; set; }
    public bool PaymentProcessed { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentReference { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    // Additional Info
    public string? AdditionalNotes { get; set; }

    // Relations
    public ICollection<MedicalExpenseDocument> Documents { get; set; } = new List<MedicalExpenseDocument>();
    public ICollection<MedicalExpenseItem> Items { get; set; } = new List<MedicalExpenseItem>();
}

public class MedicalExpenseItem : TenantEntity
{
    public Guid ClaimId { get; set; }
    public MedicalExpenseClaim Claim { get; set; } = null!;

    public string ItemDescription { get; set; } = string.Empty;
    public MedicalItemType ItemType { get; set; } // Consultation, Lab Test, Medication, Procedure
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? Remarks { get; set; }
}

public class MedicalExpenseDocument : TenantEntity
{
    public Guid ClaimId { get; set; }
    public MedicalExpenseClaim Claim { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public MedicalDocumentType Type { get; set; } // Receipt, Prescription, Medical Report, Invoice
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Company medical policy/benefits
/// </summary>
public class MedicalPolicy : TenantEntity
{
    public string PolicyName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    // Coverage
    public bool CoversEmployee { get; set; }
    public bool CoversSpouse { get; set; }
    public bool CoversChildren { get; set; }
    public int? MaxDependents { get; set; }
    public int? MaxChildAge { get; set; }

    // Limits
    public decimal AnnualLimitPerEmployee { get; set; }
    public decimal AnnualLimitPerDependent { get; set; }
    public decimal? LifetimeLimit { get; set; }

    public bool HasConsultationLimit { get; set; }
    public decimal? ConsultationLimit { get; set; }

    public bool HasMedicationLimit { get; set; }
    public decimal? MedicationLimit { get; set; }

    public bool HasHospitalizationLimit { get; set; }
    public decimal? HospitalizationLimit { get; set; }

    public bool HasOpticalLimit { get; set; }
    public decimal? OpticalLimit { get; set; }

    public bool HasDentalLimit { get; set; }
    public decimal? DentalLimit { get; set; }

    // Exclusions
    public string? Exclusions { get; set; }
    public string? PreExistingConditionPolicy { get; set; }

    // Cost Sharing
    public bool EmployeeContributes { get; set; }
    public decimal? EmployeeContributionPercentage { get; set; }
    public decimal? EmployeeContributionAmount { get; set; }

    // Claim Process
    public int ClaimSubmissionDeadlineDays { get; set; }
    public string? ClaimProcess { get; set; }
    public string? RequiredDocuments { get; set; }

    public bool IsActive { get; set; }

    public ICollection<MedicalPolicyBenefit> Benefits { get; set; } = new List<MedicalPolicyBenefit>();
    public ICollection<MedicalPolicyEligibility> Eligibility { get; set; } = new List<MedicalPolicyEligibility>();
}

public class MedicalPolicyBenefit : TenantEntity
{
    public Guid PolicyId { get; set; }
    public MedicalPolicy Policy { get; set; } = null!;

    public string BenefitName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? AnnualLimit { get; set; }
    public decimal? PerClaimLimit { get; set; }
    public int? MaxClaimsPerYear { get; set; }

    public bool RequiresPreApproval { get; set; }
    public string? Terms { get; set; }
}

public class MedicalPolicyEligibility : TenantEntity
{
    public Guid PolicyId { get; set; }
    public MedicalPolicy Policy { get; set; } = null!;

    // public EmploymentType? EmploymentType { get; set; }
    public Guid? PositionId { get; set; }
    public EmployeePosition? Position { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? MinServiceMonths { get; set; }
    public string? OtherCriteria { get; set; }
}

/// <summary>
/// Annual medical tracking
/// </summary>
public class EmployeeMedicalSummary : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }

    public Guid? PolicyId { get; set; }
    public MedicalPolicy? Policy { get; set; }

    public decimal AnnualLimit { get; set; }
    public decimal TotalClaimedAmount { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingLimit { get; set; }

    public int NumberOfClaims { get; set; }
    public int ApprovedClaims { get; set; }
    public int RejectedClaims { get; set; }
    public int PendingClaims { get; set; }

    public DateTime? LastClaimDate { get; set; }
}