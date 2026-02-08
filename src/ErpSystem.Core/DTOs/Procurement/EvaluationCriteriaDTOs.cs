using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Evaluation criterion DTO
/// </summary>
public class EvaluationCriterionDto
{
    public Guid Id { get; set; }
    public string CriterionName { get; set; } = string.Empty;
    public string CriterionCode { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    /// <summary>
    /// Specifies whether this criterion is used for Technical or Financial evaluation in QCBS.
    /// </summary>
    public string EvaluationType { get; set; } = "Technical"; // Technical, Financial
    public string? Description { get; set; }
    public int MaxScore { get; set; } = 100;
    public decimal Weight { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// Create evaluation criterion DTO
/// </summary>
public class CreateEvaluationCriterionDto
{
    [Required]
    [MaxLength(100)]
    public string CriterionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CriterionCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    /// <summary>
    /// Specifies whether this criterion is used for Technical or Financial evaluation in QCBS.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EvaluationType { get; set; } = "Technical"; // Technical, Financial

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 100)]
    public int MaxScore { get; set; } = 100;

    [Range(0, 100)]
    public decimal Weight { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Update evaluation criterion DTO
/// </summary>
public class UpdateEvaluationCriterionDto
{
    [Required]
    [MaxLength(100)]
    public string CriterionName { get; set; } = string.Empty;

    /// <summary>
    /// Specifies whether this criterion is used for Technical or Financial evaluation in QCBS.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string EvaluationType { get; set; } = "Technical"; // Technical, Financial

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, 100)]
    public int MaxScore { get; set; } = 100;

    [Range(0, 100)]
    public decimal Weight { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;
}

