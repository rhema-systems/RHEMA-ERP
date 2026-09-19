using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Stable Finance account intent accepted from an owning module. Category labels and detailed
/// classification identifiers are deliberately absent; Finance resolves the reviewed manifest.
/// </summary>
public sealed class ProvisionFinanceAccountDto
{
    public Guid TenantId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType CoreAccountType { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string? Description { get; set; }
    public bool IsSegmented { get; set; } = true;
}

public sealed class ProvisionedFinanceAccountDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string ClassificationCode { get; set; } = string.Empty;
    public IReadOnlyList<string> AccountingBookCodes { get; set; } = Array.Empty<string>();
    public bool WasCreated { get; set; }
}
