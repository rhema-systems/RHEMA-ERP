using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcFaqCategories")]
public class EhcFaqCategory : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

[Table("EhcFaqItems")]
public class EhcFaqItem : TenantEntity
{
    [Required]
    [StringLength(250)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Answer { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcFaqCategory? Category { get; set; }

    public bool IsPublished { get; set; } = true;

    public bool IsInternalOnly { get; set; } = true;

    public int SortOrder { get; set; } = 0;

    public int ViewCount { get; set; } = 0;
}

