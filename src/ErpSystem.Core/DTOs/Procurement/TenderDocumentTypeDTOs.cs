using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender document type DTO
/// </summary>
public class TenderDocumentTypeDto
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public bool IsRequired { get; set; } = true;
    public int MaxFileSizeMB { get; set; } = 10;
    public string AllowedFileTypes { get; set; } = "PDF,DOC,DOCX";
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// Create tender document type DTO
/// </summary>
public class CreateTenderDocumentTypeDto
{
    [Required]
    [MaxLength(100)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    [Range(1, 100)]
    public int MaxFileSizeMB { get; set; } = 10;

    [MaxLength(200)]
    public string AllowedFileTypes { get; set; } = "PDF,DOC,DOCX";

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;
}

/// <summary>
/// Update tender document type DTO
/// </summary>
public class UpdateTenderDocumentTypeDto
{
    [Required]
    [MaxLength(100)]
    public string DocumentName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsRequired { get; set; } = true;

    [Range(1, 100)]
    public int MaxFileSizeMB { get; set; } = 10;

    [MaxLength(200)]
    public string AllowedFileTypes { get; set; } = "PDF,DOC,DOCX";

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 0;
}

