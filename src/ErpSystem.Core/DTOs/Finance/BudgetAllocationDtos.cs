namespace ErpSystem.Core.DTOs.Finance;

// ========================================================================
// UNIT ACCOUNT BUDGET DTOs
// ========================================================================

/// <summary>
/// Unit Account Budget DTO.
/// </summary>
public record UnitAccountBudgetDto(
    Guid Id,
    Guid UnitAccountId,
    string AccountNumber,
    string AccountName,
    string UnitTypeCode,
    Guid FiscalYearId,
    Guid FiscalPeriodId,
    string PeriodName,
    decimal BudgetQuantity,
    string? Notes,
    string BudgetVersion,
    bool IsActive,
    DateTime CreatedAt
);

/// <summary>
/// Budget vs Actual comparison DTO.
/// </summary>
public record BudgetVarianceDto(
    Guid UnitAccountId,
    string AccountNumber,
    string AccountName,
    string UnitTypeCode,
    string PeriodName,
    decimal BudgetQuantity,
    decimal ActualQuantity,
    decimal Variance,
    decimal VariancePercent,
    bool IsFavorable
);

/// <summary>
/// DTO for creating a budget entry.
/// </summary>
public record CreateUnitAccountBudgetDto(
    Guid UnitAccountId,
    Guid FiscalYearId,
    Guid FiscalPeriodId,
    decimal BudgetQuantity,
    string? Notes,
    string? BudgetVersion
);

/// <summary>
/// DTO for updating a budget entry.
/// </summary>
public record UpdateUnitAccountBudgetDto(
    decimal BudgetQuantity,
    string? Notes,
    bool IsActive
);

/// <summary>
/// DTO for bulk budget entry (e.g., spreading annual budget across periods).
/// </summary>
public record BulkBudgetEntryDto(
    Guid UnitAccountId,
    Guid FiscalYearId,
    decimal AnnualBudget,
    string SpreadMethod, // "Equal", "Custom", "Historical"
    Dictionary<Guid, decimal>? PeriodAmounts, // For custom spread
    string? Notes,
    string? BudgetVersion
);

// ========================================================================
// ALLOCATION RULE DTOs
// ========================================================================

/// <summary>
/// Allocation Rule DTO.
/// </summary>
public record AllocationRuleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid SourceAccountId,
    string SourceAccountNumber,
    string SourceAccountName,
    string AllocationType,
    Guid? DriverUnitAccountId,
    string? DriverUnitAccountNumber,
    string? DriverUnitAccountName,
    bool IsActive,
    bool AutoReverse,
    DateTime? LastRunDate,
    DateTime CreatedAt,
    List<AllocationTargetDto> Targets
);

/// <summary>
/// Allocation Target DTO.
/// </summary>
public record AllocationTargetDto(
    Guid Id,
    Guid TargetAccountId,
    string TargetAccountNumber,
    string TargetAccountName,
    decimal? FixedPercentage,
    Guid? TargetDriverUnitAccountId,
    string? TargetDriverUnitAccountNumber,
    string? CostCenterCode
);

/// <summary>
/// DTO for creating an allocation rule.
/// </summary>
public record CreateAllocationRuleDto(
    string Code,
    string Name,
    string? Description,
    Guid SourceAccountId,
    string AllocationType,
    Guid? DriverUnitAccountId,
    bool AutoReverse,
    List<CreateAllocationTargetDto> Targets
);

/// <summary>
/// DTO for creating an allocation target.
/// </summary>
public record CreateAllocationTargetDto(
    Guid TargetAccountId,
    decimal? FixedPercentage,
    Guid? TargetDriverUnitAccountId,
    string? CostCenterCode
);

/// <summary>
/// DTO for updating an allocation rule.
/// </summary>
public record UpdateAllocationRuleDto(
    string Name,
    string? Description,
    Guid SourceAccountId,
    string AllocationType,
    Guid? DriverUnitAccountId,
    bool IsActive,
    bool AutoReverse,
    List<CreateAllocationTargetDto> Targets
);

/// <summary>
/// DTO for running an allocation.
/// </summary>
public record RunAllocationDto(
    Guid AllocationRuleId,
    Guid FiscalPeriodId,
    DateTime AllocationDate,
    string? Description
);

/// <summary>
/// Result of an allocation run.
/// </summary>
public record AllocationResultDto(
    Guid AllocationRuleId,
    string RuleCode,
    string RuleName,
    DateTime RunDate,
    Guid JournalEntryId,
    string JournalEntryNumber,
    decimal TotalAllocated,
    List<AllocationLineResultDto> Lines
);

/// <summary>
/// Individual line result from allocation.
/// </summary>
public record AllocationLineResultDto(
    Guid TargetAccountId,
    string TargetAccountNumber,
    string TargetAccountName,
    decimal AllocationBasis,
    decimal AllocationPercent,
    decimal AllocatedAmount
);

/// <summary>
/// DTO for creating a controlled allocation run batch.
/// </summary>
public record CreateAllocationRunBatchDto(
    Guid AllocationRuleId,
    Guid FiscalPeriodId,
    DateTime AllocationDate,
    string? Description
);

/// <summary>
/// DTO for submitting a controlled allocation run batch to workflow.
/// </summary>
public record SubmitAllocationRunBatchDto(
    string? Comment
);

/// <summary>
/// DTO for rejecting a controlled allocation run batch.
/// </summary>
public record RejectAllocationRunBatchDto(
    string Reason
);

/// <summary>
/// Controlled allocation run batch with approval and posting state.
/// </summary>
public record AllocationRunBatchDto(
    Guid Id,
    string BatchNumber,
    Guid AllocationRuleId,
    string RuleCode,
    string RuleName,
    Guid FiscalPeriodId,
    string PeriodCode,
    string PeriodName,
    DateTime AllocationDate,
    string? Description,
    string Status,
    Guid SourceAccountId,
    string SourceAccountNumber,
    string SourceAccountName,
    decimal SourcePeriodBalance,
    decimal TotalAllocated,
    string AllocationType,
    string BookClassification,
    string FunctionalCurrencyCode,
    Guid? WorkflowInstanceId,
    Guid? JournalEntryId,
    string? JournalEntryNumber,
    DateTime? SubmittedAt,
    string? SubmittedByName,
    DateTime? ApprovedAt,
    string? ApprovedByName,
    DateTime? PostedAt,
    string? PostedByName,
    string? RejectionReason,
    DateTime CreatedAt,
    List<AllocationRunBatchLineDto> Lines
);

/// <summary>
/// Stored allocation line that was reviewed and approved for posting.
/// </summary>
public record AllocationRunBatchLineDto(
    Guid Id,
    int LineNumber,
    Guid TargetAccountId,
    string TargetAccountNumber,
    string TargetAccountName,
    Guid? TargetDriverUnitAccountId,
    string? TargetDriverUnitAccountNumber,
    string? TargetDriverUnitAccountName,
    decimal AllocationBasis,
    decimal AllocationPercent,
    decimal AllocatedAmount,
    string? CostCenterCode
);
