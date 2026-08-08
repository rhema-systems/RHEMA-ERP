using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceAccessScopeGrantDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public FinanceAccessScopeType ScopeType { get; set; }
    public string? ScopeValue { get; set; }
    public string? ScopeDisplayName { get; set; }
    public FinanceAccessLevel AccessLevel { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SaveFinanceAccessScopeGrantDto
{
    [Required]
    public Guid UserId { get; set; }

    public FinanceAccessScopeType ScopeType { get; set; } = FinanceAccessScopeType.Tenant;

    [MaxLength(100)]
    public string? ScopeValue { get; set; }

    public FinanceAccessLevel AccessLevel { get; set; } = FinanceAccessLevel.Read;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Required only when updating an existing grant.</summary>
    public string? RowVersion { get; set; }
}

public sealed class FinanceAccessUserOptionDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class FinanceAccessBankAccountOptionDto
{
    public Guid BankAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string Currency { get; set; } = "GHS";
    public bool IsActive { get; set; }
}

public sealed class DeactivateFinanceAccessScopeGrantDto
{
    [Required]
    [MinLength(10)]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
