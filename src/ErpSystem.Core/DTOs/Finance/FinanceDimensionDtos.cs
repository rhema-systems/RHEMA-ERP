using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceDimensionDefinitionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Classification { get; set; } = "Analytical";
    public string ValueSourceType { get; set; } = "Lookup";
    public string? SourceEntityType { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    public List<FinanceDimensionValueDto> Values { get; set; } = [];
}

public sealed class UpsertFinanceDimensionDefinitionDto
{
    [Required, MaxLength(30)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [Required, MaxLength(20)] public string Classification { get; set; } = "Analytical";
    [Required, MaxLength(20)] public string ValueSourceType { get; set; } = "Lookup";
    [MaxLength(100)] public string? SourceEntityType { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class FinanceDimensionValueDto
{
    public Guid Id { get; set; }
    public Guid FinanceDimensionDefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentValueId { get; set; }
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class UpsertFinanceDimensionValueDto
{
    [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public Guid? ParentValueId { get; set; }
    [MaxLength(100)] public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class FinanceDimensionAccountRuleDto
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid FinanceDimensionDefinitionId { get; set; }
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public string RuleType { get; set; } = "Optional";
    public Guid? DefaultDimensionValueId { get; set; }
    public string? DefaultValueCode { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocumentType { get; set; }
    public string? PostingAction { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpsertFinanceDimensionAccountRuleDto
{
    [Required] public Guid AccountId { get; set; }
    [Required] public Guid FinanceDimensionDefinitionId { get; set; }
    [Required, MaxLength(20)] public string RuleType { get; set; } = "Optional";
    public Guid? DefaultDimensionValueId { get; set; }
    [MaxLength(50)] public string? SourceModule { get; set; }
    [MaxLength(100)] public string? SourceDocumentType { get; set; }
    [MaxLength(50)] public string? PostingAction { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class FinanceDimensionAssignmentDto
{
    public Guid DefinitionId { get; set; }
    public Guid ValueId { get; set; }
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public string ValueCode { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
}

public sealed class FinanceDimensionSetDto
{
    public Guid Id { get; set; }
    public string DisplayValue { get; set; } = string.Empty;
    public List<FinanceDimensionAssignmentDto> Assignments { get; set; } = [];
}
