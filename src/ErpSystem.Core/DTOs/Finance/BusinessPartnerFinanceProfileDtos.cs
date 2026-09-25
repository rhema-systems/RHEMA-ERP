namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Read model for the Finance-owned configuration attached to one canonical Procurement
/// Business Partner. The partner remains the identity owner; these DTOs deliberately expose no
/// duplicate supplier/customer identity or AP/AR control-account field.
/// </summary>
public sealed class BusinessPartnerFinanceProfileSetDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string? TaxIdentificationNumber { get; set; }
    public List<BusinessPartnerFinanceRoleDto> Roles { get; set; } = new();
}

public sealed class BusinessPartnerFinanceRoleDto
{
    public Guid Id { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ActiveFromUtc { get; set; }
    public List<BusinessPartnerApProfileDto> ApProfiles { get; set; } = new();
    public List<BusinessPartnerArProfileDto> ArProfiles { get; set; } = new();
}

public sealed class BusinessPartnerApProfileDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ApReferenceNumber { get; set; }
    public Guid? PaymentTermId { get; set; }
    public Guid? DefaultTaxGroupId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public bool SubjectToWithholding { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
    public List<BusinessPartnerApWhtDefaultDto> WithholdingDefaults { get; set; } = new();
}

public sealed class BusinessPartnerApWhtDefaultDto
{
    public Guid Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public Guid WithholdingTaxId { get; set; }
    public string WithholdingTaxCode { get; set; } = string.Empty;
    public string WithholdingTaxName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool IsDefaultForAp { get; set; }
    public bool IsActive { get; set; }
}

public sealed class BusinessPartnerArProfileDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerRoleId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ArReferenceNumber { get; set; }
    public Guid? PaymentTermId { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsWithholdingAgent { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? DecisionReason { get; set; }
}

public sealed class SaveBusinessPartnerApProfileRequest
{
    public Guid BusinessPartnerRoleId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ApReferenceNumber { get; set; }
    public Guid? PaymentTermId { get; set; }
    public Guid? DefaultTaxGroupId { get; set; }
    public Guid? DefaultExpenseAccountId { get; set; }
    public bool SubjectToWithholding { get; set; }
    public List<SaveBusinessPartnerApWhtDefaultRequest> WithholdingDefaults { get; set; } = new();
}

public sealed class SaveBusinessPartnerApWhtDefaultRequest
{
    public string CategoryCode { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public Guid WithholdingTaxId { get; set; }
    public bool IsDefaultForAp { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SaveBusinessPartnerArProfileRequest
{
    public Guid BusinessPartnerRoleId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? ArReferenceNumber { get; set; }
    public Guid? PaymentTermId { get; set; }
    public decimal? CreditLimit { get; set; }
    public bool IsWithholdingAgent { get; set; }
}

public sealed class BusinessPartnerFinanceProfileDecisionRequest
{
    public string? Reason { get; set; }
}
