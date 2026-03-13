using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed class EhcKnowledgeBaseCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEhcKnowledgeBaseCategoryRequestDto
{
    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateEhcKnowledgeBaseCategoryRequestDto : CreateEhcKnowledgeBaseCategoryRequestDto { }

public sealed class EhcKnowledgeBaseArticleListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? TagsCsv { get; set; }
    public bool IsPublished { get; set; }
    public bool IsInternalOnly { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class EhcKnowledgeBaseArticleDetailDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? TagsCsv { get; set; }
    public bool IsPublished { get; set; }
    public bool IsInternalOnly { get; set; }
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEhcKnowledgeBaseArticleRequestDto
{
    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Summary { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    [StringLength(500)]
    public string? TagsCsv { get; set; }

    public bool IsPublished { get; set; } = true;
    public bool IsInternalOnly { get; set; } = true;
}

public sealed class UpdateEhcKnowledgeBaseArticleRequestDto : CreateEhcKnowledgeBaseArticleRequestDto { }
