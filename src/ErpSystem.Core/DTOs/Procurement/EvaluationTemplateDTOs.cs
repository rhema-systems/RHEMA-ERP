using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Evaluation template DTO
/// </summary>
public class EvaluationTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public string TenderType { get; set; } = "RFQ";
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public decimal PassingScore { get; set; } = 70;
    public string ScoringMethod { get; set; } = "WeightedAverage";
    public int DisplayOrder { get; set; } = 0;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public int CriteriaCount { get; set; } = 0;
    public decimal TotalWeight { get; set; } = 0;
    public List<EvaluationTemplateCriterionDto> Criteria { get; set; } = new();
}

/// <summary>
/// Evaluation template criterion DTO
/// </summary>
public class EvaluationTemplateCriterionDto
{
    public Guid Id { get; set; }
    public Guid EvaluationTemplateId { get; set; }
    public Guid EvaluationCriterionId { get; set; }
    public string CriterionName { get; set; } = string.Empty;
    public string CriterionCode { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? CriterionDescription { get; set; }
    public decimal Weight { get; set; } = 0;
    public int MaxScore { get; set; } = 100;
    public bool IsMandatory { get; set; } = true;
    public decimal? MinimumScore { get; set; }
    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Create evaluation template DTO
/// </summary>
public class CreateEvaluationTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TemplateCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [Range(0, 100)]
    public decimal PassingScore { get; set; } = 70;

    [Required]
    [MaxLength(50)]
    public string ScoringMethod { get; set; } = "WeightedAverage";

    public int DisplayOrder { get; set; } = 0;

    public List<CreateEvaluationTemplateCriterionDto> Criteria { get; set; } = new();
}

/// <summary>
/// Create evaluation template criterion DTO
/// </summary>
public class CreateEvaluationTemplateCriterionDto
{
    [Required]
    public Guid EvaluationCriterionId { get; set; }

    [Required]
    [Range(0, 100)]
    public decimal Weight { get; set; } = 0;

    [Range(0, 100)]
    public int MaxScore { get; set; } = 100;

    public bool IsMandatory { get; set; } = true;

    [Range(0, 100)]
    public decimal? MinimumScore { get; set; }

    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Update evaluation template DTO
/// </summary>
public class UpdateEvaluationTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [Range(0, 100)]
    public decimal PassingScore { get; set; } = 70;

    [Required]
    [MaxLength(50)]
    public string ScoringMethod { get; set; } = "WeightedAverage";

    public int DisplayOrder { get; set; } = 0;

    public List<CreateEvaluationTemplateCriterionDto> Criteria { get; set; } = new();
}

/// <summary>
/// Evaluation template list item DTO (for dropdowns)
/// </summary>
public class EvaluationTemplateListItemDto
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string TenderType { get; set; } = "RFQ";
    public bool IsDefault { get; set; } = false;
    public int CriteriaCount { get; set; } = 0;
}

