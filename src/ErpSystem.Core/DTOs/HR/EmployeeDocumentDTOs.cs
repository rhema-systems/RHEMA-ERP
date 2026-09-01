using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.HR;

// ═══════════════════════════════════════════════════════════════════════════
//  DOCUMENT TYPES — the shared vocabulary
// ═══════════════════════════════════════════════════════════════════════════

public class EmployeeDocumentTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public bool HasExpiry { get; set; }
    public int? ExpiryReminderLeadDays { get; set; }
    public bool IsActive { get; set; }

    /// <summary>How many employee documents reference this type — what makes a delete safe or not.</summary>
    public int DocumentCount { get; set; }
}

public class CreateEmployeeDocumentTypeDto : CreateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool HasExpiry { get; set; }

    [Range(1, 3650)]
    public int? ExpiryReminderLeadDays { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeDocumentTypeDto : UpdateDtoBase
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool HasExpiry { get; set; }

    [Range(1, 3650)]
    public int? ExpiryReminderLeadDays { get; set; }

    public bool IsActive { get; set; } = true;
}

// ═══════════════════════════════════════════════════════════════════════════
//  EMPLOYEE DOCUMENTS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// A document on an employee's file.
/// </summary>
/// <remarks>
/// ⚠ <b>There is no create DTO.</b> A document row exists only as the result of an upload through
/// the controlled gate, so the metadata arrives as multipart form fields alongside the file and the
/// three DMS ids are set by the gate — never by a caller. A JSON create would be able to mint a row
/// naming a file that does not exist, which is precisely the shape D-10, D-14 and D-39 each had to
/// remove after it shipped.
/// </remarks>
public class EmployeeDocumentDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public Guid DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>True once <see cref="ExpiresOn"/> is in the past. A document expiring today is valid.</summary>
    public bool IsExpired { get; set; }

    /// <summary>Days until expiry; negative once expired, null where the document does not expire.</summary>
    public int? DaysUntilExpiry { get; set; }

    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }

    /// <summary>Present once the file is registered in the central DMS.</summary>
    public Guid? DocumentRecordId { get; set; }

    public Guid? UploadedById { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Corrects the METADATA on a document. The file itself is replaced by uploading again.</summary>
public class UpdateEmployeeDocumentDto : UpdateDtoBase
{
    [Required]
    public Guid DocumentTypeId { get; set; }

    [MaxLength(250)]
    public string? Title { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  POSITION REQUIREMENTS AND THE COMPLIANCE READ
// ═══════════════════════════════════════════════════════════════════════════

public class PositionDocumentRequirementDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public Guid DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
}

public class CreatePositionDocumentRequirementDto : CreateDtoBase
{
    [Required]
    public Guid PositionId { get; set; }

    [Required]
    public Guid DocumentTypeId { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdatePositionDocumentRequirementDto : UpdateDtoBase
{
    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// One requirement of an employee's position, and whether they satisfy it.
/// </summary>
/// <remarks>
/// ⚠ This read is what makes the requirement register more than a filing preference. A checklist
/// nobody can answer "who is missing what" from is a control that does not control.
/// </remarks>
public class EmployeeDocumentComplianceLineDto
{
    public Guid DocumentTypeId { get; set; }
    public string DocumentTypeName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }

    /// <summary>A document of this type exists and has not expired.</summary>
    public bool IsSatisfied { get; set; }

    /// <summary>The document relied on, where one is held.</summary>
    public Guid? DocumentId { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public int? DaysUntilExpiry { get; set; }

    /// <summary>
    /// Held, but expired. Distinct from never-held: "your licence lapsed" and "we never had your
    /// licence" are different conversations, and collapsing them into "missing" loses the one that
    /// tells you somebody used to comply.
    /// </summary>
    public bool IsExpiredOnly { get; set; }
}

public class EmployeeDocumentComplianceDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }

    /// <summary>
    /// True when the employee holds every MANDATORY document their position requires, unexpired.
    /// </summary>
    public bool IsCompliant { get; set; }

    public int MandatoryCount { get; set; }
    public int MandatorySatisfiedCount { get; set; }

    public List<EmployeeDocumentComplianceLineDto> Lines { get; set; } = new();
}
