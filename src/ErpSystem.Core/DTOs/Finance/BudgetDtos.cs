using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public class BudgetScenarioDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LockedDate { get; set; }
    public Guid? LockedByUserId { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTime? AdoptedAt { get; set; }
    public DateTime? AdoptionEffectiveDate { get; set; }
    public Guid? AdoptedByUserId { get; set; }
    public string? AdoptedByUserName { get; set; }
    public string? AdoptionReason { get; set; }
    public DateTime? SupersededAt { get; set; }
    public Guid? SupersededByUserId { get; set; }
    public string? SupersededByUserName { get; set; }
    public string? SupersessionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ReturnCount { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public class BudgetScenarioCommandDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public class BudgetReturnDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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
    public bool ClearAssignedToUser { get; set; }

    public Guid? ApproverUserId { get; set; }
    public bool ClearApproverUser { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public class AdoptBudgetScenarioDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class BudgetReturnCommandDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class RejectBudgetReturnRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
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
    public Guid TenantId { get; set; }
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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

    public string? RowVersion { get; set; }
}

public class BulkSaveBudgetEntriesDto
{
    [Required]
    public Guid BudgetReturnId { get; set; }

    [Required]
    public string ReturnRowVersion { get; set; } = string.Empty;
    
    public List<BudgetEntrySaveDto> Entries { get; set; } = new();
}

public class BudgetAuditEventDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

public class BudgetValidationIssueDto
{
    public string Severity { get; set; } = "Warning";
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ReturnId { get; set; }
}

public class BudgetReportPeriodDto
{
    public Guid FiscalPeriodId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
}

public class BudgetReportContributionDto
{
    public Guid BudgetReturnId { get; set; }
    public Guid? SegmentValueId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string ReturnStatus { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
}

public class BudgetReportLineDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal? VariancePercent { get; set; }
    public string Favorability { get; set; } = "OnBudget";
    public IReadOnlyList<BudgetReportContributionDto> Contributions { get; set; } =
        Array.Empty<BudgetReportContributionDto>();
}

public class BudgetUnitSummaryDto
{
    public Guid BudgetReturnId { get; set; }
    public Guid? SegmentValueId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string ReturnStatus { get; set; } = string.Empty;
    public string AssignedToUserName { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
}

public class ConsolidatedBudgetViewDto
{
    public Guid ScenarioId { get; set; }
    public string ScenarioName { get; set; } = string.Empty;
    public Guid FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public string ScenarioStatus { get; set; } = string.Empty;
    public bool IsOfficial { get; set; }
    public bool ApprovedOnly { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public string BookClassification { get; set; } = "IFRS";
    public int TotalReturnCount { get; set; }
    public int IncludedReturnCount { get; set; }
    public int ApprovedReturnCount { get; set; }
    public int DraftReturnCount { get; set; }
    public int SubmittedReturnCount { get; set; }
    public int RejectedReturnCount { get; set; }
    public bool ReadyForSubmission { get; set; }
    public decimal TotalRevenueBudget { get; set; }
    public decimal TotalExpenseBudget { get; set; }
    public decimal NetBudget { get; set; }
    public decimal TotalRevenueActual { get; set; }
    public decimal TotalExpenseActual { get; set; }
    public decimal NetActual { get; set; }
    public IReadOnlyList<BudgetReportPeriodDto> Periods { get; set; } =
        Array.Empty<BudgetReportPeriodDto>();
    public IReadOnlyList<BudgetReportLineDto> Lines { get; set; } =
        Array.Empty<BudgetReportLineDto>();
    public IReadOnlyList<BudgetUnitSummaryDto> Units { get; set; } =
        Array.Empty<BudgetUnitSummaryDto>();
    public IReadOnlyList<BudgetValidationIssueDto> ValidationIssues { get; set; } =
        Array.Empty<BudgetValidationIssueDto>();
}

public class BudgetScenarioComparisonLineDto
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal ComparisonAmount { get; set; }
    public decimal DifferenceAmount { get; set; }
    public decimal? DifferencePercent { get; set; }
}

public class BudgetScenarioComparisonDto
{
    public Guid BaseScenarioId { get; set; }
    public string BaseScenarioName { get; set; } = string.Empty;
    public Guid ComparisonScenarioId { get; set; }
    public string ComparisonScenarioName { get; set; } = string.Empty;
    public Guid FiscalYearId { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal BaseTotal { get; set; }
    public decimal ComparisonTotal { get; set; }
    public decimal DifferenceTotal { get; set; }
    public IReadOnlyList<BudgetScenarioComparisonLineDto> Lines { get; set; } =
        Array.Empty<BudgetScenarioComparisonLineDto>();
}
