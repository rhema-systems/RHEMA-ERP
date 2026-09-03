using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountClassificationDto
{
    public Guid Id { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public Guid? ParentClassificationId { get; set; }
    public string? ParentClassificationCode { get; set; }
    public string? ParentClassificationName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoreAccountType { get; set; } = string.Empty;
    public string DefaultRevaluationTreatment { get; set; } = "Exclude";
    public string? SystemRole { get; set; }
    public bool IsPostingClassification { get; set; }
    public string Status { get; set; } = "Draft";
    public int DisplayOrder { get; set; }
    public int ChildCount { get; set; }
    public int NonRetiredChildCount { get; set; }
    public int TotalAccountCount { get; set; }
    public int EnabledAccountCount { get; set; }
    public bool IsLeaf => ChildCount == 0;
    public bool CanRetire => EnabledAccountCount == 0 && NonRetiredChildCount == 0 && Status != "Retired";
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AccountClassificationUsageDto
{
    public Guid AccountAccountingBookId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public sealed class AccountClassificationWhereUsedDto
{
    public Guid ClassificationId { get; set; }
    public string ClassificationCode { get; set; } = string.Empty;
    public int TotalMappings { get; set; }
    public int EnabledMappings { get; set; }
    public IReadOnlyList<AccountClassificationUsageDto> Mappings { get; set; } = [];
    public int DraftLayoutReferences { get; set; }
    public int PublishedLayoutReferences { get; set; }
    public IReadOnlyList<AccountClassificationLayoutUsageDto> LayoutReferences { get; set; } = [];
}

public sealed class AccountClassificationLayoutUsageDto
{
    public Guid LayoutId { get; set; }
    public string LayoutCode { get; set; } = string.Empty;
    public string LayoutName { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public int VersionNumber { get; set; }
    public string VersionStatus { get; set; } = string.Empty;
    public string RowCode { get; set; } = string.Empty;
    public bool IsHistoricalSnapshot { get; set; }
}

public sealed class SaveAccountClassificationDto
{
    public Guid? Id { get; set; }
    [Required] public Guid AccountingBookId { get; set; }
    public Guid? ParentClassificationId { get; set; }
    [Required, MaxLength(50)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Description { get; set; }
    [Required] public string CoreAccountType { get; set; } = string.Empty;
    public string DefaultRevaluationTreatment { get; set; } = "Exclude";
    public string? SystemRole { get; set; }
    public bool IsPostingClassification { get; set; }
    public string Status { get; set; } = "Draft";
    public int DisplayOrder { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class RetireAccountClassificationDto
{
    [Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}
