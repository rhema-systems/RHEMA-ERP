namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementContractOperationsSearchRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Risk { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class ProcurementContractOperationsPortfolioDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalContracts { get; set; }
    public int ActiveContracts { get; set; }
    public int ContractsWithPrompts { get; set; }
    public int CriticalPromptCount { get; set; }
    public IReadOnlyList<ProcurementContractOperationsCurrencyTotalDto> CurrencyTotals { get; set; } = [];
    public IReadOnlyList<ProcurementContractOperationsListItemDto> Items { get; set; } = [];
    public IReadOnlyList<string> DecisionKeys { get; set; } = [];
}

public sealed class ProcurementContractOperationsCurrencyTotalDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public decimal CommittedSpend { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
}

public sealed class ProcurementContractOperationsListItemDto
{
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractTitle { get; set; } = string.Empty;
    public string ContractType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public decimal CommittedSpend { get; set; }
    public decimal InvoicedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public decimal RetentionReleasedAmount { get; set; }
    public int PurchaseOrderCount { get; set; }
    public int ReceiptCount { get; set; }
    public int TotalMilestones { get; set; }
    public int CompletedMilestones { get; set; }
    public int OverdueMilestones { get; set; }
    public decimal MilestoneCompletionPercent { get; set; }
    public DateTime? EndDate { get; set; }
    public int? DaysToExpiry { get; set; }
    public string RenewalStatus { get; set; } = "NotApplicable";
    public decimal? SupplierPerformanceScore { get; set; }
    public string? SupplierPerformanceBand { get; set; }
    public decimal? SupplierRiskScore { get; set; }
    public string? SupplierRiskBand { get; set; }
    public int PromptCount { get; set; }
    public int CriticalPromptCount { get; set; }
    public string OverallRisk { get; set; } = "Low";
}

public sealed class ProcurementContractOperationsDetailDto
{
    public ProcurementContractOperationsListItemDto Summary { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; }
    public string? PenaltyClause { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal RetentionPercentage { get; set; }
    public IReadOnlyList<ProcurementContractOperationsMilestoneDto> Milestones { get; set; } = [];
    public IReadOnlyList<ProcurementContractOperationsPurchaseOrderDto> PurchaseOrders { get; set; } = [];
    public IReadOnlyList<ProcurementContractOperationsInvoiceDto> Invoices { get; set; } = [];
    public IReadOnlyList<ProcurementContractOperationsKpiDto> Kpis { get; set; } = [];
    public IReadOnlyList<ProcurementContractOperationsPromptDto> Prompts { get; set; } = [];
    public ProcurementContractOperationsLineageDto Lineage { get; set; } = new();
    public IReadOnlyList<string> DecisionKeys { get; set; } = [];
}

public sealed class ProcurementContractOperationsMilestoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal PaymentAmount { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public int DaysLate { get; set; }
}

public sealed class ProcurementContractOperationsPurchaseOrderDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? PromisedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public int ReceiptCount { get; set; }
}

public sealed class ProcurementContractOperationsInvoiceDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

public sealed class ProcurementContractOperationsKpiDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public decimal? Target { get; set; }
    public string Status { get; set; } = "Unavailable";
    public string SourceReference { get; set; } = string.Empty;
}

public sealed class ProcurementContractOperationsPromptDto
{
    public string Key { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public DateTime? DueAtUtc { get; set; }
    public int DaysOverdue { get; set; }
    public decimal? EstimatedPenaltyAmount { get; set; }
    public string CalculationBasis { get; set; } = string.Empty;
    public bool RequiresIndependentApproval { get; set; } = true;
    public bool AmountAutoPosted { get; set; }
}

public sealed class ProcurementContractOperationsLineageDto
{
    public Guid? ActivationId { get; set; }
    public int? ActivationSequence { get; set; }
    public string ActivationStatus { get; set; } = "Unavailable";
    public Guid? ConfigurationProfileId { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public Guid? PolicySetId { get; set; }
    public int? PolicyVersion { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? AwardReadinessDecisionId { get; set; }
    public string? IntegrityHash { get; set; }
}

public sealed class ProcessProcurementContractOperationsAlertsRequest
{
    public Guid? ContractId { get; set; }
}

public sealed class ProcessProcurementContractOperationsAlertsResult
{
    public DateTime ProcessedAtUtc { get; set; }
    public int EvaluatedPromptCount { get; set; }
    public int PublishedAlertCount { get; set; }
    public int AlreadyPublishedCount { get; set; }
    public IReadOnlyList<string> PublishedPromptKeys { get; set; } = [];
}
