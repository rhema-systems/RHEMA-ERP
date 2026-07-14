using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public class BudgetScenarioDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LockedDate { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReturnCount { get; set; }
}

public class CreateBudgetScenarioDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid FiscalYearId { get; set; }

    [Required]
    [MaxLength(3)]
    public string BaseCurrencyCode { get; set; } = "GHS";
}

public class UpdateBudgetScenarioDto
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; }
}

public class BudgetReturnDto
{
    public Guid Id { get; set; }
    public Guid BudgetScenarioId { get; set; }
    public string BudgetScenarioName { get; set; } = string.Empty;
    public Guid? SegmentValueId { get; set; }
    public string? SegmentValueName { get; set; }
    public string? SegmentValueCode { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public Guid? ApproverUserId { get; set; }
    public string? ApproverUserName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public decimal TotalAmountBase { get; set; }
}

public class CreateBudgetReturnDto
{
    [Required]
    public Guid BudgetScenarioId { get; set; }

    public Guid? SegmentValueId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? ApproverUserId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Updates assignment metadata on a Draft/Rejected budget return. Amounts change through
/// entry bulk-save; status changes through submit/approve/reject.
/// </summary>
public class UpdateBudgetReturnDto
{
    public Guid? AssignedToUserId { get; set; }

    public Guid? ApproverUserId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Base-currency revenue/expense totals for a budget scenario.
/// </summary>
public class BudgetSummaryDto
{
    public Guid ScenarioId { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal NetIncome { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
}

public class BudgetEntryDto
{
    public Guid Id { get; set; }
    public Guid BudgetReturnId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; }
    public decimal Amount { get; set; } // Transaction Currency
    public decimal AmountBase { get; set; } // Base Currency
    public string? Notes { get; set; }
}

public class BudgetEntrySaveDto
{
    public Guid? Id { get; set; } // Null for new entry, ID for update
    
    [Required]
    public Guid BudgetReturnId { get; set; }
    
    [Required]
    public Guid AccountId { get; set; }
    
    [Required]
    public Guid FiscalPeriodId { get; set; }
    
    [Required]
    public string CurrencyCode { get; set; } = "GHS";
    
    public decimal ExchangeRate { get; set; } = 1;
    
    public decimal Amount { get; set; }
    
    public string? Notes { get; set; }
}

public class BulkSaveBudgetEntriesDto
{
    [Required]
    public Guid BudgetReturnId { get; set; }
    
    public List<BudgetEntrySaveDto> Entries { get; set; } = new();
}
