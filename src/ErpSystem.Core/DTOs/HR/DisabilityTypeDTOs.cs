using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>A kind of disability from the tenant's catalogue (round 3, lane P2), with how many records name it.</summary>
public class DisabilityTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public DisabilityCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Employees plus dependants naming this row — zero is what lets it be deleted rather than retired.</summary>
    public int UsageCount { get; set; }
}

public class CreateDisabilityTypeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    public DisabilityCategory Category { get; set; } = DisabilityCategory.Other;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateDisabilityTypeDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    public DisabilityCategory Category { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}
