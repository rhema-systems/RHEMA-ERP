namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceBudgetControlEvaluationDto
{
    public Guid SourceDocumentId { get; set; }
    public DateTime EntryDate { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string EvaluationHash { get; set; } = string.Empty;
    public bool HasTrackedExpenseLines { get; set; }
    public bool IsPostingSnapshot { get; set; }
    public bool IsAllowed { get; set; }
    public bool RequiresOverride { get; set; }
    public bool HasApprovedOverride { get; set; }
    public string? OverrideStatus { get; set; }
    public decimal TotalRequestedAmount { get; set; }
    public decimal TotalShortfallAmount { get; set; }
    public IReadOnlyList<FinanceBudgetControlLineDto> Lines { get; set; } = Array.Empty<FinanceBudgetControlLineDto>();
}

public sealed class FinanceBudgetControlLineDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodCode { get; set; } = string.Empty;
    public Guid? BudgetScenarioId { get; set; }
    public string? BudgetScenarioName { get; set; }
    public Guid? BudgetReturnId { get; set; }
    public Guid? BudgetEntryId { get; set; }
    public Guid? SegmentValueId { get; set; }
    public string? SegmentValue { get; set; }
    public Guid? FinanceDimensionSetId { get; set; }
    public string? DimensionCombinationHash { get; set; }
    public IReadOnlyList<BudgetDimensionAssignmentDto> DimensionAssignments { get; set; } = Array.Empty<BudgetDimensionAssignmentDto>();
    public decimal RequestedAmount { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal PostedActualAmount { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal ShortfallAmount { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class FinanceBudgetOverrideCommandDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class FinanceBudgetOverrideRequestDto
{
    public Guid Id { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string EvaluationHash { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ShortfallAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
