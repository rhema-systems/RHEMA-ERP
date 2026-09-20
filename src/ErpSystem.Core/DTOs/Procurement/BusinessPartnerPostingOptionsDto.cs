using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class BusinessPartnerPostingOptionsDto
{
    public List<BusinessPartnerPostingAccountOptionDto> Accounts { get; set; } = new();
    public List<BusinessPartnerChequeBookOptionDto> BankAccounts { get; set; } = new();
    public List<BusinessPartnerTaxGroupOptionDto> TaxGroups { get; set; } = new();
    public List<BusinessPartnerWithholdingTaxOptionDto> WithholdingTaxes { get; set; } = new();
}

public sealed record BusinessPartnerPostingAccountOptionDto(Guid Id, string AccountCode, string AccountNumber,
    string AccountName, AccountType AccountType, AccountStatus Status, bool AllowDirectPosting, bool IsControlAccount);

public sealed record BusinessPartnerChequeBookOptionDto(Guid Id, string AccountNumber, string AccountName,
    string Currency, bool IsActive, Guid? GlAccountId, string? GlAccountNumber, string? GlAccountName);

public sealed record BusinessPartnerTaxGroupOptionDto(Guid Id, string Code, string Name, TaxApplicability Applicability, bool IsActive);

public sealed record BusinessPartnerWithholdingTaxOptionDto(Guid Id, string Code, string Name, decimal Rate,
    DateTime EffectiveFrom, Guid? TaxPayableAccountId);
