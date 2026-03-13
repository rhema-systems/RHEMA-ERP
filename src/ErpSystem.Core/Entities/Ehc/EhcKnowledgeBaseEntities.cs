using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Ehc;

[Table("EhcKnowledgeBaseCategories")]
public class EhcKnowledgeBaseCategory : TenantEntity
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

[Table("EhcKnowledgeBaseArticles")]
public class EhcKnowledgeBaseArticle : TenantEntity
{
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string Body { get; set; } = string.Empty; // Markdown/plain text

    public Guid? CategoryId { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public virtual EhcKnowledgeBaseCategory? Category { get; set; }

    [StringLength(500)]
    public string? TagsCsv { get; set; }

    public bool IsPublished { get; set; } = true;

    // Phase 2 baseline: internal-only KB
    public bool IsInternalOnly { get; set; } = true;

    public int ViewCount { get; set; } = 0;
}

