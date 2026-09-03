using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountClassificationDto
{
    public Guid Id { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public Guid? ParentClassificationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CoreAccountType { get; set; } = string.Empty;
    public string DefaultRevaluationTreatment { get; set; } = "Exclude";
    public string? SystemRole { get; set; }
    public bool IsPostingClassification { get; set; }
    public string Status { get; set; } = "Draft";
    public int DisplayOrder { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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
