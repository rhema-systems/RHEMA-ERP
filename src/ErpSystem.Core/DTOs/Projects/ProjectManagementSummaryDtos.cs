namespace ErpSystem.Core.DTOs.Projects;

public class ProjectCatalogItemDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ProjectCatalogGroupDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ProjectCatalogItemDto> Items { get; set; } = new();
}

public class ProjectCatalogTypeSummaryDto
{
    public string CatalogType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ConfiguredCount { get; set; }
    public int RecommendedCount { get; set; }
}

public class ProjectMasterDataOverviewDto
{
    public int ProjectTypeCount { get; set; }
    public int ProjectPriorityCount { get; set; }
    public int ProjectTemplateCount { get; set; }
    public int ProjectPhaseTemplateCount { get; set; }
    public int ProjectStageGateRuleCount { get; set; }
    public int PortfolioCount { get; set; }
    public int ProgramCount { get; set; }
    public List<ProjectCatalogGroupDto> RecommendedCatalogs { get; set; } = new();
    public List<ProjectCatalogTypeSummaryDto> CatalogCoverage { get; set; } = new();
}

public class ProjectFinancialAlertDto
{
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ProjectCommercialAlertDto
{
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ProjectPhaseCommercialRollupDto
{
    public Guid? ProjectPhaseId { get; set; }
    public string PhaseName { get; set; } = string.Empty;
    public int PhaseSortOrder { get; set; }
    public int PackageCount { get; set; }
    public int BoqItemCount { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal ForecastAmount { get; set; }
    public decimal VarianceAmount { get; set; }
}

public class ProjectCommercialSummaryDto
{
    public Guid ProjectId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal EstimatedBudget { get; set; }
    public decimal ApprovedBudget { get; set; }
    public decimal PackageBudgetAmount { get; set; }
    public decimal PackageCommittedAmount { get; set; }
    public decimal PackageActualAmount { get; set; }
    public decimal PackageForecastAmount { get; set; }
    public decimal ForecastVarianceAmount { get; set; }
    public int PackageCount { get; set; }
    public int BoqItemCount { get; set; }
    public int UnassignedPackageCount { get; set; }
    public int TenderLinkedPackageCount { get; set; }
    public int ContractLinkedPackageCount { get; set; }
    public int PurchaseRequisitionLinkedPackageCount { get; set; }
    public int PurchaseOrderLinkedPackageCount { get; set; }
    public int PhaseAlignedPackageCount { get; set; }
    public int PhaseLaggingPackageCount { get; set; }
    public int VariationOrderCount { get; set; }
    public decimal ApprovedVariationAmount { get; set; }
    public int InterimValuationCount { get; set; }
    public decimal NetValuationAmount { get; set; }
    public int PaymentCertificateCount { get; set; }
    public decimal NetCertifiedAmount { get; set; }
    public decimal RetentionHeldAmount { get; set; }
    public int ExtensionOfTimeCount { get; set; }
    public int ApprovedExtensionDays { get; set; }
    public string? FinalAccountStatus { get; set; }
    public decimal? FinalAccountValue { get; set; }
    public List<ProjectPhaseCommercialRollupDto> PhaseRollups { get; set; } = new();
    public List<ProjectCommercialAlertDto> Alerts { get; set; } = new();
}

public class ProjectFinancialControlSummaryDto
{
    public Guid ProjectId { get; set; }
    public decimal EstimatedBudget { get; set; }
    public decimal ApprovedBudget { get; set; }
    public decimal BudgetBaseline { get; set; }
    public decimal ActualCost { get; set; }
    public decimal CommittedCost { get; set; }
    public decimal PendingCost { get; set; }
    public decimal ForecastCost { get; set; }
    public decimal EstimateAtCompletion { get; set; }
    public decimal RemainingBudget { get; set; }
    public decimal BudgetVariance { get; set; }
    public decimal BudgetConsumptionPercent { get; set; }
    public decimal ThresholdWarningPercent { get; set; }
    public decimal ThresholdCriticalPercent { get; set; }
    public decimal ProcurementRequestedAmount { get; set; }
    public decimal ProcurementCommittedAmount { get; set; }
    public decimal ProcurementOpenCommitmentAmount { get; set; }
    public decimal ProcurementReceivedAmount { get; set; }
    public decimal ProcurementPendingInspectionAmount { get; set; }
    public decimal TotalExposureAmount { get; set; }
    public decimal ScheduledBillingAmount { get; set; }
    public decimal InvoiceRequestedAmount { get; set; }
    public decimal RecognizedRevenue { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal PlannedValue { get; set; }
    public decimal EarnedValue { get; set; }
    public decimal ScheduleVariance { get; set; }
    public decimal CostVariance { get; set; }
    public decimal? CostPerformanceIndex { get; set; }
    public decimal? SchedulePerformanceIndex { get; set; }
    public decimal? ToCompletePerformanceIndex { get; set; }
    public decimal ProfitabilityPercent { get; set; }
    public string HealthStatus { get; set; } = "Unknown";
    public int BudgetRevisionCount { get; set; }
    public int ForecastVersionCount { get; set; }
    public string? CurrentBudgetRevisionName { get; set; }
    public string? ActiveForecastVersionName { get; set; }
    public bool ThresholdExceeded { get; set; }
    public List<ProjectFinancialAlertDto> Alerts { get; set; } = new();
}

public class ProjectIntegrationLinkDto
{
    public string LinkType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
}

public class ProjectIntegrationSummaryDto
{
    public Guid ProjectId { get; set; }
    public bool HasBusinessPartner { get; set; }
    public bool HasContract { get; set; }
    public bool HasTender { get; set; }
    public bool HasPortfolio { get; set; }
    public bool HasProgram { get; set; }
    public int LinkedAssetCount { get; set; }
    public int ResourceAllocationCount { get; set; }
    public int SharedExternalPolicyCount { get; set; }
    public int PurchaseRequisitionCount { get; set; }
    public int PendingPurchaseRequisitionCount { get; set; }
    public decimal PurchaseRequisitionAmount { get; set; }
    public int PurchaseOrderCount { get; set; }
    public int OpenPurchaseOrderCount { get; set; }
    public decimal PurchaseOrderAmount { get; set; }
    public int PurchaseReceiptCount { get; set; }
    public int PendingPurchaseReceiptInspectionCount { get; set; }
    public int InventoryRequisitionCount { get; set; }
    public int PendingInventoryRequisitionCount { get; set; }
    public decimal InventoryRequisitionValue { get; set; }
    public int IssuedInventoryRequisitionCount { get; set; }
    public decimal IssuedInventoryValue { get; set; }
    public decimal ReturnedInventoryValue { get; set; }
    public decimal NetIssuedInventoryValue { get; set; }
    public int InvoiceRequestCount { get; set; }
    public int RevenueRecognitionCount { get; set; }
    public List<ProjectIntegrationLinkDto> Links { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ProjectPolicyViolationDto
{
    public string Area { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ProjectGovernanceSummaryDto
{
    public Guid ProjectId { get; set; }
    public int OpenRiskCount { get; set; }
    public int OpenIssueCount { get; set; }
    public int OpenChangeRequestCount { get; set; }
    public int PendingDeliverableApprovalCount { get; set; }
    public int PendingTimesheetApprovalCount { get; set; }
    public int PendingExpenseApprovalCount { get; set; }
    public int OpenActionItemCount { get; set; }
    public bool HasClosureDraft { get; set; }
    public bool HasApprovedClosure { get; set; }
    public bool HasLockedBaseline { get; set; }
    public List<ProjectPolicyViolationDto> Violations { get; set; } = new();
}
