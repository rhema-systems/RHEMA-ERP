using System.Text.Json.Serialization;

namespace ErpSystem.Core.DTOs.Finance;

// ========================================================================
// UNIT TYPE DTOs
// ========================================================================

/// <summary>
/// Unit Type data transfer object.
/// </summary>
public class UnitTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DecimalPlaces { get; set; }
    public decimal? RoundingIncrement { get; set; }
    public bool IsActive { get; set; }
    public int AccountCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// DTO for creating a new unit type.
/// </summary>
public class CreateUnitTypeDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DecimalPlaces { get; set; } = 0;
    public decimal? RoundingIncrement { get; set; }
}

/// <summary>
/// DTO for updating an existing unit type.
/// </summary>
public class UpdateUnitTypeDto
{
    private decimal? _roundingIncrement;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? DecimalPlaces { get; set; }
    public decimal? RoundingIncrement
    {
        get => _roundingIncrement;
        set
        {
            _roundingIncrement = value;
            RoundingIncrementSpecified = true;
        }
    }
    [JsonIgnore]
    public bool RoundingIncrementSpecified { get; private set; }
    public bool? IsActive { get; set; }
}

// ========================================================================
// UNIT ACCOUNT DTOs
// ========================================================================

/// <summary>
/// Unit Account data transfer object.
/// </summary>
public class UnitAccountDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UnitTypeId { get; set; }
    public string UnitTypeCode { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
    public Guid? ParentAccountId { get; set; }
    public string? ParentAccountNumber { get; set; }
    public int AccountLevel { get; set; }
    public bool IsPostingAccount { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Detailed unit account with balance history.
/// </summary>
public class UnitAccountDetailDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UnitTypeId { get; set; }
    public string UnitTypeName { get; set; } = string.Empty;
    public Guid? ParentAccountId { get; set; }
    public string? ParentAccountNumber { get; set; }
    public int AccountLevel { get; set; }
    public bool IsPostingAccount { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentBalance { get; set; }
    public List<UnitAccountBalanceDto> Balances { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Hierarchical unit account with children.
/// </summary>
public class UnitAccountHierarchyDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnitTypeName { get; set; } = string.Empty;
    public int AccountLevel { get; set; }
    public bool IsPostingAccount { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentBalance { get; set; }
    public List<UnitAccountHierarchyDto> Children { get; set; } = new();
}

/// <summary>
/// DTO for creating a new unit account.
/// </summary>
public class CreateUnitAccountDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UnitTypeId { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsPostingAccount { get; set; } = true;
}

/// <summary>
/// DTO for updating an existing unit account.
/// </summary>
public class UpdateUnitAccountDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsPostingAccount { get; set; }
    public bool? IsActive { get; set; }
}

// ========================================================================
// UNIT JOURNAL ENTRY DTOs
// ========================================================================

/// <summary>
/// Unit Journal Entry data transfer object.
/// </summary>
public class UnitJournalEntryDto
{
    public bool ApprovalRequired { get; set; } = true;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public string? SourceDocument { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string Status { get; set; } = "Draft";
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Unit Journal Entry detail with lines.
/// </summary>
public class UnitJournalEntryDetailDto
{
    public bool ApprovalRequired { get; set; } = true;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string? Description { get; set; }
    public string? SourceDocument { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string? FiscalPeriodName { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? RejectionReason { get; set; }
    public List<UnitJournalEntryLineDto> Lines { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Unit Journal Entry line item DTO.
/// </summary>
public class UnitJournalEntryLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid UnitAccountId { get; set; }
    public string UnitAccountNumber { get; set; } = string.Empty;
    public string UnitAccountName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO for creating a new unit journal entry.
/// </summary>
public class CreateUnitJournalEntryDto
{
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
    public string? SourceDocument { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public List<CreateUnitJournalEntryLineDto> Lines { get; set; } = new();
}

/// <summary>
/// DTO for creating a journal entry line.
/// </summary>
public class CreateUnitJournalEntryLineDto
{
    public Guid UnitAccountId { get; set; }
    public decimal Quantity { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// DTO for updating a draft unit journal entry.
/// </summary>
public class UpdateUnitJournalEntryDto
{
    public string? Description { get; set; }
    public string? SourceDocument { get; set; }
}

/// <summary>
/// Filters for querying journal entries.
/// </summary>
public class UnitJournalEntryFilters
{
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
}

// ========================================================================
// UNIT ACCOUNT BALANCE DTOs
// ========================================================================

/// <summary>
/// Unit Account Balance DTO for period-by-period display.
/// </summary>
public class UnitAccountBalanceDto
{
    public Guid Id { get; set; }
    public Guid UnitAccountId { get; set; }
    public Guid? FiscalYearId { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal PeriodActivity { get; set; }
    public decimal ClosingBalance { get; set; }
}

// ========================================================================
// RATIO DEFINITION DTOs
// ========================================================================

/// <summary>
/// Ratio Definition DTO.
/// </summary>
public class RatioDefinitionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RatioType { get; set; } = "Standard";
    public string NumeratorType { get; set; } = "FinancialAccount";
    public Guid? NumeratorAccountId { get; set; }
    public Guid? NumeratorUnitAccountId { get; set; }
    public decimal? NumeratorConstantValue { get; set; }
    public string DenominatorType { get; set; } = "FinancialAccount";
    public Guid? DenominatorAccountId { get; set; }
    public Guid? DenominatorUnitAccountId { get; set; }
    public decimal? DenominatorConstantValue { get; set; }
    public string ResultFormat { get; set; } = "Decimal";
    public int FormatPrecision { get; set; } = 2;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// DTO for creating a ratio definition.
/// </summary>
public class CreateRatioDefinitionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RatioType { get; set; } = "Standard";
    public string NumeratorType { get; set; } = "FinancialAccount";
    public Guid? NumeratorAccountId { get; set; }
    public Guid? NumeratorUnitAccountId { get; set; }
    public decimal? NumeratorConstantValue { get; set; }
    public string DenominatorType { get; set; } = "FinancialAccount";
    public Guid? DenominatorAccountId { get; set; }
    public Guid? DenominatorUnitAccountId { get; set; }
    public decimal? DenominatorConstantValue { get; set; }
    public string ResultFormat { get; set; } = "Decimal";
    public int FormatPrecision { get; set; } = 2;
}

/// <summary>
/// DTO for updating a ratio definition.
/// </summary>
public class UpdateRatioDefinitionDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Result of a ratio calculation.
/// </summary>
public class RatioCalculationResultDto
{
    public Guid RatioId { get; set; }
    public string RatioCode { get; set; } = string.Empty;
    public string RatioName { get; set; } = string.Empty;
    public Guid? FiscalPeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public decimal Numerator { get; set; }
    public decimal Denominator { get; set; }
    public decimal Result { get; set; }
    public string FormattedResult { get; set; } = "0";
    public DateTime CalculatedAt { get; set; }
}

/// <summary>
/// Trend data for a ratio over multiple periods.
/// </summary>
public class RatioTrendResultDto
{
    public Guid RatioId { get; set; }
    public string RatioCode { get; set; } = string.Empty;
    public string RatioName { get; set; } = string.Empty;
    public Guid? FiscalYearId { get; set; }
    public List<RatioTrendDataPointDto> DataPoints { get; set; } = new();
}

/// <summary>
/// Single data point in a ratio trend.
/// </summary>
public class RatioTrendDataPointDto
{
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
